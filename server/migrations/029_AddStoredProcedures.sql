-- ============================================================================
-- Migration 029: Add Stored Procedures
-- ============================================================================
-- Converts EF Core LINQ / inline queries to stored procedures for
-- performance, maintainability, and security (parameterized execution).
--
-- Sections mirror the TODO §12 categories.
-- All procedures use CREATE OR ALTER so this migration is re-runnable.
-- ============================================================================

USE [device_management]
GO

-- ============================================================================
-- 12.2  BULK MAINTENANCE OPERATIONS — DatabaseMaintenanceService.cs
-- ============================================================================

-- ----------------------------------------------------------------------------
-- sp_mark_offline_computers
-- Sets is_online = 0 for agents whose last_seen is older than the threshold.
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_mark_offline_computers]
    @offline_threshold_minutes INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(MINUTE, -@offline_threshold_minutes, SYSUTCDATETIME());

    UPDATE [dbo].[computer_agent_statuses]
    SET [is_online] = 0
    WHERE [is_online] = 1
      AND [last_seen] < @cutoff;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ----------------------------------------------------------------------------
-- sp_cleanup_old_system_details
-- Soft-deletes system_details older than @retention_days.
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_old_system_details]
    @retention_days INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(DAY, -@retention_days, SYSUTCDATETIME());

    UPDATE [dbo].[system_details]
    SET [is_deleted]  = 1,
        [deleted_at]  = SYSUTCDATETIME()
    WHERE [collected_at] < @cutoff
      AND [is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ----------------------------------------------------------------------------
-- sp_cleanup_old_restore_points
-- Soft-deletes restore_points older than @retention_days.
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_old_restore_points]
    @retention_days INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(DAY, -@retention_days, SYSUTCDATETIME());

    UPDATE [dbo].[restore_points]
    SET [is_deleted]  = 1,
        [deleted_at]  = SYSUTCDATETIME()
    WHERE [created_at] < @cutoff
      AND [is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ----------------------------------------------------------------------------
-- sp_cleanup_old_history_records
-- Soft-deletes computer_histories older than @retention_days.
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_old_history_records]
    @retention_days INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(DAY, -@retention_days, SYSUTCDATETIME());

    UPDATE [dbo].[computer_histories]
    SET [is_deleted]  = 1,
        [deleted_at]  = SYSUTCDATETIME()
    WHERE [changed_at] < @cutoff
      AND [is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ----------------------------------------------------------------------------
-- sp_mark_stuck_updates_as_failed
-- Marks update requests as Failed when they have been running longer than
-- @timeout_hours and are still in an in-progress state.
-- AgentUpdateStatus enum: None=0, Completed=11, Failed=99.
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_mark_stuck_updates_as_failed]
    @timeout_hours INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(HOUR, -@timeout_hours, SYSUTCDATETIME());
    DECLARE @failed_status INT = 99;
    DECLARE @msg NVARCHAR(500) = CONCAT(N'Update timed out after ', @timeout_hours, N' hours');

    UPDATE r
    SET r.[update_status_id]      = @failed_status,
        r.[update_status_message] = @msg
    FROM [dbo].[computer_update_requests] r
    INNER JOIN [dbo].[computer_agent_statuses] s
        ON s.[active_update_request_id] = r.[id]
    WHERE r.[update_status_id] > 0        -- not None
      AND r.[update_status_id] < 11       -- not Completed
      AND r.[update_status_id] <> 99      -- not already Failed
      AND r.[update_started_at] < @cutoff;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ----------------------------------------------------------------------------
-- sp_cleanup_excess_system_details
-- Keeps the newest @max_records_per_computer system_details per computer and
-- soft-deletes the rest via ROW_NUMBER().
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_excess_system_details]
    @max_records_per_computer INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE t
    SET t.[is_deleted]  = 1,
        t.[deleted_at]  = SYSUTCDATETIME()
    FROM [dbo].[system_details] t
    INNER JOIN (
        SELECT [id],
               ROW_NUMBER() OVER (
                   PARTITION BY [computer_id]
                   ORDER BY [collected_at] DESC
               ) AS rn
        FROM [dbo].[system_details]
        WHERE [is_deleted] = 0
    ) ranked ON ranked.[id] = t.[id]
    WHERE ranked.rn > @max_records_per_computer;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ----------------------------------------------------------------------------
-- sp_cleanup_excess_restore_points
-- Keeps the newest @max_records_per_computer restore_points per computer and
-- soft-deletes the rest via ROW_NUMBER().
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_excess_restore_points]
    @max_records_per_computer INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE t
    SET t.[is_deleted]  = 1,
        t.[deleted_at]  = SYSUTCDATETIME()
    FROM [dbo].[restore_points] t
    INNER JOIN (
        SELECT [id],
               ROW_NUMBER() OVER (
                   PARTITION BY [computer_id]
                   ORDER BY [created_at] DESC
               ) AS rn
        FROM [dbo].[restore_points]
        WHERE [is_deleted] = 0
    ) ranked ON ranked.[id] = t.[id]
    WHERE ranked.rn > @max_records_per_computer;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ----------------------------------------------------------------------------
-- sp_cleanup_excess_records
-- Generic version: keeps the newest @max_records_per_computer rows per computer
-- for a whitelisted table and soft-deletes the rest via ROW_NUMBER().
-- Supported tables: system_details, restore_points.
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_excess_records]
    @table_name              NVARCHAR(128),
    @max_records_per_computer INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Whitelist: map table name → ordering column (all partition by computer_id).
    DECLARE @order_column NVARCHAR(128) = CASE @table_name
        WHEN N'system_details' THEN N'collected_at'
        WHEN N'restore_points' THEN N'created_at'
        ELSE NULL
    END;

    IF @order_column IS NULL
    BEGIN
        RAISERROR('sp_cleanup_excess_records: unsupported table name ''%s''.', 16, 1, @table_name);
        RETURN;
    END;

    DECLARE @sql NVARCHAR(MAX) = N'
        UPDATE t
        SET t.[is_deleted]  = 1,
            t.[deleted_at]  = SYSUTCDATETIME()
        FROM ' + QUOTENAME(@table_name) + N' t
        INNER JOIN (
            SELECT [id],
                   ROW_NUMBER() OVER (
                       PARTITION BY [computer_id]
                       ORDER BY ' + QUOTENAME(@order_column) + N' DESC
                   ) AS rn
            FROM ' + QUOTENAME(@table_name) + N'
            WHERE [is_deleted] = 0
        ) ranked ON ranked.[id] = t.[id]
        WHERE ranked.rn > @max_per_computer;

        SELECT @@ROWCOUNT AS [affected_rows];';

    EXEC sp_executesql @sql,
         N'@max_per_computer INT',
         @max_per_computer = @max_records_per_computer;
