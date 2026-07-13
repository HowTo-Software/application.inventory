-- Migration: Add LocationSource field to ComputerGeoLocations table
-- This migration adds support for GPS-based location (exact coordinates from Windows Location Services)
-- Run this migration on the database before deploying the updated application

-- Add LocationSource column (default to 'IP' for existing records)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ComputerGeoLocations' AND COLUMN_NAME = 'LocationSource'
)
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD LocationSource NVARCHAR(10) NOT NULL DEFAULT 'IP';
    
    PRINT 'Added LocationSource column to ComputerGeoLocations';
END
ELSE
BEGIN
    PRINT 'LocationSource column already exists';
END
GO

-- Add GpsAccuracyMeters column (nullable, only applicable for GPS locations)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ComputerGeoLocations' AND COLUMN_NAME = 'GpsAccuracyMeters'
)
BEGIN
    ALTER TABLE ComputerGeoLocations 
    ADD GpsAccuracyMeters FLOAT NULL;
    
    PRINT 'Added GpsAccuracyMeters column to ComputerGeoLocations';
END
ELSE
BEGIN
    PRINT 'GpsAccuracyMeters column already exists';
END
GO

-- Update existing records to use 'IP' as the location source (they were all IP-based before)
UPDATE ComputerGeoLocations 
SET LocationSource = 'IP' 
WHERE LocationSource IS NULL OR LocationSource = '';

PRINT 'Migration completed successfully';
GO
