-- ============================================================================
-- Migration 009: Add Centralized Package Versioning & Distribution
-- 
-- Adds tables for tracking package versions in the database with lifecycle
-- management (Draft → Published → Active → Deprecated → Archived) and
-- distribution rules for targeted rollouts.
-- ============================================================================

-- Package versions table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'package_versions')
BEGIN
    CREATE TABLE package_versions (
        id                   INT IDENTITY(1,1)  PRIMARY KEY,
        version              NVARCHAR(50)       NOT NULL,
        package_type         NVARCHAR(20)       NOT NULL DEFAULT 'Agent',
        file_name            NVARCHAR(255)      NOT NULL,
        storage_path         NVARCHAR(500)      NOT NULL,
        checksum             NVARCHAR(128)      NOT NULL,
        file_size            BIGINT             NOT NULL,
        release_notes        NVARCHAR(MAX)      NULL,
        is_required          BIT                NOT NULL DEFAULT 0,
        is_breaking_change   BIT                NOT NULL DEFAULT 0,
        minimum_agent_version NVARCHAR(50)      NULL,
        status               INT                NOT NULL DEFAULT 0,  -- 0=Draft, 1=Published, 2=Active, 3=Deprecated, 4=Archived
        created_at           DATETIME2          NOT NULL DEFAULT GETUTCDATE(),
        created_by           NVARCHAR(100)      NULL,
        published_at         DATETIME2          NULL,
        deprecated_at        DATETIME2          NULL,
        download_count       INT                NOT NULL DEFAULT 0,
        channel              NVARCHAR(20)       NOT NULL DEFAULT 'stable',

        CONSTRAINT UQ_package_versions_version_type UNIQUE (version, package_type)
    );

    -- Index for looking up the active version quickly
    CREATE INDEX IX_package_versions_status_type 
        ON package_versions (status, package_type);
    
    -- Index for channel-based queries
    CREATE INDEX IX_package_versions_channel_type 
        ON package_versions (channel, package_type);
    
    -- Index for created date ordering
    CREATE INDEX IX_package_versions_created_at 
        ON package_versions (created_at DESC);

    PRINT 'Created table: package_versions';
END
ELSE
    PRINT 'Table package_versions already exists — skipping.';
GO

-- Package distribution rules table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'package_distribution_rules')
BEGIN
    CREATE TABLE package_distribution_rules (
        id                   INT IDENTITY(1,1)  PRIMARY KEY,
        package_version_id   INT                NOT NULL,
        rule_type            INT                NOT NULL DEFAULT 0,  -- 0=AllComputers, 1=BySerialNumber, 2=ByDepartment, 3=ByLocation
        target_value         NVARCHAR(200)      NOT NULL,
        is_enabled           BIT                NOT NULL DEFAULT 1,
        created_at           DATETIME2          NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_distribution_rules_package 
            FOREIGN KEY (package_version_id) 
            REFERENCES package_versions (id) 
            ON DELETE CASCADE
    );

    -- Index for looking up rules by package
    CREATE INDEX IX_distribution_rules_package_id 
        ON package_distribution_rules (package_version_id);

    PRINT 'Created table: package_distribution_rules';
END
ELSE
    PRINT 'Table package_distribution_rules already exists — skipping.';
GO

PRINT 'Migration 009 complete: Centralized Package Versioning & Distribution';
GO