END
GO

-- ============================================================================
-- 12.3  REPORTS — SystemDetailsController / SystemDetailsRepository
-- ============================================================================

-- ----------------------------------------------------------------------------
-- sp_get_system_details_report
-- Returns the latest system_details record per computer with all 16 sub-table
-- joins, paged. Two result sets: (1) total count, (2) paged rows.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_system_details_report]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    -- CTE: find the latest system_details row per computer
    ;WITH latest AS (
        SELECT sd.[id],
               sd.[computer_id],
               sd.[collected_at],
               sd.[machine_guid],
               sd.[product_id],
               ROW_NUMBER() OVER (
                   PARTITION BY sd.[computer_id]
                   ORDER BY sd.[collected_at] DESC
               ) AS rn
        FROM [dbo].[system_details] sd
        WHERE sd.[is_deleted] = 0
    )
    SELECT COUNT(DISTINCT [computer_id]) AS [total_count]
    FROM latest
    WHERE rn = 1;

    -- Paged result
    ;WITH latest AS (
        SELECT sd.[id],
               sd.[computer_id],
               sd.[collected_at],
               sd.[machine_guid],
               sd.[product_id],
               ROW_NUMBER() OVER (
                   PARTITION BY sd.[computer_id]
                   ORDER BY sd.[collected_at] DESC
               ) AS rn
        FROM [dbo].[system_details] sd
        WHERE sd.[is_deleted] = 0
    )
    SELECT
        l.[id], l.[computer_id], l.[collected_at], l.[machine_guid], l.[product_id],
        c.[hostname], c.[serial_number],
        -- BIOS
        b.[bios_version], b.[bios_manufacturer], b.[bios_release_date], b.[bios_serial_number], b.[smbios_version],
        -- Motherboard
        mb.[motherboard_manufacturer], mb.[motherboard_product], mb.[motherboard_serial_number], mb.[motherboard_version],
        -- CPU
        cpu.[cpu_name], cpu.[cpu_manufacturer], cpu.[cpu_cores], cpu.[cpu_logical_processors],
        cpu.[cpu_max_clock_speed], cpu.[cpu_current_clock_speed], cpu.[cpu_architecture],
        cpu.[cpu_l2cache_size], cpu.[cpu_l3cache_size], cpu.[cpu_socket_designation], cpu.[cpu_processor_id],
        -- Memory
        mem.[total_physical_memory], mem.[available_physical_memory], mem.[total_virtual_memory],
        mem.[available_virtual_memory], mem.[memory_slots], mem.[memory_slots_used], mem.[memory_modules_json],
        -- Storage
        stor.[disks_json], stor.[partitions_json], stor.[logical_drives_json],
        stor.[total_disk_space], stor.[free_disk_space], stor.[shared_folders_json], stor.[mapped_drives_json],
        -- GPU
        gpu.[gpu_name], gpu.[gpu_manufacturer], gpu.[gpu_driver_version], gpu.[gpu_driver_date],
        gpu.[gpu_ram], gpu.[gpu_resolution], gpu.[graphics_cards_json], gpu.[monitors_json], gpu.[monitor_count],
        -- Network
        net.[network_adapters_json], net.[domain_name], net.[dns_hostname], net.[workgroup],
        net.[domain_joined], net.[default_gateway], net.[dns_servers], net.[dhcp_server],
        -- OS
        os.[os_name], os.[os_version], os.[os_build], os.[os_architecture], os.[os_install_date],
        os.[os_last_boot_time], os.[os_serial_number], os.[os_product_key],
        os.[os_service_pack_major], os.[os_service_pack_minor], os.[os_locale], os.[os_language],
        os.[time_zone], os.[os_uptime], os.[windows_product_id], os.[windows_registered_owner],
        os.[windows_registered_organization], os.[windows_is_activated], os.[windows_activation_status],
        os.[environment_variables_json],
        -- Security
        sec.[antivirus_product], sec.[antivirus_status], sec.[firewall_status],
        sec.[bit_locker_enabled], sec.[bit_locker_status], sec.[secure_boot_enabled],
        sec.[tpm_present], sec.[tpm_version], sec.[uac_enabled],
        -- Battery
        bat.[has_battery], bat.[battery_charge_percent], bat.[battery_status],
        bat.[battery_estimated_runtime], bat.[battery_health], bat.[battery_cycle_count],
        bat.[battery_design_capacity], bat.[battery_full_charge_capacity],
        -- Chassis
        ch.[system_sku_number], ch.[system_family], ch.[chassis_type], ch.[chassis_manufacturer],
        ch.[chassis_serial_number], ch.[chassis_asset_tag],
        -- Software
        sw.[installed_software_json], sw.[installed_software_count], sw.[installed_updates_json],
        sw.[installed_updates_count], sw.[last_update_date], sw.[running_processes_json],
        sw.[running_processes_count], sw.[services_json], sw.[running_services_count],
        sw.[stopped_services_count], sw.[startup_programs_json], sw.[startup_programs_count],
        sw.[scheduled_tasks_json], sw.[scheduled_tasks_count],
        -- Users / Groups
        ug.[local_users_json], ug.[local_groups_json], ug.[logged_on_users_json],
        -- Peripherals
        ph.[printers_json], ph.[usb_devices_json], ph.[audio_devices_json],
        -- Event Logs
        el.[recent_errors_json], el.[recent_warnings_json], el.[blue_screen_count], el.[last_blue_screen],
        -- Performance
        perf.[cpu_usage_percent], perf.[memory_usage_percent], perf.[disk_usage_percent]
    FROM latest l
    INNER JOIN [dbo].[computers] c         ON c.[id] = l.[computer_id]
    LEFT  JOIN [dbo].[system_detail_bios]          b    ON b.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_motherboards]  mb   ON mb.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_cpus]          cpu  ON cpu.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_memories]      mem  ON mem.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_storages]      stor ON stor.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_gpus]          gpu  ON gpu.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_networks]      net  ON net.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_oses]          os   ON os.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_securities]    sec  ON sec.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_batteries]     bat  ON bat.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_chasses]       ch   ON ch.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_softwares]     sw   ON sw.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_users_groups]  ug   ON ug.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_peripherals]   ph   ON ph.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_event_logs]    el   ON el.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_performances]  perf ON perf.[system_detail_id] = l.[id]
    WHERE l.rn = 1
      AND c.[is_deleted] = 0
    ORDER BY c.[hostname]
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ============================================================================
-- 12.4  SEARCH & GEO-LOCATION
-- ============================================================================

