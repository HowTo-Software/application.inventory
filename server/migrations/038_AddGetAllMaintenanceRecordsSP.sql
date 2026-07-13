-- ============================================================================
-- Migration 038: Add sp_get_all_maintenance_records
-- Replaces the EF Core LINQ query in MaintenanceRepository.GetPagedAsync()
-- that uses 3 includes (Computer, TypeLookup, StatusLookup) with a stored
-- procedure that does server-side sorting, pagination, and denormalized JOINs.
--
-- Returns two result sets:
--   1) Total count (INT)
--   2) Paged maintenance records with denormalized lookup/computer names
-- ============================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_maintenance_records]
    @sort_by        NVARCHAR(50)    = NULL,   -- sort column key (matches SortExtensions)
    @sort_direction NVARCHAR(4)     = 'DESC', -- 'ASC' or 'DESC'
    @page           INT             = 1,
    @page_size      INT             = 25
AS
BEGIN
    SET NOCOUNT ON;

    -- ── Count ───────────────────────────────────────────────────────────
    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[maintenance_records] mr
    WHERE mr.[is_deleted] = 0;

    -- ── Paged data ──────────────────────────────────────────────────────
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
        -- Denormalized names for in-memory navigation property hydration
        c.[hostname]        AS [_computer_hostname],
        c.[serial_number]   AS [_computer_serial_number],
        mt.[name]           AS [_type_name],
        ms.[name]           AS [_status_name]
    FROM [dbo].[maintenance_records] mr
    INNER JOIN [dbo].[computers] c               ON c.[id]  = mr.[computer_id]
    INNER JOIN [dbo].[maintenance_types] mt       ON mt.[id] = mr.[type_id]
    INNER JOIN [dbo].[maintenance_statuses] ms    ON ms.[id] = mr.[status_id]
    WHERE mr.[is_deleted] = 0
    ORDER BY
        -- String sorts
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'type'        THEN mt.[name]
                WHEN 'status'      THEN ms.[name]
                WHEN 'computer'    THEN c.[hostname]
                WHEN 'performedby' THEN mr.[performed_by]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'type'        THEN mt.[name]
                WHEN 'status'      THEN ms.[name]
                WHEN 'computer'    THEN c.[hostname]
                WHEN 'performedby' THEN mr.[performed_by]
            END
        END DESC,
        -- Date sorts
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'scheduleddate'  THEN mr.[scheduled_date]
                WHEN 'completeddate'  THEN mr.[completed_date]
                WHEN 'createdat'      THEN mr.[created_at]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'scheduleddate'  THEN mr.[scheduled_date]
                WHEN 'completeddate'  THEN mr.[completed_date]
                WHEN 'createdat'      THEN mr.[created_at]
            END
        END DESC,
        -- Numeric sorts
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'cost' THEN mr.[cost]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'cost' THEN mr.[cost]
            END
        END DESC,
        mr.[scheduled_date] DESC  -- default tiebreaker
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO
