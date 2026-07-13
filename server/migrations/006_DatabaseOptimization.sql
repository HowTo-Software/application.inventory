-- ============================================================================
-- Database Optimization Migration
-- ============================================================================
-- This migration adds comprehensive indexes, constraints, and statistics
-- for optimal database performance
-- ============================================================================

PRINT 'Starting database optimization migration...';
PRINT '';

-- ============================================================================
-- SECTION 1: COMPUTERS TABLE INDEXES
-- ============================================================================
PRINT '=== COMPUTERS TABLE INDEXES ===';

-- Index for LastSeen queries (agent heartbeat, online/offline filtering)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_LastSeen' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_LastSeen ON Computers (LastSeen DESC)
    INCLUDE (IsOnline, Hostname, SerialNumber);
    PRINT 'Created IX_Computers_LastSeen index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_LastSeen index already exists';
END

-- Index for IsOnline filtering (dashboard, monitoring)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_IsOnline' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_IsOnline ON Computers (IsOnline)
    INCLUDE (Hostname, SerialNumber, LastSeen, CurrentUser);
    PRINT 'Created IX_Computers_IsOnline index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_IsOnline index already exists';
END

-- Index for Status filtering (common filter on dashboard)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_Status' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_Status ON Computers (Status)
    INCLUDE (Hostname, SerialNumber, Department, Location);
    PRINT 'Created IX_Computers_Status index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_Status index already exists';
END

-- Composite index for Department filtering with common includes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_Department' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_Department ON Computers (Department)
    INCLUDE (Hostname, Status, Location, IsOnline);
    PRINT 'Created IX_Computers_Department index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_Department index already exists';
END

-- Composite index for Location filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_Location' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_Location ON Computers (Location)
    INCLUDE (Hostname, Status, Department, IsOnline);
    PRINT 'Created IX_Computers_Location index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_Location index already exists';
END

-- Index for HasAgent filtering (agent management)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_HasAgent' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_HasAgent ON Computers (HasAgent)
    INCLUDE (SerialNumber, Hostname, AgentVersion, LastSeen);
    PRINT 'Created IX_Computers_HasAgent index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_HasAgent index already exists';
END

-- Index for update status monitoring
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_UpdateStatus' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_UpdateStatus ON Computers (UpdateStatus)
    WHERE UpdateStatus > 0;  -- Filtered index for active updates only
    PRINT 'Created IX_Computers_UpdateStatus filtered index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_UpdateStatus index already exists';
END

-- Index for ForceUpdate/ForceReinstall flags (agent check-in queries)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_ForceUpdate' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_ForceUpdate ON Computers (SerialNumber)
    INCLUDE (ForceUpdate, ForceReinstall, RequestImmediateCheckin)
    WHERE ForceUpdate = 1 OR ForceReinstall = 1 OR RequestImmediateCheckin = 1;
    PRINT 'Created IX_Computers_ForceUpdate filtered index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_ForceUpdate index already exists';
END

-- Index for Hostname (commonly searched)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_Hostname' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_Hostname ON Computers (Hostname)
    INCLUDE (SerialNumber, Status, IsOnline);
    PRINT 'Created IX_Computers_Hostname index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_Hostname index already exists';
END

-- Index for PublicIpAddress (geolocation lookups)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_PublicIpAddress' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_PublicIpAddress ON Computers (PublicIpAddress)
    WHERE PublicIpAddress IS NOT NULL;
    PRINT 'Created IX_Computers_PublicIpAddress filtered index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_PublicIpAddress index already exists';
END

-- Index for CreatedAt (audit/reporting)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Computers_CreatedAt' AND object_id = OBJECT_ID('Computers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Computers_CreatedAt ON Computers (CreatedAt DESC);
    PRINT 'Created IX_Computers_CreatedAt index';
END
ELSE
BEGIN
    PRINT 'IX_Computers_CreatedAt index already exists';
END

-- ============================================================================
-- SECTION 2: COMPUTER HISTORY TABLE INDEXES
-- ============================================================================
PRINT '';
PRINT '=== COMPUTER HISTORY TABLE INDEXES ===';

-- Composite index for history lookups by computer and time
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComputerHistories_ComputerId_ChangedAt' AND object_id = OBJECT_ID('ComputerHistories'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ComputerHistories_ComputerId_ChangedAt 
    ON ComputerHistories (ComputerId, ChangedAt DESC)
    INCLUDE (Action, OldValue, NewValue, ChangedBy);
    PRINT 'Created IX_ComputerHistories_ComputerId_ChangedAt index';
END
ELSE
BEGIN
    PRINT 'IX_ComputerHistories_ComputerId_ChangedAt index already exists';
END

-- Index for ChangedAt (reporting/audit queries)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComputerHistories_ChangedAt' AND object_id = OBJECT_ID('ComputerHistories'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ComputerHistories_ChangedAt 
    ON ComputerHistories (ChangedAt DESC)
    INCLUDE (ComputerId, Action, ChangedBy);
    PRINT 'Created IX_ComputerHistories_ChangedAt index';
END
ELSE
BEGIN
    PRINT 'IX_ComputerHistories_ChangedAt index already exists';
END

-- Index for Action type filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComputerHistories_Action' AND object_id = OBJECT_ID('ComputerHistories'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ComputerHistories_Action 
    ON ComputerHistories (Action)
    INCLUDE (ComputerId, ChangedAt, ChangedBy);
    PRINT 'Created IX_ComputerHistories_Action index';
END
ELSE
BEGIN
    PRINT 'IX_ComputerHistories_Action index already exists';
END

-- ============================================================================
-- SECTION 3: MAINTENANCE RECORDS TABLE INDEXES
-- ============================================================================
PRINT '';
PRINT '=== MAINTENANCE RECORDS TABLE INDEXES ===';

-- Composite index for maintenance lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaintenanceRecords_ComputerId_ScheduledDate' AND object_id = OBJECT_ID('MaintenanceRecords'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MaintenanceRecords_ComputerId_ScheduledDate 
    ON MaintenanceRecords (ComputerId, ScheduledDate DESC)
    INCLUDE (Type, Status, Description, Cost);
    PRINT 'Created IX_MaintenanceRecords_ComputerId_ScheduledDate index';
END
ELSE
BEGIN
    PRINT 'IX_MaintenanceRecords_ComputerId_ScheduledDate index already exists';
END

-- Index for Status filtering (pending maintenance views)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaintenanceRecords_Status' AND object_id = OBJECT_ID('MaintenanceRecords'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MaintenanceRecords_Status 
    ON MaintenanceRecords (Status)
    INCLUDE (ComputerId, ScheduledDate, Type);
    PRINT 'Created IX_MaintenanceRecords_Status index';
END
ELSE
BEGIN
    PRINT 'IX_MaintenanceRecords_Status index already exists';
END

-- Index for ScheduledDate (upcoming maintenance)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaintenanceRecords_ScheduledDate' AND object_id = OBJECT_ID('MaintenanceRecords'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MaintenanceRecords_ScheduledDate 
    ON MaintenanceRecords (ScheduledDate)
    WHERE Status IN (0, 1); -- Scheduled, InProgress
    PRINT 'Created IX_MaintenanceRecords_ScheduledDate filtered index';
END
ELSE
BEGIN
    PRINT 'IX_MaintenanceRecords_ScheduledDate index already exists';
END

-- ============================================================================
-- SECTION 4: SYSTEM DETAILS TABLE INDEXES
-- ============================================================================
PRINT '';
PRINT '=== SYSTEM DETAILS TABLE INDEXES ===';

-- Index already exists from entity config: ComputerId + CollectedAt
-- Add index for latest system details per computer
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SystemDetails_Latest' AND object_id = OBJECT_ID('SystemDetails'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SystemDetails_Latest 
    ON SystemDetails (ComputerId, CollectedAt DESC);
    PRINT 'Created IX_SystemDetails_Latest index';
END
ELSE
BEGIN
    PRINT 'IX_SystemDetails_Latest index already exists';
END

-- ============================================================================
-- SECTION 5: RESTORE POINTS TABLE INDEXES
-- ============================================================================
PRINT '';
PRINT '=== RESTORE POINTS TABLE INDEXES ===';

-- Additional index for Type filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RestorePoints_Type' AND object_id = OBJECT_ID('RestorePoints'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RestorePoints_Type 
    ON RestorePoints (Type)
    INCLUDE (ComputerId, CreatedAt, Description);
    PRINT 'Created IX_RestorePoints_Type index';
END
ELSE
BEGIN
    PRINT 'IX_RestorePoints_Type index already exists';
END