-- ----------------------------------------------------------------------------
-- sp_search_computers
-- Multi-column LIKE search across computers and related tables.
-- Returns matching computers with standard includes.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_search_computers]
    @search_term NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @term NVARCHAR(202) = N'%' + @search_term + N'%';

    SELECT
        c.[id], c.[serial_number], c.[hostname], c.[ip_address], c.[mac_address],
        c.[model_id], c.[operating_system_id], c.[processor], c.[ram_gb], c.[storage_gb],
        c.[type_id], c.[status_id], c.[location_id], c.[department_id],
        c.[notes], c.[purchase_date], c.[warranty_expiration], c.[purchase_price],
        c.[assigned_to], c.[created_at], c.[updated_at],
        m.[model_name],
        mfr.[id] AS [manufacturer_id], mfr.[name] AS [manufacturer_name],
        os.[name] AS [os_name],
        ct.[name] AS [type_name],
        cs.[name] AS [status_name],
        loc.[name] AS [location_name],
        dept.[name] AS [department_name],
        cas.[last_seen], cas.[is_online], cas.[current_user], cas.[has_agent],
        cas.[agent_version], cas.[force_update]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m      ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr      ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os   ON os.[id] = c.[operating_system_id]
    INNER JOIN [dbo].[computer_types] ct      ON ct.[id] = c.[type_id]
    INNER JOIN [dbo].[computer_statuses] cs   ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[locations] loc          ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept       ON dept.[id] = c.[department_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    WHERE c.[is_deleted] = 0
      AND (
          c.[hostname]       LIKE @term
       OR c.[serial_number]  LIKE @term
       OR c.[ip_address]     LIKE @term
       OR mfr.[name]         LIKE @term
       OR m.[model_name]     LIKE @term
       OR loc.[name]         LIKE @term
       OR dept.[name]        LIKE @term
       OR c.[assigned_to]    LIKE @term
       OR cas.[current_user] LIKE @term
      )
    ORDER BY c.[hostname];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_computer_ids_needing_geo_lookup
-- Returns computer IDs for online agents whose IP geolocation is missing
-- or stale (older than @cutoff_hours).
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_ids_needing_geo_lookup]
    @cutoff_hours INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(HOUR, -@cutoff_hours, SYSUTCDATETIME());

    SELECT c.[id]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_geo_locations] g    ON g.[computer_id]   = c.[id]
    WHERE c.[is_deleted] = 0
      AND cas.[has_agent] = 1
      AND cas.[is_online] = 1
      AND cas.[public_ip_address] IS NOT NULL
      AND cas.[public_ip_address] <> N''
      AND (
          g.[id] IS NULL
       OR g.[ip_last_updated] IS NULL
       OR g.[ip_last_updated] < @cutoff
      );
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_all_computer_locations
-- Returns all computers that have a public IP or geo-location data,
-- with GPS/IP coordinate resolution for map display.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_computer_locations]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.[id], c.[hostname], c.[serial_number], c.[status_id],
        cas.[public_ip_address], cas.[is_online], cas.[last_seen], cas.[current_user],
        cas.[preferred_location_source],
        g.[gps_latitude], g.[gps_longitude], g.[gps_last_updated],
        g.[ip_latitude], g.[ip_longitude], g.[ip_last_updated],
        g.[city], g.[region],
        co.[name]         AS [country],
        co.[country_code],
        g.[last_updated]  AS [location_updated],
        g.[latitude]      AS [legacy_latitude],
        g.[longitude]     AS [legacy_longitude],
        g.[location_source] AS [legacy_location_source]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_geo_locations] g    ON g.[computer_id]   = c.[id]
    LEFT  JOIN [dbo].[countries] co                ON co.[id]           = g.[country_id]
    WHERE c.[is_deleted] = 0
      AND (cas.[public_ip_address] IS NOT NULL OR g.[id] IS NOT NULL)
    ORDER BY c.[hostname];
END
GO

-- ----------------------------------------------------------------------------
-- sp_upsert_geo_location
-- Inserts or updates a computer's geo-location using MERGE.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_upsert_geo_location]
    @computer_id       INT,
    @public_ip_address NVARCHAR(45)  = NULL,
    @gps_latitude      FLOAT         = NULL,
    @gps_longitude     FLOAT         = NULL,
    @gps_last_updated  DATETIME2     = NULL,
    @gps_accuracy      FLOAT         = NULL,
    @ip_latitude       FLOAT         = NULL,
    @ip_longitude      FLOAT         = NULL,
    @ip_last_updated   DATETIME2     = NULL,
    @latitude          FLOAT         = NULL,
    @longitude         FLOAT         = NULL,
    @city              NVARCHAR(100) = NULL,
    @region            NVARCHAR(100) = NULL,
    @country_id        INT           = NULL,
    @isp               NVARCHAR(200) = NULL,
    @timezone          NVARCHAR(50)  = NULL,
    @lookup_successful BIT           = 0,
    @lookup_error      NVARCHAR(500) = NULL,
    @location_source   NVARCHAR(MAX) = N'Auto'
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [dbo].[computer_geo_locations] AS tgt
    USING (SELECT @computer_id AS [computer_id]) AS src
        ON tgt.[computer_id] = src.[computer_id]
    WHEN MATCHED THEN
        UPDATE SET
            [public_ip_address]  = @public_ip_address,
            [gps_latitude]       = @gps_latitude,
            [gps_longitude]      = @gps_longitude,
            [gps_last_updated]   = @gps_last_updated,
            [gps_accuracy_meters]= @gps_accuracy,
            [ip_latitude]        = @ip_latitude,
            [ip_longitude]       = @ip_longitude,
            [ip_last_updated]    = @ip_last_updated,
            [latitude]           = @latitude,
            [longitude]          = @longitude,
            [city]               = @city,
            [region]             = @region,
            [country_id]         = @country_id,
            [isp]                = @isp,
            [timezone]           = @timezone,
            [last_updated]       = SYSUTCDATETIME(),
            [lookup_successful]  = @lookup_successful,
            [lookup_error]       = @lookup_error,
            [location_source]    = @location_source
    WHEN NOT MATCHED THEN
        INSERT ([computer_id], [public_ip_address],
                [gps_latitude], [gps_longitude], [gps_last_updated], [gps_accuracy_meters],
                [ip_latitude], [ip_longitude], [ip_last_updated],
                [latitude], [longitude], [city], [region], [country_id],
                [isp], [timezone], [last_updated], [lookup_successful],
                [lookup_error], [location_source])
        VALUES (@computer_id, @public_ip_address,
                @gps_latitude, @gps_longitude, @gps_last_updated, @gps_accuracy,
                @ip_latitude, @ip_longitude, @ip_last_updated,
                @latitude, @longitude, @city, @region, @country_id,
                @isp, @timezone, SYSUTCDATETIME(), @lookup_successful,
                @lookup_error, @location_source);
