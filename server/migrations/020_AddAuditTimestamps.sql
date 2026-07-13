-- Migration: 020_AddAuditTimestamps
-- Date: 2026-03-12
-- Description: Add created_at / updated_at audit columns to entities that were missing them.
-- Affected tables: computer_histories, restore_point_system_infos, restore_point_snapshots,
--   departments, locations, manufacturers, computer_models, operating_systems, countries,
--   computer_types, computer_statuses, update_statuses, maintenance_types, maintenance_statuses,
--   restore_point_types, version_types, user_roles

-- ================================================================
-- REFERENCE TABLES
-- ================================================================

-- departments
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('departments') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[departments] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_departments_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[departments] ADD [updated_at] DATETIME2 NULL;
END;

-- locations
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('locations') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[locations] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_locations_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[locations] ADD [updated_at] DATETIME2 NULL;
END;

-- manufacturers
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('manufacturers') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[manufacturers] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_manufacturers_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[manufacturers] ADD [updated_at] DATETIME2 NULL;
END;

-- computer_models
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('computer_models') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[computer_models] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_computer_models_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[computer_models] ADD [updated_at] DATETIME2 NULL;
END;

-- operating_systems
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('operating_systems') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[operating_systems] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_operating_systems_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[operating_systems] ADD [updated_at] DATETIME2 NULL;
END;

-- countries
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('countries') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[countries] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_countries_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[countries] ADD [updated_at] DATETIME2 NULL;
END;

-- ================================================================
-- LOOKUP TABLES
-- ================================================================

-- computer_types
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('computer_types') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[computer_types] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_computer_types_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[computer_types] ADD [updated_at] DATETIME2 NULL;
END;

-- computer_statuses
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('computer_statuses') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[computer_statuses] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_computer_statuses_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[computer_statuses] ADD [updated_at] DATETIME2 NULL;
END;

-- update_statuses
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('update_statuses') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[update_statuses] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_update_statuses_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[update_statuses] ADD [updated_at] DATETIME2 NULL;
END;

-- maintenance_types
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('maintenance_types') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[maintenance_types] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_maintenance_types_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[maintenance_types] ADD [updated_at] DATETIME2 NULL;
END;

-- maintenance_statuses
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('maintenance_statuses') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[maintenance_statuses] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_maintenance_statuses_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[maintenance_statuses] ADD [updated_at] DATETIME2 NULL;
END;

-- restore_point_types
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('restore_point_types') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[restore_point_types] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_restore_point_types_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[restore_point_types] ADD [updated_at] DATETIME2 NULL;
END;

-- version_types
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('version_types') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[version_types] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_version_types_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[version_types] ADD [updated_at] DATETIME2 NULL;
END;

-- user_roles
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('user_roles') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[user_roles] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_user_roles_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[user_roles] ADD [updated_at] DATETIME2 NULL;
END;

-- ================================================================
-- DATA TABLES
-- ================================================================

-- computer_histories
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('computer_histories') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[computer_histories] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_computer_histories_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[computer_histories] ADD [updated_at] DATETIME2 NULL;

    -- Backfill: set created_at to the existing changed_at value for historical rows
    -- Wrapped in EXEC so the column reference is parsed after ALTER TABLE completes
    EXEC('UPDATE [dbo].[computer_histories] SET [created_at] = [changed_at] WHERE [created_at] = ''0001-01-01''');
END;

-- restore_point_system_infos
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('restore_point_system_infos') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[restore_point_system_infos] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_restore_point_system_infos_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[restore_point_system_infos] ADD [updated_at] DATETIME2 NULL;

    -- Backfill: set created_at from parent restore_point's created_at
    -- Wrapped in EXEC so the column reference is parsed after ALTER TABLE completes
    EXEC('
        UPDATE rsi
        SET rsi.[created_at] = rp.[created_at]
        FROM [dbo].[restore_point_system_infos] rsi
        INNER JOIN [dbo].[restore_points] rp ON rsi.[restore_point_id] = rp.[id]
        WHERE rsi.[created_at] = ''0001-01-01''
    ');
END;

-- restore_point_snapshots
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('restore_point_snapshots') AND name = 'created_at')
BEGIN
    ALTER TABLE [dbo].[restore_point_snapshots] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_restore_point_snapshots_created_at] DEFAULT (GETUTCDATE());
    ALTER TABLE [dbo].[restore_point_snapshots] ADD [updated_at] DATETIME2 NULL;

    -- Backfill: set created_at from parent restore_point's created_at
    -- Wrapped in EXEC so the column reference is parsed after ALTER TABLE completes
    EXEC('
        UPDATE rps
        SET rps.[created_at] = rp.[created_at]
        FROM [dbo].[restore_point_snapshots] rps
        INNER JOIN [dbo].[restore_points] rp ON rps.[restore_point_id] = rp.[id]
        WHERE rps.[created_at] = ''0001-01-01''
    ');
END;

PRINT 'Migration 020_AddAuditTimestamps completed successfully.';


-- computer_histories: use the original changed_at
UPDATE [dbo].[computer_histories]
SET [created_at] = [changed_at]
WHERE [changed_at] < [created_at];

-- restore_point_system_infos: use parent's created_at
UPDATE rsi
SET rsi.[created_at] = rp.[created_at]
FROM [dbo].[restore_point_system_infos] rsi
INNER JOIN [dbo].[restore_points] rp ON rsi.[restore_point_id] = rp.[id]
WHERE rp.[created_at] < rsi.[created_at];

-- restore_point_snapshots: use parent's created_at
UPDATE rps
SET rps.[created_at] = rp.[created_at]
FROM [dbo].[restore_point_snapshots] rps
INNER JOIN [dbo].[restore_points] rp ON rps.[restore_point_id] = rp.[id]
WHERE rp.[created_at] < rps.[created_at];