-- Index for CreatedAt (for cleanup queries of old restore points)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RestorePoints_CreatedAt' AND object_id = OBJECT_ID('RestorePoints'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RestorePoints_CreatedAt 
    ON RestorePoints (CreatedAt DESC)
    INCLUDE (ComputerId, Type);
    PRINT 'Created IX_RestorePoints_CreatedAt index';
END
ELSE
BEGIN
    PRINT 'IX_RestorePoints_CreatedAt index already exists';
END

-- ============================================================================
-- SECTION 6: VERSION HISTORY TABLE INDEXES
-- ============================================================================
PRINT '';
PRINT '=== VERSION HISTORY TABLE INDEXES ===';

-- Index for IsCurrentVersion lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VersionHistories_CurrentVersion' AND object_id = OBJECT_ID('VersionHistories'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VersionHistories_CurrentVersion 
    ON VersionHistories (IsCurrentVersion)
    WHERE IsCurrentVersion = 1;
    PRINT 'Created IX_VersionHistories_CurrentVersion filtered index';
END
ELSE
BEGIN
    PRINT 'IX_VersionHistories_CurrentVersion index already exists';
END

-- Index for ReleasedAt (version history display)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VersionHistories_ReleasedAt' AND object_id = OBJECT_ID('VersionHistories'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VersionHistories_ReleasedAt 
    ON VersionHistories (ReleasedAt DESC)
    INCLUDE (Type, Version, IsCurrentVersion);
    PRINT 'Created IX_VersionHistories_ReleasedAt index';
END
ELSE
BEGIN
    PRINT 'IX_VersionHistories_ReleasedAt index already exists';
END

-- ============================================================================
-- SECTION 7: GEO LOCATION TABLE INDEXES
-- ============================================================================
PRINT '';
PRINT '=== GEO LOCATION TABLE INDEXES ===';

-- Index for GPS locations (map filtering)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComputerGeoLocations_GpsCoords' AND object_id = OBJECT_ID('ComputerGeoLocations'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ComputerGeoLocations_GpsCoords 
    ON ComputerGeoLocations (GpsLatitude, GpsLongitude)
    WHERE GpsLatitude IS NOT NULL AND GpsLongitude IS NOT NULL;
    PRINT 'Created IX_ComputerGeoLocations_GpsCoords filtered index';
END
ELSE
BEGIN
    PRINT 'IX_ComputerGeoLocations_GpsCoords index already exists';
END

-- Index for IP locations (map filtering)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComputerGeoLocations_IpCoords' AND object_id = OBJECT_ID('ComputerGeoLocations'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ComputerGeoLocations_IpCoords 
    ON ComputerGeoLocations (IpLatitude, IpLongitude)
    WHERE IpLatitude IS NOT NULL AND IpLongitude IS NOT NULL;
    PRINT 'Created IX_ComputerGeoLocations_IpCoords filtered index';
END
ELSE
BEGIN
    PRINT 'IX_ComputerGeoLocations_IpCoords index already exists';
END

-- Index for Country grouping (regional reports)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComputerGeoLocations_Country' AND object_id = OBJECT_ID('ComputerGeoLocations'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ComputerGeoLocations_Country 
    ON ComputerGeoLocations (Country)
    INCLUDE (City, Region, ComputerId);
    PRINT 'Created IX_ComputerGeoLocations_Country index';
END
ELSE
BEGIN
    PRINT 'IX_ComputerGeoLocations_Country index already exists';
END

-- ============================================================================
-- SECTION 8: USERS TABLE INDEXES
-- ============================================================================
PRINT '';
PRINT '=== USERS TABLE INDEXES ===';

-- Index for IsActive filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_IsActive' AND object_id = OBJECT_ID('Users'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Users_IsActive 
    ON Users (IsActive)
    INCLUDE (Username, Email, Role);
    PRINT 'Created IX_Users_IsActive index';
END
ELSE
BEGIN
    PRINT 'IX_Users_IsActive index already exists';
END

-- Index for Role filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Role' AND object_id = OBJECT_ID('Users'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Users_Role 
    ON Users (Role)
    INCLUDE (Username, IsActive);
    PRINT 'Created IX_Users_Role index';
END
ELSE
BEGIN
    PRINT 'IX_Users_Role index already exists';
END

-- ============================================================================
-- SECTION 9: UPDATE STATISTICS
-- ============================================================================
PRINT '';
PRINT '=== UPDATING STATISTICS ===';

-- Update statistics on all tables for optimal query plans
UPDATE STATISTICS Computers WITH FULLSCAN;
PRINT 'Updated Computers statistics';

