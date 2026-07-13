-- ============================================================================
-- DDL Script for Database: device_management
-- Generated from EF Core model (authoritative source of truth)
-- Server: 10.241.24.41,1433
--
-- This script creates the database if it doesn't exist, drops all existing
-- objects, recreates them to match the EF Core model exactly, seeds lookup
-- tables, and creates the default admin user.
--
-- Table naming follows EF Core snake_case convention with pluralization.
--
-- Consolidated from migrations 001–045 on 2026-03-26.
-- ============================================================================

USE [master]
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'device_management')
BEGIN
    CREATE DATABASE [device_management]
END
GO

USE [device_management]
GO

-- ============================================================================
-- SECTION 0: DROP ALL STORED PROCEDURES, FOREIGN KEYS, THEN TABLES
-- ============================================================================

-- Drop every stored procedure in the database
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'DROP PROCEDURE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(p.name) + N';' + CHAR(13)
FROM sys.procedures p
JOIN sys.schemas s ON p.schema_id = s.schema_id;
EXEC sp_executesql @sql;
GO

-- Drop every FK constraint in the database so tables can be dropped in any order
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
    + N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';' + CHAR(13)
FROM sys.foreign_keys f
JOIN sys.tables t ON f.parent_object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id;
EXEC sp_executesql @sql;
GO

-- Drop system_detail sub-tables (children of system_details)
DROP TABLE IF EXISTS [dbo].[system_detail_performances];
DROP TABLE IF EXISTS [dbo].[system_detail_event_logs];
DROP TABLE IF EXISTS [dbo].[system_detail_peripherals];
DROP TABLE IF EXISTS [dbo].[system_detail_users_groups];
DROP TABLE IF EXISTS [dbo].[system_detail_softwares];
DROP TABLE IF EXISTS [dbo].[system_detail_chasses];
DROP TABLE IF EXISTS [dbo].[system_detail_batteries];
DROP TABLE IF EXISTS [dbo].[system_detail_securities];
DROP TABLE IF EXISTS [dbo].[system_detail_oses];
DROP TABLE IF EXISTS [dbo].[system_detail_networks];
DROP TABLE IF EXISTS [dbo].[system_detail_gpus];
DROP TABLE IF EXISTS [dbo].[system_detail_storages];
DROP TABLE IF EXISTS [dbo].[system_detail_memories];
DROP TABLE IF EXISTS [dbo].[system_detail_cpus];
DROP TABLE IF EXISTS [dbo].[system_detail_motherboards];
DROP TABLE IF EXISTS [dbo].[system_detail_bios];
GO

-- Drop restore_point sub-tables (children of restore_points)
DROP TABLE IF EXISTS [dbo].[restore_point_snapshots];
DROP TABLE IF EXISTS [dbo].[restore_point_system_infos];
GO

-- Drop tables that reference computers
DROP TABLE IF EXISTS [dbo].[computer_agent_statuses];
DROP TABLE IF EXISTS [dbo].[computer_update_requests];
DROP TABLE IF EXISTS [dbo].[computer_ownerships];
DROP TABLE IF EXISTS [dbo].[system_details];
DROP TABLE IF EXISTS [dbo].[restore_points];
DROP TABLE IF EXISTS [dbo].[maintenance_records];
DROP TABLE IF EXISTS [dbo].[computer_histories];
DROP TABLE IF EXISTS [dbo].[computer_geo_locations];
GO

-- Drop package versioning tables
DROP TABLE IF EXISTS [dbo].[package_distribution_rules];
DROP TABLE IF EXISTS [dbo].[package_versions];
GO

-- Drop authentication / administration tables
DROP TABLE IF EXISTS [dbo].[refresh_tokens];
DROP TABLE IF EXISTS [dbo].[install_tokens];
DROP TABLE IF EXISTS [dbo].[invite_codes];
GO

-- Drop core tables
DROP TABLE IF EXISTS [dbo].[computers];
DROP TABLE IF EXISTS [dbo].[users];
DROP TABLE IF EXISTS [dbo].[version_histories];
GO

-- Drop app settings
DROP TABLE IF EXISTS [dbo].[app_settings];
GO

-- Drop reference/normalized tables
DROP TABLE IF EXISTS [dbo].[computer_models];
DROP TABLE IF EXISTS [dbo].[manufacturers];
DROP TABLE IF EXISTS [dbo].[operating_systems];
DROP TABLE IF EXISTS [dbo].[locations];
DROP TABLE IF EXISTS [dbo].[departments];
DROP TABLE IF EXISTS [dbo].[countries];
GO

-- Drop lookup tables
DROP TABLE IF EXISTS [dbo].[computer_types];
DROP TABLE IF EXISTS [dbo].[computer_statuses];
DROP TABLE IF EXISTS [dbo].[update_statuses];
DROP TABLE IF EXISTS [dbo].[maintenance_types];
DROP TABLE IF EXISTS [dbo].[maintenance_statuses];
DROP TABLE IF EXISTS [dbo].[restore_point_types];
DROP TABLE IF EXISTS [dbo].[version_types];
DROP TABLE IF EXISTS [dbo].[user_roles];
GO

-- Drop old singular-named tables if they exist (from previous DDL)
DROP TABLE IF EXISTS [dbo].[system_detail_performance];
DROP TABLE IF EXISTS [dbo].[system_detail_peripherals];
DROP TABLE IF EXISTS [dbo].[system_detail_software];
DROP TABLE IF EXISTS [dbo].[system_detail_chassis];
DROP TABLE IF EXISTS [dbo].[system_detail_battery];
DROP TABLE IF EXISTS [dbo].[system_detail_security];
DROP TABLE IF EXISTS [dbo].[system_detail_os];
DROP TABLE IF EXISTS [dbo].[system_detail_network];
DROP TABLE IF EXISTS [dbo].[system_detail_gpu];
DROP TABLE IF EXISTS [dbo].[system_detail_storage];
DROP TABLE IF EXISTS [dbo].[system_detail_memory];
DROP TABLE IF EXISTS [dbo].[system_detail_cpu];
DROP TABLE IF EXISTS [dbo].[system_detail_motherboard];
DROP TABLE IF EXISTS [dbo].[restore_point_system_info];
GO

-- ============================================================================
-- SECTION 1: LOOKUP TABLES
-- IDs match C# enum integer values for seamless FK/enum mapping.
-- ============================================================================

