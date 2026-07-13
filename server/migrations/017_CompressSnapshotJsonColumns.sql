-- ============================================================================
-- Migration 017: Compress Restore Point Snapshot JSON Columns
--
-- Converts all 21 JSON columns in restore_point_snapshots from NVARCHAR(MAX)
-- to VARBINARY(MAX) with GZip compression. The EF Core value converter
-- (GZipCompressedStringConverter) handles transparent compress/decompress.
--
-- This migration:
--   1. Adds new VARBINARY(MAX) columns
--   2. Compresses existing data using the SQL COMPRESS() function
--   3. Drops the old NVARCHAR(MAX) columns
--   4. Renames the new columns to match the original names
--
-- Requires SQL Server 2016+ for COMPRESS/DECOMPRESS functions.
-- ============================================================================

-- Step 1: Add compressed columns alongside originals
ALTER TABLE [dbo].[restore_point_snapshots] ADD [system_details_json_gz]        VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [installed_software_json_gz]    VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [running_processes_json_gz]     VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [windows_services_json_gz]      VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [startup_programs_json_gz]      VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [scheduled_tasks_json_gz]       VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [environment_variables_json_gz] VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [network_config_json_gz]        VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [storage_config_json_gz]        VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [event_log_summary_json_gz]     VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [windows_updates_json_gz]       VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [device_drivers_json_gz]        VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [firewall_rules_json_gz]        VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [local_users_json_gz]           VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [local_groups_json_gz]          VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [shared_folders_json_gz]        VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [printers_json_gz]              VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [usb_devices_json_gz]           VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [battery_status_json_gz]        VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [bit_locker_status_json_gz]     VARBINARY(MAX) NULL;
ALTER TABLE [dbo].[restore_point_snapshots] ADD [security_status_json_gz]       VARBINARY(MAX) NULL;
GO

-- Step 2: Compress existing data into the new columns
-- COMPRESS() uses GZip and returns VARBINARY(MAX)
UPDATE [dbo].[restore_point_snapshots] SET
    [system_details_json_gz]        = COMPRESS([system_details_json]),
    [installed_software_json_gz]    = COMPRESS([installed_software_json]),
    [running_processes_json_gz]     = COMPRESS([running_processes_json]),
    [windows_services_json_gz]      = COMPRESS([windows_services_json]),
    [startup_programs_json_gz]      = COMPRESS([startup_programs_json]),
    [scheduled_tasks_json_gz]       = COMPRESS([scheduled_tasks_json]),
    [environment_variables_json_gz] = COMPRESS([environment_variables_json]),
    [network_config_json_gz]        = COMPRESS([network_config_json]),
    [storage_config_json_gz]        = COMPRESS([storage_config_json]),
    [event_log_summary_json_gz]     = COMPRESS([event_log_summary_json]),
    [windows_updates_json_gz]       = COMPRESS([windows_updates_json]),
    [device_drivers_json_gz]        = COMPRESS([device_drivers_json]),
    [firewall_rules_json_gz]        = COMPRESS([firewall_rules_json]),
    [local_users_json_gz]           = COMPRESS([local_users_json]),
    [local_groups_json_gz]          = COMPRESS([local_groups_json]),
    [shared_folders_json_gz]        = COMPRESS([shared_folders_json]),
    [printers_json_gz]              = COMPRESS([printers_json]),
    [usb_devices_json_gz]           = COMPRESS([usb_devices_json]),
    [battery_status_json_gz]        = COMPRESS([battery_status_json]),
    [bit_locker_status_json_gz]     = COMPRESS([bit_locker_status_json]),
    [security_status_json_gz]       = COMPRESS([security_status_json]);
GO

-- Step 3: Drop old NVARCHAR(MAX) columns
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [system_details_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [installed_software_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [running_processes_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [windows_services_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [startup_programs_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [scheduled_tasks_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [environment_variables_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [network_config_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [storage_config_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [event_log_summary_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [windows_updates_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [device_drivers_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [firewall_rules_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [local_users_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [local_groups_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [shared_folders_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [printers_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [usb_devices_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [battery_status_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [bit_locker_status_json];
ALTER TABLE [dbo].[restore_point_snapshots] DROP COLUMN [security_status_json];
GO

-- Step 4: Rename compressed columns to original names
EXEC sp_rename 'dbo.restore_point_snapshots.system_details_json_gz',        'system_details_json',        'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.installed_software_json_gz',    'installed_software_json',    'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.running_processes_json_gz',     'running_processes_json',     'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.windows_services_json_gz',      'windows_services_json',      'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.startup_programs_json_gz',      'startup_programs_json',      'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.scheduled_tasks_json_gz',       'scheduled_tasks_json',       'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.environment_variables_json_gz', 'environment_variables_json', 'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.network_config_json_gz',        'network_config_json',        'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.storage_config_json_gz',        'storage_config_json',        'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.event_log_summary_json_gz',     'event_log_summary_json',     'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.windows_updates_json_gz',       'windows_updates_json',       'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.device_drivers_json_gz',        'device_drivers_json',        'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.firewall_rules_json_gz',        'firewall_rules_json',        'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.local_users_json_gz',           'local_users_json',           'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.local_groups_json_gz',          'local_groups_json',          'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.shared_folders_json_gz',        'shared_folders_json',        'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.printers_json_gz',              'printers_json',              'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.usb_devices_json_gz',           'usb_devices_json',           'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.battery_status_json_gz',        'battery_status_json',        'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.bit_locker_status_json_gz',     'bit_locker_status_json',     'COLUMN';
EXEC sp_rename 'dbo.restore_point_snapshots.security_status_json_gz',       'security_status_json',       'COLUMN';
GO
