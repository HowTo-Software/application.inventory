-- Add Update Progress Tracking columns to Computers table
-- Run this script against the SQL Server database to add the new update progress fields

-- Check if columns already exist before adding
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Computers') AND name = 'UpdateStatus')
BEGIN
    ALTER TABLE Computers ADD UpdateStatus INT NOT NULL DEFAULT 0;
    PRINT 'Added UpdateStatus column';
END
ELSE
BEGIN
    PRINT 'UpdateStatus column already exists';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Computers') AND name = 'UpdateProgress')
BEGIN
    ALTER TABLE Computers ADD UpdateProgress INT NOT NULL DEFAULT 0;
    PRINT 'Added UpdateProgress column';
END
ELSE
BEGIN
    PRINT 'UpdateProgress column already exists';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Computers') AND name = 'UpdateStatusMessage')
BEGIN
    ALTER TABLE Computers ADD UpdateStatusMessage NVARCHAR(500) NULL;
    PRINT 'Added UpdateStatusMessage column';
END
ELSE
BEGIN
    PRINT 'UpdateStatusMessage column already exists';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Computers') AND name = 'UpdateStartedAt')
BEGIN
    ALTER TABLE Computers ADD UpdateStartedAt DATETIME2 NULL;
    PRINT 'Added UpdateStartedAt column';
END
ELSE
BEGIN
    PRINT 'UpdateStartedAt column already exists';
END

PRINT 'Migration completed successfully';
