-- ============================================================================
-- Migration 046: Add remaining stored procedures for full LINQ → SP conversion
-- Generated: 2026-03-26
--
-- Covers all repository methods that still use LINQ/EF queries.
-- Write operations (Add/Update/Delete via EF change tracking) remain as-is.
-- ============================================================================

USE [device_management]
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 1. LOOKUP SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at]
    FROM [dbo].[maintenance_types]
    ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_statuses]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at]
    FROM [dbo].[maintenance_statuses]
    ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_user_roles]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at]
    FROM [dbo].[user_roles]
    ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_version_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at]
    FROM [dbo].[version_types]
    ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_department_name_exists]
    @name       NVARCHAR(100),
    @exclude_id INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[departments]
        WHERE [name] = @name
          AND (@exclude_id IS NULL OR [id] <> @exclude_id)
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_department_usage_count]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @computer_count INT, @user_count INT;
    SELECT @computer_count = COUNT(*) FROM [dbo].[computers] WHERE [department_id] = @id AND [is_deleted] = 0;
    SELECT @user_count = COUNT(*) FROM [dbo].[users] WHERE [department_id] = @id AND [is_deleted] = 0;
    SELECT (@computer_count + @user_count) AS [usage_count];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_location_name_exists]
    @name       NVARCHAR(200),
    @exclude_id INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[locations]
        WHERE [name] = @name
          AND (@exclude_id IS NULL OR [id] <> @exclude_id)
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_location_usage_count]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) AS [usage_count]
    FROM [dbo].[computers]
    WHERE [location_id] = @id AND [is_deleted] = 0;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 2. COMPUTER SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_dashboard_counts]
    @latest_agent_version NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @stale_cutoff DATETIME2 = DATEADD(HOUR, -24, GETUTCDATE());
    DECLARE @now DATETIME2 = GETUTCDATE();

    SELECT
        COUNT(*)                                                                    AS [total_computers],
        SUM(CASE WHEN cas.[is_online] = 1 THEN 1 ELSE 0 END)                       AS [online_computers],
        SUM(CASE WHEN cas.[is_online] = 0 THEN 1 ELSE 0 END)                       AS [offline_computers],
        SUM(CASE WHEN cas.[has_agent] = 1 THEN 1 ELSE 0 END)                       AS [agents_installed],
        SUM(CASE WHEN ur.[id] IS NOT NULL
                  AND ur.[update_status_id] NOT IN (0, 50, 99) THEN 1 ELSE 0 END)  AS [agents_updating],
        SUM(CASE WHEN ur.[id] IS NOT NULL
                  AND ur.[update_status_id] = 99 THEN 1 ELSE 0 END)                AS [agents_failed_update],
        SUM(CASE WHEN cas.[force_update] = 1 THEN 1 ELSE 0 END)                    AS [agents_pending_update],
        CASE WHEN @latest_agent_version IS NOT NULL
             THEN SUM(CASE WHEN cas.[has_agent] = 1
                            AND cas.[agent_version] = @latest_agent_version
                       THEN 1 ELSE 0 END)
             ELSE 0 END                                                             AS [agents_on_latest_version],
        CASE WHEN @latest_agent_version IS NOT NULL
             THEN SUM(CASE WHEN cas.[has_agent] = 1
                            AND cas.[agent_version] IS NOT NULL
                            AND cas.[agent_version] <> ''
                            AND cas.[agent_version] <> @latest_agent_version
                       THEN 1 ELSE 0 END)
             ELSE 0 END                                                             AS [agents_on_older_version],
        SUM(CASE WHEN cas.[has_agent] = 1
                  AND cas.[last_seen] IS NOT NULL
                  AND cas.[last_seen] < @stale_cutoff THEN 1 ELSE 0 END)            AS [stale_agents],
        SUM(CASE WHEN c.[warranty_expiration] IS NOT NULL
                  AND c.[warranty_expiration] < @now THEN 1 ELSE 0 END)             AS [expired_warranties]
    FROM [dbo].[computers] c
    LEFT JOIN [dbo].[computer_agent_statuses] cas ON c.[id] = cas.[computer_id]
    LEFT JOIN [dbo].[computer_update_requests] ur ON cas.[active_update_request_id] = ur.[id]
    WHERE c.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_by_serial_number_basic]
    @serial_number  NVARCHAR(100),
    @include_deleted BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.*
    FROM [dbo].[computers] c
    WHERE c.[serial_number] = @serial_number
      AND (@include_deleted = 1 OR c.[is_deleted] = 0);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_computer_exists]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[computers] WHERE [id] = @id AND [is_deleted] = 0
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_bulk_update_computer_status]
    @id_list    NVARCHAR(MAX),
    @status_id  INT,
    @updated_by NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE c
    SET c.[status_id] = @status_id,
        c.[updated_by] = @updated_by,
        c.[updated_at] = GETUTCDATE()
    FROM [dbo].[computers] c
    JOIN STRING_SPLIT(@id_list, ',') s ON c.[id] = CAST(LTRIM(RTRIM(s.[value])) AS INT)
    WHERE c.[is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 3. MAINTENANCE SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_summary_counts]
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @start_of_month DATETIME2 = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);

    SELECT
        SUM(CASE WHEN [status_id] IN (0, 1) THEN 1 ELSE 0 END) AS [open_count],
        SUM(CASE WHEN [status_id] = 0 THEN 1 ELSE 0 END)       AS [scheduled_count],
        SUM(CASE WHEN [status_id] = 2 AND [completed_date] >= @start_of_month THEN 1 ELSE 0 END) AS [completed_this_month]
    FROM [dbo].[maintenance_records]
    WHERE [is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_by_id]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.[id], m.[computer_id], m.[type_id], m.[description],
        m.[scheduled_date], m.[completed_date], m.[cost],
        m.[performed_by], m.[notes], m.[status_id],
        m.[created_at], m.[created_by], m.[row_version],
        c.[hostname]        AS [_computer_hostname],
        c.[serial_number]   AS [_computer_serial_number],
        mt.[name]           AS [_type_name],
        ms.[name]           AS [_status_name]
    FROM [dbo].[maintenance_records] m
    JOIN [dbo].[computers] c ON m.[computer_id] = c.[id]
    JOIN [dbo].[maintenance_types] mt ON m.[type_id] = mt.[id]
    JOIN [dbo].[maintenance_statuses] ms ON m.[status_id] = ms.[id]
    WHERE m.[id] = @id AND m.[is_deleted] = 0;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 4. USER SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_user_by_id]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.[id], u.[username], u.[email], u.[password_hash],
        u.[first_name], u.[last_name], u.[department_id], u.[phone],
        u.[role_id], u.[is_active], u.[is_email_verified],
        u.[created_at], u.[updated_at], u.[last_login_at],
        d.[name] AS [_department_name],
        r.[name] AS [_role_name]
    FROM [dbo].[users] u
    LEFT JOIN [dbo].[departments] d ON u.[department_id] = d.[id]
    JOIN [dbo].[user_roles] r ON u.[role_id] = r.[id]
    WHERE u.[id] = @id AND u.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_users_by_role]
    @role_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.[id], u.[username], u.[email],
        u.[first_name], u.[last_name], u.[department_id], u.[phone],
        u.[role_id], u.[is_active], u.[is_email_verified],
        u.[created_at], u.[updated_at], u.[last_login_at],
        d.[name] AS [_department_name],
        r.[name] AS [_role_name]
    FROM [dbo].[users] u
    LEFT JOIN [dbo].[departments] d ON u.[department_id] = d.[id]
    JOIN [dbo].[user_roles] r ON u.[role_id] = r.[id]
    WHERE u.[role_id] = @role_id AND u.[is_deleted] = 0
    ORDER BY u.[last_name], u.[first_name];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 5. GEOLOCATION SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_geo_location_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        g.[id], g.[computer_id], g.[public_ip_address],
        g.[gps_latitude], g.[gps_longitude], g.[gps_last_updated],
        g.[ip_latitude], g.[ip_longitude], g.[ip_last_updated],
        g.[latitude], g.[longitude], g.[city], g.[region],
        g.[country_id], g.[isp], g.[timezone],
        g.[last_updated], g.[lookup_successful], g.[lookup_error],
        g.[location_source], g.[gps_accuracy_meters],
        co.[id] AS [_country_id], co.[country_code] AS [_country_code], co.[name] AS [_country_name]
    FROM [dbo].[computer_geo_locations] g
    LEFT JOIN [dbo].[countries] co ON g.[country_id] = co.[id]
    WHERE g.[computer_id] = @computer_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_geo_location_simple]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM [dbo].[computer_geo_locations] WHERE [computer_id] = @computer_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_geo_locations_with_computers]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        g.[id], g.[computer_id], g.[public_ip_address],
        g.[gps_latitude], g.[gps_longitude], g.[gps_last_updated],
        g.[ip_latitude], g.[ip_longitude], g.[ip_last_updated],
        g.[latitude], g.[longitude], g.[city], g.[region],
        g.[country_id], g.[isp], g.[timezone],
        g.[last_updated], g.[lookup_successful], g.[lookup_error],
        g.[location_source], g.[gps_accuracy_meters],
        c.[hostname] AS [_computer_hostname],
        c.[serial_number] AS [_computer_serial_number],
        c.[ip_address] AS [_computer_ip_address],
        co.[id] AS [_country_id], co.[country_code] AS [_country_code], co.[name] AS [_country_name]
    FROM [dbo].[computer_geo_locations] g
    JOIN [dbo].[computers] c ON g.[computer_id] = c.[id]
    LEFT JOIN [dbo].[countries] co ON g.[country_id] = co.[id]
    WHERE c.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_geo_locations_with_valid_coordinates]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        g.[id], g.[computer_id], g.[public_ip_address],
        g.[gps_latitude], g.[gps_longitude], g.[gps_last_updated],
        g.[ip_latitude], g.[ip_longitude], g.[ip_last_updated],
        g.[latitude], g.[longitude], g.[city], g.[region],
        g.[country_id], g.[isp], g.[timezone],
        g.[last_updated], g.[lookup_successful], g.[lookup_error],
        g.[location_source], g.[gps_accuracy_meters],
        c.[hostname] AS [_computer_hostname],
        c.[serial_number] AS [_computer_serial_number],
        c.[ip_address] AS [_computer_ip_address],
        co.[id] AS [_country_id], co.[country_code] AS [_country_code], co.[name] AS [_country_name]
    FROM [dbo].[computer_geo_locations] g
    JOIN [dbo].[computers] c ON g.[computer_id] = c.[id]
    LEFT JOIN [dbo].[countries] co ON g.[country_id] = co.[id]
    WHERE g.[latitude] IS NOT NULL AND g.[longitude] IS NOT NULL
      AND c.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_update_gps_location]
    @computer_id       INT,
    @latitude          FLOAT,
    @longitude         FLOAT,
    @public_ip_address NVARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @now DATETIME2 = GETUTCDATE();

    MERGE [dbo].[computer_geo_locations] WITH (HOLDLOCK) AS tgt
    USING (SELECT @computer_id AS [computer_id]) AS src
    ON tgt.[computer_id] = src.[computer_id]
    WHEN MATCHED THEN
        UPDATE SET
            [gps_latitude]  = @latitude,
            [gps_longitude] = @longitude,
            [gps_last_updated] = @now,
            [latitude]      = @latitude,
            [longitude]     = @longitude,
            [location_source] = N'GPS',
            [last_updated]  = @now,
            [lookup_successful] = 1,
            [lookup_error]  = NULL,
            [public_ip_address] = COALESCE(@public_ip_address, tgt.[public_ip_address])
    WHEN NOT MATCHED THEN
        INSERT ([computer_id], [public_ip_address],
                [gps_latitude], [gps_longitude], [gps_last_updated],
                [latitude], [longitude], [location_source],
                [last_updated], [lookup_successful])
        VALUES (@computer_id, @public_ip_address,
                @latitude, @longitude, @now,
                @latitude, @longitude, N'GPS',
                @now, 1);

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_update_ip_location]
    @computer_id       INT,
    @latitude          FLOAT,
    @longitude         FLOAT,
    @city              NVARCHAR(100) = NULL,
    @region            NVARCHAR(100) = NULL,
    @country           NVARCHAR(100) = NULL,
    @country_code      NVARCHAR(10) = NULL,
    @isp               NVARCHAR(200) = NULL,
    @timezone          NVARCHAR(50) = NULL,
    @public_ip_address NVARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @now DATETIME2 = GETUTCDATE();
    DECLARE @country_id INT = NULL;

    -- Resolve country to FK if provided
    IF @country IS NOT NULL AND @country_code IS NOT NULL
    BEGIN
        MERGE [dbo].[countries] WITH (HOLDLOCK) AS tgt
        USING (SELECT @country_code AS [country_code]) AS src
        ON tgt.[country_code] = src.[country_code]
        WHEN NOT MATCHED THEN
            INSERT ([name], [country_code]) VALUES (@country, @country_code);
        SET @country_id = (SELECT [id] FROM [dbo].[countries] WHERE [country_code] = @country_code);
    END

    MERGE [dbo].[computer_geo_locations] WITH (HOLDLOCK) AS tgt
    USING (SELECT @computer_id AS [computer_id]) AS src
    ON tgt.[computer_id] = src.[computer_id]
    WHEN MATCHED THEN
        UPDATE SET
            [ip_latitude]       = @latitude,
            [ip_longitude]      = @longitude,
            [ip_last_updated]   = @now,
            [public_ip_address] = @public_ip_address,
            [city]              = @city,
            [region]            = @region,
            [country_id]        = COALESCE(@country_id, tgt.[country_id]),
            [isp]               = @isp,
            [timezone]          = @timezone,
            [lookup_successful] = 1,
            [lookup_error]      = NULL,
            [last_updated]      = @now,
            -- Only update display columns if GPS is not available
            [latitude]          = CASE WHEN tgt.[gps_latitude] IS NOT NULL AND tgt.[gps_longitude] IS NOT NULL
                                       THEN tgt.[latitude] ELSE @latitude END,
            [longitude]         = CASE WHEN tgt.[gps_latitude] IS NOT NULL AND tgt.[gps_longitude] IS NOT NULL
                                       THEN tgt.[longitude] ELSE @longitude END,
            [location_source]   = CASE WHEN tgt.[gps_latitude] IS NOT NULL AND tgt.[gps_longitude] IS NOT NULL
                                       THEN tgt.[location_source] ELSE N'IP' END
    WHEN NOT MATCHED THEN
        INSERT ([computer_id], [public_ip_address],
                [ip_latitude], [ip_longitude], [ip_last_updated],
                [latitude], [longitude], [location_source],
                [city], [region], [country_id], [isp], [timezone],
                [last_updated], [lookup_successful])
        VALUES (@computer_id, @public_ip_address,
                @latitude, @longitude, @now,
                @latitude, @longitude, N'IP',
                @city, @region, @country_id, @isp, @timezone,
                @now, 1);

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_delete_geo_location]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[computer_geo_locations] WHERE [computer_id] = @computer_id;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 6. COMPUTER HISTORY SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_history_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [computer_id], [action], [old_value], [new_value],
           [changed_by], [changed_at], [notes]
    FROM [dbo].[computer_histories]
    WHERE [computer_id] = @computer_id AND [is_deleted] = 0
    ORDER BY [changed_at] DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_history_paged]
    @page           INT = 1,
    @page_size      INT = 25,
    @action         NVARCHAR(100) = NULL,
    @search         NVARCHAR(200) = NULL,
    @from_date      DATETIME2 = NULL,
    @to_date        DATETIME2 = NULL,
    @sort_by        NVARCHAR(50) = NULL,
    @sort_direction NVARCHAR(4) = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    -- Count
    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[computer_histories] h
    JOIN [dbo].[computers] c ON h.[computer_id] = c.[id]
    WHERE h.[is_deleted] = 0
      AND (@action IS NULL OR h.[action] = @action)
      AND (@from_date IS NULL OR h.[changed_at] >= @from_date)
      AND (@to_date IS NULL OR h.[changed_at] <= @to_date)
      AND (@search IS NULL OR (
           c.[hostname] LIKE '%' + @search + '%'
        OR c.[serial_number] LIKE '%' + @search + '%'
        OR h.[notes] LIKE '%' + @search + '%'
        OR h.[changed_by] LIKE '%' + @search + '%'));

    -- Data
    SELECT
        h.[id], h.[computer_id], h.[action], h.[old_value], h.[new_value],
        h.[changed_by], h.[changed_at], h.[notes],
        c.[hostname] AS [_computer_hostname],
        c.[serial_number] AS [_computer_serial_number]
    FROM [dbo].[computer_histories] h
    JOIN [dbo].[computers] c ON h.[computer_id] = c.[id]
    WHERE h.[is_deleted] = 0
      AND (@action IS NULL OR h.[action] = @action)
      AND (@from_date IS NULL OR h.[changed_at] >= @from_date)
      AND (@to_date IS NULL OR h.[changed_at] <= @to_date)
      AND (@search IS NULL OR (
           c.[hostname] LIKE '%' + @search + '%'
        OR c.[serial_number] LIKE '%' + @search + '%'
        OR h.[notes] LIKE '%' + @search + '%'
        OR h.[changed_by] LIKE '%' + @search + '%'))
    ORDER BY
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE LOWER(@sort_by)
                WHEN 'action'    THEN h.[action]
                WHEN 'changedby' THEN h.[changed_by]
                WHEN 'computer'  THEN c.[hostname]
            END
        END ASC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE LOWER(@sort_by)
                WHEN 'changedat' THEN h.[changed_at]
            END
        END ASC,
        CASE WHEN @sort_direction <> 'ASC' THEN
            CASE LOWER(@sort_by)
                WHEN 'action'    THEN h.[action]
                WHEN 'changedby' THEN h.[changed_by]
                WHEN 'computer'  THEN c.[hostname]
            END
        END DESC,
        CASE WHEN @sort_direction <> 'ASC' THEN
            CASE LOWER(@sort_by)
                WHEN 'changedat' THEN h.[changed_at]
                ELSE h.[changed_at]
            END
        END DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_distinct_history_actions]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT [action]
    FROM [dbo].[computer_histories]
    WHERE [is_deleted] = 0
    ORDER BY [action];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_soft_delete_old_history]
    @cutoff DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[computer_histories]
    SET [is_deleted] = 1, [deleted_at] = GETUTCDATE()
    WHERE [changed_at] < @cutoff AND [is_deleted] = 0;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 7. COMPUTER OWNERSHIP SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_ownership]
    @computer_id INT,
    @user_id     INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [computer_id], [user_id], [access_level], [granted_at], [granted_by]
    FROM [dbo].[computer_ownerships]
    WHERE [computer_id] = @computer_id AND [user_id] = @user_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_ownerships_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        o.[id], o.[computer_id], o.[user_id], o.[access_level], o.[granted_at], o.[granted_by],
        u.[username] AS [_user_username], u.[first_name] AS [_user_first_name],
        u.[last_name] AS [_user_last_name], u.[email] AS [_user_email]
    FROM [dbo].[computer_ownerships] o
    JOIN [dbo].[users] u ON o.[user_id] = u.[id]
    WHERE o.[computer_id] = @computer_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_ownerships_by_user_id]
    @user_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        o.[id], o.[computer_id], o.[user_id], o.[access_level], o.[granted_at], o.[granted_by],
        c.[hostname] AS [_computer_hostname], c.[serial_number] AS [_computer_serial_number]
    FROM [dbo].[computer_ownerships] o
    JOIN [dbo].[computers] c ON o.[computer_id] = c.[id]
    WHERE o.[user_id] = @user_id AND c.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_has_ownership_access]
    @computer_id    INT,
    @user_id        INT,
    @minimum_level  INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[computer_ownerships]
        WHERE [computer_id] = @computer_id
          AND [user_id] = @user_id
          AND [access_level] >= @minimum_level
    ) THEN 1 ELSE 0 END AS BIT) AS [has_access];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_remove_ownership]
    @computer_id INT,
    @user_id     INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[computer_ownerships]
    WHERE [computer_id] = @computer_id AND [user_id] = @user_id;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_remove_all_ownerships_for_computer]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[computer_ownerships] WHERE [computer_id] = @computer_id;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8. RESTORE POINT SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_restore_point_by_guid]
    @restore_point_id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [computer_id], [restore_point_id], [created_at],
           [type_id], [description], [data_size_bytes], [data_hash],
           [is_complete], [error_message], [is_deleted], [deleted_at]
    FROM [dbo].[restore_points]
    WHERE [restore_point_id] = @restore_point_id AND [is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_restore_point_by_guid_with_details]
    @restore_point_id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- RS1: Restore point + computer + type
    SELECT
        rp.[id], rp.[computer_id], rp.[restore_point_id], rp.[created_at],
        rp.[type_id], rp.[description], rp.[data_size_bytes], rp.[data_hash],
        rp.[is_complete], rp.[error_message], rp.[is_deleted], rp.[deleted_at],
        c.[hostname] AS [_computer_hostname], c.[serial_number] AS [_computer_serial_number],
        rt.[name] AS [_type_name]
    FROM [dbo].[restore_points] rp
    JOIN [dbo].[computers] c ON rp.[computer_id] = c.[id]
    JOIN [dbo].[restore_point_types] rt ON rp.[type_id] = rt.[id]
    WHERE rp.[restore_point_id] = @restore_point_id AND rp.[is_deleted] = 0;

    -- RS2: System info
    SELECT si.[id], si.[restore_point_id], si.[system_boot_time], si.[uptime_seconds],
           si.[hostname], si.[serial_number], si.[ip_address], si.[mac_address],
           si.[manufacturer], si.[model], si.[current_user], si.[created_at], si.[updated_at]
    FROM [dbo].[restore_point_system_infos] si
    JOIN [dbo].[restore_points] rp ON si.[restore_point_id] = rp.[id]
    WHERE rp.[restore_point_id] = @restore_point_id AND rp.[is_deleted] = 0;

    -- RS3: Snapshot (GZip-compressed VARBINARY columns)
    SELECT sn.[id], sn.[restore_point_id],
           sn.[system_details_json], sn.[installed_software_json],
           sn.[running_processes_json], sn.[windows_services_json],
           sn.[startup_programs_json], sn.[scheduled_tasks_json],
           sn.[environment_variables_json], sn.[network_config_json],
           sn.[storage_config_json], sn.[event_log_summary_json],
           sn.[windows_updates_json], sn.[device_drivers_json],
           sn.[firewall_rules_json], sn.[local_users_json],
           sn.[local_groups_json], sn.[shared_folders_json],
           sn.[printers_json], sn.[usb_devices_json],
           sn.[battery_status_json], sn.[bit_locker_status_json],
           sn.[security_status_json],
           sn.[created_at], sn.[updated_at]
    FROM [dbo].[restore_point_snapshots] sn
    JOIN [dbo].[restore_points] rp ON sn.[restore_point_id] = rp.[id]
    WHERE rp.[restore_point_id] = @restore_point_id AND rp.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_restore_points_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;

    -- RS1: Restore points
    SELECT rp.[id], rp.[computer_id], rp.[restore_point_id], rp.[created_at],
           rp.[type_id], rp.[description], rp.[data_size_bytes], rp.[data_hash],
           rp.[is_complete], rp.[error_message], rp.[is_deleted], rp.[deleted_at]
    FROM [dbo].[restore_points] rp
    WHERE rp.[computer_id] = @computer_id AND rp.[is_deleted] = 0
    ORDER BY rp.[created_at] DESC;

    -- RS2: System infos for those restore points
    SELECT si.[id], si.[restore_point_id], si.[system_boot_time], si.[uptime_seconds],
           si.[hostname], si.[serial_number], si.[ip_address], si.[mac_address],
           si.[manufacturer], si.[model], si.[current_user], si.[created_at], si.[updated_at]
    FROM [dbo].[restore_point_system_infos] si
    JOIN [dbo].[restore_points] rp ON si.[restore_point_id] = rp.[id]
    WHERE rp.[computer_id] = @computer_id AND rp.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_latest_complete_restore_point]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @rp_id INT;
    SELECT TOP 1 @rp_id = [id] FROM [dbo].[restore_points]
    WHERE [computer_id] = @computer_id AND [is_complete] = 1 AND [is_deleted] = 0
    ORDER BY [created_at] DESC;

    IF @rp_id IS NULL RETURN;

    -- RS1: Restore point
    SELECT [id], [computer_id], [restore_point_id], [created_at],
           [type_id], [description], [data_size_bytes], [data_hash],
           [is_complete], [error_message], [is_deleted], [deleted_at]
    FROM [dbo].[restore_points] WHERE [id] = @rp_id;

    -- RS2: System info
    SELECT [id], [restore_point_id], [system_boot_time], [uptime_seconds],
           [hostname], [serial_number], [ip_address], [mac_address],
           [manufacturer], [model], [current_user], [created_at], [updated_at]
    FROM [dbo].[restore_point_system_infos] WHERE [restore_point_id] = @rp_id;

    -- RS3: Snapshot
    SELECT [id], [restore_point_id],
           [system_details_json], [installed_software_json],
           [running_processes_json], [windows_services_json],
           [startup_programs_json], [scheduled_tasks_json],
           [environment_variables_json], [network_config_json],
           [storage_config_json], [event_log_summary_json],
           [windows_updates_json], [device_drivers_json],
           [firewall_rules_json], [local_users_json],
           [local_groups_json], [shared_folders_json],
           [printers_json], [usb_devices_json],
           [battery_status_json], [bit_locker_status_json],
           [security_status_json],
           [created_at], [updated_at]
    FROM [dbo].[restore_point_snapshots] WHERE [restore_point_id] = @rp_id;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 9. PACKAGE SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_packages]
