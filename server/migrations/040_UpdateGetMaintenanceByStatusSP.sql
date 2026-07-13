-- Migration:  040_UpdateGetMaintenanceByStatusSP
-- Created:    2026-03-23
-- Purpose:    Update sp_get_maintenance_by_status to add pagination and
--             use underscore-prefixed aliases for denormalized columns
--             (consistent with sp_get_maintenance_by_computer_id pattern).
--             Replaces the EF Core LINQ query in MaintenanceRepository.GetByStatusPagedAsync().

CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_by_status]
    @status_id      INT,
    @page           INT = 1,
    @page_size      INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    -- Result Set 1: Total count for this status
    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[maintenance_records] mr
    WHERE mr.[status_id] = @status_id
      AND mr.[is_deleted] = 0;

    -- Result Set 2: Paged data with denormalized joins
    SELECT
        mr.[id],
        mr.[computer_id],
        mr.[type_id],
        mr.[description],
        mr.[scheduled_date],
        mr.[completed_date],
        mr.[cost],
        mr.[performed_by],
        mr.[notes],
        mr.[status_id],
        mr.[created_at],
        mr.[created_by],
        mr.[row_version],
        c.[hostname]        AS [_computer_hostname],
        c.[serial_number]   AS [_computer_serial_number],
        mt.[name]           AS [_type_name],
        ms.[name]           AS [_status_name]
    FROM [dbo].[maintenance_records] mr
    INNER JOIN [dbo].[computers] c              ON c.[id]  = mr.[computer_id]
    INNER JOIN [dbo].[maintenance_types] mt     ON mt.[id] = mr.[type_id]
    INNER JOIN [dbo].[maintenance_statuses] ms  ON ms.[id] = mr.[status_id]
    WHERE mr.[status_id] = @status_id
      AND mr.[is_deleted] = 0
    ORDER BY mr.[scheduled_date] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO
