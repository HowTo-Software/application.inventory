-- Migration: Add GPS/IP separate coordinate fields and admin location source preference
-- This migration allows admin to choose between GPS and IP-based location per computer
-- Run this migration on the database before deploying the updated application

-- Add GPS-specific coordinate columns to ComputerGeoLocations
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ComputerGeoLocations' AND COLUMN_NAME = 'GpsLatitude'
)
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD GpsLatitude FLOAT NULL;
    
    PRINT 'Added GpsLatitude column to ComputerGeoLocations';
END
ELSE
BEGIN
    PRINT 'GpsLatitude column already exists';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ComputerGeoLocations' AND COLUMN_NAME = 'GpsLongitude'
)
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD GpsLongitude FLOAT NULL;
    
    PRINT 'Added GpsLongitude column to ComputerGeoLocations';
END
ELSE
BEGIN
    PRINT 'GpsLongitude column already exists';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ComputerGeoLocations' AND COLUMN_NAME = 'GpsLastUpdated'
)
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD GpsLastUpdated DATETIME2 NULL;
    
    PRINT 'Added GpsLastUpdated column to ComputerGeoLocations';
END
ELSE
BEGIN
    PRINT 'GpsLastUpdated column already exists';
END
GO

-- Add IP-specific coordinate columns to ComputerGeoLocations
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ComputerGeoLocations' AND COLUMN_NAME = 'IpLatitude'
)
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD IpLatitude FLOAT NULL;
    
    PRINT 'Added IpLatitude column to ComputerGeoLocations';
END
ELSE
BEGIN
    PRINT 'IpLatitude column already exists';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ComputerGeoLocations' AND COLUMN_NAME = 'IpLongitude'
)
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD IpLongitude FLOAT NULL;
    
    PRINT 'Added IpLongitude column to ComputerGeoLocations';
END
ELSE
BEGIN
    PRINT 'IpLongitude column already exists';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ComputerGeoLocations' AND COLUMN_NAME = 'IpLastUpdated'
)
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD IpLastUpdated DATETIME2 NULL;
    
    PRINT 'Added IpLastUpdated column to ComputerGeoLocations';
END
ELSE
BEGIN
    PRINT 'IpLastUpdated column already exists';
END
GO

-- Add PreferredLocationSource column to Computers table
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Computers' AND COLUMN_NAME = 'PreferredLocationSource'
)
BEGIN
    ALTER TABLE Computers 
    ADD PreferredLocationSource NVARCHAR(10) NOT NULL DEFAULT 'Auto';
    
    PRINT 'Added PreferredLocationSource column to Computers';
END
ELSE
BEGIN
    PRINT 'PreferredLocationSource column already exists';
END
GO

-- Migrate existing data: copy current coordinates to appropriate columns based on LocationSource
-- For GPS locations, copy to GPS columns
UPDATE g
SET 
    g.GpsLatitude = g.Latitude,
    g.GpsLongitude = g.Longitude,
    g.GpsLastUpdated = g.LastUpdated
FROM ComputerGeoLocations g
WHERE g.LocationSource = 'GPS'
    AND g.GpsLatitude IS NULL
    AND g.Latitude IS NOT NULL;

PRINT 'Migrated existing GPS coordinates to GpsLatitude/GpsLongitude';

-- For IP locations, copy to IP columns
UPDATE g
SET 
    g.IpLatitude = g.Latitude,
    g.IpLongitude = g.Longitude,
    g.IpLastUpdated = g.LastUpdated
FROM ComputerGeoLocations g
WHERE g.LocationSource = 'IP'
    AND g.IpLatitude IS NULL
    AND g.Latitude IS NOT NULL;

PRINT 'Migrated existing IP coordinates to IpLatitude/IpLongitude';

PRINT 'Migration 003_AddLocationSourcePreference completed successfully';
GO