UPDATE STATISTICS ComputerHistories WITH FULLSCAN;
PRINT 'Updated ComputerHistories statistics';

UPDATE STATISTICS MaintenanceRecords WITH FULLSCAN;
PRINT 'Updated MaintenanceRecords statistics';

UPDATE STATISTICS SystemDetails WITH FULLSCAN;
PRINT 'Updated SystemDetails statistics';

UPDATE STATISTICS RestorePoints WITH FULLSCAN;
PRINT 'Updated RestorePoints statistics';

UPDATE STATISTICS VersionHistories WITH FULLSCAN;
PRINT 'Updated VersionHistories statistics';

UPDATE STATISTICS ComputerGeoLocations WITH FULLSCAN;
PRINT 'Updated ComputerGeoLocations statistics';

UPDATE STATISTICS Users WITH FULLSCAN;
PRINT 'Updated Users statistics';

-- ============================================================================
-- SECTION 10: SET OPTIMAL DATABASE OPTIONS
-- ============================================================================
PRINT '';
PRINT '=== SETTING DATABASE OPTIONS ===';

-- Enable auto-create and auto-update statistics
ALTER DATABASE CURRENT SET AUTO_CREATE_STATISTICS ON;
ALTER DATABASE CURRENT SET AUTO_UPDATE_STATISTICS ON;
ALTER DATABASE CURRENT SET AUTO_UPDATE_STATISTICS_ASYNC ON;
PRINT 'Enabled automatic statistics management';

-- Enable query store for performance monitoring (SQL Server 2016+)
BEGIN TRY
    ALTER DATABASE CURRENT SET QUERY_STORE = ON;
    ALTER DATABASE CURRENT SET QUERY_STORE (
        OPERATION_MODE = READ_WRITE,
        CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30),
        DATA_FLUSH_INTERVAL_SECONDS = 900,
        MAX_STORAGE_SIZE_MB = 100,
        INTERVAL_LENGTH_MINUTES = 60
    );
    PRINT 'Enabled Query Store for performance monitoring';
END TRY
BEGIN CATCH
    PRINT 'Query Store configuration skipped (may not be supported)';
END CATCH

-- ============================================================================
-- SECTION 11: ADD CHECK CONSTRAINTS
-- ============================================================================
PRINT '';
PRINT '=== ADDING CHECK CONSTRAINTS ===';

-- Constraint for valid Status values
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Computers_Status')
BEGIN
    ALTER TABLE Computers 
    ADD CONSTRAINT CK_Computers_Status CHECK (Status >= 0 AND Status <= 6);
    PRINT 'Added CK_Computers_Status constraint';
END
ELSE
BEGIN
    PRINT 'CK_Computers_Status constraint already exists';
END

-- Constraint for valid Type values
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Computers_Type')
BEGIN
    ALTER TABLE Computers 
    ADD CONSTRAINT CK_Computers_Type CHECK (Type >= 0 AND Type <= 5);
    PRINT 'Added CK_Computers_Type constraint';
END
ELSE
BEGIN
    PRINT 'CK_Computers_Type constraint already exists';
END

-- Constraint for UpdateStatus values
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Computers_UpdateStatus')
BEGIN
    ALTER TABLE Computers 
    ADD CONSTRAINT CK_Computers_UpdateStatus CHECK (UpdateStatus >= 0 AND UpdateStatus <= 99);
    PRINT 'Added CK_Computers_UpdateStatus constraint';
END
ELSE
BEGIN
    PRINT 'CK_Computers_UpdateStatus constraint already exists';
END

-- Constraint for UpdateProgress percentage
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Computers_UpdateProgress')
BEGIN
    ALTER TABLE Computers 
    ADD CONSTRAINT CK_Computers_UpdateProgress CHECK (UpdateProgress >= 0 AND UpdateProgress <= 100);
    PRINT 'Added CK_Computers_UpdateProgress constraint';
END
ELSE
BEGIN
    PRINT 'CK_Computers_UpdateProgress constraint already exists';
END

-- Constraint for valid MaintenanceRecord Status
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_MaintenanceRecords_Status')
BEGIN
    ALTER TABLE MaintenanceRecords 
    ADD CONSTRAINT CK_MaintenanceRecords_Status CHECK (Status >= 0 AND Status <= 3);
    PRINT 'Added CK_MaintenanceRecords_Status constraint';
