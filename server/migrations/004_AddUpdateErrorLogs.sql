-- Add UpdateErrorLogs column to Computers table
-- This stores error logs from failed agent updates for diagnostic purposes

-- Check if column already exists before adding
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Computers') AND name = 'UpdateErrorLogs')
BEGIN
    ALTER TABLE Computers ADD UpdateErrorLogs NVARCHAR(MAX) NULL;
    PRINT 'Added UpdateErrorLogs column';
END
ELSE
BEGIN
    PRINT 'UpdateErrorLogs column already exists';
END

PRINT 'Migration 004_AddUpdateErrorLogs completed successfully';