AS
BEGIN
    SET NOCOUNT ON;
    -- RS1: Package versions
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [is_deleted] = 0
    ORDER BY [created_at] DESC;

    -- RS2: Distribution rules for all non-deleted packages
    SELECT dr.[id], dr.[package_version_id], dr.[rule_type], dr.[target_value],
           dr.[is_enabled], dr.[created_at]
    FROM [dbo].[package_distribution_rules] dr
    JOIN [dbo].[package_versions] pv ON dr.[package_version_id] = pv.[id]
    WHERE pv.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_package_by_id_with_rules]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    -- RS1: Package
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [id] = @id AND [is_deleted] = 0;

    -- RS2: Distribution rules
    SELECT [id], [package_version_id], [rule_type], [target_value], [is_enabled], [created_at]
    FROM [dbo].[package_distribution_rules]
    WHERE [package_version_id] = @id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_package_by_version]
    @version      NVARCHAR(50),
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    -- RS1: Package
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [version] = @version AND [package_type] = @package_type AND [is_deleted] = 0;

    -- RS2: Distribution rules
    SELECT dr.[id], dr.[package_version_id], dr.[rule_type], dr.[target_value],
           dr.[is_enabled], dr.[created_at]
    FROM [dbo].[package_distribution_rules] dr
    JOIN [dbo].[package_versions] pv ON dr.[package_version_id] = pv.[id]
    WHERE pv.[version] = @version AND pv.[package_type] = @package_type AND pv.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_active_package_version]
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    -- Status 2 = Active
    -- RS1: Package
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [status] = 2 AND [package_type] = @package_type AND [is_deleted] = 0;

    -- RS2: Distribution rules
    SELECT dr.[id], dr.[package_version_id], dr.[rule_type], dr.[target_value],
           dr.[is_enabled], dr.[created_at]
    FROM [dbo].[package_distribution_rules] dr
    JOIN [dbo].[package_versions] pv ON dr.[package_version_id] = pv.[id]
    WHERE pv.[status] = 2 AND pv.[package_type] = @package_type AND pv.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_packages_by_status]
    @status       INT,
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [status] = @status AND [package_type] = @package_type AND [is_deleted] = 0
    ORDER BY [created_at] DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_packages_by_channel]
    @channel      NVARCHAR(20),
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [channel] = @channel AND [package_type] = @package_type AND [is_deleted] = 0
    ORDER BY [created_at] DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_package_version_exists]
    @version      NVARCHAR(50),
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[package_versions]
        WHERE [version] = @version AND [package_type] = @package_type AND [is_deleted] = 0
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_increment_download_count]
    @package_id INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[package_versions]
    SET [download_count] = [download_count] + 1
    WHERE [id] = @package_id AND [is_deleted] = 0;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_distribution_rules]
    @package_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [package_version_id], [rule_type], [target_value], [is_enabled], [created_at]
    FROM [dbo].[package_distribution_rules]
    WHERE [package_version_id] = @package_id;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 10. SYSTEM DETAILS SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_latest_system_details_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sd_id INT;
    SELECT TOP 1 @sd_id = [id] FROM [dbo].[system_details]
    WHERE [computer_id] = @computer_id AND [is_deleted] = 0
    ORDER BY [collected_at] DESC;

    IF @sd_id IS NULL RETURN;

    -- RS1: system_details + computer
    SELECT sd.[id], sd.[computer_id], sd.[collected_at], sd.[machine_guid], sd.[product_id],
           c.[hostname] AS [_computer_hostname], c.[serial_number] AS [_computer_serial_number]
    FROM [dbo].[system_details] sd
    JOIN [dbo].[computers] c ON sd.[computer_id] = c.[id]
    WHERE sd.[id] = @sd_id;

    -- RS2: bios
    SELECT * FROM [dbo].[system_detail_bios] WHERE [system_detail_id] = @sd_id;
    -- RS3: motherboard
    SELECT * FROM [dbo].[system_detail_motherboards] WHERE [system_detail_id] = @sd_id;
    -- RS4: cpu
    SELECT * FROM [dbo].[system_detail_cpus] WHERE [system_detail_id] = @sd_id;
    -- RS5: memory
    SELECT * FROM [dbo].[system_detail_memories] WHERE [system_detail_id] = @sd_id;
    -- RS6: storage
    SELECT * FROM [dbo].[system_detail_storages] WHERE [system_detail_id] = @sd_id;
    -- RS7: gpu
    SELECT * FROM [dbo].[system_detail_gpus] WHERE [system_detail_id] = @sd_id;
    -- RS8: network
    SELECT * FROM [dbo].[system_detail_networks] WHERE [system_detail_id] = @sd_id;
    -- RS9: os
    SELECT * FROM [dbo].[system_detail_oses] WHERE [system_detail_id] = @sd_id;
    -- RS10: security
    SELECT * FROM [dbo].[system_detail_securities] WHERE [system_detail_id] = @sd_id;
    -- RS11: battery
    SELECT * FROM [dbo].[system_detail_batteries] WHERE [system_detail_id] = @sd_id;
    -- RS12: chassis
    SELECT * FROM [dbo].[system_detail_chasses] WHERE [system_detail_id] = @sd_id;
    -- RS13: software
    SELECT * FROM [dbo].[system_detail_softwares] WHERE [system_detail_id] = @sd_id;
    -- RS14: users_groups
    SELECT * FROM [dbo].[system_detail_users_groups] WHERE [system_detail_id] = @sd_id;
    -- RS15: peripherals
    SELECT * FROM [dbo].[system_detail_peripherals] WHERE [system_detail_id] = @sd_id;
    -- RS16: event_logs
    SELECT * FROM [dbo].[system_detail_event_logs] WHERE [system_detail_id] = @sd_id;
    -- RS17: performance
    SELECT * FROM [dbo].[system_detail_performances] WHERE [system_detail_id] = @sd_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_system_details_history]
    @computer_id INT,
    @limit       INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    -- Get IDs of the N latest details for this computer
    DECLARE @ids TABLE ([id] INT);
    INSERT INTO @ids
    SELECT TOP (@limit) [id] FROM [dbo].[system_details]
    WHERE [computer_id] = @computer_id AND [is_deleted] = 0
    ORDER BY [collected_at] DESC;

    -- RS1: system_details
    SELECT sd.[id], sd.[computer_id], sd.[collected_at], sd.[machine_guid], sd.[product_id]
    FROM [dbo].[system_details] sd
    WHERE sd.[id] IN (SELECT [id] FROM @ids)
    ORDER BY sd.[collected_at] DESC;

    -- RS2: os
    SELECT o.* FROM [dbo].[system_detail_oses] o
    WHERE o.[system_detail_id] IN (SELECT [id] FROM @ids);

    -- RS3: performance
    SELECT p.* FROM [dbo].[system_detail_performances] p
    WHERE p.[system_detail_id] IN (SELECT [id] FROM @ids);

    -- RS4: software
    SELECT s.* FROM [dbo].[system_detail_softwares] s
    WHERE s.[system_detail_id] IN (SELECT [id] FROM @ids);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_latest_system_details_all]
    @page      INT = NULL,
    @page_size INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Find latest system_details ID per computer
    DECLARE @latest TABLE ([id] INT);
    INSERT INTO @latest
    SELECT MAX(sd.[id])
    FROM [dbo].[system_details] sd
    JOIN (
        SELECT [computer_id], MAX([collected_at]) AS [max_collected]
        FROM [dbo].[system_details]
        WHERE [is_deleted] = 0
        GROUP BY [computer_id]
    ) sub ON sd.[computer_id] = sub.[computer_id] AND sd.[collected_at] = sub.[max_collected]
    WHERE sd.[is_deleted] = 0
    GROUP BY sd.[computer_id];

    -- RS1: total count
    SELECT COUNT(*) AS [total_count] FROM @latest;

    -- Apply pagination if specified
    DECLARE @paged TABLE ([id] INT);
    IF @page IS NOT NULL AND @page_size IS NOT NULL
        INSERT INTO @paged SELECT [id] FROM @latest ORDER BY [id] OFFSET (@page - 1) * @page_size ROWS FETCH NEXT @page_size ROWS ONLY;
    ELSE
        INSERT INTO @paged SELECT [id] FROM @latest;

    -- RS2: system_details + computer
    SELECT sd.[id], sd.[computer_id], sd.[collected_at], sd.[machine_guid], sd.[product_id],
           c.[hostname] AS [_computer_hostname], c.[serial_number] AS [_computer_serial_number]
    FROM [dbo].[system_details] sd
    JOIN [dbo].[computers] c ON sd.[computer_id] = c.[id]
    WHERE sd.[id] IN (SELECT [id] FROM @paged);

    -- RS3-RS18: all sub-entities for those IDs
    SELECT * FROM [dbo].[system_detail_bios] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_motherboards] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_cpus] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_memories] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_storages] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_gpus] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_networks] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_oses] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_securities] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_batteries] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_chasses] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_softwares] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_users_groups] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_peripherals] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_event_logs] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_performances] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 11. VERSION HISTORY SPs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_version_history_by_type_unpaged]
    @type_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [type_id], [change_log], [released_at], [released_by],
           [is_current_version], [checksum], [file_size],
           [is_deleted], [deleted_at]
    FROM [dbo].[version_histories]
    WHERE [type_id] = @type_id AND [is_deleted] = 0
    ORDER BY [released_at] DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_version_exists]
    @type_id INT,
    @version NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[version_histories]
        WHERE [type_id] = @type_id AND [version] = @version AND [is_deleted] = 0
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_find_version_by_type_and_version]
    @type_id INT,
    @version NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [type_id], [change_log], [released_at], [released_by],
           [is_current_version], [checksum], [file_size],
           [is_deleted], [deleted_at]
    FROM [dbo].[version_histories]
    WHERE [type_id] = @type_id AND [version] = @version AND [is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_current_versions_by_type]
    @type_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [type_id], [change_log], [released_at], [released_by],
           [is_current_version], [checksum], [file_size],
           [is_deleted], [deleted_at]
    FROM [dbo].[version_histories]
    WHERE [type_id] = @type_id AND [is_current_version] = 1 AND [is_deleted] = 0;
END
GO
