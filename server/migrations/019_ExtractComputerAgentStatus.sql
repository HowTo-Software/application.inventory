-- Migration: Extract agent/update fields from [computers] into [computer_agent_statuses]
-- Creates a 1:1 relationship table for agent connectivity, command flags, and update tracking.
-- This separates hardware/inventory data from volatile agent runtime state.

BEGIN TRANSACTION;

-- 1. Create the new table
CREATE TABLE [dbo].[computer_agent_statuses] (
    [id]                          INT              IDENTITY(1,1) NOT NULL,
    [computer_id]                 INT              NOT NULL,

    -- Agent connectivity
    [last_seen]                   DATETIME2        NULL,
    [is_online]                   BIT              NOT NULL DEFAULT 0,
    [current_user]                NVARCHAR(MAX)    NULL,
    [has_agent]                   BIT              NOT NULL DEFAULT 0,
    [public_ip_address]           NVARCHAR(MAX)    NULL,
    [agent_version]               NVARCHAR(50)     NULL,
    [agent_session_id]            NVARCHAR(64)     NULL,

    -- Heartbeat configuration
    [heartbeat_interval_minutes]  INT              NOT NULL DEFAULT 5,

    -- Agent command flags
    [force_update]                BIT              NOT NULL DEFAULT 0,
    [force_reinstall]             BIT              NOT NULL DEFAULT 0,
    [reinstall_attempted]         BIT              NOT NULL DEFAULT 0,
    [request_immediate_checkin]   BIT              NOT NULL DEFAULT 0,
    [preferred_location_source]   NVARCHAR(MAX)    NOT NULL DEFAULT 'Auto',

    -- Update tracking
    [update_status]               INT              NOT NULL DEFAULT 0,
    [update_progress]             INT              NOT NULL DEFAULT 0,
    [update_status_message]       NVARCHAR(500)    NULL,
    [update_started_at]           DATETIME2        NULL,
    [update_error_logs]           NVARCHAR(MAX)    NULL,
    [update_requested_at]         DATETIME2        NULL,
    [update_requested_by]         NVARCHAR(100)    NULL,
    [last_updated_at]             DATETIME2        NULL,
    [previous_agent_version]      NVARCHAR(50)     NULL,

    CONSTRAINT [pk_computer_agent_statuses] PRIMARY KEY ([id]),
    CONSTRAINT [fk_computer_agent_statuses_computers_computer_id]
        FOREIGN KEY ([computer_id]) REFERENCES [dbo].[computers] ([id]) ON DELETE CASCADE
);

-- 2. Create indexes
CREATE UNIQUE INDEX [ix_computer_agent_statuses_computer_id]
    ON [dbo].[computer_agent_statuses] ([computer_id]);

CREATE INDEX [ix_computer_agent_statuses_last_seen]
    ON [dbo].[computer_agent_statuses] ([last_seen]);

CREATE INDEX [ix_computer_agent_statuses_is_online]
    ON [dbo].[computer_agent_statuses] ([is_online]);

CREATE INDEX [ix_computer_agent_statuses_has_agent]
    ON [dbo].[computer_agent_statuses] ([has_agent]);

-- 3. Migrate data from computers into computer_agent_statuses
INSERT INTO [dbo].[computer_agent_statuses] (
    [computer_id],
    [last_seen], [is_online], [current_user], [has_agent], [public_ip_address],
    [agent_version], [agent_session_id], [heartbeat_interval_minutes],
    [force_update], [force_reinstall], [reinstall_attempted], [request_immediate_checkin],
    [preferred_location_source],
    [update_status], [update_progress], [update_status_message], [update_started_at],
    [update_error_logs], [update_requested_at], [update_requested_by],
    [last_updated_at], [previous_agent_version]
)
SELECT
    [id],
    [last_seen], [is_online], [current_user], [has_agent], [public_ip_address],
    [agent_version], [agent_session_id], [heartbeat_interval_minutes],
    [force_update], [force_reinstall], [reinstall_attempted], [request_immediate_checkin],
    [preferred_location_source],
    [update_status], [update_progress], [update_status_message], [update_started_at],
    [update_error_logs], [update_requested_at], [update_requested_by],
    [last_updated_at], [previous_agent_version]
FROM [dbo].[computers];

-- 4. Drop old indexes on columns being moved
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_computers_last_seen' AND object_id = OBJECT_ID('dbo.computers'))
    DROP INDEX [ix_computers_last_seen] ON [dbo].[computers];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_computers_is_online' AND object_id = OBJECT_ID('dbo.computers'))
    DROP INDEX [ix_computers_is_online] ON [dbo].[computers];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_computers_has_agent' AND object_id = OBJECT_ID('dbo.computers'))
    DROP INDEX [ix_computers_has_agent] ON [dbo].[computers];

-- 5. Drop default constraints on columns being moved
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql += N'ALTER TABLE [dbo].[computers] DROP CONSTRAINT [' + dc.name + N'];' + CHAR(13)
FROM sys.default_constraints dc
JOIN sys.columns col ON dc.parent_object_id = col.object_id AND dc.parent_column_id = col.column_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.computers')
  AND col.name IN (
    'last_seen', 'is_online', 'current_user', 'has_agent', 'public_ip_address',
    'agent_version', 'agent_session_id', 'heartbeat_interval_minutes',
    'force_update', 'force_reinstall', 'reinstall_attempted', 'request_immediate_checkin',
    'preferred_location_source',
    'update_status', 'update_progress', 'update_status_message', 'update_started_at',
    'update_error_logs', 'update_requested_at', 'update_requested_by',
    'last_updated_at', 'previous_agent_version'
  );

EXEC sp_executesql @sql;

-- 6. Drop moved columns from computers table
ALTER TABLE [dbo].[computers] DROP COLUMN
    [last_seen],
    [is_online],
    [current_user],
    [has_agent],
    [public_ip_address],
    [agent_version],
    [agent_session_id],
    [heartbeat_interval_minutes],
    [force_update],
    [force_reinstall],
    [reinstall_attempted],
    [request_immediate_checkin],
    [preferred_location_source],
    [update_status],
    [update_progress],
    [update_status_message],
    [update_started_at],
    [update_error_logs],
    [update_requested_at],
    [update_requested_by],
    [last_updated_at],
    [previous_agent_version];

COMMIT TRANSACTION;
