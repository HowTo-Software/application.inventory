-- Wrap SUM() aggregations in ISNULL() to avoid returning NULL when there are
-- no matching rows (e.g., empty computers table).  The C# reader cast
-- (int)reader["column"] throws InvalidCastException on DBNull.

CREATE OR ALTER PROCEDURE [dbo].[sp_get_dashboard_counts]
    @latest_agent_version NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @stale_cutoff DATETIME2 = DATEADD(HOUR, -24, GETUTCDATE());
    DECLARE @now DATETIME2 = GETUTCDATE();

    SELECT
        COUNT(*)                                                                              AS [total_computers],
        ISNULL(SUM(CASE WHEN cas.[is_online] = 1 THEN 1 ELSE 0 END), 0)                      AS [online_computers],
        ISNULL(SUM(CASE WHEN cas.[is_online] = 0 THEN 1 ELSE 0 END), 0)                      AS [offline_computers],
        ISNULL(SUM(CASE WHEN cas.[has_agent] = 1 THEN 1 ELSE 0 END), 0)                      AS [agents_installed],
        ISNULL(SUM(CASE WHEN ur.[id] IS NOT NULL
                  AND ur.[update_status_id] NOT IN (0, 50, 99) THEN 1 ELSE 0 END), 0)        AS [agents_updating],
        ISNULL(SUM(CASE WHEN ur.[id] IS NOT NULL
                  AND ur.[update_status_id] = 99 THEN 1 ELSE 0 END), 0)                      AS [agents_failed_update],
        ISNULL(SUM(CASE WHEN cas.[force_update] = 1 THEN 1 ELSE 0 END), 0)                   AS [agents_pending_update],
        CASE WHEN @latest_agent_version IS NOT NULL
             THEN ISNULL(SUM(CASE WHEN cas.[has_agent] = 1
                            AND cas.[agent_version] = @latest_agent_version
                       THEN 1 ELSE 0 END), 0)
             ELSE 0 END                                                                       AS [agents_on_latest_version],
        CASE WHEN @latest_agent_version IS NOT NULL
             THEN ISNULL(SUM(CASE WHEN cas.[has_agent] = 1
                            AND cas.[agent_version] IS NOT NULL
                            AND cas.[agent_version] <> ''
                            AND cas.[agent_version] <> @latest_agent_version
                       THEN 1 ELSE 0 END), 0)
             ELSE 0 END                                                                       AS [agents_on_older_version],
        ISNULL(SUM(CASE WHEN cas.[has_agent] = 1
                  AND cas.[last_seen] IS NOT NULL
                  AND cas.[last_seen] < @stale_cutoff THEN 1 ELSE 0 END), 0)                  AS [stale_agents],
        ISNULL(SUM(CASE WHEN c.[warranty_expiration] IS NOT NULL
                  AND c.[warranty_expiration] < @now THEN 1 ELSE 0 END), 0)                   AS [expired_warranties]
    FROM [dbo].[computers] c
    LEFT JOIN [dbo].[computer_agent_statuses] cas ON c.[id] = cas.[computer_id]
    LEFT JOIN [dbo].[computer_update_requests] ur ON cas.[active_update_request_id] = ur.[id]
    WHERE c.[is_deleted] = 0;
END
GO

-- Also fix sp_get_maintenance_summary_counts which has the same NULL issue
CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_summary_counts]
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @start_of_month DATETIME2 = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);

    SELECT
        ISNULL(SUM(CASE WHEN [status_id] IN (0, 1) THEN 1 ELSE 0 END), 0) AS [open_count],
        ISNULL(SUM(CASE WHEN [status_id] = 0 THEN 1 ELSE 0 END), 0)       AS [scheduled_count],
        ISNULL(SUM(CASE WHEN [status_id] = 2 AND [completed_date] >= @start_of_month THEN 1 ELSE 0 END), 0) AS [completed_this_month]
    FROM [dbo].[maintenance_records]
    WHERE [is_deleted] = 0;
END
GO
