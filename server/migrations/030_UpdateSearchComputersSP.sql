-- ============================================================================
-- 030 — Update sp_search_computers
-- Replaces the full-row SP with an ID-only version for EF Core hydration.
-- Returns two result sets:
--   1) Total count of matching computers
--   2) Matching computer IDs (ordered by hostname)
-- ============================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_search_computers]
    @search_term NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @term NVARCHAR(202) = N'%' + @search_term + N'%';

    SELECT c.[id]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m      ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr      ON mfr.[id] = m.[manufacturer_id]
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
