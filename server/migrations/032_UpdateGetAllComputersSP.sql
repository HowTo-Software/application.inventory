-- ============================================================================
-- Migration 032: Update sp_get_all_computers
-- Replaces the simple all-computers query with a server-side filtered,
-- sorted, paginated stored procedure that returns the same column layout
-- as sp_get_computer_by_serial_number (c.*, agent status, update request)
-- so the repository can reuse MapComputerFromReader / MapAgentStatusFromReader
-- / MapUpdateRequestFromReader.
-- ============================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_computers]
    @search_ids         NVARCHAR(MAX)   = NULL,   -- comma-separated IDs from sp_search_computers (NULL = no search filter)
    @status             NVARCHAR(100)   = NULL,   -- filter by computer_statuses.name
    @type               NVARCHAR(100)   = NULL,   -- filter by computer_types.name
    @location           NVARCHAR(100)   = NULL,   -- filter by locations.name
    @department         NVARCHAR(100)   = NULL,   -- filter by departments.name
    @connection         NVARCHAR(20)    = NULL,   -- 'Online', 'Offline', 'NoAgent'
    @cutoff_minutes     INT             = 10,     -- minutes for online/offline threshold
    @sort_by            NVARCHAR(50)    = NULL,   -- sort column key (matches SortExtensions)
    @sort_direction     NVARCHAR(4)     = 'ASC',  -- 'ASC' or 'DESC'
    @page               INT             = 1,
    @page_size          INT             = 50
AS
BEGIN
    SET NOCOUNT ON;

    -- Parse comma-separated search IDs into a table variable
    DECLARE @SearchIds TABLE ([id] INT);
    IF @search_ids IS NOT NULL
    BEGIN
        INSERT INTO @SearchIds ([id])
        SELECT CAST(value AS INT)
        FROM STRING_SPLIT(@search_ids, ',')
        WHERE RTRIM(LTRIM(value)) <> '';
    END

    DECLARE @cutoff DATETIME2 = DATEADD(MINUTE, -@cutoff_minutes, SYSUTCDATETIME());

    -- ── Count ───────────────────────────────────────────────────────────
    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[computers] c
    LEFT JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT JOIN [dbo].[computer_statuses] cs        ON cs.[id] = c.[status_id]
    LEFT JOIN [dbo].[computer_types] ct           ON ct.[id] = c.[type_id]
    LEFT JOIN [dbo].[locations] loc               ON loc.[id] = c.[location_id]
    LEFT JOIN [dbo].[departments] dept            ON dept.[id] = c.[department_id]
    WHERE c.[is_deleted] = 0
      AND (@search_ids IS NULL OR c.[id] IN (SELECT [id] FROM @SearchIds))
      AND (@status     IS NULL OR cs.[name] = @status)
      AND (@type       IS NULL OR ct.[name] = @type)
      AND (@location   IS NULL OR loc.[name] = @location)
      AND (@department IS NULL OR dept.[name] = @department)
      AND (
            @connection IS NULL
            OR (@connection = 'Online'  AND cas.[has_agent] = 1 AND cas.[last_seen] IS NOT NULL AND cas.[last_seen] > @cutoff)
            OR (@connection = 'Offline' AND cas.[has_agent] = 1 AND (cas.[last_seen] IS NULL OR cas.[last_seen] <= @cutoff))
            OR (@connection = 'NoAgent' AND (cas.[has_agent] = 0 OR cas.[has_agent] IS NULL))
          );

    -- ── Paged data ──────────────────────────────────────────────────────
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
        ur.[created_at] AS [ur_created_at],
        -- Denormalized names for in-memory navigation property hydration
        cs.[name]   AS [_status_name],
        ct.[name]   AS [_type_name],
        loc.[name]  AS [_location_name],
        dept.[name] AS [_department_name],
        m.[model_name]        AS [_model_name],
        m.[manufacturer_id]   AS [_manufacturer_id],
        mfr.[name]            AS [_manufacturer_name]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m          ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr          ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os       ON os.[id] = c.[operating_system_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_update_requests] ur ON ur.[id] = cas.[active_update_request_id]
    LEFT  JOIN [dbo].[computer_statuses] cs       ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[computer_types] ct          ON ct.[id] = c.[type_id]
    LEFT  JOIN [dbo].[locations] loc              ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept           ON dept.[id] = c.[department_id]
    WHERE c.[is_deleted] = 0
      AND (@search_ids IS NULL OR c.[id] IN (SELECT [id] FROM @SearchIds))
      AND (@status     IS NULL OR cs.[name] = @status)
      AND (@type       IS NULL OR ct.[name] = @type)
      AND (@location   IS NULL OR loc.[name] = @location)
      AND (@department IS NULL OR dept.[name] = @department)
      AND (
            @connection IS NULL
            OR (@connection = 'Online'  AND cas.[has_agent] = 1 AND cas.[last_seen] IS NOT NULL AND cas.[last_seen] > @cutoff)
            OR (@connection = 'Offline' AND cas.[has_agent] = 1 AND (cas.[last_seen] IS NULL OR cas.[last_seen] <= @cutoff))
            OR (@connection = 'NoAgent' AND (cas.[has_agent] = 0 OR cas.[has_agent] IS NULL))
          )
    ORDER BY
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END DESC,
        -- Date/boolean/numeric sorts need separate CASE blocks due to type differences
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END DESC,
        c.[hostname] ASC  -- default tiebreaker
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO
