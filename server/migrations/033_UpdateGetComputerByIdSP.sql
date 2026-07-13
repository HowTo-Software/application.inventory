-- 033_UpdateGetComputerByIdSP.sql
-- Align sp_get_computer_by_id column naming with sp_get_all_computers convention:
--   • Use c.* to include is_deleted, deleted_at, row_version (needed by MapComputerFromReader)
--   • Prefix navigation property columns with _ (e.g. _model_name, _status_name)
--   • Add cas_computer_id / ur_computer_id aliases (needed by MapAgentStatusFromReader / MapUpdateRequestFromReader)
-- ----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_by_id]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: Computer with includes (denormalized navigation columns)
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
        ur.[update_requested_by], ur.[previous_agent_version],
        ur.[last_updated_at] AS [ur_last_updated], ur.[created_at] AS [ur_created_at],
        -- Denormalized names for in-memory navigation property hydration
        cs.[name]   AS [_status_name],
        ct.[name]   AS [_type_name],
        loc.[name]  AS [_location_name],
        dept.[name] AS [_department_name],
        m.[model_name]        AS [_model_name],
        m.[manufacturer_id]   AS [_manufacturer_id],
        mfr.[name]            AS [_manufacturer_name],
        os.[name]             AS [_os_name]
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
