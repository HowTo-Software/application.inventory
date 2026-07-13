-- Migration: Remove duplicated update-tracking fields from [computer_agent_statuses]
-- The update state now lives exclusively on [computer_update_requests].
-- A new [active_update_request_id] FK on [computer_agent_statuses] points to the current request.

BEGIN TRANSACTION;

-- 1. Add the FK column pointing to the active update request
ALTER TABLE [dbo].[computer_agent_statuses]
    ADD [active_update_request_id] INT NULL;

-- 2. Migrate current in-flight update state into [computer_update_requests]
--    For every agent status row that has an active update (status > 0), create
--    a corresponding update request record and link it.
INSERT INTO [dbo].[computer_update_requests]
    ([computer_id], [update_status_id], [update_progress], [update_status_message],
     [update_started_at], [update_error_logs], [update_requested_at],
     [update_requested_by], [previous_agent_version], [last_updated_at], [created_at])
SELECT
    s.[computer_id],
    s.[update_status],
    s.[update_progress],
    s.[update_status_message],
    s.[update_started_at],
    s.[update_error_logs],
    s.[update_requested_at],
    s.[update_requested_by],
    s.[previous_agent_version],
    s.[last_updated_at],
    GETUTCDATE()
FROM [dbo].[computer_agent_statuses] s
WHERE s.[update_status] > 0
   OR s.[update_requested_at] IS NOT NULL
   OR s.[update_error_logs] IS NOT NULL;

-- 3. Link the newly created requests back to agent statuses
--    Uses EXEC so the statement compiles in a new scope that sees the column added in step 1.
EXEC('
UPDATE s
SET s.[active_update_request_id] = r.[id]
FROM [dbo].[computer_agent_statuses] s
INNER JOIN [dbo].[computer_update_requests] r
    ON r.[computer_id] = s.[computer_id]
    AND r.[id] = (
        SELECT MAX(r2.[id])
        FROM [dbo].[computer_update_requests] r2
        WHERE r2.[computer_id] = s.[computer_id]
    )
WHERE s.[update_status] > 0
   OR s.[update_requested_at] IS NOT NULL
   OR s.[update_error_logs] IS NOT NULL;
');

-- 4. Add the FK constraint
ALTER TABLE [dbo].[computer_agent_statuses]
    ADD CONSTRAINT [fk_computer_agent_statuses_active_update_request]
        FOREIGN KEY ([active_update_request_id])
        REFERENCES [dbo].[computer_update_requests] ([id])
        ON DELETE NO ACTION;

-- 5. Drop default constraints, then drop the duplicated columns from [computer_agent_statuses]
--    Default constraint names are auto-generated, so we must look them up dynamically.
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql + N'ALTER TABLE [dbo].[computer_agent_statuses] DROP CONSTRAINT [' + dc.name + N'];' + CHAR(13)
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.computer_agent_statuses')
  AND c.name IN (
      'update_status', 'update_progress', 'update_status_message',
      'update_started_at', 'update_error_logs', 'update_requested_at',
      'update_requested_by', 'last_updated_at', 'previous_agent_version'
  );

EXEC sp_executesql @sql;

ALTER TABLE [dbo].[computer_agent_statuses] DROP COLUMN [update_status];
ALTER TABLE [dbo].[computer_agent_statuses] DROP COLUMN [update_progress];
ALTER TABLE [dbo].[computer_agent_statuses] DROP COLUMN [update_status_message];
ALTER TABLE [dbo].[computer_agent_statuses] DROP COLUMN [update_started_at];
ALTER TABLE [dbo].[computer_agent_statuses] DROP COLUMN [update_error_logs];
ALTER TABLE [dbo].[computer_agent_statuses] DROP COLUMN [update_requested_at];
ALTER TABLE [dbo].[computer_agent_statuses] DROP COLUMN [update_requested_by];
ALTER TABLE [dbo].[computer_agent_statuses] DROP COLUMN [last_updated_at];
ALTER TABLE [dbo].[computer_agent_statuses] DROP COLUMN [previous_agent_version];

COMMIT TRANSACTION;