END
GO

-- ============================================================================
-- 12.5  RESTORE POINTS — RestorePointController / RestorePointRepository
-- ============================================================================

-- ----------------------------------------------------------------------------
-- sp_get_restore_point_summary
-- Returns a paged summary of restore points grouped by computer.
-- Two result sets: (1) total count, (2) paged rows.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_restore_point_summary]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: total distinct computers with restore points
    SELECT COUNT(DISTINCT rp.[computer_id]) AS [total_count]
    FROM [dbo].[restore_points] rp
    WHERE rp.[is_deleted] = 0;

    -- Result set 2: paged summary per computer
    SELECT
        rp.[computer_id],
        c.[hostname],
        c.[serial_number],
        COUNT(*)                    AS [total_restore_points],
        MAX(rp.[created_at])        AS [latest_restore_point],
        MIN(rp.[created_at])        AS [oldest_restore_point],
        SUM(ISNULL(rp.[data_size_bytes], 0)) AS [total_size_bytes],
        SUM(CASE WHEN rp.[type_id] = 0 THEN 1 ELSE 0 END) AS [reboot_count]
    FROM [dbo].[restore_points] rp
    INNER JOIN [dbo].[computers] c ON c.[id] = rp.[computer_id]
    WHERE rp.[is_deleted] = 0
      AND c.[is_deleted] = 0
    GROUP BY rp.[computer_id], c.[hostname], c.[serial_number]
    ORDER BY MAX(rp.[created_at]) DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ----------------------------------------------------------------------------
-- sp_cleanup_old_restore_points_by_computer
-- Keeps the latest @keep_count restore points for a specific computer
-- and hard-deletes the rest (cascade removes sub-tables).
-- Returns: number of rows deleted.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_old_restore_points_by_computer]
    @computer_id INT,
    @keep_count  INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH keep AS (
        SELECT TOP (@keep_count) [id]
        FROM [dbo].[restore_points]
        WHERE [computer_id] = @computer_id
          AND [is_deleted] = 0
        ORDER BY [created_at] DESC
    )
    DELETE FROM [dbo].[restore_points]
    WHERE [computer_id] = @computer_id
      AND [is_deleted] = 0
      AND [id] NOT IN (SELECT [id] FROM keep);

    SELECT @@ROWCOUNT AS [deleted_rows];
END
GO

-- ============================================================================
-- 12.6  COMPUTER REPOSITORY — ComputerRepository.cs
-- ============================================================================

-- ----------------------------------------------------------------------------
-- sp_get_computer_by_serial_number
-- Returns a computer by serial number with agent status + active update request.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_by_serial_number]
    @serial_number NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.*,
        cas.[id] AS [agent_status_id], cas.[computer_id] AS [cas_computer_id],
        cas.[last_seen], cas.[is_online], cas.[current_user], cas.[has_agent],
        cas.[public_ip_address] AS [cas_public_ip], cas.[agent_version],
        cas.[agent_session_id], cas.[heartbeat_interval_minutes] AS [cas_heartbeat],
        cas.[force_update], cas.[force_reinstall], cas.[reinstall_attempted],
        cas.[request_immediate_checkin], cas.[preferred_location_source],
        cas.[active_update_request_id],
        ur.[id] AS [ur_id], ur.[computer_id] AS [ur_computer_id],
        ur.[update_status_id], ur.[update_progress], ur.[update_status_message],
        ur.[update_started_at], ur.[update_error_logs], ur.[update_requested_at],
        ur.[update_requested_by], ur.[previous_agent_version], ur.[last_updated_at] AS [ur_last_updated],
        ur.[created_at] AS [ur_created_at]
    FROM [dbo].[computers] c
    LEFT JOIN [dbo].[computer_agent_statuses] cas    ON cas.[computer_id] = c.[id]
    LEFT JOIN [dbo].[computer_update_requests] ur    ON ur.[id] = cas.[active_update_request_id]
    WHERE c.[serial_number] = @serial_number
      AND c.[is_deleted] = 0;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_total_computer_count
-- Returns the count of non-deleted computers.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_total_computer_count]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[computers]
    WHERE [is_deleted] = 0;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_status_counts
-- Returns computer counts grouped by status_id.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_status_counts]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [status_id], COUNT(*) AS [count]
    FROM [dbo].[computers]
    WHERE [is_deleted] = 0
    GROUP BY [status_id];
END
GO