END
ELSE
BEGIN
    PRINT 'CK_MaintenanceRecords_Status constraint already exists';
END

-- Constraint for valid MaintenanceRecord Type
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_MaintenanceRecords_Type')
BEGIN
    ALTER TABLE MaintenanceRecords 
    ADD CONSTRAINT CK_MaintenanceRecords_Type CHECK (Type >= 0 AND Type <= 6);
    PRINT 'Added CK_MaintenanceRecords_Type constraint';
END
ELSE
BEGIN
    PRINT 'CK_MaintenanceRecords_Type constraint already exists';
END

-- Constraint for positive Cost in MaintenanceRecords
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_MaintenanceRecords_Cost')
BEGIN
    ALTER TABLE MaintenanceRecords 
    ADD CONSTRAINT CK_MaintenanceRecords_Cost CHECK (Cost IS NULL OR Cost >= 0);
    PRINT 'Added CK_MaintenanceRecords_Cost constraint';
END
ELSE
BEGIN
    PRINT 'CK_MaintenanceRecords_Cost constraint already exists';
END

-- Constraint for positive PurchasePrice
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Computers_PurchasePrice')
BEGIN
    ALTER TABLE Computers 
    ADD CONSTRAINT CK_Computers_PurchasePrice CHECK (PurchasePrice IS NULL OR PurchasePrice >= 0);
    PRINT 'Added CK_Computers_PurchasePrice constraint';
END
ELSE
BEGIN
    PRINT 'CK_Computers_PurchasePrice constraint already exists';
END

-- Constraint for valid latitude range
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ComputerGeoLocations_Latitude')
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD CONSTRAINT CK_ComputerGeoLocations_Latitude CHECK (
        (Latitude IS NULL OR (Latitude >= -90 AND Latitude <= 90)) AND
        (GpsLatitude IS NULL OR (GpsLatitude >= -90 AND GpsLatitude <= 90)) AND
        (IpLatitude IS NULL OR (IpLatitude >= -90 AND IpLatitude <= 90))
    );
    PRINT 'Added CK_ComputerGeoLocations_Latitude constraint';
END
ELSE
BEGIN
    PRINT 'CK_ComputerGeoLocations_Latitude constraint already exists';
END

-- Constraint for valid longitude range
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ComputerGeoLocations_Longitude')
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD CONSTRAINT CK_ComputerGeoLocations_Longitude CHECK (
        (Longitude IS NULL OR (Longitude >= -180 AND Longitude <= 180)) AND
        (GpsLongitude IS NULL OR (GpsLongitude >= -180 AND GpsLongitude <= 180)) AND
        (IpLongitude IS NULL OR (IpLongitude >= -180 AND IpLongitude <= 180))
    );
    PRINT 'Added CK_ComputerGeoLocations_Longitude constraint';
END
ELSE
BEGIN
    PRINT 'CK_ComputerGeoLocations_Longitude constraint already exists';
END

-- ============================================================================
-- SECTION 12: CREATE USEFUL DATABASE VIEWS
-- ============================================================================
PRINT '';
PRINT '=== CREATING DATABASE VIEWS ===';

-- View for online computers summary
IF OBJECT_ID('vw_OnlineComputers', 'V') IS NOT NULL
    DROP VIEW vw_OnlineComputers;
GO

CREATE VIEW vw_OnlineComputers AS
SELECT 
    c.Id,
    c.Hostname,
    c.SerialNumber,
    c.IpAddress,
    c.CurrentUser,
    c.LastSeen,
    c.AgentVersion,
    c.Department,
    c.Location,
    c.Status,
    DATEDIFF(MINUTE, c.LastSeen, GETUTCDATE()) AS MinutesSinceLastSeen
FROM Computers c
WHERE c.IsOnline = 1 AND c.HasAgent = 1;
GO
PRINT 'Created vw_OnlineComputers view';

-- View for computers needing updates
IF OBJECT_ID('vw_ComputersNeedingUpdate', 'V') IS NOT NULL
    DROP VIEW vw_ComputersNeedingUpdate;
GO

CREATE VIEW vw_ComputersNeedingUpdate AS
SELECT 
    c.Id,
    c.Hostname,
    c.SerialNumber,
    c.AgentVersion,
    c.ForceUpdate,
    c.ForceReinstall,
    c.UpdateStatus,
    c.UpdateProgress,
    c.UpdateStatusMessage,
    c.UpdateStartedAt,
    c.LastSeen
