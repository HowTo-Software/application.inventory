-- ============================================================================
-- Migration 021: Add Soft-Delete Support
-- Adds is_deleted (BIT) and deleted_at (DATETIME2) columns to business entities.
-- A global query filter in EF Core automatically excludes soft-deleted rows.
-- Use IgnoreQueryFilters() to query deleted records when needed.
-- ============================================================================

-- ================================================================
-- STEP 1: Add columns to all tables
-- ================================================================

-- COMPUTERS
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('computers') AND name = 'is_deleted')
BEGIN
    ALTER TABLE [dbo].[computers] ADD [is_deleted] BIT NOT NULL CONSTRAINT [DF_computers_is_deleted] DEFAULT 0;
    ALTER TABLE [dbo].[computers] ADD [deleted_at] DATETIME2 NULL;
    PRINT 'Added soft-delete columns to computers';
END
GO

-- USERS
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('users') AND name = 'is_deleted')
BEGIN
    ALTER TABLE [dbo].[users] ADD [is_deleted] BIT NOT NULL CONSTRAINT [DF_users_is_deleted] DEFAULT 0;
    ALTER TABLE [dbo].[users] ADD [deleted_at] DATETIME2 NULL;
    PRINT 'Added soft-delete columns to users';
END
GO

-- MAINTENANCE RECORDS
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('maintenance_records') AND name = 'is_deleted')
BEGIN
    ALTER TABLE [dbo].[maintenance_records] ADD [is_deleted] BIT NOT NULL CONSTRAINT [DF_maintenance_records_is_deleted] DEFAULT 0;
    ALTER TABLE [dbo].[maintenance_records] ADD [deleted_at] DATETIME2 NULL;
    PRINT 'Added soft-delete columns to maintenance_records';
END
GO

-- COMPUTER HISTORIES
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('computer_histories') AND name = 'is_deleted')
BEGIN
    ALTER TABLE [dbo].[computer_histories] ADD [is_deleted] BIT NOT NULL CONSTRAINT [DF_computer_histories_is_deleted] DEFAULT 0;
    ALTER TABLE [dbo].[computer_histories] ADD [deleted_at] DATETIME2 NULL;
    PRINT 'Added soft-delete columns to computer_histories';
END
GO

-- SYSTEM DETAILS
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('system_details') AND name = 'is_deleted')
BEGIN
    ALTER TABLE [dbo].[system_details] ADD [is_deleted] BIT NOT NULL CONSTRAINT [DF_system_details_is_deleted] DEFAULT 0;
    ALTER TABLE [dbo].[system_details] ADD [deleted_at] DATETIME2 NULL;
    PRINT 'Added soft-delete columns to system_details';
END
GO

-- RESTORE POINTS
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('restore_points') AND name = 'is_deleted')
BEGIN
    ALTER TABLE [dbo].[restore_points] ADD [is_deleted] BIT NOT NULL CONSTRAINT [DF_restore_points_is_deleted] DEFAULT 0;
    ALTER TABLE [dbo].[restore_points] ADD [deleted_at] DATETIME2 NULL;
    PRINT 'Added soft-delete columns to restore_points';
END
GO

-- VERSION HISTORIES
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('version_histories') AND name = 'is_deleted')
BEGIN
    ALTER TABLE [dbo].[version_histories] ADD [is_deleted] BIT NOT NULL CONSTRAINT [DF_version_histories_is_deleted] DEFAULT 0;
    ALTER TABLE [dbo].[version_histories] ADD [deleted_at] DATETIME2 NULL;
    PRINT 'Added soft-delete columns to version_histories';
END
GO

-- PACKAGE VERSIONS
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('package_versions') AND name = 'is_deleted')
BEGIN
    ALTER TABLE [dbo].[package_versions] ADD [is_deleted] BIT NOT NULL CONSTRAINT [DF_package_versions_is_deleted] DEFAULT 0;
    ALTER TABLE [dbo].[package_versions] ADD [deleted_at] DATETIME2 NULL;
    PRINT 'Added soft-delete columns to package_versions';
END
GO

-- ================================================================
-- STEP 2: Create filtered indexes (separate batch so columns exist)
-- ================================================================

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_computers_is_deleted')
    CREATE INDEX [ix_computers_is_deleted] ON [dbo].[computers] ([is_deleted]) WHERE [is_deleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_users_is_deleted')
    CREATE INDEX [ix_users_is_deleted] ON [dbo].[users] ([is_deleted]) WHERE [is_deleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_maintenance_records_is_deleted')
    CREATE INDEX [ix_maintenance_records_is_deleted] ON [dbo].[maintenance_records] ([is_deleted]) WHERE [is_deleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_computer_histories_is_deleted')
    CREATE INDEX [ix_computer_histories_is_deleted] ON [dbo].[computer_histories] ([is_deleted]) WHERE [is_deleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_system_details_is_deleted')
    CREATE INDEX [ix_system_details_is_deleted] ON [dbo].[system_details] ([is_deleted]) WHERE [is_deleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_restore_points_is_deleted')
    CREATE INDEX [ix_restore_points_is_deleted] ON [dbo].[restore_points] ([is_deleted]) WHERE [is_deleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_version_histories_is_deleted')
    CREATE INDEX [ix_version_histories_is_deleted] ON [dbo].[version_histories] ([is_deleted]) WHERE [is_deleted] = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_package_versions_is_deleted')
    CREATE INDEX [ix_package_versions_is_deleted] ON [dbo].[package_versions] ([is_deleted]) WHERE [is_deleted] = 0;
GO

PRINT 'Migration 021_AddSoftDeleteSupport completed successfully';
GO
