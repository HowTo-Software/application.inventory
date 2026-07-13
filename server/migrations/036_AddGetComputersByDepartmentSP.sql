-- ============================================================================
-- Migration 036: Add sp_get_computers_by_department
-- Replaces EF Core LINQ queries in ComputerRepository.GetByDepartmentAsync()
-- with a server-side filtered, sorted, paginated stored procedure.
-- Returns the same column layout as sp_get_all_computers so the repository
-- can reuse MapComputerFromReader / MapAgentStatusFromReader /
-- MapUpdateRequestFromReader.
-- ============================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_get_computers_by_department]
    @department_id   INT,
    @sort_by         NVARCHAR(50)  = NULL,
    @sort_direction  NVARCHAR(4)   = 'ASC',
    @page            INT           = NULL,      -- NULL = return all rows (no paging)
    @page_size       INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ── Count ───────────────────────────────────────────────────────────
    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[computers] c
    WHERE c.[is_deleted] = 0
      AND c.[department_id] = @department_id;

    -- ── Data ────────────────────────────────────────────────────────────
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
      AND c.[department_id] = @department_id
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
    OFFSET ISNULL((@page - 1) * @page_size, 0) ROWS
    FETCH NEXT ISNULL(@page_size, 2147483647) ROWS ONLY;
END
GO