FROM Computers c
WHERE c.HasAgent = 1 
  AND (c.ForceUpdate = 1 OR c.ForceReinstall = 1 OR c.UpdateStatus > 0);
GO
PRINT 'Created vw_ComputersNeedingUpdate view';

-- View for computer inventory summary by department
IF OBJECT_ID('vw_DepartmentSummary', 'V') IS NOT NULL
    DROP VIEW vw_DepartmentSummary;
GO

CREATE VIEW vw_DepartmentSummary AS
SELECT 
    ISNULL(c.Department, 'Unassigned') AS Department,
    COUNT(*) AS TotalComputers,
    SUM(CASE WHEN c.IsOnline = 1 THEN 1 ELSE 0 END) AS OnlineCount,
    SUM(CASE WHEN c.Status = 0 THEN 1 ELSE 0 END) AS AvailableCount,
    SUM(CASE WHEN c.Status = 1 THEN 1 ELSE 0 END) AS AssignedCount,
    SUM(CASE WHEN c.Status = 2 THEN 1 ELSE 0 END) AS InMaintenanceCount,
    SUM(CASE WHEN c.HasAgent = 1 THEN 1 ELSE 0 END) AS WithAgentCount
FROM Computers c
GROUP BY c.Department;
GO
PRINT 'Created vw_DepartmentSummary view';

-- View for recent activity
IF OBJECT_ID('vw_RecentActivity', 'V') IS NOT NULL
    DROP VIEW vw_RecentActivity;
GO

CREATE VIEW vw_RecentActivity AS
SELECT TOP 100
    ch.Id,
    ch.ComputerId,
    c.Hostname,
    c.SerialNumber,
    ch.Action,
    ch.OldValue,
    ch.NewValue,
    ch.ChangedBy,
    ch.ChangedAt
FROM ComputerHistories ch
INNER JOIN Computers c ON ch.ComputerId = c.Id
ORDER BY ch.ChangedAt DESC;
GO
PRINT 'Created vw_RecentActivity view';

-- View for pending maintenance
IF OBJECT_ID('vw_PendingMaintenance', 'V') IS NOT NULL
    DROP VIEW vw_PendingMaintenance;
GO

CREATE VIEW vw_PendingMaintenance AS
SELECT 
    mr.Id,
    mr.ComputerId,
    c.Hostname,
    c.SerialNumber,
    mr.Type,
    mr.Description,
    mr.ScheduledDate,
    mr.Status,
    mr.PerformedBy,
    DATEDIFF(DAY, GETUTCDATE(), mr.ScheduledDate) AS DaysUntilDue
FROM MaintenanceRecords mr
INNER JOIN Computers c ON mr.ComputerId = c.Id
WHERE mr.Status IN (0, 1) -- Scheduled, InProgress
ORDER BY mr.ScheduledDate;
GO
PRINT 'Created vw_PendingMaintenance view';

-- ============================================================================
-- SECTION 13: CREATE MAINTENANCE STORED PROCEDURES
-- ============================================================================
PRINT '';
PRINT '=== CREATING STORED PROCEDURES ===';

-- Procedure to mark offline computers (called periodically)
IF OBJECT_ID('sp_MarkOfflineComputers', 'P') IS NOT NULL
    DROP PROCEDURE sp_MarkOfflineComputers;
GO

CREATE PROCEDURE sp_MarkOfflineComputers
    @OfflineThresholdMinutes INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CutoffTime DATETIME2 = DATEADD(MINUTE, -@OfflineThresholdMinutes, GETUTCDATE());
    DECLARE @UpdatedCount INT;
    
    UPDATE Computers
    SET IsOnline = 0
    WHERE IsOnline = 1 
      AND LastSeen < @CutoffTime;
    
    SET @UpdatedCount = @@ROWCOUNT;
    
    SELECT @UpdatedCount AS ComputersMarkedOffline;
END;
GO
PRINT 'Created sp_MarkOfflineComputers procedure';

-- Procedure to clean up old restore points
IF OBJECT_ID('sp_CleanupOldRestorePoints', 'P') IS NOT NULL
    DROP PROCEDURE sp_CleanupOldRestorePoints;
GO