-- ----------------------------------------------------------------------------
-- sp_delete_computer_direct
-- Soft-deletes a computer by ID.
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_delete_computer_direct]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[computers]
    SET [is_deleted] = 1,
        [deleted_at] = SYSUTCDATETIME()
    WHERE [id] = @computer_id
      AND [is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_all_computers
-- Returns all non-deleted computers with standard includes (model,
-- manufacturer, OS, type, status, location, department, agent status).
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_computers]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.[id], c.[serial_number], c.[hostname], c.[ip_address], c.[mac_address],
        c.[model_id], c.[operating_system_id], c.[processor], c.[ram_gb], c.[storage_gb],
        c.[type_id], c.[status_id], c.[location_id], c.[department_id],
        c.[notes], c.[purchase_date], c.[warranty_expiration], c.[purchase_price],
        c.[assigned_to], c.[created_at], c.[updated_at], c.[created_by], c.[updated_by],
        m.[model_name],
        mfr.[id] AS [manufacturer_id], mfr.[name] AS [manufacturer_name],
        os.[name] AS [os_name],
        ct.[name] AS [type_name],
        cs.[name] AS [status_name],
        loc.[name] AS [location_name],
        dept.[name] AS [department_name],
        cas.[last_seen], cas.[is_online], cas.[current_user], cas.[has_agent],
        cas.[agent_version], cas.[force_update], cas.[active_update_request_id],
        cas.[preferred_location_source], cas.[heartbeat_interval_minutes] AS [cas_heartbeat]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m      ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr      ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os   ON os.[id] = c.[operating_system_id]
    INNER JOIN [dbo].[computer_types] ct      ON ct.[id] = c.[type_id]
    INNER JOIN [dbo].[computer_statuses] cs   ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[locations] loc          ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept       ON dept.[id] = c.[department_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    WHERE c.[is_deleted] = 0
    ORDER BY c.[hostname];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_computer_by_id
-- Returns a single computer by ID with all standard includes plus
-- maintenance records and recent history (top 50).
-- Two result sets: (1) computer row, (2) maintenance records,
-- (3) recent history.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_by_id]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: Computer with includes
    SELECT
        c.[id], c.[serial_number], c.[hostname], c.[ip_address], c.[mac_address],
        c.[model_id], c.[operating_system_id], c.[processor], c.[ram_gb], c.[storage_gb],
        c.[type_id], c.[status_id], c.[location_id], c.[department_id],
        c.[notes], c.[purchase_date], c.[warranty_expiration], c.[purchase_price],
        c.[assigned_to], c.[created_at], c.[updated_at], c.[created_by], c.[updated_by],
        m.[model_name],
        mfr.[id] AS [manufacturer_id], mfr.[name] AS [manufacturer_name],
        os.[name] AS [os_name],
        ct.[name] AS [type_name],
        cs.[name] AS [status_name],
        loc.[name] AS [location_name],
        dept.[name] AS [department_name],
        cas.[id] AS [agent_status_id], cas.[last_seen], cas.[is_online], cas.[current_user],
        cas.[has_agent], cas.[public_ip_address] AS [cas_public_ip],
        cas.[agent_version], cas.[agent_session_id],
        cas.[heartbeat_interval_minutes] AS [cas_heartbeat],
        cas.[force_update], cas.[force_reinstall], cas.[reinstall_attempted],
        cas.[request_immediate_checkin], cas.[preferred_location_source],
        cas.[active_update_request_id],
        ur.[id] AS [ur_id], ur.[update_status_id], ur.[update_progress],
        ur.[update_status_message], ur.[update_started_at], ur.[update_error_logs],
        ur.[update_requested_at], ur.[update_requested_by], ur.[previous_agent_version],
        ur.[last_updated_at] AS [ur_last_updated], ur.[created_at] AS [ur_created_at]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m      ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr      ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os   ON os.[id] = c.[operating_system_id]
    INNER JOIN [dbo].[computer_types] ct      ON ct.[id] = c.[type_id]
    INNER JOIN [dbo].[computer_statuses] cs   ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[locations] loc          ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept       ON dept.[id] = c.[department_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_update_requests] ur ON ur.[id] = cas.[active_update_request_id]
    WHERE c.[id] = @id
      AND c.[is_deleted] = 0;

    -- Result set 2: Maintenance records
    SELECT
        mr.[id], mr.[computer_id], mr.[type_id], mr.[description],
        mr.[scheduled_date], mr.[completed_date], mr.[cost],
        mr.[performed_by], mr.[notes], mr.[status_id],
        mr.[created_at], mr.[created_by],
        mt.[name] AS [type_name],
        ms.[name] AS [status_name]
    FROM [dbo].[maintenance_records] mr
    INNER JOIN [dbo].[maintenance_types] mt    ON mt.[id] = mr.[type_id]
    INNER JOIN [dbo].[maintenance_statuses] ms ON ms.[id] = mr.[status_id]
    WHERE mr.[computer_id] = @id
      AND mr.[is_deleted] = 0
    ORDER BY mr.[scheduled_date] DESC;

    -- Result set 3: Recent history (top 50)
    SELECT TOP (50)
        ch.[id], ch.[computer_id], ch.[action], ch.[old_value], ch.[new_value],
        ch.[changed_by], ch.[changed_at], ch.[notes]
    FROM [dbo].[computer_histories] ch
    WHERE ch.[computer_id] = @id
      AND ch.[is_deleted] = 0
    ORDER BY ch.[changed_at] DESC;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_computers_by_status