CREATE TABLE [dbo].[computer_types] (
    [id]           INT           NOT NULL,
    [name]         NVARCHAR(50)  NOT NULL,
    [created_at]   DATETIME2     NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2     NULL,
    CONSTRAINT [pk_computer_types] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[computer_statuses] (
    [id]           INT           NOT NULL,
    [name]         NVARCHAR(50)  NOT NULL,
    [created_at]   DATETIME2     NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2     NULL,
    CONSTRAINT [pk_computer_statuses] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[update_statuses] (
    [id]           INT           NOT NULL,
    [name]         NVARCHAR(50)  NOT NULL,
    [created_at]   DATETIME2     NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2     NULL,
    CONSTRAINT [pk_update_statuses] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[maintenance_types] (
    [id]           INT           NOT NULL,
    [name]         NVARCHAR(50)  NOT NULL,
    [created_at]   DATETIME2     NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2     NULL,
    CONSTRAINT [pk_maintenance_types] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[maintenance_statuses] (
    [id]           INT           NOT NULL,
    [name]         NVARCHAR(50)  NOT NULL,
    [created_at]   DATETIME2     NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2     NULL,
    CONSTRAINT [pk_maintenance_statuses] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[restore_point_types] (
    [id]           INT           NOT NULL,
    [name]         NVARCHAR(50)  NOT NULL,
    [created_at]   DATETIME2     NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2     NULL,
    CONSTRAINT [pk_restore_point_types] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[version_types] (
    [id]           INT           NOT NULL,
    [name]         NVARCHAR(50)  NOT NULL,
    [created_at]   DATETIME2     NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2     NULL,
    CONSTRAINT [pk_version_types] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[user_roles] (
    [id]           INT           NOT NULL,
    [name]         NVARCHAR(50)  NOT NULL,
    [created_at]   DATETIME2     NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2     NULL,
    CONSTRAINT [pk_user_roles] PRIMARY KEY ([id])
);
GO

-- ============================================================================
-- SECTION 2: REFERENCE / NORMALIZED TABLES
-- ============================================================================

CREATE TABLE [dbo].[departments] (
    [id]           INT            NOT NULL IDENTITY,
    [name]         NVARCHAR(100)  NOT NULL,
    [created_at]   DATETIME2      NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2      NULL,
    CONSTRAINT [pk_departments] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[locations] (
    [id]           INT            NOT NULL IDENTITY,
    [name]         NVARCHAR(200)  NOT NULL,
    [created_at]   DATETIME2      NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2      NULL,
    CONSTRAINT [pk_locations] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[manufacturers] (
    [id]           INT            NOT NULL IDENTITY,
    [name]         NVARCHAR(100)  NOT NULL,
    [created_at]   DATETIME2      NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2      NULL,
    CONSTRAINT [pk_manufacturers] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[computer_models] (
    [id]                INT            NOT NULL IDENTITY,
    [manufacturer_id]   INT            NOT NULL,
    [model_name]        NVARCHAR(100)  NOT NULL,
    [created_at]        DATETIME2      NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]        DATETIME2      NULL,
    CONSTRAINT [pk_computer_models] PRIMARY KEY ([id]),
    CONSTRAINT [fk_computer_models_manufacturers_manufacturer_id]
        FOREIGN KEY ([manufacturer_id]) REFERENCES [dbo].[manufacturers] ([id])
);
GO

CREATE TABLE [dbo].[operating_systems] (
    [id]           INT            NOT NULL IDENTITY,
    [name]         NVARCHAR(100)  NOT NULL,
    [created_at]   DATETIME2      NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]   DATETIME2      NULL,
    CONSTRAINT [pk_operating_systems] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[countries] (
    [id]             INT            NOT NULL IDENTITY,
    [name]           NVARCHAR(100)  NOT NULL,
    [country_code]   NVARCHAR(10)   NOT NULL,
    [created_at]     DATETIME2      NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]     DATETIME2      NULL,
    CONSTRAINT [pk_countries] PRIMARY KEY ([id])
);
GO

-- ============================================================================
-- SECTION 3: CORE TABLES
-- ============================================================================

-- computers: hardware/inventory data only (agent runtime state is in computer_agent_statuses)
CREATE TABLE [dbo].[computers] (
    [id]                          INT              NOT NULL IDENTITY,
    [serial_number]               NVARCHAR(100)    NOT NULL,
    [hostname]                    NVARCHAR(100)    NOT NULL,
    [ip_address]                  NVARCHAR(45)     NULL,
    [mac_address]                 NVARCHAR(17)     NULL,
    [model_id]                    INT              NOT NULL,
    [operating_system_id]         INT              NULL,
    [processor]                   NVARCHAR(200)    NULL,
    [ram_gb]                      INT              NULL,
    [storage_gb]                  INT              NULL,
    [type_id]                     INT              NOT NULL,
    [status_id]                   INT              NOT NULL,
    [location_id]                 INT              NULL,
    [department_id]               INT              NULL,
    [notes]                       NVARCHAR(2000)   NULL,
    [purchase_date]               DATETIME2        NOT NULL,
    [warranty_expiration]         DATETIME2        NULL,
    [purchase_price]              DECIMAL(18,2)    NULL,
    [assigned_to]                 NVARCHAR(200)    NULL,
    [created_at]                  DATETIME2        NOT NULL,
    [updated_at]                  DATETIME2        NULL,
    [created_by]                  NVARCHAR(100)    NULL,
    [updated_by]                  NVARCHAR(100)    NULL,
    [row_version]                 ROWVERSION       NOT NULL,
    [is_deleted]                  BIT              NOT NULL DEFAULT 0,
    [deleted_at]                  DATETIME2        NULL,
    CONSTRAINT [pk_computers] PRIMARY KEY ([id]),
    CONSTRAINT [fk_computers_computer_models_model_id]
        FOREIGN KEY ([model_id]) REFERENCES [dbo].[computer_models] ([id]) ON DELETE NO ACTION,
    CONSTRAINT [fk_computers_computer_statuses_status_id]
        FOREIGN KEY ([status_id]) REFERENCES [dbo].[computer_statuses] ([id]) ON DELETE NO ACTION,
    CONSTRAINT [fk_computers_computer_types_type_id]
        FOREIGN KEY ([type_id]) REFERENCES [dbo].[computer_types] ([id]) ON DELETE NO ACTION,
    CONSTRAINT [fk_computers_departments_department_id]
        FOREIGN KEY ([department_id]) REFERENCES [dbo].[departments] ([id]) ON DELETE NO ACTION,
    CONSTRAINT [fk_computers_locations_location_id]
        FOREIGN KEY ([location_id]) REFERENCES [dbo].[locations] ([id]) ON DELETE NO ACTION,
    CONSTRAINT [fk_computers_operating_systems_operating_system_id]
        FOREIGN KEY ([operating_system_id]) REFERENCES [dbo].[operating_systems] ([id]) ON DELETE NO ACTION
);
GO

-- computer_update_requests: historical log of update requests
CREATE TABLE [dbo].[computer_update_requests] (
    [id]                       INT              NOT NULL IDENTITY,
    [computer_id]              INT              NOT NULL,
    [update_status_id]         INT              NOT NULL,
    [update_progress]          INT              NOT NULL,
    [update_status_message]    NVARCHAR(500)    NULL,
    [update_started_at]        DATETIME2        NULL,
    [update_error_logs]        NVARCHAR(MAX)    NULL,
    [update_requested_at]      DATETIME2        NULL,
    [update_requested_by]      NVARCHAR(100)    NULL,
    [previous_agent_version]   NVARCHAR(50)     NULL,
    [last_updated_at]          DATETIME2        NULL,
    [created_at]               DATETIME2        NOT NULL,
    CONSTRAINT [pk_computer_update_requests] PRIMARY KEY ([id]),
    CONSTRAINT [fk_computer_update_requests_computers_computer_id]
        FOREIGN KEY ([computer_id]) REFERENCES [dbo].[computers] ([id]) ON DELETE CASCADE,
    CONSTRAINT [fk_computer_update_requests_update_statuses_update_status_id]
        FOREIGN KEY ([update_status_id]) REFERENCES [dbo].[update_statuses] ([id]) ON DELETE NO ACTION
);
GO

-- computer_agent_statuses: volatile agent runtime state (1:1 with computers)
CREATE TABLE [dbo].[computer_agent_statuses] (
    [id]                          INT              NOT NULL IDENTITY,
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

    -- Active update link
    [active_update_request_id]    INT              NULL,

    CONSTRAINT [pk_computer_agent_statuses] PRIMARY KEY ([id]),
    CONSTRAINT [fk_computer_agent_statuses_computers_computer_id]
        FOREIGN KEY ([computer_id]) REFERENCES [dbo].[computers] ([id]) ON DELETE CASCADE,
    CONSTRAINT [fk_computer_agent_statuses_active_update_request]
        FOREIGN KEY ([active_update_request_id])
        REFERENCES [dbo].[computer_update_requests] ([id]) ON DELETE NO ACTION
);
GO

-- computer_geo_locations
CREATE TABLE [dbo].[computer_geo_locations] (
    [id]                    INT              NOT NULL IDENTITY,
    [computer_id]           INT              NOT NULL,
    [public_ip_address]     NVARCHAR(45)     NULL,
    [gps_latitude]          FLOAT            NULL,
    [gps_longitude]         FLOAT            NULL,
    [gps_last_updated]      DATETIME2        NULL,
    [ip_latitude]           FLOAT            NULL,
    [ip_longitude]          FLOAT            NULL,
    [ip_last_updated]       DATETIME2        NULL,
    [latitude]              FLOAT            NULL,
    [longitude]             FLOAT            NULL,
    [city]                  NVARCHAR(100)    NULL,
    [region]                NVARCHAR(100)    NULL,
    [country_id]            INT              NULL,
    [isp]                   NVARCHAR(200)    NULL,
    [timezone]              NVARCHAR(50)     NULL,
    [last_updated]          DATETIME2        NOT NULL,
    [lookup_successful]     BIT              NOT NULL,
    [lookup_error]          NVARCHAR(500)    NULL,
    [location_source]       NVARCHAR(MAX)    NOT NULL,
    [gps_accuracy_meters]   FLOAT            NULL,
    CONSTRAINT [pk_computer_geo_locations] PRIMARY KEY ([id]),
    CONSTRAINT [fk_computer_geo_locations_computers_computer_id]
        FOREIGN KEY ([computer_id]) REFERENCES [dbo].[computers] ([id]) ON DELETE CASCADE,
    CONSTRAINT [fk_computer_geo_locations_countries_country_id]
        FOREIGN KEY ([country_id]) REFERENCES [dbo].[countries] ([id]) ON DELETE NO ACTION
);
GO

-- computer_histories
CREATE TABLE [dbo].[computer_histories] (
    [id]            INT              NOT NULL IDENTITY,
    [computer_id]   INT              NOT NULL,
    [action]        NVARCHAR(100)    NOT NULL,
    [old_value]     NVARCHAR(500)    NULL,
    [new_value]     NVARCHAR(500)    NULL,
    [changed_by]    NVARCHAR(100)    NULL,
    [changed_at]    DATETIME2        NOT NULL,
    [notes]         NVARCHAR(500)    NULL,
    [created_at]    DATETIME2        NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]    DATETIME2        NULL,
    [row_version]   ROWVERSION       NOT NULL,
    [is_deleted]    BIT              NOT NULL DEFAULT 0,
    [deleted_at]    DATETIME2        NULL,
    CONSTRAINT [pk_computer_histories] PRIMARY KEY ([id]),
    CONSTRAINT [fk_computer_histories_computers_computer_id]
        FOREIGN KEY ([computer_id]) REFERENCES [dbo].[computers] ([id]) ON DELETE CASCADE
);
GO

-- maintenance_records
CREATE TABLE [dbo].[maintenance_records] (
    [id]               INT              NOT NULL IDENTITY,
    [computer_id]      INT              NOT NULL,
    [type_id]          INT              NOT NULL,
    [description]      NVARCHAR(1000)   NOT NULL,
    [scheduled_date]   DATETIME2        NOT NULL,
    [completed_date]   DATETIME2        NULL,
    [cost]             DECIMAL(18,2)    NULL,
    [performed_by]     NVARCHAR(100)    NULL,
    [notes]            NVARCHAR(2000)   NULL,
    [status_id]        INT              NOT NULL,
    [created_at]       DATETIME2        NOT NULL,
    [created_by]       NVARCHAR(100)    NULL,
    [row_version]      ROWVERSION       NOT NULL,
    [is_deleted]       BIT              NOT NULL DEFAULT 0,
    [deleted_at]       DATETIME2        NULL,
    CONSTRAINT [pk_maintenance_records] PRIMARY KEY ([id]),
    CONSTRAINT [fk_maintenance_records_computers_computer_id]
        FOREIGN KEY ([computer_id]) REFERENCES [dbo].[computers] ([id]) ON DELETE CASCADE,
    CONSTRAINT [fk_maintenance_records_maintenance_statuses_status_id]
        FOREIGN KEY ([status_id]) REFERENCES [dbo].[maintenance_statuses] ([id]) ON DELETE NO ACTION,
    CONSTRAINT [fk_maintenance_records_maintenance_types_type_id]
        FOREIGN KEY ([type_id]) REFERENCES [dbo].[maintenance_types] ([id]) ON DELETE NO ACTION
);
GO

-- restore_points
CREATE TABLE [dbo].[restore_points] (
    [id]                   INT                  NOT NULL IDENTITY,
    [computer_id]          INT                  NOT NULL,
    [restore_point_id]     UNIQUEIDENTIFIER     NOT NULL,
    [created_at]           DATETIME2            NOT NULL,
    [type_id]              INT                  NOT NULL,
    [description]          NVARCHAR(500)        NULL,
    [data_size_bytes]      BIGINT               NULL,
    [data_hash]            NVARCHAR(128)        NULL,
    [is_complete]          BIT                  NOT NULL,
    [error_message]        NVARCHAR(2000)       NULL,
    [is_deleted]           BIT                  NOT NULL DEFAULT 0,
    [deleted_at]           DATETIME2            NULL,
    CONSTRAINT [pk_restore_points] PRIMARY KEY ([id]),
    CONSTRAINT [fk_restore_points_computers_computer_id]
        FOREIGN KEY ([computer_id]) REFERENCES [dbo].[computers] ([id]) ON DELETE CASCADE,
    CONSTRAINT [fk_restore_points_restore_point_types_type_id]
        FOREIGN KEY ([type_id]) REFERENCES [dbo].[restore_point_types] ([id]) ON DELETE NO ACTION
);
GO

-- restore_point_system_infos
CREATE TABLE [dbo].[restore_point_system_infos] (
    [id]                   INT              NOT NULL IDENTITY,
    [restore_point_id]     INT              NOT NULL,
    [system_boot_time]     DATETIME2        NULL,
    [uptime_seconds]       BIGINT           NULL,
    [hostname]             NVARCHAR(100)    NULL,
    [serial_number]        NVARCHAR(100)    NULL,
    [ip_address]           NVARCHAR(45)     NULL,
    [mac_address]          NVARCHAR(17)     NULL,
    [manufacturer]         NVARCHAR(100)    NULL,
    [model]                NVARCHAR(100)    NULL,
    [current_user]         NVARCHAR(100)    NULL,
    [created_at]           DATETIME2        NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]           DATETIME2        NULL,
    CONSTRAINT [pk_restore_point_system_infos] PRIMARY KEY ([id]),
    CONSTRAINT [fk_restore_point_system_infos_restore_points_restore_point_id]
        FOREIGN KEY ([restore_point_id]) REFERENCES [dbo].[restore_points] ([id]) ON DELETE CASCADE
);
GO

-- restore_point_snapshots (all JSON columns stored as GZip-compressed VARBINARY)
CREATE TABLE [dbo].[restore_point_snapshots] (
    [id]                           INT              NOT NULL IDENTITY,
    [restore_point_id]             INT              NOT NULL,
    [system_details_json]          VARBINARY(MAX)   NULL,
    [installed_software_json]      VARBINARY(MAX)   NULL,
    [running_processes_json]       VARBINARY(MAX)   NULL,
    [windows_services_json]        VARBINARY(MAX)   NULL,
    [startup_programs_json]        VARBINARY(MAX)   NULL,
    [scheduled_tasks_json]         VARBINARY(MAX)   NULL,
    [environment_variables_json]   VARBINARY(MAX)   NULL,
    [network_config_json]          VARBINARY(MAX)   NULL,
    [storage_config_json]          VARBINARY(MAX)   NULL,
    [event_log_summary_json]       VARBINARY(MAX)   NULL,
    [windows_updates_json]         VARBINARY(MAX)   NULL,
    [device_drivers_json]          VARBINARY(MAX)   NULL,
    [firewall_rules_json]          VARBINARY(MAX)   NULL,
    [local_users_json]             VARBINARY(MAX)   NULL,
    [local_groups_json]            VARBINARY(MAX)   NULL,
    [shared_folders_json]          VARBINARY(MAX)   NULL,
    [printers_json]                VARBINARY(MAX)   NULL,
    [usb_devices_json]             VARBINARY(MAX)   NULL,
    [battery_status_json]          VARBINARY(MAX)   NULL,
    [bit_locker_status_json]       VARBINARY(MAX)   NULL,
    [security_status_json]         VARBINARY(MAX)   NULL,
    [created_at]                   DATETIME2        NOT NULL DEFAULT (GETUTCDATE()),
    [updated_at]                   DATETIME2        NULL,
    CONSTRAINT [pk_restore_point_snapshots] PRIMARY KEY ([id]),
    CONSTRAINT [fk_restore_point_snapshots_restore_points_restore_point_id]
        FOREIGN KEY ([restore_point_id]) REFERENCES [dbo].[restore_points] ([id]) ON DELETE CASCADE
);
GO

-- ============================================================================
-- SECTION 4: system_details and sub-tables
-- ============================================================================

CREATE TABLE [dbo].[system_details] (
    [id]                INT              NOT NULL IDENTITY,
    [computer_id]       INT              NOT NULL,
    [collected_at]      DATETIME2        NOT NULL,
    [machine_guid]      NVARCHAR(MAX)    NULL,
    [product_id]        NVARCHAR(MAX)    NULL,
    [row_version]       ROWVERSION       NOT NULL,
    [is_deleted]        BIT              NOT NULL DEFAULT 0,
    [deleted_at]        DATETIME2        NULL,
    CONSTRAINT [pk_system_details] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_details_computers_computer_id]
        FOREIGN KEY ([computer_id]) REFERENCES [dbo].[computers] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_bios] (
    [id]                    INT              NOT NULL IDENTITY,
    [system_detail_id]      INT              NOT NULL,
    [bios_version]          NVARCHAR(MAX)    NULL,
    [bios_manufacturer]     NVARCHAR(MAX)    NULL,
    [bios_release_date]     DATETIME2        NULL,
    [bios_serial_number]    NVARCHAR(MAX)    NULL,
    [smbios_version]        NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_bios] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_bios_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_motherboards] (
    [id]                            INT              NOT NULL IDENTITY,
    [system_detail_id]              INT              NOT NULL,
    [motherboard_manufacturer]      NVARCHAR(MAX)    NULL,
    [motherboard_product]           NVARCHAR(MAX)    NULL,
    [motherboard_serial_number]     NVARCHAR(MAX)    NULL,
    [motherboard_version]           NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_motherboards] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_motherboards_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_cpus] (
    [id]                        INT              NOT NULL IDENTITY,
    [system_detail_id]          INT              NOT NULL,
    [cpu_name]                  NVARCHAR(MAX)    NULL,
    [cpu_manufacturer]          NVARCHAR(MAX)    NULL,
    [cpu_cores]                 INT              NULL,
    [cpu_logical_processors]    INT              NULL,
    [cpu_max_clock_speed]       INT              NULL,
    [cpu_current_clock_speed]   INT              NULL,
    [cpu_architecture]          NVARCHAR(MAX)    NULL,
    [cpu_l2cache_size]          INT              NULL,
    [cpu_l3cache_size]          INT              NULL,
    [cpu_socket_designation]    NVARCHAR(MAX)    NULL,
    [cpu_processor_id]          NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_cpus] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_cpus_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_memories] (
    [id]                          INT              NOT NULL IDENTITY,
    [system_detail_id]            INT              NOT NULL,
    [total_physical_memory]       BIGINT           NULL,
    [available_physical_memory]   BIGINT           NULL,
    [total_virtual_memory]        BIGINT           NULL,
    [available_virtual_memory]    BIGINT           NULL,
    [memory_slots]                INT              NULL,
    [memory_slots_used]           INT              NULL,
    [memory_modules_json]         NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_memories] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_memories_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_storages] (
    [id]                    INT              NOT NULL IDENTITY,
    [system_detail_id]      INT              NOT NULL,
    [disks_json]            NVARCHAR(MAX)    NULL,
    [partitions_json]       NVARCHAR(MAX)    NULL,
    [logical_drives_json]   NVARCHAR(MAX)    NULL,
    [total_disk_space]      BIGINT           NULL,
    [free_disk_space]       BIGINT           NULL,
    [shared_folders_json]   NVARCHAR(MAX)    NULL,
    [mapped_drives_json]    NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_storages] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_storages_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_gpus] (
    [id]                    INT              NOT NULL IDENTITY,
    [system_detail_id]      INT              NOT NULL,
    [gpu_name]              NVARCHAR(MAX)    NULL,
    [gpu_manufacturer]      NVARCHAR(MAX)    NULL,
    [gpu_driver_version]    NVARCHAR(MAX)    NULL,
    [gpu_driver_date]       DATETIME2        NULL,
    [gpu_ram]               BIGINT           NULL,
    [gpu_resolution]        NVARCHAR(MAX)    NULL,
    [graphics_cards_json]   NVARCHAR(MAX)    NULL,
    [monitors_json]         NVARCHAR(MAX)    NULL,
    [monitor_count]         INT              NULL,
    CONSTRAINT [pk_system_detail_gpus] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_gpus_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_networks] (
    [id]                      INT              NOT NULL IDENTITY,
    [system_detail_id]        INT              NOT NULL,
    [network_adapters_json]   NVARCHAR(MAX)    NULL,
    [domain_name]             NVARCHAR(MAX)    NULL,
    [dns_hostname]            NVARCHAR(MAX)    NULL,
    [workgroup]               NVARCHAR(MAX)    NULL,
    [domain_joined]           BIT              NULL,
    [default_gateway]         NVARCHAR(MAX)    NULL,
    [dns_servers]             NVARCHAR(MAX)    NULL,
    [dhcp_server]             NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_networks] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_networks_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_oses] (
    [id]                                INT              NOT NULL IDENTITY,
    [system_detail_id]                  INT              NOT NULL,
    [os_name]                           NVARCHAR(MAX)    NULL,
    [os_version]                        NVARCHAR(MAX)    NULL,
    [os_build]                          NVARCHAR(MAX)    NULL,
    [os_architecture]                   NVARCHAR(MAX)    NULL,
    [os_install_date]                   NVARCHAR(MAX)    NULL,
    [os_last_boot_time]                 NVARCHAR(MAX)    NULL,
    [os_serial_number]                  NVARCHAR(MAX)    NULL,
    [os_product_key]                    NVARCHAR(MAX)    NULL,
    [os_service_pack_major]             INT              NULL,
    [os_service_pack_minor]             INT              NULL,
    [os_locale]                         NVARCHAR(MAX)    NULL,
    [os_language]                       NVARCHAR(MAX)    NULL,
    [time_zone]                         NVARCHAR(MAX)    NULL,
    [os_uptime]                         BIGINT           NULL,
    [windows_product_id]                NVARCHAR(MAX)    NULL,
    [windows_registered_owner]          NVARCHAR(MAX)    NULL,
    [windows_registered_organization]   NVARCHAR(MAX)    NULL,
    [windows_is_activated]              BIT              NULL,
    [windows_activation_status]         NVARCHAR(MAX)    NULL,
    [environment_variables_json]        NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_oses] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_oses_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_securities] (
    [id]                    INT              NOT NULL IDENTITY,
    [system_detail_id]      INT              NOT NULL,
    [antivirus_product]     NVARCHAR(MAX)    NULL,
    [antivirus_status]      NVARCHAR(MAX)    NULL,
    [firewall_status]       NVARCHAR(MAX)    NULL,
    [bit_locker_enabled]    BIT              NULL,
    [bit_locker_status]     NVARCHAR(MAX)    NULL,
    [secure_boot_enabled]   BIT              NULL,
    [tpm_present]           BIT              NULL,
    [tpm_version]           NVARCHAR(MAX)    NULL,
    [uac_enabled]           BIT              NULL,
    CONSTRAINT [pk_system_detail_securities] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_securities_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_batteries] (
    [id]                            INT              NOT NULL IDENTITY,
    [system_detail_id]              INT              NOT NULL,
    [has_battery]                   BIT              NULL,
    [battery_charge_percent]        INT              NULL,
    [battery_status]                NVARCHAR(MAX)    NULL,
    [battery_estimated_runtime]     INT              NULL,
    [battery_health]                NVARCHAR(MAX)    NULL,
    [battery_cycle_count]           INT              NULL,
    [battery_design_capacity]       INT              NULL,
    [battery_full_charge_capacity]  INT              NULL,
    CONSTRAINT [pk_system_detail_batteries] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_batteries_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_chasses] (
    [id]                       INT              NOT NULL IDENTITY,
    [system_detail_id]         INT              NOT NULL,
    [system_sku_number]        NVARCHAR(MAX)    NULL,
    [system_family]            NVARCHAR(MAX)    NULL,
    [chassis_type]             NVARCHAR(MAX)    NULL,
    [chassis_manufacturer]     NVARCHAR(MAX)    NULL,
    [chassis_serial_number]    NVARCHAR(MAX)    NULL,
    [chassis_asset_tag]        NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_chasses] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_chasses_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_softwares] (
    [id]                         INT              NOT NULL IDENTITY,
    [system_detail_id]           INT              NOT NULL,
    [installed_software_json]    NVARCHAR(MAX)    NULL,
    [installed_software_count]   INT              NULL,
    [installed_updates_json]     NVARCHAR(MAX)    NULL,
    [installed_updates_count]    INT              NULL,
    [last_update_date]           DATETIME2        NULL,
    [running_processes_json]     NVARCHAR(MAX)    NULL,
    [running_processes_count]    INT              NULL,
    [services_json]              NVARCHAR(MAX)    NULL,
    [running_services_count]     INT              NULL,
    [stopped_services_count]     INT              NULL,
    [startup_programs_json]      NVARCHAR(MAX)    NULL,
    [startup_programs_count]     INT              NULL,
    [scheduled_tasks_json]       NVARCHAR(MAX)    NULL,
    [scheduled_tasks_count]      INT              NULL,
    CONSTRAINT [pk_system_detail_softwares] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_softwares_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_users_groups] (
    [id]                    INT              NOT NULL IDENTITY,
    [system_detail_id]      INT              NOT NULL,
    [local_users_json]      NVARCHAR(MAX)    NULL,
    [local_groups_json]     NVARCHAR(MAX)    NULL,
    [logged_on_users_json]  NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_users_groups] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_users_groups_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_peripherals] (
    [id]                  INT              NOT NULL IDENTITY,
    [system_detail_id]    INT              NOT NULL,
    [printers_json]       NVARCHAR(MAX)    NULL,
    [usb_devices_json]    NVARCHAR(MAX)    NULL,
    [audio_devices_json]  NVARCHAR(MAX)    NULL,
    CONSTRAINT [pk_system_detail_peripherals] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_peripherals_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_event_logs] (
    [id]                    INT              NOT NULL IDENTITY,
    [system_detail_id]      INT              NOT NULL,
    [recent_errors_json]    NVARCHAR(MAX)    NULL,
    [recent_warnings_json]  NVARCHAR(MAX)    NULL,
    [blue_screen_count]     INT              NULL,
    [last_blue_screen]      DATETIME2        NULL,
    CONSTRAINT [pk_system_detail_event_logs] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_event_logs_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[system_detail_performances] (
    [id]                       INT       NOT NULL IDENTITY,
    [system_detail_id]         INT       NOT NULL,
    [cpu_usage_percent]        FLOAT     NULL,
    [memory_usage_percent]     FLOAT     NULL,
    [disk_usage_percent]       FLOAT     NULL,
    CONSTRAINT [pk_system_detail_performances] PRIMARY KEY ([id]),
    CONSTRAINT [fk_system_detail_performances_system_details_system_detail_id]
        FOREIGN KEY ([system_detail_id]) REFERENCES [dbo].[system_details] ([id]) ON DELETE CASCADE
);
GO

-- ============================================================================
-- SECTION 5: REMAINING CORE TABLES
-- ============================================================================

CREATE TABLE [dbo].[users] (
    [id]                                INT              NOT NULL IDENTITY,
    [username]                          NVARCHAR(50)     NOT NULL,
    [email]                             NVARCHAR(100)    NOT NULL,
    [password_hash]                     NVARCHAR(MAX)    NOT NULL,
    [first_name]                        NVARCHAR(50)     NOT NULL,
    [last_name]                         NVARCHAR(50)     NOT NULL,
    [department_id]                     INT              NULL,
    [phone]                             NVARCHAR(20)     NULL,
    [role_id]                           INT              NOT NULL,
    [is_active]                         BIT              NOT NULL,
    [is_email_verified]                 BIT              NOT NULL DEFAULT 0,
    [email_verification_token]          NVARCHAR(128)    NULL,
    [email_verification_token_expiry]   DATETIME2        NULL,
    [created_at]                        DATETIME2        NOT NULL,
    [updated_at]                        DATETIME2        NULL,
    [last_login_at]                     DATETIME2        NULL,
    [is_deleted]                        BIT              NOT NULL DEFAULT 0,
    [deleted_at]                        DATETIME2        NULL,
    CONSTRAINT [pk_users] PRIMARY KEY ([id]),
    CONSTRAINT [fk_users_departments_department_id]
        FOREIGN KEY ([department_id]) REFERENCES [dbo].[departments] ([id]) ON DELETE NO ACTION,
    CONSTRAINT [fk_users_user_roles_role_id]
        FOREIGN KEY ([role_id]) REFERENCES [dbo].[user_roles] ([id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [dbo].[version_histories] (
    [id]                   INT              NOT NULL IDENTITY,
    [version]              NVARCHAR(50)     NOT NULL,
    [type_id]              INT              NOT NULL,
    [change_log]           NVARCHAR(MAX)    NOT NULL,
    [released_at]          DATETIME2        NOT NULL,
    [released_by]          NVARCHAR(100)    NULL,
    [is_current_version]   BIT              NOT NULL,
    [checksum]             NVARCHAR(128)    NULL,
    [file_size]            BIGINT           NULL,
    [is_deleted]           BIT              NOT NULL DEFAULT 0,
    [deleted_at]           DATETIME2        NULL,
    CONSTRAINT [pk_version_histories] PRIMARY KEY ([id]),
    CONSTRAINT [fk_version_histories_version_types_type_id]
        FOREIGN KEY ([type_id]) REFERENCES [dbo].[version_types] ([id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [dbo].[package_versions] (
    [id]                    INT              NOT NULL IDENTITY,
    [version]               NVARCHAR(50)     NOT NULL,
    [package_type]          NVARCHAR(20)     NOT NULL DEFAULT 'Agent',
    [file_name]             NVARCHAR(255)    NOT NULL,
    [storage_path]          NVARCHAR(500)    NOT NULL,
    [checksum]              NVARCHAR(128)    NOT NULL,
    [file_size]             BIGINT           NOT NULL,
    [release_notes]         NVARCHAR(MAX)    NULL,
    [is_required]           BIT              NOT NULL DEFAULT 0,
    [is_breaking_change]    BIT              NOT NULL DEFAULT 0,
    [minimum_agent_version] NVARCHAR(50)     NULL,
    [status]                INT              NOT NULL DEFAULT 0,  -- 0=Draft, 1=Published, 2=Active, 3=Deprecated, 4=Archived
    [created_at]            DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    [created_by]            NVARCHAR(100)    NULL,
    [published_at]          DATETIME2        NULL,
    [deprecated_at]         DATETIME2        NULL,
    [download_count]        INT              NOT NULL DEFAULT 0,
    [channel]               NVARCHAR(20)     NOT NULL DEFAULT 'stable',
    [is_deleted]            BIT              NOT NULL DEFAULT 0,
    [deleted_at]            DATETIME2        NULL,
    CONSTRAINT [pk_package_versions] PRIMARY KEY ([id]),
    CONSTRAINT [uq_package_versions_version_type] UNIQUE ([version], [package_type])
);
GO

CREATE TABLE [dbo].[package_distribution_rules] (
    [id]                  INT              NOT NULL IDENTITY,
    [package_version_id]  INT              NOT NULL,
    [rule_type]           INT              NOT NULL DEFAULT 0,  -- 0=AllComputers, 1=BySerialNumber, 2=ByDepartment, 3=ByLocation
    [target_value]        NVARCHAR(200)    NOT NULL,
    [is_enabled]          BIT              NOT NULL DEFAULT 1,
    [created_at]          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [pk_package_distribution_rules] PRIMARY KEY ([id]),
    CONSTRAINT [fk_distribution_rules_package_version_id]
        FOREIGN KEY ([package_version_id]) REFERENCES [dbo].[package_versions] ([id]) ON DELETE CASCADE
);
GO

-- Refresh tokens (secure token rotation for JWT refresh)
CREATE TABLE [dbo].[refresh_tokens] (
    [id]          INT IDENTITY(1,1) NOT NULL,
    [user_id]     INT             NOT NULL,
    [token_hash]  NVARCHAR(128)   NOT NULL,
    [expires_at]  DATETIME2       NOT NULL,
    [created_at]  DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    [consumed_at] DATETIME2       NULL,
    [revoked_at]  DATETIME2       NULL,
    [family]      NVARCHAR(64)    NOT NULL,
    CONSTRAINT [pk_refresh_tokens] PRIMARY KEY ([id]),
    CONSTRAINT [fk_refresh_tokens_users_user_id] FOREIGN KEY ([user_id])
        REFERENCES [dbo].[users] ([id]) ON DELETE CASCADE
);
GO

-- Install tokens (short-lived, single-use tokens for secure install script access)
CREATE TABLE [dbo].[install_tokens] (
    [id]          INT            IDENTITY(1,1) NOT NULL,
    [token_hash]  NVARCHAR(128)  NOT NULL,
    [expires_at]  DATETIME2      NOT NULL,
    [created_at]  DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    [consumed_at] DATETIME2      NULL,
    [created_by]  NVARCHAR(256)  NOT NULL,
    CONSTRAINT [pk_install_tokens] PRIMARY KEY CLUSTERED ([id])
);
GO

-- Invite codes (gating user registration)
CREATE TABLE [dbo].[invite_codes] (
    [id]          INT            IDENTITY(1,1) NOT NULL,
    [code_hash]   NVARCHAR(128)  NOT NULL,
    [expires_at]  DATETIME2      NOT NULL,
    [created_at]  DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    [max_uses]    INT            NOT NULL DEFAULT 1,
    [use_count]   INT            NOT NULL DEFAULT 0,
    [created_by]  NVARCHAR(256)  NOT NULL,
    [note]        NVARCHAR(500)  NULL,
    [is_revoked]  BIT            NOT NULL DEFAULT 0,
    CONSTRAINT [pk_invite_codes] PRIMARY KEY CLUSTERED ([id])
);
GO

-- Computer ownerships (per-computer access control)
CREATE TABLE [dbo].[computer_ownerships] (
    [id]           INT            IDENTITY(1,1) NOT NULL,
    [computer_id]  INT            NOT NULL,
    [user_id]      INT            NOT NULL,
    [access_level] INT            NOT NULL DEFAULT 0,  -- 0=Viewer, 1=Manager, 2=Owner
    [granted_at]   DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    [granted_by]   NVARCHAR(100)  NULL,
    CONSTRAINT [pk_computer_ownerships] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [fk_computer_ownerships_computers] FOREIGN KEY ([computer_id])
        REFERENCES [dbo].[computers] ([id]) ON DELETE CASCADE,
    CONSTRAINT [fk_computer_ownerships_users] FOREIGN KEY ([user_id])
        REFERENCES [dbo].[users] ([id]) ON DELETE CASCADE
);
GO

-- App settings (key-value store for persisting application state)
CREATE TABLE [dbo].[app_settings] (
    [key]          NVARCHAR(128)  NOT NULL,
    [value]        NVARCHAR(1024) NOT NULL,
    [updated_at]   DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [pk_app_settings] PRIMARY KEY ([key])
);
GO

-- ============================================================================
-- SECTION 6: INDEXES
-- ============================================================================

-- Lookup table unique indexes
CREATE UNIQUE INDEX [ix_computer_types_name]        ON [dbo].[computer_types] ([name]);
CREATE UNIQUE INDEX [ix_computer_statuses_name]     ON [dbo].[computer_statuses] ([name]);
CREATE UNIQUE INDEX [ix_update_statuses_name]       ON [dbo].[update_statuses] ([name]);
CREATE UNIQUE INDEX [ix_maintenance_types_name]     ON [dbo].[maintenance_types] ([name]);
CREATE UNIQUE INDEX [ix_maintenance_statuses_name]  ON [dbo].[maintenance_statuses] ([name]);
CREATE UNIQUE INDEX [ix_restore_point_types_name]   ON [dbo].[restore_point_types] ([name]);
CREATE UNIQUE INDEX [ix_version_types_name]         ON [dbo].[version_types] ([name]);
CREATE UNIQUE INDEX [ix_user_roles_name]            ON [dbo].[user_roles] ([name]);
GO

-- Reference table unique indexes
CREATE UNIQUE INDEX [ix_departments_name]    ON [dbo].[departments] ([name]);
CREATE UNIQUE INDEX [ix_locations_name]      ON [dbo].[locations] ([name]);
CREATE UNIQUE INDEX [ix_manufacturers_name]  ON [dbo].[manufacturers] ([name]);
CREATE UNIQUE INDEX [ix_operating_systems_name] ON [dbo].[operating_systems] ([name]);
CREATE UNIQUE INDEX [ix_countries_name]         ON [dbo].[countries] ([name]);
CREATE UNIQUE INDEX [ix_countries_country_code] ON [dbo].[countries] ([country_code]);
CREATE UNIQUE INDEX [ix_computer_models_manufacturer_id_model_name]
    ON [dbo].[computer_models] ([manufacturer_id], [model_name]);
GO

-- Computers indexes
CREATE UNIQUE INDEX [ix_computers_serial_number]  ON [dbo].[computers] ([serial_number]);
CREATE INDEX [ix_computers_hostname]              ON [dbo].[computers] ([hostname]);
CREATE INDEX [ix_computers_status_id]             ON [dbo].[computers] ([status_id]);
CREATE INDEX [ix_computers_department_id]         ON [dbo].[computers] ([department_id]);
CREATE INDEX [ix_computers_location_id]           ON [dbo].[computers] ([location_id]);
CREATE INDEX [ix_computers_created_at]            ON [dbo].[computers] ([created_at]);
CREATE INDEX [ix_computers_type_id]               ON [dbo].[computers] ([type_id]);
CREATE INDEX [ix_computers_model_id]              ON [dbo].[computers] ([model_id]);
CREATE INDEX [ix_computers_operating_system_id]   ON [dbo].[computers] ([operating_system_id]);
CREATE INDEX [ix_computers_is_deleted]            ON [dbo].[computers] ([is_deleted]) WHERE [is_deleted] = 0;
GO

-- Computer agent statuses indexes
CREATE UNIQUE INDEX [ix_computer_agent_statuses_computer_id]
    ON [dbo].[computer_agent_statuses] ([computer_id]);
CREATE INDEX [ix_computer_agent_statuses_last_seen]
    ON [dbo].[computer_agent_statuses] ([last_seen]);
CREATE INDEX [ix_computer_agent_statuses_is_online]
    ON [dbo].[computer_agent_statuses] ([is_online]);
CREATE INDEX [ix_computer_agent_statuses_has_agent]
    ON [dbo].[computer_agent_statuses] ([has_agent]);
GO

-- Computer update requests indexes
CREATE INDEX [ix_computer_update_requests_computer_id]
    ON [dbo].[computer_update_requests] ([computer_id]);
CREATE INDEX [ix_computer_update_requests_computer_id_created_at]
    ON [dbo].[computer_update_requests] ([computer_id], [created_at]);
CREATE INDEX [ix_computer_update_requests_update_status_id]
    ON [dbo].[computer_update_requests] ([update_status_id]) WHERE [update_status_id] > 0;
GO

-- Computer geo locations indexes
CREATE UNIQUE INDEX [ix_computer_geo_locations_computer_id]   ON [dbo].[computer_geo_locations] ([computer_id]);
CREATE INDEX [ix_computer_geo_locations_country_id]           ON [dbo].[computer_geo_locations] ([country_id]);
CREATE INDEX [ix_computer_geo_locations_last_updated]         ON [dbo].[computer_geo_locations] ([last_updated]);
GO

-- Computer histories indexes
CREATE INDEX [ix_computer_histories_computer_id_changed_at]  ON [dbo].[computer_histories] ([computer_id], [changed_at]);
CREATE INDEX [ix_computer_histories_action]                  ON [dbo].[computer_histories] ([action]);
CREATE INDEX [ix_computer_histories_changed_at]              ON [dbo].[computer_histories] ([changed_at]);
CREATE INDEX [ix_computer_histories_is_deleted]              ON [dbo].[computer_histories] ([is_deleted]) WHERE [is_deleted] = 0;
GO

-- Maintenance records indexes
CREATE INDEX [ix_maintenance_records_computer_id]             ON [dbo].[maintenance_records] ([computer_id]);
CREATE INDEX [ix_maintenance_records_computer_id_scheduled_date]
    ON [dbo].[maintenance_records] ([computer_id], [scheduled_date]);
CREATE INDEX [ix_maintenance_records_scheduled_date]  ON [dbo].[maintenance_records] ([scheduled_date]);
CREATE INDEX [ix_maintenance_records_status_id]       ON [dbo].[maintenance_records] ([status_id]);
CREATE INDEX [ix_maintenance_records_type_id]         ON [dbo].[maintenance_records] ([type_id]);
CREATE INDEX [ix_maintenance_records_is_deleted]      ON [dbo].[maintenance_records] ([is_deleted]) WHERE [is_deleted] = 0;
GO

-- Restore points indexes
CREATE INDEX [ix_restore_points_computer_id_created_at]    ON [dbo].[restore_points] ([computer_id], [created_at]);
CREATE INDEX [ix_restore_points_computer_id_type_id]       ON [dbo].[restore_points] ([computer_id], [type_id]);
CREATE UNIQUE INDEX [ix_restore_points_restore_point_id]   ON [dbo].[restore_points] ([restore_point_id]);
CREATE INDEX [ix_restore_points_type_id]                   ON [dbo].[restore_points] ([type_id]);
CREATE INDEX [ix_restore_points_is_deleted]                ON [dbo].[restore_points] ([is_deleted]) WHERE [is_deleted] = 0;
GO

-- Restore point sub-table indexes
CREATE UNIQUE INDEX [ix_restore_point_system_infos_restore_point_id]
    ON [dbo].[restore_point_system_infos] ([restore_point_id]);
CREATE UNIQUE INDEX [ix_restore_point_snapshots_restore_point_id]
    ON [dbo].[restore_point_snapshots] ([restore_point_id]);
GO

-- System details indexes
CREATE INDEX [ix_system_details_computer_id_collected_at]
    ON [dbo].[system_details] ([computer_id], [collected_at]);
CREATE INDEX [ix_system_details_is_deleted]
    ON [dbo].[system_details] ([is_deleted]) WHERE [is_deleted] = 0;
GO

-- System detail sub-table unique indexes (1:1 on system_detail_id)
CREATE UNIQUE INDEX [ix_system_detail_bios_system_detail_id]
    ON [dbo].[system_detail_bios] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_motherboards_system_detail_id]
    ON [dbo].[system_detail_motherboards] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_cpus_system_detail_id]
    ON [dbo].[system_detail_cpus] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_memories_system_detail_id]
    ON [dbo].[system_detail_memories] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_storages_system_detail_id]
    ON [dbo].[system_detail_storages] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_gpus_system_detail_id]
    ON [dbo].[system_detail_gpus] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_networks_system_detail_id]
    ON [dbo].[system_detail_networks] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_oses_system_detail_id]
    ON [dbo].[system_detail_oses] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_securities_system_detail_id]
    ON [dbo].[system_detail_securities] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_batteries_system_detail_id]
    ON [dbo].[system_detail_batteries] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_chasses_system_detail_id]
    ON [dbo].[system_detail_chasses] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_softwares_system_detail_id]
    ON [dbo].[system_detail_softwares] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_users_groups_system_detail_id]
    ON [dbo].[system_detail_users_groups] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_peripherals_system_detail_id]
    ON [dbo].[system_detail_peripherals] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_event_logs_system_detail_id]
    ON [dbo].[system_detail_event_logs] ([system_detail_id]);
CREATE UNIQUE INDEX [ix_system_detail_performances_system_detail_id]
    ON [dbo].[system_detail_performances] ([system_detail_id]);
GO

-- Users indexes
CREATE UNIQUE INDEX [ix_users_username]       ON [dbo].[users] ([username]);
CREATE UNIQUE INDEX [ix_users_email]          ON [dbo].[users] ([email]);
CREATE INDEX [ix_users_department_id]         ON [dbo].[users] ([department_id]);
CREATE INDEX [ix_users_role_id]               ON [dbo].[users] ([role_id]);
CREATE INDEX [ix_users_is_deleted]            ON [dbo].[users] ([is_deleted]) WHERE [is_deleted] = 0;
GO

-- Version histories indexes
CREATE INDEX [ix_version_histories_released_at]               ON [dbo].[version_histories] ([released_at]);
CREATE INDEX [ix_version_histories_type_id_is_current_version]
    ON [dbo].[version_histories] ([type_id], [is_current_version]);
CREATE UNIQUE INDEX [ix_version_histories_type_id_version]
    ON [dbo].[version_histories] ([type_id], [version]);
CREATE INDEX [ix_version_histories_is_deleted]
    ON [dbo].[version_histories] ([is_deleted]) WHERE [is_deleted] = 0;
GO

-- Package versions indexes
CREATE INDEX [ix_package_versions_status_type]
    ON [dbo].[package_versions] ([status], [package_type]);
CREATE INDEX [ix_package_versions_channel_type]
    ON [dbo].[package_versions] ([channel], [package_type]);
CREATE INDEX [ix_package_versions_created_at]
    ON [dbo].[package_versions] ([created_at] DESC);
CREATE INDEX [ix_package_versions_is_deleted]
    ON [dbo].[package_versions] ([is_deleted]) WHERE [is_deleted] = 0;
GO

-- Package distribution rules indexes
CREATE INDEX [ix_package_distribution_rules_package_version_id]
    ON [dbo].[package_distribution_rules] ([package_version_id]);
GO

-- Refresh tokens indexes
CREATE INDEX [ix_refresh_tokens_token_hash] ON [dbo].[refresh_tokens] ([token_hash]);
CREATE INDEX [ix_refresh_tokens_user_id]    ON [dbo].[refresh_tokens] ([user_id]);
CREATE INDEX [ix_refresh_tokens_family]     ON [dbo].[refresh_tokens] ([family]);
GO

-- Install tokens indexes
CREATE INDEX [ix_install_tokens_token_hash] ON [dbo].[install_tokens] ([token_hash]);
GO

-- Invite codes indexes
CREATE INDEX [ix_invite_codes_code_hash] ON [dbo].[invite_codes] ([code_hash]);
GO

-- Computer ownerships indexes
CREATE UNIQUE INDEX [ix_computer_ownerships_computer_id_user_id]
    ON [dbo].[computer_ownerships] ([computer_id], [user_id]);
CREATE INDEX [ix_computer_ownerships_user_id]
    ON [dbo].[computer_ownerships] ([user_id]);
GO

-- ============================================================================
-- SECTION 7: SEED DATA
-- ============================================================================

-- computer_types
INSERT INTO [dbo].[computer_types] ([id], [name]) VALUES
    (0, N'Desktop'),
    (1, N'Laptop'),
    (2, N'Server'),
    (3, N'Workstation'),
    (4, N'Tablet'),
    (5, N'Other');
GO

-- computer_statuses
INSERT INTO [dbo].[computer_statuses] ([id], [name]) VALUES
    (0, N'Unassigned'),
    (1, N'Assigned'),
    (2, N'InMaintenance'),
    (3, N'Retired'),
    (4, N'Lost'),
    (5, N'Stolen'),
    (6, N'Disposed');
GO

-- update_statuses
INSERT INTO [dbo].[update_statuses] ([id], [name]) VALUES
    (0, N'None'),
    (1, N'Pending'),
    (2, N'CheckingForUpdate'),
    (3, N'Downloading'),
    (4, N'Verifying'),
    (5, N'Extracting'),
    (6, N'PreparingUpdater'),
    (7, N'StoppingService'),
    (8, N'BackingUp'),
    (9, N'CopyingFiles'),
    (10, N'StartingService'),
    (11, N'Completed'),
    (99, N'Failed');
GO

-- maintenance_types
INSERT INTO [dbo].[maintenance_types] ([id], [name]) VALUES
    (0, N'Repair'),
    (1, N'Upgrade'),
    (2, N'Cleaning'),
    (3, N'SoftwareUpdate'),
    (4, N'HardwareReplacement'),
    (5, N'Inspection'),
    (6, N'Other');
GO

-- maintenance_statuses
INSERT INTO [dbo].[maintenance_statuses] ([id], [name]) VALUES
    (0, N'Scheduled'),
    (1, N'InProgress'),
    (2, N'Completed'),
    (3, N'Cancelled');
GO

-- restore_point_types
INSERT INTO [dbo].[restore_point_types] ([id], [name]) VALUES
    (0, N'Reboot'),
    (1, N'Scheduled'),
    (2, N'Manual'),
    (3, N'Initial'),
    (4, N'PreChange'),
    (5, N'PostChange');
GO

-- version_types
INSERT INTO [dbo].[version_types] ([id], [name]) VALUES
    (0, N'Server'),
    (1, N'ThinClient');
GO

-- user_roles
INSERT INTO [dbo].[user_roles] ([id], [name]) VALUES
    (0, N'Viewer'),
    (1, N'Technician'),
    (2, N'Admin');
GO

-- Default admin user (password: Admin123!)
SET IDENTITY_INSERT [dbo].[users] ON;
INSERT INTO [dbo].[users] ([id], [username], [email], [password_hash], [first_name], [last_name], [role_id], [is_active], [is_email_verified], [created_at])
VALUES (1, N'admin', N'admin@inventory.local',
    N'$2a$11$.te4SnOIeqfLk7OMIlYMl.BEMTfdLx4pVB9Crc3Cbu4CWCkQ/BGTG',
    N'System', N'Administrator', 2, 1, 1, GETUTCDATE());
SET IDENTITY_INSERT [dbo].[users] OFF;
GO

-- ============================================================================
-- SECTION 8: STORED PROCEDURES
-- ============================================================================
-- All procedures use CREATE OR ALTER so this section is re-runnable.
-- Final definitions incorporate updates from migrations 029–045.
-- ============================================================================

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.1  BULK MAINTENANCE OPERATIONS — DatabaseMaintenanceService.cs
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_mark_offline_computers]
    @offline_threshold_minutes INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(MINUTE, -@offline_threshold_minutes, SYSUTCDATETIME());

    UPDATE [dbo].[computer_agent_statuses]
    SET [is_online] = 0
    WHERE [is_online] = 1
      AND [last_seen] < @cutoff;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_old_system_details]
    @retention_days INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(DAY, -@retention_days, SYSUTCDATETIME());

    UPDATE [dbo].[system_details]
    SET [is_deleted]  = 1,
        [deleted_at]  = SYSUTCDATETIME()
    WHERE [collected_at] < @cutoff
      AND [is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_old_restore_points]
    @retention_days INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(DAY, -@retention_days, SYSUTCDATETIME());

    UPDATE [dbo].[restore_points]
    SET [is_deleted]  = 1,
        [deleted_at]  = SYSUTCDATETIME()
    WHERE [created_at] < @cutoff
      AND [is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_old_history_records]
    @retention_days INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(DAY, -@retention_days, SYSUTCDATETIME());

    UPDATE [dbo].[computer_histories]
    SET [is_deleted]  = 1,
        [deleted_at]  = SYSUTCDATETIME()
    WHERE [changed_at] < @cutoff
      AND [is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_mark_stuck_updates_as_failed]
    @timeout_hours INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(HOUR, -@timeout_hours, SYSUTCDATETIME());
    DECLARE @failed_status INT = 99;
    DECLARE @msg NVARCHAR(500) = CONCAT(N'Update timed out after ', @timeout_hours, N' hours');

    UPDATE r
    SET r.[update_status_id]      = @failed_status,
        r.[update_status_message] = @msg
    FROM [dbo].[computer_update_requests] r
    INNER JOIN [dbo].[computer_agent_statuses] s
        ON s.[active_update_request_id] = r.[id]
    WHERE r.[update_status_id] > 0
      AND r.[update_status_id] < 11
      AND r.[update_status_id] <> 99
      AND r.[update_started_at] < @cutoff;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_excess_system_details]
    @max_records_per_computer INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE t
    SET t.[is_deleted]  = 1,
        t.[deleted_at]  = SYSUTCDATETIME()
    FROM [dbo].[system_details] t
    INNER JOIN (
        SELECT [id],
               ROW_NUMBER() OVER (
                   PARTITION BY [computer_id]
                   ORDER BY [collected_at] DESC
               ) AS rn
        FROM [dbo].[system_details]
        WHERE [is_deleted] = 0
    ) ranked ON ranked.[id] = t.[id]
    WHERE ranked.rn > @max_records_per_computer;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_excess_restore_points]
    @max_records_per_computer INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE t
    SET t.[is_deleted]  = 1,
        t.[deleted_at]  = SYSUTCDATETIME()
    FROM [dbo].[restore_points] t
    INNER JOIN (
        SELECT [id],
               ROW_NUMBER() OVER (
                   PARTITION BY [computer_id]
                   ORDER BY [created_at] DESC
               ) AS rn
        FROM [dbo].[restore_points]
        WHERE [is_deleted] = 0
    ) ranked ON ranked.[id] = t.[id]
    WHERE ranked.rn > @max_records_per_computer;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_excess_records]
    @table_name              NVARCHAR(128),
    @max_records_per_computer INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @order_column NVARCHAR(128) = CASE @table_name
        WHEN N'system_details' THEN N'collected_at'
        WHEN N'restore_points' THEN N'created_at'
        ELSE NULL
    END;

    IF @order_column IS NULL
    BEGIN
        RAISERROR('sp_cleanup_excess_records: unsupported table name ''%s''.', 16, 1, @table_name);
        RETURN;
    END;

    DECLARE @sql NVARCHAR(MAX) = N'
        UPDATE t
        SET t.[is_deleted]  = 1,
            t.[deleted_at]  = SYSUTCDATETIME()
        FROM ' + QUOTENAME(@table_name) + N' t
        INNER JOIN (
            SELECT [id],
                   ROW_NUMBER() OVER (
                       PARTITION BY [computer_id]
                       ORDER BY ' + QUOTENAME(@order_column) + N' DESC
                   ) AS rn
            FROM ' + QUOTENAME(@table_name) + N'
            WHERE [is_deleted] = 0
        ) ranked ON ranked.[id] = t.[id]
        WHERE ranked.rn > @max_per_computer;

        SELECT @@ROWCOUNT AS [affected_rows];';

    EXEC sp_executesql @sql,
         N'@max_per_computer INT',
         @max_per_computer = @max_records_per_computer;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.2  REPORTS — SystemDetailsController / SystemDetailsRepository
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_system_details_report]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH latest AS (
        SELECT sd.[id],
               sd.[computer_id],
               sd.[collected_at],
               sd.[machine_guid],
               sd.[product_id],
               ROW_NUMBER() OVER (
                   PARTITION BY sd.[computer_id]
                   ORDER BY sd.[collected_at] DESC
               ) AS rn
        FROM [dbo].[system_details] sd
        WHERE sd.[is_deleted] = 0
    )
    SELECT COUNT(DISTINCT [computer_id]) AS [total_count]
    FROM latest
    WHERE rn = 1;

    ;WITH latest AS (
        SELECT sd.[id],
               sd.[computer_id],
               sd.[collected_at],
               sd.[machine_guid],
               sd.[product_id],
               ROW_NUMBER() OVER (
                   PARTITION BY sd.[computer_id]
                   ORDER BY sd.[collected_at] DESC
               ) AS rn
        FROM [dbo].[system_details] sd
        WHERE sd.[is_deleted] = 0
    )
    SELECT
        l.[id], l.[computer_id], l.[collected_at], l.[machine_guid], l.[product_id],
        c.[hostname], c.[serial_number],
        b.[bios_version], b.[bios_manufacturer], b.[bios_release_date], b.[bios_serial_number], b.[smbios_version],
        mb.[motherboard_manufacturer], mb.[motherboard_product], mb.[motherboard_serial_number], mb.[motherboard_version],
        cpu.[cpu_name], cpu.[cpu_manufacturer], cpu.[cpu_cores], cpu.[cpu_logical_processors],
        cpu.[cpu_max_clock_speed], cpu.[cpu_current_clock_speed], cpu.[cpu_architecture],
        cpu.[cpu_l2cache_size], cpu.[cpu_l3cache_size], cpu.[cpu_socket_designation], cpu.[cpu_processor_id],
        mem.[total_physical_memory], mem.[available_physical_memory], mem.[total_virtual_memory],
        mem.[available_virtual_memory], mem.[memory_slots], mem.[memory_slots_used], mem.[memory_modules_json],
        stor.[disks_json], stor.[partitions_json], stor.[logical_drives_json],
        stor.[total_disk_space], stor.[free_disk_space], stor.[shared_folders_json], stor.[mapped_drives_json],
        gpu.[gpu_name], gpu.[gpu_manufacturer], gpu.[gpu_driver_version], gpu.[gpu_driver_date],
        gpu.[gpu_ram], gpu.[gpu_resolution], gpu.[graphics_cards_json], gpu.[monitors_json], gpu.[monitor_count],
        net.[network_adapters_json], net.[domain_name], net.[dns_hostname], net.[workgroup],
        net.[domain_joined], net.[default_gateway], net.[dns_servers], net.[dhcp_server],
        os.[os_name], os.[os_version], os.[os_build], os.[os_architecture], os.[os_install_date],
        os.[os_last_boot_time], os.[os_serial_number], os.[os_product_key],
        os.[os_service_pack_major], os.[os_service_pack_minor], os.[os_locale], os.[os_language],
        os.[time_zone], os.[os_uptime], os.[windows_product_id], os.[windows_registered_owner],
        os.[windows_registered_organization], os.[windows_is_activated], os.[windows_activation_status],
        os.[environment_variables_json],
        sec.[antivirus_product], sec.[antivirus_status], sec.[firewall_status],
        sec.[bit_locker_enabled], sec.[bit_locker_status], sec.[secure_boot_enabled],
        sec.[tpm_present], sec.[tpm_version], sec.[uac_enabled],
        bat.[has_battery], bat.[battery_charge_percent], bat.[battery_status],
        bat.[battery_estimated_runtime], bat.[battery_health], bat.[battery_cycle_count],
        bat.[battery_design_capacity], bat.[battery_full_charge_capacity],
        ch.[system_sku_number], ch.[system_family], ch.[chassis_type], ch.[chassis_manufacturer],
        ch.[chassis_serial_number], ch.[chassis_asset_tag],
        sw.[installed_software_json], sw.[installed_software_count], sw.[installed_updates_json],
        sw.[installed_updates_count], sw.[last_update_date], sw.[running_processes_json],
        sw.[running_processes_count], sw.[services_json], sw.[running_services_count],
        sw.[stopped_services_count], sw.[startup_programs_json], sw.[startup_programs_count],
        sw.[scheduled_tasks_json], sw.[scheduled_tasks_count],
        ug.[local_users_json], ug.[local_groups_json], ug.[logged_on_users_json],
        ph.[printers_json], ph.[usb_devices_json], ph.[audio_devices_json],
        el.[recent_errors_json], el.[recent_warnings_json], el.[blue_screen_count], el.[last_blue_screen],
        perf.[cpu_usage_percent], perf.[memory_usage_percent], perf.[disk_usage_percent]
    FROM latest l
    INNER JOIN [dbo].[computers] c         ON c.[id] = l.[computer_id]
    LEFT  JOIN [dbo].[system_detail_bios]          b    ON b.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_motherboards]  mb   ON mb.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_cpus]          cpu  ON cpu.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_memories]      mem  ON mem.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_storages]      stor ON stor.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_gpus]          gpu  ON gpu.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_networks]      net  ON net.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_oses]          os   ON os.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_securities]    sec  ON sec.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_batteries]     bat  ON bat.[system_detail_id] = l.[id]
    LEFT  JOIN [dbo].[system_detail_chasses]       ch   ON ch.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_softwares]     sw   ON sw.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_users_groups]  ug   ON ug.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_peripherals]   ph   ON ph.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_event_logs]    el   ON el.[system_detail_id]  = l.[id]
    LEFT  JOIN [dbo].[system_detail_performances]  perf ON perf.[system_detail_id] = l.[id]
    WHERE l.rn = 1
      AND c.[is_deleted] = 0
    ORDER BY c.[hostname]
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.3  SEARCH & GEO-LOCATION
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

-- sp_search_computers (030: returns IDs only for EF Core hydration)
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

CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_ids_needing_geo_lookup]
    @cutoff_hours INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff DATETIME2 = DATEADD(HOUR, -@cutoff_hours, SYSUTCDATETIME());

    SELECT c.[id]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_geo_locations] g    ON g.[computer_id]   = c.[id]
    WHERE c.[is_deleted] = 0
      AND cas.[has_agent] = 1
      AND cas.[is_online] = 1
      AND cas.[public_ip_address] IS NOT NULL
      AND cas.[public_ip_address] <> N''
      AND (
          g.[id] IS NULL
       OR g.[ip_last_updated] IS NULL
       OR g.[ip_last_updated] < @cutoff
      );
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_computer_locations]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.[id], c.[hostname], c.[serial_number], c.[status_id],
        cas.[public_ip_address], cas.[is_online], cas.[last_seen], cas.[current_user],
        cas.[preferred_location_source],
        g.[gps_latitude], g.[gps_longitude], g.[gps_last_updated],
        g.[ip_latitude], g.[ip_longitude], g.[ip_last_updated],
        g.[city], g.[region],
        co.[name]         AS [country],
        co.[country_code],
        g.[last_updated]  AS [location_updated],
        g.[latitude]      AS [legacy_latitude],
        g.[longitude]     AS [legacy_longitude],
        g.[location_source] AS [legacy_location_source]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_geo_locations] g    ON g.[computer_id]   = c.[id]
    LEFT  JOIN [dbo].[countries] co                ON co.[id]           = g.[country_id]
    WHERE c.[is_deleted] = 0
      AND (cas.[public_ip_address] IS NOT NULL OR g.[id] IS NOT NULL)
    ORDER BY c.[hostname];
END
GO

-- sp_upsert_geo_location (031: COALESCE on WHEN MATCHED)
CREATE OR ALTER PROCEDURE [dbo].[sp_upsert_geo_location]
    @computer_id       INT,
    @public_ip_address NVARCHAR(45)  = NULL,
    @gps_latitude      FLOAT         = NULL,
    @gps_longitude     FLOAT         = NULL,
    @gps_last_updated  DATETIME2     = NULL,
    @gps_accuracy      FLOAT         = NULL,
    @ip_latitude       FLOAT         = NULL,
    @ip_longitude      FLOAT         = NULL,
    @ip_last_updated   DATETIME2     = NULL,
    @latitude          FLOAT         = NULL,
    @longitude         FLOAT         = NULL,
    @city              NVARCHAR(100) = NULL,
    @region            NVARCHAR(100) = NULL,
    @country_id        INT           = NULL,
    @isp               NVARCHAR(200) = NULL,
    @timezone          NVARCHAR(50)  = NULL,
    @lookup_successful BIT           = 0,
    @lookup_error      NVARCHAR(500) = NULL,
    @location_source   NVARCHAR(MAX) = N'Auto'
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [dbo].[computer_geo_locations] AS tgt
    USING (SELECT @computer_id AS [computer_id]) AS src
        ON tgt.[computer_id] = src.[computer_id]
    WHEN MATCHED THEN
        UPDATE SET
            [public_ip_address]  = COALESCE(@public_ip_address,  tgt.[public_ip_address]),
            [gps_latitude]       = COALESCE(@gps_latitude,       tgt.[gps_latitude]),
            [gps_longitude]      = COALESCE(@gps_longitude,      tgt.[gps_longitude]),
            [gps_last_updated]   = COALESCE(@gps_last_updated,   tgt.[gps_last_updated]),
            [gps_accuracy_meters]= COALESCE(@gps_accuracy,       tgt.[gps_accuracy_meters]),
            [ip_latitude]        = COALESCE(@ip_latitude,        tgt.[ip_latitude]),
            [ip_longitude]       = COALESCE(@ip_longitude,       tgt.[ip_longitude]),
            [ip_last_updated]    = COALESCE(@ip_last_updated,    tgt.[ip_last_updated]),
            [latitude]           = COALESCE(@latitude,           tgt.[latitude]),
            [longitude]          = COALESCE(@longitude,          tgt.[longitude]),
            [city]               = COALESCE(@city,               tgt.[city]),
            [region]             = COALESCE(@region,             tgt.[region]),
            [country_id]         = COALESCE(@country_id,         tgt.[country_id]),
            [isp]                = COALESCE(@isp,                tgt.[isp]),
            [timezone]           = COALESCE(@timezone,           tgt.[timezone]),
            [last_updated]       = SYSUTCDATETIME(),
            [lookup_successful]  = @lookup_successful,
            [lookup_error]       = @lookup_error,
            [location_source]    = @location_source
    WHEN NOT MATCHED THEN
        INSERT ([computer_id], [public_ip_address],
                [gps_latitude], [gps_longitude], [gps_last_updated], [gps_accuracy_meters],
                [ip_latitude], [ip_longitude], [ip_last_updated],
                [latitude], [longitude], [city], [region], [country_id],
                [isp], [timezone], [last_updated], [lookup_successful],
                [lookup_error], [location_source])
        VALUES (@computer_id, @public_ip_address,
                @gps_latitude, @gps_longitude, @gps_last_updated, @gps_accuracy,
                @ip_latitude, @ip_longitude, @ip_last_updated,
                @latitude, @longitude, @city, @region, @country_id,
                @isp, @timezone, SYSUTCDATETIME(), @lookup_successful,
                @lookup_error, @location_source);
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.4  RESTORE POINTS
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_restore_point_summary]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(DISTINCT rp.[computer_id]) AS [total_count]
    FROM [dbo].[restore_points] rp
    WHERE rp.[is_deleted] = 0;

    SELECT
        rp.[computer_id],
        c.[hostname],
        c.[serial_number],
        COUNT(*)                    AS [total_restore_points],
        MAX(rp.[created_at])        AS [latest_restore_point],
        MIN(rp.[created_at])        AS [oldest_restore_point],
        SUM(ISNULL(rp.[data_size_bytes], 0)) AS [total_size_bytes],
        SUM(CASE WHEN rp.[type_id] = 0 THEN 1 ELSE 0 END) AS [reboot_count]
    FROM [dbo].[restore_points] rp
    INNER JOIN [dbo].[computers] c ON c.[id] = rp.[computer_id]
    WHERE rp.[is_deleted] = 0
      AND c.[is_deleted] = 0
    GROUP BY rp.[computer_id], c.[hostname], c.[serial_number]
    ORDER BY MAX(rp.[created_at]) DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_cleanup_old_restore_points_by_computer]
    @computer_id INT,
    @keep_count  INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH keep AS (
        SELECT TOP (@keep_count) [id]
        FROM [dbo].[restore_points]
        WHERE [computer_id] = @computer_id
          AND [is_deleted] = 0
        ORDER BY [created_at] DESC
    )
    DELETE FROM [dbo].[restore_points]
    WHERE [computer_id] = @computer_id
      AND [is_deleted] = 0
      AND [id] NOT IN (SELECT [id] FROM keep);

    SELECT @@ROWCOUNT AS [deleted_rows];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.5  COMPUTER REPOSITORY
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_by_serial_number]
    @serial_number NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

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
        ur.[update_requested_by], ur.[previous_agent_version], ur.[last_updated_at] AS [ur_last_updated],
        ur.[created_at] AS [ur_created_at]
    FROM [dbo].[computers] c
    LEFT JOIN [dbo].[computer_agent_statuses] cas    ON cas.[computer_id] = c.[id]
    LEFT JOIN [dbo].[computer_update_requests] ur    ON ur.[id] = cas.[active_update_request_id]
    WHERE c.[serial_number] = @serial_number
      AND c.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_total_computer_count]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) AS [total_count] FROM [dbo].[computers] WHERE [is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_status_counts]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [status_id], COUNT(*) AS [count] FROM [dbo].[computers] WHERE [is_deleted] = 0 GROUP BY [status_id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_delete_computer_direct]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[computers]
    SET [is_deleted] = 1,
        [deleted_at] = SYSUTCDATETIME()
    WHERE [id] = @computer_id
      AND [is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- sp_get_all_computers (032: server-side filter/sort/page)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_computers]
    @search_ids         NVARCHAR(MAX)   = NULL,
    @status             NVARCHAR(100)   = NULL,
    @type               NVARCHAR(100)   = NULL,
    @location           NVARCHAR(100)   = NULL,
    @department         NVARCHAR(100)   = NULL,
    @connection         NVARCHAR(20)    = NULL,
    @cutoff_minutes     INT             = 10,
    @sort_by            NVARCHAR(50)    = NULL,
    @sort_direction     NVARCHAR(4)     = 'ASC',
    @page               INT             = 1,
    @page_size          INT             = 50
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SearchIds TABLE ([id] INT);
    IF @search_ids IS NOT NULL
    BEGIN
        INSERT INTO @SearchIds ([id])
        SELECT CAST(value AS INT)
        FROM STRING_SPLIT(@search_ids, ',')
        WHERE RTRIM(LTRIM(value)) <> '';
    END

    DECLARE @cutoff DATETIME2 = DATEADD(MINUTE, -@cutoff_minutes, SYSUTCDATETIME());

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[computers] c
    LEFT JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT JOIN [dbo].[computer_statuses] cs        ON cs.[id] = c.[status_id]
    LEFT JOIN [dbo].[computer_types] ct           ON ct.[id] = c.[type_id]
    LEFT JOIN [dbo].[locations] loc               ON loc.[id] = c.[location_id]
    LEFT JOIN [dbo].[departments] dept            ON dept.[id] = c.[department_id]
    WHERE c.[is_deleted] = 0
      AND (@search_ids IS NULL OR c.[id] IN (SELECT [id] FROM @SearchIds))
      AND (@status     IS NULL OR cs.[name] = @status)
      AND (@type       IS NULL OR ct.[name] = @type)
      AND (@location   IS NULL OR loc.[name] = @location)
      AND (@department IS NULL OR dept.[name] = @department)
      AND (
            @connection IS NULL
            OR (@connection = 'Online'  AND cas.[has_agent] = 1 AND cas.[last_seen] IS NOT NULL AND cas.[last_seen] > @cutoff)
            OR (@connection = 'Offline' AND cas.[has_agent] = 1 AND (cas.[last_seen] IS NULL OR cas.[last_seen] <= @cutoff))
            OR (@connection = 'NoAgent' AND (cas.[has_agent] = 0 OR cas.[has_agent] IS NULL))
          );

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
        ur.[update_requested_by], ur.[previous_agent_version], ur.[last_updated_at] AS [ur_last_updated],
        ur.[created_at] AS [ur_created_at],
        cs.[name]   AS [_status_name],
        ct.[name]   AS [_type_name],
        loc.[name]  AS [_location_name],
        dept.[name] AS [_department_name],
        m.[model_name]        AS [_model_name],
        m.[manufacturer_id]   AS [_manufacturer_id],
        mfr.[name]            AS [_manufacturer_name]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m          ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr          ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os       ON os.[id] = c.[operating_system_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_update_requests] ur ON ur.[id] = cas.[active_update_request_id]
    LEFT  JOIN [dbo].[computer_statuses] cs       ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[computer_types] ct          ON ct.[id] = c.[type_id]
    LEFT  JOIN [dbo].[locations] loc              ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept           ON dept.[id] = c.[department_id]
    WHERE c.[is_deleted] = 0
      AND (@search_ids IS NULL OR c.[id] IN (SELECT [id] FROM @SearchIds))
      AND (@status     IS NULL OR cs.[name] = @status)
      AND (@type       IS NULL OR ct.[name] = @type)
      AND (@location   IS NULL OR loc.[name] = @location)
      AND (@department IS NULL OR dept.[name] = @department)
      AND (
            @connection IS NULL
            OR (@connection = 'Online'  AND cas.[has_agent] = 1 AND cas.[last_seen] IS NOT NULL AND cas.[last_seen] > @cutoff)
            OR (@connection = 'Offline' AND cas.[has_agent] = 1 AND (cas.[last_seen] IS NULL OR cas.[last_seen] <= @cutoff))
            OR (@connection = 'NoAgent' AND (cas.[has_agent] = 0 OR cas.[has_agent] IS NULL))
          )
    ORDER BY
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END DESC,
        c.[hostname] ASC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- sp_get_computer_by_id (033: c.* + underscore-prefixed nav columns)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_by_id]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: Computer with includes
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

-- sp_get_computers_by_status (034: sort/page)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computers_by_status]
    @status_id       INT,
    @sort_by         NVARCHAR(50)  = NULL,
    @sort_direction  NVARCHAR(4)   = 'ASC',
    @page            INT           = NULL,
    @page_size       INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[computers] c
    WHERE c.[is_deleted] = 0
      AND c.[status_id] = @status_id;

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
        ur.[update_requested_by], ur.[previous_agent_version], ur.[last_updated_at] AS [ur_last_updated],
        ur.[created_at] AS [ur_created_at],
        cs.[name]   AS [_status_name],
        ct.[name]   AS [_type_name],
        loc.[name]  AS [_location_name],
        dept.[name] AS [_department_name],
        m.[model_name]        AS [_model_name],
        m.[manufacturer_id]   AS [_manufacturer_id],
        mfr.[name]            AS [_manufacturer_name]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m          ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr          ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os       ON os.[id] = c.[operating_system_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_update_requests] ur ON ur.[id] = cas.[active_update_request_id]
    LEFT  JOIN [dbo].[computer_statuses] cs       ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[computer_types] ct          ON ct.[id] = c.[type_id]
    LEFT  JOIN [dbo].[locations] loc              ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept           ON dept.[id] = c.[department_id]
    WHERE c.[is_deleted] = 0
      AND c.[status_id] = @status_id
    ORDER BY
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END DESC,
        c.[hostname] ASC
    OFFSET ISNULL((@page - 1) * @page_size, 0) ROWS
    FETCH NEXT ISNULL(@page_size, 2147483647) ROWS ONLY;
END
GO

-- sp_get_computers_by_location (035: sort/page)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computers_by_location]
    @location_id     INT,
    @sort_by         NVARCHAR(50)  = NULL,
    @sort_direction  NVARCHAR(4)   = 'ASC',
    @page            INT           = NULL,
    @page_size       INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[computers] c
    WHERE c.[is_deleted] = 0
      AND c.[location_id] = @location_id;

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
        ur.[update_requested_by], ur.[previous_agent_version], ur.[last_updated_at] AS [ur_last_updated],
        ur.[created_at] AS [ur_created_at],
        cs.[name]   AS [_status_name],
        ct.[name]   AS [_type_name],
        loc.[name]  AS [_location_name],
        dept.[name] AS [_department_name],
        m.[model_name]        AS [_model_name],
        m.[manufacturer_id]   AS [_manufacturer_id],
        mfr.[name]            AS [_manufacturer_name]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m          ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr          ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os       ON os.[id] = c.[operating_system_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_update_requests] ur ON ur.[id] = cas.[active_update_request_id]
    LEFT  JOIN [dbo].[computer_statuses] cs       ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[computer_types] ct          ON ct.[id] = c.[type_id]
    LEFT  JOIN [dbo].[locations] loc              ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept           ON dept.[id] = c.[department_id]
    WHERE c.[is_deleted] = 0
      AND c.[location_id] = @location_id
    ORDER BY
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END DESC,
        c.[hostname] ASC
    OFFSET ISNULL((@page - 1) * @page_size, 0) ROWS
    FETCH NEXT ISNULL(@page_size, 2147483647) ROWS ONLY;
END
GO

-- sp_get_computers_by_department (036: sort/page)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_computers_by_department]
    @department_id   INT,
    @sort_by         NVARCHAR(50)  = NULL,
    @sort_direction  NVARCHAR(4)   = 'ASC',
    @page            INT           = NULL,
    @page_size       INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[computers] c
    WHERE c.[is_deleted] = 0
      AND c.[department_id] = @department_id;

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
        ur.[update_requested_by], ur.[previous_agent_version], ur.[last_updated_at] AS [ur_last_updated],
        ur.[created_at] AS [ur_created_at],
        cs.[name]   AS [_status_name],
        ct.[name]   AS [_type_name],
        loc.[name]  AS [_location_name],
        dept.[name] AS [_department_name],
        m.[model_name]        AS [_model_name],
        m.[manufacturer_id]   AS [_manufacturer_id],
        mfr.[name]            AS [_manufacturer_name]
    FROM [dbo].[computers] c
    INNER JOIN [dbo].[computer_models] m          ON m.[id]  = c.[model_id]
    INNER JOIN [dbo].[manufacturers] mfr          ON mfr.[id] = m.[manufacturer_id]
    LEFT  JOIN [dbo].[operating_systems] os       ON os.[id] = c.[operating_system_id]
    LEFT  JOIN [dbo].[computer_agent_statuses] cas ON cas.[computer_id] = c.[id]
    LEFT  JOIN [dbo].[computer_update_requests] ur ON ur.[id] = cas.[active_update_request_id]
    LEFT  JOIN [dbo].[computer_statuses] cs       ON cs.[id] = c.[status_id]
    LEFT  JOIN [dbo].[computer_types] ct          ON ct.[id] = c.[type_id]
    LEFT  JOIN [dbo].[locations] loc              ON loc.[id] = c.[location_id]
    LEFT  JOIN [dbo].[departments] dept           ON dept.[id] = c.[department_id]
    WHERE c.[is_deleted] = 0
      AND c.[department_id] = @department_id
    ORDER BY
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'hostname'     THEN c.[hostname]
                WHEN 'serialnumber' THEN c.[serial_number]
                WHEN 'ipaddress'    THEN c.[ip_address]
                WHEN 'status'       THEN cs.[name]
                WHEN 'type'         THEN ct.[name]
                WHEN 'location'     THEN loc.[name]
                WHEN 'department'   THEN dept.[name]
                WHEN 'manufacturer' THEN mfr.[name]
                WHEN 'model'        THEN m.[model_name]
                WHEN 'assignedto'   THEN c.[assigned_to]
                WHEN 'agentversion' THEN cas.[agent_version]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'updatedat'  THEN c.[updated_at]
                WHEN 'createdat'  THEN c.[created_at]
                WHEN 'lastseen'   THEN cas.[last_seen]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'isonline' THEN CAST(cas.[is_online] AS INT)
            END
        END DESC,
        c.[hostname] ASC
    OFFSET ISNULL((@page - 1) * @page_size, 0) ROWS
    FETCH NEXT ISNULL(@page_size, 2147483647) ROWS ONLY;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.6  LOOKUPS
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[computer_types] ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_statuses]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[computer_statuses] ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_departments]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[departments] ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_locations]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[locations] ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_manufacturers]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[manufacturers] ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_models]
    @manufacturer_id INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        cm.[id],
        cm.[model_name]        AS [name],
        cm.[manufacturer_id],
        mfr.[name]             AS [manufacturer_name]
    FROM [dbo].[computer_models] cm
    INNER JOIN [dbo].[manufacturers] mfr ON mfr.[id] = cm.[manufacturer_id]
    WHERE @manufacturer_id IS NULL
       OR cm.[manufacturer_id] = @manufacturer_id
    ORDER BY mfr.[name], cm.[model_name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_operating_systems]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[operating_systems] ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_location_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[locations] ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_department_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[departments] ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_manufacturer_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at] FROM [dbo].[manufacturers] ORDER BY [name];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.7  REFERENCE DATA — find-or-create via MERGE
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_manufacturer]
    @name NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[manufacturers] WITH (HOLDLOCK) AS tgt
    USING (SELECT @name AS [name]) AS src
        ON tgt.[name] = src.[name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[name] = tgt.[name]
    WHEN NOT MATCHED THEN
        INSERT ([name], [created_at])
        VALUES (@name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_computer_model]
    @manufacturer_id INT,
    @model_name      NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[computer_models] WITH (HOLDLOCK) AS tgt
    USING (SELECT @manufacturer_id AS [manufacturer_id],
                  @model_name      AS [model_name]) AS src
        ON tgt.[manufacturer_id] = src.[manufacturer_id]
       AND tgt.[model_name]      = src.[model_name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[model_name] = tgt.[model_name]
    WHEN NOT MATCHED THEN
        INSERT ([manufacturer_id], [model_name], [created_at])
        VALUES (@manufacturer_id, @model_name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_operating_system]
    @name         NVARCHAR(100),
    @version      NVARCHAR(100) = NULL,
    @build_number NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[operating_systems] WITH (HOLDLOCK) AS tgt
    USING (SELECT @name AS [name]) AS src
        ON tgt.[name] = src.[name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[name] = tgt.[name]
    WHEN NOT MATCHED THEN
        INSERT ([name], [created_at])
        VALUES (@name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_department]
    @name NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[departments] WITH (HOLDLOCK) AS tgt
    USING (SELECT @name AS [name]) AS src
        ON tgt.[name] = src.[name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[name] = tgt.[name]
    WHEN NOT MATCHED THEN
        INSERT ([name], [created_at])
        VALUES (@name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_find_or_create_location]
    @name NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[locations] WITH (HOLDLOCK) AS tgt
    USING (SELECT @name AS [name]) AS src
        ON tgt.[name] = src.[name]
    WHEN MATCHED THEN
        UPDATE SET tgt.[name] = tgt.[name]
    WHEN NOT MATCHED THEN
        INSERT ([name], [created_at])
        VALUES (@name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.8  OTHER CONTROLLERS & SERVICES
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

-- sp_add_computer_history (037: includes old_value/new_value)
CREATE OR ALTER PROCEDURE [dbo].[sp_add_computer_history]
    @computer_id INT,
    @action      NVARCHAR(100),
    @changed_by  NVARCHAR(100) = NULL,
    @old_value   NVARCHAR(500) = NULL,
    @new_value   NVARCHAR(500) = NULL,
    @notes       NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[computer_histories]
        ([computer_id], [action], [old_value], [new_value], [notes], [changed_by], [changed_at], [created_at])
    VALUES
        (@computer_id, @action, @old_value, @new_value, @notes, @changed_by, SYSUTCDATETIME(), SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS [id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_set_location_source_preference]
    @computer_id INT,
    @preference  NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [dbo].[computer_agent_statuses] WITH (HOLDLOCK) AS tgt
    USING (SELECT @computer_id AS [computer_id]) AS src
        ON tgt.[computer_id] = src.[computer_id]
    WHEN MATCHED THEN
        UPDATE SET [preferred_location_source] = @preference
    WHEN NOT MATCHED THEN
        INSERT ([computer_id], [preferred_location_source])
        VALUES (@computer_id, @preference);

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_or_create_country_id]
    @country_code NVARCHAR(10),
    @country_name NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @result TABLE ([id] INT);

    MERGE [dbo].[countries] WITH (HOLDLOCK) AS tgt
    USING (SELECT @country_code AS [country_code]) AS src
        ON tgt.[country_code] = src.[country_code]
    WHEN MATCHED THEN
        UPDATE SET tgt.[country_code] = tgt.[country_code]
    WHEN NOT MATCHED THEN
        INSERT ([country_code], [name], [created_at])
        VALUES (@country_code, @country_name, SYSUTCDATETIME())
    OUTPUT INSERTED.[id] INTO @result;

    SELECT [id] FROM @result;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.9  MAINTENANCE RECORDS
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

-- sp_get_all_maintenance_records (038: sort/page with row_version)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_maintenance_records]
    @sort_by        NVARCHAR(50)    = NULL,
    @sort_direction NVARCHAR(4)     = 'DESC',
    @page           INT             = 1,
    @page_size      INT             = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[maintenance_records] mr
    WHERE mr.[is_deleted] = 0;

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
    INNER JOIN [dbo].[computers] c               ON c.[id]  = mr.[computer_id]
    INNER JOIN [dbo].[maintenance_types] mt       ON mt.[id] = mr.[type_id]
    INNER JOIN [dbo].[maintenance_statuses] ms    ON ms.[id] = mr.[status_id]
    WHERE mr.[is_deleted] = 0
    ORDER BY
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
        mr.[scheduled_date] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- sp_get_maintenance_by_computer_id (039: paginated)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_by_computer_id]
    @computer_id    INT,
    @page           INT = 1,
    @page_size      INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[maintenance_records] mr
    WHERE mr.[computer_id] = @computer_id
      AND mr.[is_deleted] = 0;

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
    INNER JOIN [dbo].[computers] c  ON c.[id]  = mr.[computer_id]
    INNER JOIN [dbo].[maintenance_types] mt ON mt.[id] = mr.[type_id]
    INNER JOIN [dbo].[maintenance_statuses] ms ON ms.[id] = mr.[status_id]
    WHERE mr.[computer_id] = @computer_id
      AND mr.[is_deleted] = 0
    ORDER BY mr.[scheduled_date] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- sp_get_maintenance_by_status (040: paginated)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_by_status]
    @status_id      INT,
    @page           INT = 1,
    @page_size      INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[maintenance_records] mr
    WHERE mr.[status_id] = @status_id
      AND mr.[is_deleted] = 0;

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

-- sp_get_scheduled_maintenance (041: paginated)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_scheduled_maintenance]
    @start_date     DATETIME2,
    @end_date       DATETIME2,
    @page           INT = 1,
    @page_size      INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[maintenance_records] mr
    WHERE mr.[scheduled_date] >= @start_date
      AND mr.[scheduled_date] <= @end_date
      AND mr.[is_deleted] = 0;

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
    WHERE mr.[scheduled_date] >= @start_date
      AND mr.[scheduled_date] <= @end_date
      AND mr.[is_deleted] = 0
      AND c.[is_deleted] = 0
    ORDER BY mr.[scheduled_date] ASC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.10  USERS
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

-- sp_get_all_users (042: search/sort/page with is_email_verified)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_users]
    @search_term    NVARCHAR(100)   = NULL,
    @sort_by        NVARCHAR(50)    = NULL,
    @sort_direction NVARCHAR(4)     = 'ASC',
    @page           INT             = 1,
    @page_size      INT             = 25
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @term NVARCHAR(102) = NULL;
    IF @search_term IS NOT NULL AND LEN(LTRIM(RTRIM(@search_term))) > 0
        SET @term = N'%' + LTRIM(RTRIM(@search_term)) + N'%';

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[users] u
    LEFT JOIN [dbo].[departments] d ON d.[id] = u.[department_id]
    WHERE u.[is_deleted] = 0
      AND (@term IS NULL
           OR u.[username]   LIKE @term
           OR u.[email]      LIKE @term
           OR u.[first_name] LIKE @term
           OR u.[last_name]  LIKE @term
           OR d.[name]       LIKE @term);

    SELECT
        u.[id],
        u.[username],
        u.[email],
        u.[first_name],
        u.[last_name],
        u.[department_id],
        u.[phone],
        u.[role_id],
        u.[is_active],
        u.[is_email_verified],
        u.[created_at],
        u.[updated_at],
        u.[last_login_at],
        d.[name]  AS [_department_name],
        r.[name]  AS [_role_name]
    FROM [dbo].[users] u
    LEFT  JOIN [dbo].[departments] d  ON d.[id] = u.[department_id]
    INNER JOIN [dbo].[user_roles]  r  ON r.[id] = u.[role_id]
    WHERE u.[is_deleted] = 0
      AND (@term IS NULL
           OR u.[username]   LIKE @term
           OR u.[email]      LIKE @term
           OR u.[first_name] LIKE @term
           OR u.[last_name]  LIKE @term
           OR d.[name]       LIKE @term)
    ORDER BY
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'username'   THEN u.[username]
                WHEN 'email'      THEN u.[email]
                WHEN 'firstname'  THEN u.[first_name]
                WHEN 'lastname'   THEN u.[last_name]
                WHEN 'fullname'   THEN u.[last_name]
                WHEN 'role'       THEN r.[name]
                WHEN 'department' THEN d.[name]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'username'   THEN u.[username]
                WHEN 'email'      THEN u.[email]
                WHEN 'firstname'  THEN u.[first_name]
                WHEN 'lastname'   THEN u.[last_name]
                WHEN 'fullname'   THEN u.[last_name]
                WHEN 'role'       THEN r.[name]
                WHEN 'department' THEN d.[name]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'createdat'   THEN u.[created_at]
                WHEN 'lastloginat' THEN u.[last_login_at]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'createdat'   THEN u.[created_at]
                WHEN 'lastloginat' THEN u.[last_login_at]
            END
        END DESC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'isactive' THEN u.[is_active]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'isactive' THEN u.[is_active]
            END
        END DESC,
        u.[last_name] ASC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- sp_get_user_by_username (043: includes is_email_verified)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_user_by_username]
    @username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.[id],
        u.[username],
        u.[email],
        u.[password_hash],
        u.[first_name],
        u.[last_name],
        u.[department_id],
        u.[phone],
        u.[role_id],
        u.[is_active],
        u.[is_email_verified],
        u.[created_at],
        u.[updated_at],
        u.[last_login_at],
        d.[name]  AS [_department_name],
        r.[name]  AS [_role_name]
    FROM [dbo].[users] u
    LEFT  JOIN [dbo].[departments] d ON d.[id] = u.[department_id]
    INNER JOIN [dbo].[user_roles]  r ON r.[id] = u.[role_id]
    WHERE u.[username] = @username
      AND u.[is_deleted] = 0;
END
GO

-- sp_get_user_by_email (044)
CREATE OR ALTER PROCEDURE [dbo].[sp_get_user_by_email]
    @email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.[id],
        u.[username],
        u.[email],
        u.[password_hash],
        u.[first_name],
        u.[last_name],
        u.[department_id],
        u.[phone],
        u.[role_id],
        u.[is_active],
        u.[is_email_verified],
        u.[created_at],
        u.[updated_at],
        u.[last_login_at],
        d.[name]  AS [_department_name],
        r.[name]  AS [_role_name]
    FROM [dbo].[users] u
    LEFT  JOIN [dbo].[departments] d ON d.[id] = u.[department_id]
    INNER JOIN [dbo].[user_roles]  r ON r.[id] = u.[role_id]
    WHERE u.[email] = @email
      AND u.[is_deleted] = 0;
END
GO

-- sp_check_user_exists (045: OUTPUT params, nullable inputs)
CREATE OR ALTER PROCEDURE [dbo].[sp_check_user_exists]
    @username NVARCHAR(50)  = NULL,
    @email    NVARCHAR(256) = NULL,
    @username_exists BIT OUTPUT,
    @email_exists    BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @username_exists = CASE
        WHEN @username IS NOT NULL
             AND EXISTS (SELECT 1 FROM [dbo].[users] WHERE [username] = @username)
        THEN 1 ELSE 0 END;

    SET @email_exists = CASE
        WHEN @email IS NOT NULL
             AND EXISTS (SELECT 1 FROM [dbo].[users] WHERE [email] = @email)
        THEN 1 ELSE 0 END;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.11  VERSION HISTORY & HEALTH CHECK
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_version_history]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 0
      AND [is_deleted] = 0;

    SELECT [id], [version], [type_id], [change_log], [released_at],
           [released_by], [is_current_version], [checksum], [file_size]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 0
      AND [is_deleted] = 0
    ORDER BY [released_at] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 1
      AND [is_deleted] = 0;

    SELECT [id], [version], [type_id], [change_log], [released_at],
           [released_by], [is_current_version], [checksum], [file_size]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 1
      AND [is_deleted] = 0
    ORDER BY [released_at] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_server_history]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 0
      AND [is_deleted] = 0;

    SELECT [id], [version], [type_id], [change_log], [released_at],
           [released_by], [is_current_version], [checksum], [file_size]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 0
      AND [is_deleted] = 0
    ORDER BY [released_at] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_thin_client_history]
    @page      INT = 1,
    @page_size INT = 25
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 1
      AND [is_deleted] = 0;

    SELECT [id], [version], [type_id], [change_log], [released_at],
           [released_by], [is_current_version], [checksum], [file_size]
    FROM [dbo].[version_histories]
    WHERE [type_id] = 1
      AND [is_deleted] = 0
    ORDER BY [released_at] DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_health_check]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @start DATETIME2 = SYSUTCDATETIME();

    SELECT COUNT(*) AS [computer_count]
    FROM [dbo].[computers]
    WHERE [is_deleted] = 0;

    SELECT DATEDIFF(MILLISECOND, @start, SYSUTCDATETIME()) AS [query_time_ms];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.1 LOOKUP SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at]
    FROM [dbo].[maintenance_types]
    ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_statuses]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at]
    FROM [dbo].[maintenance_statuses]
    ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_user_roles]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at]
    FROM [dbo].[user_roles]
    ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_version_types]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [name], [created_at], [updated_at]
    FROM [dbo].[version_types]
    ORDER BY [name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_department_name_exists]
    @name       NVARCHAR(100),
    @exclude_id INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[departments]
        WHERE [name] = @name
          AND (@exclude_id IS NULL OR [id] <> @exclude_id)
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_department_usage_count]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @computer_count INT, @user_count INT;
    SELECT @computer_count = COUNT(*) FROM [dbo].[computers] WHERE [department_id] = @id AND [is_deleted] = 0;
    SELECT @user_count = COUNT(*) FROM [dbo].[users] WHERE [department_id] = @id AND [is_deleted] = 0;
    SELECT (@computer_count + @user_count) AS [usage_count];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_location_name_exists]
    @name       NVARCHAR(200),
    @exclude_id INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[locations]
        WHERE [name] = @name
          AND (@exclude_id IS NULL OR [id] <> @exclude_id)
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_location_usage_count]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) AS [usage_count]
    FROM [dbo].[computers]
    WHERE [location_id] = @id AND [is_deleted] = 0;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.2 COMPUTER SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_dashboard_counts]
    @latest_agent_version NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @stale_cutoff DATETIME2 = DATEADD(HOUR, -24, GETUTCDATE());
    DECLARE @now DATETIME2 = GETUTCDATE();

    SELECT
        COUNT(*)                                                                    AS [total_computers],
        SUM(CASE WHEN cas.[is_online] = 1 THEN 1 ELSE 0 END)                       AS [online_computers],
        SUM(CASE WHEN cas.[is_online] = 0 THEN 1 ELSE 0 END)                       AS [offline_computers],
        SUM(CASE WHEN cas.[has_agent] = 1 THEN 1 ELSE 0 END)                       AS [agents_installed],
        SUM(CASE WHEN ur.[id] IS NOT NULL
                  AND ur.[update_status_id] NOT IN (0, 50, 99) THEN 1 ELSE 0 END)  AS [agents_updating],
        SUM(CASE WHEN ur.[id] IS NOT NULL
                  AND ur.[update_status_id] = 99 THEN 1 ELSE 0 END)                AS [agents_failed_update],
        SUM(CASE WHEN cas.[force_update] = 1 THEN 1 ELSE 0 END)                    AS [agents_pending_update],
        CASE WHEN @latest_agent_version IS NOT NULL
             THEN SUM(CASE WHEN cas.[has_agent] = 1
                            AND cas.[agent_version] = @latest_agent_version
                       THEN 1 ELSE 0 END)
             ELSE 0 END                                                             AS [agents_on_latest_version],
        CASE WHEN @latest_agent_version IS NOT NULL
             THEN SUM(CASE WHEN cas.[has_agent] = 1
                            AND cas.[agent_version] IS NOT NULL
                            AND cas.[agent_version] <> ''
                            AND cas.[agent_version] <> @latest_agent_version
                       THEN 1 ELSE 0 END)
             ELSE 0 END                                                             AS [agents_on_older_version],
        SUM(CASE WHEN cas.[has_agent] = 1
                  AND cas.[last_seen] IS NOT NULL
                  AND cas.[last_seen] < @stale_cutoff THEN 1 ELSE 0 END)            AS [stale_agents],
        SUM(CASE WHEN c.[warranty_expiration] IS NOT NULL
                  AND c.[warranty_expiration] < @now THEN 1 ELSE 0 END)             AS [expired_warranties]
    FROM [dbo].[computers] c
    LEFT JOIN [dbo].[computer_agent_statuses] cas ON c.[id] = cas.[computer_id]
    LEFT JOIN [dbo].[computer_update_requests] ur ON cas.[active_update_request_id] = ur.[id]
    WHERE c.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_computer_by_serial_number_basic]
    @serial_number  NVARCHAR(100),
    @include_deleted BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.*
    FROM [dbo].[computers] c
    WHERE c.[serial_number] = @serial_number
      AND (@include_deleted = 1 OR c.[is_deleted] = 0);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_computer_exists]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[computers] WHERE [id] = @id AND [is_deleted] = 0
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_bulk_update_computer_status]
    @id_list    NVARCHAR(MAX),
    @status_id  INT,
    @updated_by NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE c
    SET c.[status_id] = @status_id,
        c.[updated_by] = @updated_by,
        c.[updated_at] = GETUTCDATE()
    FROM [dbo].[computers] c
    JOIN STRING_SPLIT(@id_list, ',') s ON c.[id] = CAST(LTRIM(RTRIM(s.[value])) AS INT)
    WHERE c.[is_deleted] = 0;

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.3 MAINTENANCE SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_summary_counts]
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @start_of_month DATETIME2 = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);

    SELECT
        SUM(CASE WHEN [status_id] IN (0, 1) THEN 1 ELSE 0 END) AS [open_count],
        SUM(CASE WHEN [status_id] = 0 THEN 1 ELSE 0 END)       AS [scheduled_count],
        SUM(CASE WHEN [status_id] = 2 AND [completed_date] >= @start_of_month THEN 1 ELSE 0 END) AS [completed_this_month]
    FROM [dbo].[maintenance_records]
    WHERE [is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_maintenance_by_id]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.[id], m.[computer_id], m.[type_id], m.[description],
        m.[scheduled_date], m.[completed_date], m.[cost],
        m.[performed_by], m.[notes], m.[status_id],
        m.[created_at], m.[created_by], m.[row_version],
        c.[hostname]        AS [_computer_hostname],
        c.[serial_number]   AS [_computer_serial_number],
        mt.[name]           AS [_type_name],
        ms.[name]           AS [_status_name]
    FROM [dbo].[maintenance_records] m
    JOIN [dbo].[computers] c ON m.[computer_id] = c.[id]
    JOIN [dbo].[maintenance_types] mt ON m.[type_id] = mt.[id]
    JOIN [dbo].[maintenance_statuses] ms ON m.[status_id] = ms.[id]
    WHERE m.[id] = @id AND m.[is_deleted] = 0;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.4 USER SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_user_by_id]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.[id], u.[username], u.[email], u.[password_hash],
        u.[first_name], u.[last_name], u.[department_id], u.[phone],
        u.[role_id], u.[is_active], u.[is_email_verified],
        u.[created_at], u.[updated_at], u.[last_login_at],
        d.[name] AS [_department_name],
        r.[name] AS [_role_name]
    FROM [dbo].[users] u
    LEFT JOIN [dbo].[departments] d ON u.[department_id] = d.[id]
    JOIN [dbo].[user_roles] r ON u.[role_id] = r.[id]
    WHERE u.[id] = @id AND u.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_users_by_role]
    @role_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.[id], u.[username], u.[email],
        u.[first_name], u.[last_name], u.[department_id], u.[phone],
        u.[role_id], u.[is_active], u.[is_email_verified],
        u.[created_at], u.[updated_at], u.[last_login_at],
        d.[name] AS [_department_name],
        r.[name] AS [_role_name]
    FROM [dbo].[users] u
    LEFT JOIN [dbo].[departments] d ON u.[department_id] = d.[id]
    JOIN [dbo].[user_roles] r ON u.[role_id] = r.[id]
    WHERE u.[role_id] = @role_id AND u.[is_deleted] = 0
    ORDER BY u.[last_name], u.[first_name];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.5 GEOLOCATION SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_geo_location_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        g.[id], g.[computer_id], g.[public_ip_address],
        g.[gps_latitude], g.[gps_longitude], g.[gps_last_updated],
        g.[ip_latitude], g.[ip_longitude], g.[ip_last_updated],
        g.[latitude], g.[longitude], g.[city], g.[region],
        g.[country_id], g.[isp], g.[timezone],
        g.[last_updated], g.[lookup_successful], g.[lookup_error],
        g.[location_source], g.[gps_accuracy_meters],
        co.[id] AS [_country_id], co.[country_code] AS [_country_code], co.[name] AS [_country_name]
    FROM [dbo].[computer_geo_locations] g
    LEFT JOIN [dbo].[countries] co ON g.[country_id] = co.[id]
    WHERE g.[computer_id] = @computer_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_geo_location_simple]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM [dbo].[computer_geo_locations] WHERE [computer_id] = @computer_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_geo_locations_with_computers]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        g.[id], g.[computer_id], g.[public_ip_address],
        g.[gps_latitude], g.[gps_longitude], g.[gps_last_updated],
        g.[ip_latitude], g.[ip_longitude], g.[ip_last_updated],
        g.[latitude], g.[longitude], g.[city], g.[region],
        g.[country_id], g.[isp], g.[timezone],
        g.[last_updated], g.[lookup_successful], g.[lookup_error],
        g.[location_source], g.[gps_accuracy_meters],
        c.[hostname] AS [_computer_hostname],
        c.[serial_number] AS [_computer_serial_number],
        c.[ip_address] AS [_computer_ip_address],
        co.[id] AS [_country_id], co.[country_code] AS [_country_code], co.[name] AS [_country_name]
    FROM [dbo].[computer_geo_locations] g
    JOIN [dbo].[computers] c ON g.[computer_id] = c.[id]
    LEFT JOIN [dbo].[countries] co ON g.[country_id] = co.[id]
    WHERE c.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_geo_locations_with_valid_coordinates]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        g.[id], g.[computer_id], g.[public_ip_address],
        g.[gps_latitude], g.[gps_longitude], g.[gps_last_updated],
        g.[ip_latitude], g.[ip_longitude], g.[ip_last_updated],
        g.[latitude], g.[longitude], g.[city], g.[region],
        g.[country_id], g.[isp], g.[timezone],
        g.[last_updated], g.[lookup_successful], g.[lookup_error],
        g.[location_source], g.[gps_accuracy_meters],
        c.[hostname] AS [_computer_hostname],
        c.[serial_number] AS [_computer_serial_number],
        c.[ip_address] AS [_computer_ip_address],
        co.[id] AS [_country_id], co.[country_code] AS [_country_code], co.[name] AS [_country_name]
    FROM [dbo].[computer_geo_locations] g
    JOIN [dbo].[computers] c ON g.[computer_id] = c.[id]
    LEFT JOIN [dbo].[countries] co ON g.[country_id] = co.[id]
    WHERE g.[latitude] IS NOT NULL AND g.[longitude] IS NOT NULL
      AND c.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_update_gps_location]
    @computer_id       INT,
    @latitude          FLOAT,
    @longitude         FLOAT,
    @public_ip_address NVARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @now DATETIME2 = GETUTCDATE();

    MERGE [dbo].[computer_geo_locations] WITH (HOLDLOCK) AS tgt
    USING (SELECT @computer_id AS [computer_id]) AS src
    ON tgt.[computer_id] = src.[computer_id]
    WHEN MATCHED THEN
        UPDATE SET
            [gps_latitude]  = @latitude,
            [gps_longitude] = @longitude,
            [gps_last_updated] = @now,
            [latitude]      = @latitude,
            [longitude]     = @longitude,
            [location_source] = N'GPS',
            [last_updated]  = @now,
            [lookup_successful] = 1,
            [lookup_error]  = NULL,
            [public_ip_address] = COALESCE(@public_ip_address, tgt.[public_ip_address])
    WHEN NOT MATCHED THEN
        INSERT ([computer_id], [public_ip_address],
                [gps_latitude], [gps_longitude], [gps_last_updated],
                [latitude], [longitude], [location_source],
                [last_updated], [lookup_successful])
        VALUES (@computer_id, @public_ip_address,
                @latitude, @longitude, @now,
                @latitude, @longitude, N'GPS',
                @now, 1);

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_update_ip_location]
    @computer_id       INT,
    @latitude          FLOAT,
    @longitude         FLOAT,
    @city              NVARCHAR(100) = NULL,
    @region            NVARCHAR(100) = NULL,
    @country           NVARCHAR(100) = NULL,
    @country_code      NVARCHAR(10) = NULL,
    @isp               NVARCHAR(200) = NULL,
    @timezone          NVARCHAR(50) = NULL,
    @public_ip_address NVARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @now DATETIME2 = GETUTCDATE();
    DECLARE @country_id INT = NULL;

    -- Resolve country to FK if provided
    IF @country IS NOT NULL AND @country_code IS NOT NULL
    BEGIN
        MERGE [dbo].[countries] WITH (HOLDLOCK) AS tgt
        USING (SELECT @country_code AS [country_code]) AS src
        ON tgt.[country_code] = src.[country_code]
        WHEN NOT MATCHED THEN
            INSERT ([name], [country_code]) VALUES (@country, @country_code);
        SET @country_id = (SELECT [id] FROM [dbo].[countries] WHERE [country_code] = @country_code);
    END

    MERGE [dbo].[computer_geo_locations] WITH (HOLDLOCK) AS tgt
    USING (SELECT @computer_id AS [computer_id]) AS src
    ON tgt.[computer_id] = src.[computer_id]
    WHEN MATCHED THEN
        UPDATE SET
            [ip_latitude]       = @latitude,
            [ip_longitude]      = @longitude,
            [ip_last_updated]   = @now,
            [public_ip_address] = @public_ip_address,
            [city]              = @city,
            [region]            = @region,
            [country_id]        = COALESCE(@country_id, tgt.[country_id]),
            [isp]               = @isp,
            [timezone]          = @timezone,
            [lookup_successful] = 1,
            [lookup_error]      = NULL,
            [last_updated]      = @now,
            -- Only update display columns if GPS is not available
            [latitude]          = CASE WHEN tgt.[gps_latitude] IS NOT NULL AND tgt.[gps_longitude] IS NOT NULL
                                       THEN tgt.[latitude] ELSE @latitude END,
            [longitude]         = CASE WHEN tgt.[gps_latitude] IS NOT NULL AND tgt.[gps_longitude] IS NOT NULL
                                       THEN tgt.[longitude] ELSE @longitude END,
            [location_source]   = CASE WHEN tgt.[gps_latitude] IS NOT NULL AND tgt.[gps_longitude] IS NOT NULL
                                       THEN tgt.[location_source] ELSE N'IP' END
    WHEN NOT MATCHED THEN
        INSERT ([computer_id], [public_ip_address],
                [ip_latitude], [ip_longitude], [ip_last_updated],
                [latitude], [longitude], [location_source],
                [city], [region], [country_id], [isp], [timezone],
                [last_updated], [lookup_successful])
        VALUES (@computer_id, @public_ip_address,
                @latitude, @longitude, @now,
                @latitude, @longitude, N'IP',
                @city, @region, @country_id, @isp, @timezone,
                @now, 1);

    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_delete_geo_location]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[computer_geo_locations] WHERE [computer_id] = @computer_id;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.6 COMPUTER HISTORY SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_history_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [computer_id], [action], [old_value], [new_value],
           [changed_by], [changed_at], [notes]
    FROM [dbo].[computer_histories]
    WHERE [computer_id] = @computer_id AND [is_deleted] = 0
    ORDER BY [changed_at] DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_history_paged]
    @page           INT = 1,
    @page_size      INT = 25,
    @action         NVARCHAR(100) = NULL,
    @search         NVARCHAR(200) = NULL,
    @from_date      DATETIME2 = NULL,
    @to_date        DATETIME2 = NULL,
    @sort_by        NVARCHAR(50) = NULL,
    @sort_direction NVARCHAR(4) = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    -- Count
    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[computer_histories] h
    JOIN [dbo].[computers] c ON h.[computer_id] = c.[id]
    WHERE h.[is_deleted] = 0
      AND (@action IS NULL OR h.[action] = @action)
      AND (@from_date IS NULL OR h.[changed_at] >= @from_date)
      AND (@to_date IS NULL OR h.[changed_at] <= @to_date)
      AND (@search IS NULL OR (
           c.[hostname] LIKE '%' + @search + '%'
        OR c.[serial_number] LIKE '%' + @search + '%'
        OR h.[notes] LIKE '%' + @search + '%'
        OR h.[changed_by] LIKE '%' + @search + '%'));

    -- Data
    SELECT
        h.[id], h.[computer_id], h.[action], h.[old_value], h.[new_value],
        h.[changed_by], h.[changed_at], h.[notes],
        c.[hostname] AS [_computer_hostname],
        c.[serial_number] AS [_computer_serial_number]
    FROM [dbo].[computer_histories] h
    JOIN [dbo].[computers] c ON h.[computer_id] = c.[id]
    WHERE h.[is_deleted] = 0
      AND (@action IS NULL OR h.[action] = @action)
      AND (@from_date IS NULL OR h.[changed_at] >= @from_date)
      AND (@to_date IS NULL OR h.[changed_at] <= @to_date)
      AND (@search IS NULL OR (
           c.[hostname] LIKE '%' + @search + '%'
        OR c.[serial_number] LIKE '%' + @search + '%'
        OR h.[notes] LIKE '%' + @search + '%'
        OR h.[changed_by] LIKE '%' + @search + '%'))
    ORDER BY
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE LOWER(@sort_by)
                WHEN 'action'    THEN h.[action]
                WHEN 'changedby' THEN h.[changed_by]
                WHEN 'computer'  THEN c.[hostname]
            END
        END ASC,
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE LOWER(@sort_by)
                WHEN 'changedat' THEN h.[changed_at]
            END
        END ASC,
        CASE WHEN @sort_direction <> 'ASC' THEN
            CASE LOWER(@sort_by)
                WHEN 'action'    THEN h.[action]
                WHEN 'changedby' THEN h.[changed_by]
                WHEN 'computer'  THEN c.[hostname]
            END
        END DESC,
        CASE WHEN @sort_direction <> 'ASC' THEN
            CASE LOWER(@sort_by)
                WHEN 'changedat' THEN h.[changed_at]
                ELSE h.[changed_at]
            END
        END DESC
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_distinct_history_actions]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT [action]
    FROM [dbo].[computer_histories]
    WHERE [is_deleted] = 0
    ORDER BY [action];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_soft_delete_old_history]
    @cutoff DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[computer_histories]
    SET [is_deleted] = 1, [deleted_at] = GETUTCDATE()
    WHERE [changed_at] < @cutoff AND [is_deleted] = 0;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.7 COMPUTER OWNERSHIP SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_ownership]
    @computer_id INT,
    @user_id     INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [computer_id], [user_id], [access_level], [granted_at], [granted_by]
    FROM [dbo].[computer_ownerships]
    WHERE [computer_id] = @computer_id AND [user_id] = @user_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_ownerships_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        o.[id], o.[computer_id], o.[user_id], o.[access_level], o.[granted_at], o.[granted_by],
        u.[username] AS [_user_username], u.[first_name] AS [_user_first_name],
        u.[last_name] AS [_user_last_name], u.[email] AS [_user_email]
    FROM [dbo].[computer_ownerships] o
    JOIN [dbo].[users] u ON o.[user_id] = u.[id]
    WHERE o.[computer_id] = @computer_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_ownerships_by_user_id]
    @user_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        o.[id], o.[computer_id], o.[user_id], o.[access_level], o.[granted_at], o.[granted_by],
        c.[hostname] AS [_computer_hostname], c.[serial_number] AS [_computer_serial_number]
    FROM [dbo].[computer_ownerships] o
    JOIN [dbo].[computers] c ON o.[computer_id] = c.[id]
    WHERE o.[user_id] = @user_id AND c.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_has_ownership_access]
    @computer_id    INT,
    @user_id        INT,
    @minimum_level  INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[computer_ownerships]
        WHERE [computer_id] = @computer_id
          AND [user_id] = @user_id
          AND [access_level] >= @minimum_level
    ) THEN 1 ELSE 0 END AS BIT) AS [has_access];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_remove_ownership]
    @computer_id INT,
    @user_id     INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[computer_ownerships]
    WHERE [computer_id] = @computer_id AND [user_id] = @user_id;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_remove_all_ownerships_for_computer]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[computer_ownerships] WHERE [computer_id] = @computer_id;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.8 RESTORE POINT SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_restore_point_by_guid]
    @restore_point_id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [computer_id], [restore_point_id], [created_at],
           [type_id], [description], [data_size_bytes], [data_hash],
           [is_complete], [error_message], [is_deleted], [deleted_at]
    FROM [dbo].[restore_points]
    WHERE [restore_point_id] = @restore_point_id AND [is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_restore_point_by_guid_with_details]
    @restore_point_id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- RS1: Restore point + computer + type
    SELECT
        rp.[id], rp.[computer_id], rp.[restore_point_id], rp.[created_at],
        rp.[type_id], rp.[description], rp.[data_size_bytes], rp.[data_hash],
        rp.[is_complete], rp.[error_message], rp.[is_deleted], rp.[deleted_at],
        c.[hostname] AS [_computer_hostname], c.[serial_number] AS [_computer_serial_number],
        rt.[name] AS [_type_name]
    FROM [dbo].[restore_points] rp
    JOIN [dbo].[computers] c ON rp.[computer_id] = c.[id]
    JOIN [dbo].[restore_point_types] rt ON rp.[type_id] = rt.[id]
    WHERE rp.[restore_point_id] = @restore_point_id AND rp.[is_deleted] = 0;

    -- RS2: System info
    SELECT si.[id], si.[restore_point_id], si.[system_boot_time], si.[uptime_seconds],
           si.[hostname], si.[serial_number], si.[ip_address], si.[mac_address],
           si.[manufacturer], si.[model], si.[current_user], si.[created_at], si.[updated_at]
    FROM [dbo].[restore_point_system_infos] si
    JOIN [dbo].[restore_points] rp ON si.[restore_point_id] = rp.[id]
    WHERE rp.[restore_point_id] = @restore_point_id AND rp.[is_deleted] = 0;

    -- RS3: Snapshot (GZip-compressed VARBINARY columns)
    SELECT sn.[id], sn.[restore_point_id],
           sn.[system_details_json], sn.[installed_software_json],
           sn.[running_processes_json], sn.[windows_services_json],
           sn.[startup_programs_json], sn.[scheduled_tasks_json],
           sn.[environment_variables_json], sn.[network_config_json],
           sn.[storage_config_json], sn.[event_log_summary_json],
           sn.[windows_updates_json], sn.[device_drivers_json],
           sn.[firewall_rules_json], sn.[local_users_json],
           sn.[local_groups_json], sn.[shared_folders_json],
           sn.[printers_json], sn.[usb_devices_json],
           sn.[battery_status_json], sn.[bit_locker_status_json],
           sn.[security_status_json],
           sn.[created_at], sn.[updated_at]
    FROM [dbo].[restore_point_snapshots] sn
    JOIN [dbo].[restore_points] rp ON sn.[restore_point_id] = rp.[id]
    WHERE rp.[restore_point_id] = @restore_point_id AND rp.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_restore_points_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;

    -- RS1: Restore points
    SELECT rp.[id], rp.[computer_id], rp.[restore_point_id], rp.[created_at],
           rp.[type_id], rp.[description], rp.[data_size_bytes], rp.[data_hash],
           rp.[is_complete], rp.[error_message], rp.[is_deleted], rp.[deleted_at]
    FROM [dbo].[restore_points] rp
    WHERE rp.[computer_id] = @computer_id AND rp.[is_deleted] = 0
    ORDER BY rp.[created_at] DESC;

    -- RS2: System infos for those restore points
    SELECT si.[id], si.[restore_point_id], si.[system_boot_time], si.[uptime_seconds],
           si.[hostname], si.[serial_number], si.[ip_address], si.[mac_address],
           si.[manufacturer], si.[model], si.[current_user], si.[created_at], si.[updated_at]
    FROM [dbo].[restore_point_system_infos] si
    JOIN [dbo].[restore_points] rp ON si.[restore_point_id] = rp.[id]
    WHERE rp.[computer_id] = @computer_id AND rp.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_latest_complete_restore_point]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @rp_id INT;
    SELECT TOP 1 @rp_id = [id] FROM [dbo].[restore_points]
    WHERE [computer_id] = @computer_id AND [is_complete] = 1 AND [is_deleted] = 0
    ORDER BY [created_at] DESC;

    IF @rp_id IS NULL RETURN;

    -- RS1: Restore point
    SELECT [id], [computer_id], [restore_point_id], [created_at],
           [type_id], [description], [data_size_bytes], [data_hash],
           [is_complete], [error_message], [is_deleted], [deleted_at]
    FROM [dbo].[restore_points] WHERE [id] = @rp_id;

    -- RS2: System info
    SELECT [id], [restore_point_id], [system_boot_time], [uptime_seconds],
           [hostname], [serial_number], [ip_address], [mac_address],
           [manufacturer], [model], [current_user], [created_at], [updated_at]
    FROM [dbo].[restore_point_system_infos] WHERE [restore_point_id] = @rp_id;

    -- RS3: Snapshot
    SELECT [id], [restore_point_id],
           [system_details_json], [installed_software_json],
           [running_processes_json], [windows_services_json],
           [startup_programs_json], [scheduled_tasks_json],
           [environment_variables_json], [network_config_json],
           [storage_config_json], [event_log_summary_json],
           [windows_updates_json], [device_drivers_json],
           [firewall_rules_json], [local_users_json],
           [local_groups_json], [shared_folders_json],
           [printers_json], [usb_devices_json],
           [battery_status_json], [bit_locker_status_json],
           [security_status_json],
           [created_at], [updated_at]
    FROM [dbo].[restore_point_snapshots] WHERE [restore_point_id] = @rp_id;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.9 PACKAGE SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_packages]