CREATE PROCEDURE sp_CleanupOldRestorePoints
    @RetentionDays INT = 90,
    @MaxPointsPerComputer INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @DeletedByAge INT = 0;
    DECLARE @DeletedByCount INT = 0;
    
    -- Delete restore points older than retention period
    DELETE FROM RestorePoints
    WHERE CreatedAt < DATEADD(DAY, -@RetentionDays, GETUTCDATE());
    
    SET @DeletedByAge = @@ROWCOUNT;
    
    -- Delete excess restore points per computer (keep most recent)
    ;WITH RankedPoints AS (
        SELECT Id,
               ROW_NUMBER() OVER (PARTITION BY ComputerId ORDER BY CreatedAt DESC) AS RowNum
        FROM RestorePoints
    )
    DELETE FROM RestorePoints
    WHERE Id IN (SELECT Id FROM RankedPoints WHERE RowNum > @MaxPointsPerComputer);
    
    SET @DeletedByCount = @@ROWCOUNT;
    
    SELECT @DeletedByAge AS DeletedByAge, @DeletedByCount AS DeletedByExcessCount;
END;
GO
PRINT 'Created sp_CleanupOldRestorePoints procedure';

-- Procedure to clean up old system details
IF OBJECT_ID('sp_CleanupOldSystemDetails', 'P') IS NOT NULL
    DROP PROCEDURE sp_CleanupOldSystemDetails;
GO

CREATE PROCEDURE sp_CleanupOldSystemDetails
    @RetentionDays INT = 30,
    @MaxPerComputer INT = 100
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @DeletedByAge INT = 0;
    DECLARE @DeletedByCount INT = 0;
    
    -- Delete system details older than retention period
    DELETE FROM SystemDetails
    WHERE CollectedAt < DATEADD(DAY, -@RetentionDays, GETUTCDATE());
    
    SET @DeletedByAge = @@ROWCOUNT;
    
    -- Delete excess system details per computer (keep most recent)
    ;WITH RankedDetails AS (
        SELECT Id,
               ROW_NUMBER() OVER (PARTITION BY ComputerId ORDER BY CollectedAt DESC) AS RowNum
        FROM SystemDetails
    )
    DELETE FROM SystemDetails
    WHERE Id IN (SELECT Id FROM RankedDetails WHERE RowNum > @MaxPerComputer);
    
    SET @DeletedByCount = @@ROWCOUNT;
    
    SELECT @DeletedByAge AS DeletedByAge, @DeletedByCount AS DeletedByExcessCount;
END;
GO
PRINT 'Created sp_CleanupOldSystemDetails procedure';

-- Procedure to get computer dashboard stats
IF OBJECT_ID('sp_GetDashboardStats', 'P') IS NOT NULL
    DROP PROCEDURE sp_GetDashboardStats;
GO

CREATE PROCEDURE sp_GetDashboardStats
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        COUNT(*) AS TotalComputers,
        SUM(CASE WHEN IsOnline = 1 THEN 1 ELSE 0 END) AS OnlineCount,
        SUM(CASE WHEN HasAgent = 1 THEN 1 ELSE 0 END) AS WithAgentCount,
        SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS AvailableCount,
        SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS AssignedCount,
        SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS InMaintenanceCount,
        SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS RetiredCount,
        SUM(CASE WHEN UpdateStatus > 0 AND UpdateStatus < 11 THEN 1 ELSE 0 END) AS UpdatingCount,
        SUM(CASE WHEN UpdateStatus = 99 THEN 1 ELSE 0 END) AS UpdateFailedCount
    FROM Computers;
END;
GO
PRINT 'Created sp_GetDashboardStats procedure';

-- ============================================================================
-- COMPLETE
-- ============================================================================
PRINT '';
PRINT '============================================================================';
PRINT 'Database optimization migration completed successfully!';
PRINT '============================================================================';
PRINT '';
PRINT 'Summary of changes:';
PRINT '  - Added 25+ optimized indexes for common query patterns';
PRINT '  - Added filtered indexes for sparse data optimization';
PRINT '  - Added check constraints for data integrity';
PRINT '  - Created 5 database views for common queries';
PRINT '  - Created 4 stored procedures for maintenance tasks';
PRINT '  - Updated statistics on all tables';
PRINT '  - Enabled Query Store for performance monitoring';
PRINT '';
PRINT 'Recommended: Schedule these procedures to run periodically:';
PRINT '  - EXEC sp_MarkOfflineComputers @OfflineThresholdMinutes = 5';
PRINT '  - EXEC sp_CleanupOldRestorePoints @RetentionDays = 90';
PRINT '  - EXEC sp_CleanupOldSystemDetails @RetentionDays = 30';
PRINT '';