-- Returns computers filtered by status_id with standard includes.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computers_by_status]
    @status_id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.[id], c.[serial_number], c.[hostname], c.[ip_address], c.[mac_address],
        c.[model_id], c.[operating_system_id], c.[processor], c.[ram_gb], c.[storage_gb],
        c.[type_id], c.[status_id], c.[location_id], c.[department_id],
        c.[notes], c.[purchase_date], c.[warranty_expiration], c.[purchase_price],
        c.[assigned_to], c.[created_at], c.[updated_at],
        m.[model_name],
        mfr.[id] AS [manufacturer_id], mfr.[name] AS [manufacturer_name],
        os.[name] AS [os_name],
        ct.[name] AS [type_name],
        cs.[name] AS [status_name],
        loc.[name] AS [location_name],
        dept.[name] AS [department_name],
        cas.[last_seen], cas.[is_online], cas.[current_user], cas.[has_agent],
        cas.[agent_version], cas.[force_update]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m      ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr      ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os   ON os.[id] = c.[operating_system_id]
    INNER JOIN [dbo].[computer_types] ct      ON ct.[id] = c.[type_id]
    INNER JOIN [dbo].[computer_statuses] cs   ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[locations] loc          ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept       ON dept.[id] = c.[department_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    WHERE c.[is_deleted] = 0
      AND c.[status_id] = @status_id
    ORDER BY c.[hostname];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_computers_by_location
-- Returns computers filtered by location_id with standard includes.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computers_by_location]
    @location_id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.[id], c.[serial_number], c.[hostname], c.[ip_address], c.[mac_address],
        c.[model_id], c.[operating_system_id], c.[processor], c.[ram_gb], c.[storage_gb],
        c.[type_id], c.[status_id], c.[location_id], c.[department_id],
        c.[notes], c.[purchase_date], c.[warranty_expiration], c.[purchase_price],
        c.[assigned_to], c.[created_at], c.[updated_at],
        m.[model_name],
        mfr.[id] AS [manufacturer_id], mfr.[name] AS [manufacturer_name],
        os.[name] AS [os_name],
        ct.[name] AS [type_name],
        cs.[name] AS [status_name],
        loc.[name] AS [location_name],
        dept.[name] AS [department_name],
        cas.[last_seen], cas.[is_online], cas.[current_user], cas.[has_agent],
        cas.[agent_version], cas.[force_update]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m      ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr      ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os   ON os.[id] = c.[operating_system_id]
    INNER JOIN [dbo].[computer_types] ct      ON ct.[id] = c.[type_id]
    INNER JOIN [dbo].[computer_statuses] cs   ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[locations] loc          ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept       ON dept.[id] = c.[department_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    WHERE c.[is_deleted] = 0
      AND c.[location_id] = @location_id
    ORDER BY c.[hostname];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_computers_by_department
-- Returns computers filtered by department_id with standard includes.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computers_by_department]
    @department_id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.[id], c.[serial_number], c.[hostname], c.[ip_address], c.[mac_address],
        c.[model_id], c.[operating_system_id], c.[processor], c.[ram_gb], c.[storage_gb],
        c.[type_id], c.[status_id], c.[location_id], c.[department_id],
        c.[notes], c.[purchase_date], c.[warranty_expiration], c.[purchase_price],
        c.[assigned_to], c.[created_at], c.[updated_at],
        m.[model_name],
        mfr.[id] AS [manufacturer_id], mfr.[name] AS [manufacturer_name],
        os.[name] AS [os_name],
        ct.[name] AS [type_name],
        cs.[name] AS [status_name],
        loc.[name] AS [location_name],
        dept.[name] AS [department_name],
        cas.[last_seen], cas.[is_online], cas.[current_user], cas.[has_agent],
        cas.[agent_version], cas.[force_update]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m      ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr      ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os   ON os.[id] = c.[operating_system_id]
    INNER JOIN [dbo].[computer_types] ct      ON ct.[id] = c.[type_id]
    INNER JOIN [dbo].[computer_statuses] cs   ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[locations] loc          ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept       ON dept.[id] = c.[department_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    WHERE c.[is_deleted] = 0
      AND c.[department_id] = @department_id
    ORDER BY c.[hostname];
END
GO

-- ============================================================================
-- 12.7  LOOKUPS — LookupsController.cs
-- ============================================================================

-- ----------------------------------------------------------------------------
-- sp_get_computer_types
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at]
    FROM [dbo].[computer_types]
    ORDER BY [name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_computer_statuses
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_statuses]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[computer_statuses] ORDER BY [name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_departments
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_departments]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[departments] ORDER BY [name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_locations
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_locations]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[locations] ORDER BY [name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_manufacturers
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_manufacturers]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[manufacturers] ORDER BY [name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_computer_models
-- Returns all models, optionally filtered by manufacturer_id.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_models]
    @manufacturer_id INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        cm.[id],
        cm.[model_name]        AS [name],
        cm.[manufacturer_id],
        mfr.[name]             AS [manufacturer_name]
    FROM [dbo].[computer_models] cm
    INNER JOIN [dbo].[manufacturers] mfr ON mfr.[id] = cm.[manufacturer_id]
    WHERE @manufacturer_id IS NULL
       OR cm.[manufacturer_id] = @manufacturer_id
    ORDER BY mfr.[name], cm.[model_name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_operating_systems
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_operating_systems]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[operating_systems] ORDER BY [name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_location_types  (alias: returns locations)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_location_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[locations] ORDER BY [name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_department_types  (alias: returns departments)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_department_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[departments] ORDER BY [name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_manufacturer_types  (alias: returns manufacturers)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_manufacturer_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[manufacturers] ORDER BY [name];
END
GO

-- ============================================================================
-- 12.8  REFERENCE DATA — ReferenceDataResolver.cs
-- ============================================================================

-- ----------------------------------------------------------------------------
-- sp_find_or_create_manufacturer
-- Returns the manufacturer ID, creating a new row if it doesn't exist.
-- Uses MERGE for atomicity (avoids race conditions).
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_manufacturer]
    @name NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[manufacturers] WITH (HOLDLOCK) AS tgt
    USING (SELECT @name AS [name]) AS src
        ON tgt.[name] = src.[name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[name] = tgt.[name]  -- no-op to allow OUTPUT
    WHEN NOT MATCHED THEN
        INSERT ([name], [created_at])
        VALUES (@name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

-- ----------------------------------------------------------------------------
-- sp_find_or_create_computer_model
-- Returns the model ID, creating a new row if @model_name doesn't exist
-- for the given @manufacturer_id.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_computer_model]
    @manufacturer_id INT,
    @model_name      NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[computer_models] WITH (HOLDLOCK) AS tgt
    USING (SELECT @manufacturer_id AS [manufacturer_id],
                  @model_name      AS [model_name]) AS src
        ON tgt.[manufacturer_id] = src.[manufacturer_id]
       AND tgt.[model_name]      = src.[model_name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[model_name] = tgt.[model_name]  -- no-op
    WHEN NOT MATCHED THEN
        INSERT ([manufacturer_id], [model_name], [created_at])
        VALUES (@manufacturer_id, @model_name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

-- ----------------------------------------------------------------------------
-- sp_find_or_create_operating_system
-- Returns the OS ID, creating a new row if @name doesn't exist.
-- @version and @build_number are accepted but the lookup table only has name.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_operating_system]
    @name         NVARCHAR(100),
    @version      NVARCHAR(100) = NULL,
    @build_number NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[operating_systems] WITH (HOLDLOCK) AS tgt
    USING (SELECT @name AS [name]) AS src
        ON tgt.[name] = src.[name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[name] = tgt.[name]  -- no-op
    WHEN NOT MATCHED THEN
        INSERT ([name], [created_at])
        VALUES (@name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

-- ----------------------------------------------------------------------------
-- sp_find_or_create_department
-- Returns the department ID, creating a new row if @name doesn't exist.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_department]
    @name NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[departments] WITH (HOLDLOCK) AS tgt
    USING (SELECT @name AS [name]) AS src
        ON tgt.[name] = src.[name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[name] = tgt.[name]  -- no-op
    WHEN NOT MATCHED THEN
        INSERT ([name], [created_at])
        VALUES (@name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

-- ----------------------------------------------------------------------------
-- sp_find_or_create_location
-- Returns the location ID, creating a new row if @name doesn't exist.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_location]
    @name NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[locations] WITH (HOLDLOCK) AS tgt
    USING (SELECT @name AS [name]) AS src
        ON tgt.[name] = src.[name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[name] = tgt.[name]  -- no-op
    WHEN NOT MATCHED THEN
        INSERT ([name], [created_at])
        VALUES (@name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

-- ============================================================================
-- 12.9  OTHER CONTROLLERS & SERVICES
-- ============================================================================

-- ----------------------------------------------------------------------------
-- sp_add_computer_history
-- Inserts a history record for a computer.
-- Returns: the new history record ID.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_add_computer_history]
    @computer_id INT,
    @action      NVARCHAR(100),
    @details     NVARCHAR(500) = NULL,
    @changed_by  NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[computer_histories]
        ([computer_id], [action], [notes], [changed_by], [changed_at], [created_at])
    VALUES
        (@computer_id, @action, @details, @changed_by, SYSUTCDATETIME(), SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS [id];
END
GO

-- ----------------------------------------------------------------------------
-- sp_set_location_source_preference
-- Updates the preferred location source for a computer's agent status.
-- Valid values: 'GPS', 'IP', 'Auto'.
-- Uses MERGE to create the agent status row if it doesn't exist yet.
-- Returns: number of rows affected.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_set_location_source_preference]
    @computer_id INT,
    @preference  NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [dbo].[computer_agent_statuses] WITH (HOLDLOCK) AS tgt
    USING (SELECT @computer_id AS [computer_id]) AS src
        ON tgt.[computer_id] = src.[computer_id]
    WHEN MATCHED THEN
        UPDATE SET [preferred_location_source] = @preference
    WHEN NOT MATCHED THEN
        INSERT ([computer_id], [preferred_location_source])
        VALUES (@computer_id, @preference);

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_or_create_country_id
-- Returns the country ID, creating a new row if the country_code doesn't exist.
-- Uses MERGE for concurrency safety.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_or_create_country_id]
    @country_code NVARCHAR(10),
    @country_name NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[countries] WITH (HOLDLOCK) AS tgt
    USING (SELECT @country_code AS [country_code]) AS src
        ON tgt.[country_code] = src.[country_code]
    WHEN MATCHED THEN
        UPDATE SET tgt.[country_code] = tgt.[country_code]  -- no-op
    WHEN NOT MATCHED THEN
        INSERT ([country_code], [name], [created_at])
        VALUES (@country_code, @country_name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

-- ============================================================================
-- 12.10 MAINTENANCE, USERS, VERSION & HEALTH
-- ============================================================================

-- ----------------------------------------------------------------------------
-- sp_get_all_maintenance_records
-- Returns all non-deleted maintenance records with computer, type, and
-- status includes.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_maintenance_records]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        mr.[id], mr.[computer_id], mr.[type_id], mr.[description],
        mr.[scheduled_date], mr.[completed_date], mr.[cost],
        mr.[performed_by], mr.[notes], mr.[status_id],
        mr.[created_at], mr.[created_by],
        c.[hostname]   AS [computer_hostname],
        c.[serial_number] AS [computer_serial_number],
        mt.[name]      AS [type_name],
        ms.[name]      AS [status_name]
    FROM [dbo].[maintenance_records] mr
    INNER JOIN [dbo].[computers] c             ON c.[id]  = mr.[computer_id]
    INNER JOIN [dbo].[maintenance_types] mt    ON mt.[id] = mr.[type_id]
    INNER JOIN [dbo].[maintenance_statuses] ms ON ms.[id] = mr.[status_id]
    WHERE mr.[is_deleted] = 0
      AND c.[is_deleted] = 0
    ORDER BY mr.[scheduled_date] DESC;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_maintenance_by_computer_id
-- Returns maintenance records for a specific computer.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        mr.[id], mr.[computer_id], mr.[type_id], mr.[description],
        mr.[scheduled_date], mr.[completed_date], mr.[cost],
        mr.[performed_by], mr.[notes], mr.[status_id],
        mr.[created_at], mr.[created_by],
        c.[hostname]   AS [computer_hostname],
        c.[serial_number] AS [computer_serial_number],
        mt.[name]      AS [type_name],
        ms.[name]      AS [status_name]
    FROM [dbo].[maintenance_records] mr
    INNER JOIN [dbo].[computers] c             ON c.[id]  = mr.[computer_id]
    INNER JOIN [dbo].[maintenance_types] mt    ON mt.[id] = mr.[type_id]
    INNER JOIN [dbo].[maintenance_statuses] ms ON ms.[id] = mr.[status_id]
    WHERE mr.[computer_id] = @computer_id
      AND mr.[is_deleted] = 0
    ORDER BY mr.[scheduled_date] DESC;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_maintenance_by_status
-- Returns maintenance records filtered by status_id.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_by_status]
    @status_id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        mr.[id], mr.[computer_id], mr.[type_id], mr.[description],
        mr.[scheduled_date], mr.[completed_date], mr.[cost],
        mr.[performed_by], mr.[notes], mr.[status_id],
        mr.[created_at], mr.[created_by],
        c.[hostname]   AS [computer_hostname],
        c.[serial_number] AS [computer_serial_number],
        mt.[name]      AS [type_name],
        ms.[name]      AS [status_name]
    FROM [dbo].[maintenance_records] mr
    INNER JOIN [dbo].[computers] c             ON c.[id]  = mr.[computer_id]
    INNER JOIN [dbo].[maintenance_types] mt    ON mt.[id] = mr.[type_id]
    INNER JOIN [dbo].[maintenance_statuses] ms ON ms.[id] = mr.[status_id]
    WHERE mr.[status_id] = @status_id
      AND mr.[is_deleted] = 0
    ORDER BY mr.[scheduled_date] DESC;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_scheduled_maintenance
-- Returns maintenance records within a date range.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_scheduled_maintenance]
    @start_date DATETIME2,
    @end_date   DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        mr.[id], mr.[computer_id], mr.[type_id], mr.[description],
        mr.[scheduled_date], mr.[completed_date], mr.[cost],
        mr.[performed_by], mr.[notes], mr.[status_id],
        mr.[created_at], mr.[created_by],
        c.[hostname]   AS [computer_hostname],
        c.[serial_number] AS [computer_serial_number],
        mt.[name]      AS [type_name],
        ms.[name]      AS [status_name]
    FROM [dbo].[maintenance_records] mr
    INNER JOIN [dbo].[computers] c             ON c.[id]  = mr.[computer_id]
    INNER JOIN [dbo].[maintenance_types] mt    ON mt.[id] = mr.[type_id]
    INNER JOIN [dbo].[maintenance_statuses] ms ON ms.[id] = mr.[status_id]
    WHERE mr.[scheduled_date] >= @start_date
      AND mr.[scheduled_date] <= @end_date
      AND mr.[is_deleted] = 0
    ORDER BY mr.[scheduled_date] ASC;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_all_users
-- Returns all non-deleted users with department and role includes.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_users]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.[id], u.[username], u.[email], u.[first_name], u.[last_name],
        u.[department_id], u.[phone], u.[role_id], u.[is_active],
        u.[created_at], u.[updated_at], u.[last_login_at],
        d.[name]  AS [department_name],
        r.[name]  AS [role_name]
    FROM [dbo].[users] u
    LEFT  JOIN [dbo].[departments] d ON d.[id] = u.[department_id]
    INNER JOIN [dbo].[user_roles] r  ON r.[id] = u.[role_id]
    WHERE u.[is_deleted] = 0
    ORDER BY u.[last_name], u.[first_name];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_user_by_username
-- Returns a single user by username with department and role.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_user_by_username]
    @username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.[id], u.[username], u.[email], u.[password_hash],
        u.[first_name], u.[last_name], u.[department_id], u.[phone],
        u.[role_id], u.[is_active], u.[created_at], u.[updated_at], u.[last_login_at],
        d.[name]  AS [department_name],
        r.[name]  AS [role_name]
    FROM [dbo].[users] u
    LEFT  JOIN [dbo].[departments] d ON d.[id] = u.[department_id]
    INNER JOIN [dbo].[user_roles] r  ON r.[id] = u.[role_id]
    WHERE u.[username] = @username
      AND u.[is_deleted] = 0;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_user_by_email
-- Returns a single user by email with department and role.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_user_by_email]
    @email NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.[id], u.[username], u.[email], u.[password_hash],
        u.[first_name], u.[last_name], u.[department_id], u.[phone],
        u.[role_id], u.[is_active], u.[created_at], u.[updated_at], u.[last_login_at],
        d.[name]  AS [department_name],
        r.[name]  AS [role_name]
    FROM [dbo].[users] u
    LEFT  JOIN [dbo].[departments] d ON d.[id] = u.[department_id]
    INNER JOIN [dbo].[user_roles] r  ON r.[id] = u.[role_id]
    WHERE u.[email] = @email
      AND u.[is_deleted] = 0;
END
GO

-- ----------------------------------------------------------------------------
-- sp_check_user_exists
-- Checks whether a username or email already exists.
-- Returns: username_exists BIT, email_exists BIT.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_check_user_exists]
    @username NVARCHAR(50),
    @email    NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CAST(CASE WHEN EXISTS (
            SELECT 1 FROM [dbo].[users] WHERE [username] = @username AND [is_deleted] = 0
        ) THEN 1 ELSE 0 END AS BIT) AS [username_exists],
        CAST(CASE WHEN EXISTS (
            SELECT 1 FROM [dbo].[users] WHERE [email] = @email AND [is_deleted] = 0
        ) THEN 1 ELSE 0 END AS BIT) AS [email_exists];
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_version_history
-- Returns all version history records, ordered by released_at DESC.
-- Two result sets: (1) Server versions, (2) ThinClient versions.
-- VersionType: Server=0, ThinClient=1.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_version_history]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: Server versions count + paged
    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 0
      AND [is_deleted] = 0;

    SELECT [id], [version], [type_id], [change_log], [released_at],
           [released_by], [is_current_version], [checksum], [file_size]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 0
      AND [is_deleted] = 0
    ORDER BY [released_at] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;

    -- Result set 2: ThinClient versions count + paged
    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 1
      AND [is_deleted] = 0;

    SELECT [id], [version], [type_id], [change_log], [released_at],
           [released_by], [is_current_version], [checksum], [file_size]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 1
      AND [is_deleted] = 0
    ORDER BY [released_at] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_server_history
-- Returns paged server version history.
-- Two result sets: (1) total count, (2) paged rows.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_server_history]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 0
      AND [is_deleted] = 0;

    SELECT [id], [version], [type_id], [change_log], [released_at],
           [released_by], [is_current_version], [checksum], [file_size]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 0
      AND [is_deleted] = 0
    ORDER BY [released_at] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ----------------------------------------------------------------------------
-- sp_get_thin_client_history
-- Returns paged thin client version history.
-- Two result sets: (1) total count, (2) paged rows.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_get_thin_client_history]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 1
      AND [is_deleted] = 0;

    SELECT [id], [version], [type_id], [change_log], [released_at],
           [released_by], [is_current_version], [checksum], [file_size]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 1
      AND [is_deleted] = 0
    ORDER BY [released_at] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ----------------------------------------------------------------------------
-- sp_health_check
-- Performs a simple health check: tests connectivity and returns
-- the computer count and query execution time.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_health_check]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @start DATETIME2 = SYSUTCDATETIME();

    SELECT COUNT(*) AS [computer_count]
    FROM [dbo].[computers]
    WHERE [is_deleted] = 0;

    SELECT DATEDIFF(MILLISECOND, @start, SYSUTCDATETIME()) AS [query_time_ms];
END
GO

-- ============================================================================
-- END OF MIGRATION 029
-- ============================================================================
PRINT 'Migration 029_AddStoredProcedures.sql applied successfully.';
GO