AS
BEGIN
    SET NOCOUNT ON;
    -- RS1: Package versions
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [is_deleted] = 0
    ORDER BY [created_at] DESC;

    -- RS2: Distribution rules for all non-deleted packages
    SELECT dr.[id], dr.[package_version_id], dr.[rule_type], dr.[target_value],
           dr.[is_enabled], dr.[created_at]
    FROM [dbo].[package_distribution_rules] dr
    JOIN [dbo].[package_versions] pv ON dr.[package_version_id] = pv.[id]
    WHERE pv.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_package_by_id_with_rules]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    -- RS1: Package
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [id] = @id AND [is_deleted] = 0;

    -- RS2: Distribution rules
    SELECT [id], [package_version_id], [rule_type], [target_value], [is_enabled], [created_at]
    FROM [dbo].[package_distribution_rules]
    WHERE [package_version_id] = @id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_package_by_version]
    @version      NVARCHAR(50),
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    -- RS1: Package
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [version] = @version AND [package_type] = @package_type AND [is_deleted] = 0;

    -- RS2: Distribution rules
    SELECT dr.[id], dr.[package_version_id], dr.[rule_type], dr.[target_value],
           dr.[is_enabled], dr.[created_at]
    FROM [dbo].[package_distribution_rules] dr
    JOIN [dbo].[package_versions] pv ON dr.[package_version_id] = pv.[id]
    WHERE pv.[version] = @version AND pv.[package_type] = @package_type AND pv.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_active_package_version]
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    -- Status 2 = Active
    -- RS1: Package
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [status] = 2 AND [package_type] = @package_type AND [is_deleted] = 0;

    -- RS2: Distribution rules
    SELECT dr.[id], dr.[package_version_id], dr.[rule_type], dr.[target_value],
           dr.[is_enabled], dr.[created_at]
    FROM [dbo].[package_distribution_rules] dr
    JOIN [dbo].[package_versions] pv ON dr.[package_version_id] = pv.[id]
    WHERE pv.[status] = 2 AND pv.[package_type] = @package_type AND pv.[is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_packages_by_status]
    @status       INT,
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [status] = @status AND [package_type] = @package_type AND [is_deleted] = 0
    ORDER BY [created_at] DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_packages_by_channel]
    @channel      NVARCHAR(20),
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [package_type], [file_name], [storage_path], [checksum],
           [file_size], [release_notes], [is_required], [is_breaking_change],
           [minimum_agent_version], [status], [created_at], [created_by],
           [published_at], [deprecated_at], [is_deleted], [deleted_at],
           [download_count], [channel]
    FROM [dbo].[package_versions]
    WHERE [channel] = @channel AND [package_type] = @package_type AND [is_deleted] = 0
    ORDER BY [created_at] DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_package_version_exists]
    @version      NVARCHAR(50),
    @package_type NVARCHAR(50) = N'Agent'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[package_versions]
        WHERE [version] = @version AND [package_type] = @package_type AND [is_deleted] = 0
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_increment_download_count]
    @package_id INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[package_versions]
    SET [download_count] = [download_count] + 1
    WHERE [id] = @package_id AND [is_deleted] = 0;
    SELECT @@ROWCOUNT AS [affected_rows];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_distribution_rules]
    @package_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [package_version_id], [rule_type], [target_value], [is_enabled], [created_at]
    FROM [dbo].[package_distribution_rules]
    WHERE [package_version_id] = @package_id;
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.10 SYSTEM DETAILS SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_latest_system_details_by_computer_id]
    @computer_id INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sd_id INT;
    SELECT TOP 1 @sd_id = [id] FROM [dbo].[system_details]
    WHERE [computer_id] = @computer_id AND [is_deleted] = 0
    ORDER BY [collected_at] DESC;

    IF @sd_id IS NULL RETURN;

    -- RS1: system_details + computer
    SELECT sd.[id], sd.[computer_id], sd.[collected_at], sd.[machine_guid], sd.[product_id],
           c.[hostname] AS [_computer_hostname], c.[serial_number] AS [_computer_serial_number]
    FROM [dbo].[system_details] sd
    JOIN [dbo].[computers] c ON sd.[computer_id] = c.[id]
    WHERE sd.[id] = @sd_id;

    -- RS2: bios
    SELECT * FROM [dbo].[system_detail_bios] WHERE [system_detail_id] = @sd_id;
    -- RS3: motherboard
    SELECT * FROM [dbo].[system_detail_motherboards] WHERE [system_detail_id] = @sd_id;
    -- RS4: cpu
    SELECT * FROM [dbo].[system_detail_cpus] WHERE [system_detail_id] = @sd_id;
    -- RS5: memory
    SELECT * FROM [dbo].[system_detail_memories] WHERE [system_detail_id] = @sd_id;
    -- RS6: storage
    SELECT * FROM [dbo].[system_detail_storages] WHERE [system_detail_id] = @sd_id;
    -- RS7: gpu
    SELECT * FROM [dbo].[system_detail_gpus] WHERE [system_detail_id] = @sd_id;
    -- RS8: network
    SELECT * FROM [dbo].[system_detail_networks] WHERE [system_detail_id] = @sd_id;
    -- RS9: os
    SELECT * FROM [dbo].[system_detail_oses] WHERE [system_detail_id] = @sd_id;
    -- RS10: security
    SELECT * FROM [dbo].[system_detail_securities] WHERE [system_detail_id] = @sd_id;
    -- RS11: battery
    SELECT * FROM [dbo].[system_detail_batteries] WHERE [system_detail_id] = @sd_id;
    -- RS12: chassis
    SELECT * FROM [dbo].[system_detail_chasses] WHERE [system_detail_id] = @sd_id;
    -- RS13: software
    SELECT * FROM [dbo].[system_detail_softwares] WHERE [system_detail_id] = @sd_id;
    -- RS14: users_groups
    SELECT * FROM [dbo].[system_detail_users_groups] WHERE [system_detail_id] = @sd_id;
    -- RS15: peripherals
    SELECT * FROM [dbo].[system_detail_peripherals] WHERE [system_detail_id] = @sd_id;
    -- RS16: event_logs
    SELECT * FROM [dbo].[system_detail_event_logs] WHERE [system_detail_id] = @sd_id;
    -- RS17: performance
    SELECT * FROM [dbo].[system_detail_performances] WHERE [system_detail_id] = @sd_id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_system_details_history]
    @computer_id INT,
    @limit       INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    -- Get IDs of the N latest details for this computer
    DECLARE @ids TABLE ([id] INT);
    INSERT INTO @ids
    SELECT TOP (@limit) [id] FROM [dbo].[system_details]
    WHERE [computer_id] = @computer_id AND [is_deleted] = 0
    ORDER BY [collected_at] DESC;

    -- RS1: system_details
    SELECT sd.[id], sd.[computer_id], sd.[collected_at], sd.[machine_guid], sd.[product_id]
    FROM [dbo].[system_details] sd
    WHERE sd.[id] IN (SELECT [id] FROM @ids)
    ORDER BY sd.[collected_at] DESC;

    -- RS2: os
    SELECT o.* FROM [dbo].[system_detail_oses] o
    WHERE o.[system_detail_id] IN (SELECT [id] FROM @ids);

    -- RS3: performance
    SELECT p.* FROM [dbo].[system_detail_performances] p
    WHERE p.[system_detail_id] IN (SELECT [id] FROM @ids);

    -- RS4: software
    SELECT s.* FROM [dbo].[system_detail_softwares] s
    WHERE s.[system_detail_id] IN (SELECT [id] FROM @ids);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_latest_system_details_all]
    @page      INT = NULL,
    @page_size INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Find latest system_details ID per computer
    DECLARE @latest TABLE ([id] INT);
    INSERT INTO @latest
    SELECT MAX(sd.[id])
    FROM [dbo].[system_details] sd
    JOIN (
        SELECT [computer_id], MAX([collected_at]) AS [max_collected]
        FROM [dbo].[system_details]
        WHERE [is_deleted] = 0
        GROUP BY [computer_id]
    ) sub ON sd.[computer_id] = sub.[computer_id] AND sd.[collected_at] = sub.[max_collected]
    WHERE sd.[is_deleted] = 0
    GROUP BY sd.[computer_id];

    -- RS1: total count
    SELECT COUNT(*) AS [total_count] FROM @latest;

    -- Apply pagination if specified
    DECLARE @paged TABLE ([id] INT);
    IF @page IS NOT NULL AND @page_size IS NOT NULL
        INSERT INTO @paged SELECT [id] FROM @latest ORDER BY [id] OFFSET (@page - 1) * @page_size ROWS FETCH NEXT @page_size ROWS ONLY;
    ELSE
        INSERT INTO @paged SELECT [id] FROM @latest;

    -- RS2: system_details + computer
    SELECT sd.[id], sd.[computer_id], sd.[collected_at], sd.[machine_guid], sd.[product_id],
           c.[hostname] AS [_computer_hostname], c.[serial_number] AS [_computer_serial_number]
    FROM [dbo].[system_details] sd
    JOIN [dbo].[computers] c ON sd.[computer_id] = c.[id]
    WHERE sd.[id] IN (SELECT [id] FROM @paged);

    -- RS3-RS18: all sub-entities for those IDs
    SELECT * FROM [dbo].[system_detail_bios] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_motherboards] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_cpus] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_memories] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_storages] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_gpus] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_networks] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_oses] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_securities] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_batteries] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_chasses] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_softwares] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_users_groups] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_peripherals] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_event_logs] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
    SELECT * FROM [dbo].[system_detail_performances] WHERE [system_detail_id] IN (SELECT [id] FROM @paged);
END
GO

-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
-- 8.11 VERSION HISTORY SPs (Migration 046)
-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

CREATE OR ALTER PROCEDURE [dbo].[sp_get_version_history_by_type_unpaged]
    @type_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [type_id], [change_log], [released_at], [released_by],
           [is_current_version], [checksum], [file_size],
           [is_deleted], [deleted_at]
    FROM [dbo].[version_histories]
    WHERE [type_id] = @type_id AND [is_deleted] = 0
    ORDER BY [released_at] DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_version_exists]
    @type_id INT,
    @version NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM [dbo].[version_histories]
        WHERE [type_id] = @type_id AND [version] = @version AND [is_deleted] = 0
    ) THEN 1 ELSE 0 END AS BIT) AS [exists];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_find_version_by_type_and_version]
    @type_id INT,
    @version NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [type_id], [change_log], [released_at], [released_by],
           [is_current_version], [checksum], [file_size],
           [is_deleted], [deleted_at]
    FROM [dbo].[version_histories]
    WHERE [type_id] = @type_id AND [version] = @version AND [is_deleted] = 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_get_current_versions_by_type]
    @type_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [id], [version], [type_id], [change_log], [released_at], [released_by],
           [is_current_version], [checksum], [file_size],
           [is_deleted], [deleted_at]
    FROM [dbo].[version_histories]
    WHERE [type_id] = @type_id AND [is_current_version] = 1 AND [is_deleted] = 0;
END
GO

-- ============================================================================
-- END OF DDL SCRIPT
-- ============================================================================
