-- Migration: Add row_version to maintenance_records, computer_histories, system_details
-- Date: 2026-03-13
-- Description: Extends optimistic concurrency control (row_version) to maintenance_records,
--              computer_histories, and system_details tables — previously only computers had it.

-- maintenance_records
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('maintenance_records') AND name = 'row_version'
)
BEGIN
    ALTER TABLE [dbo].[maintenance_records] ADD [row_version] rowversion NOT NULL;
    PRINT 'Added row_version column to maintenance_records table';
END
ELSE
BEGIN
    PRINT 'row_version column already exists on maintenance_records table';
END
GO

-- computer_histories
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('computer_histories') AND name = 'row_version'
)
BEGIN
    ALTER TABLE [dbo].[computer_histories] ADD [row_version] rowversion NOT NULL;
    PRINT 'Added row_version column to computer_histories table';
END
ELSE
BEGIN
    PRINT 'row_version column already exists on computer_histories table';
END
GO

-- system_details
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('system_details') AND name = 'row_version'
)
BEGIN
    ALTER TABLE [dbo].[system_details] ADD [row_version] rowversion NOT NULL;
    PRINT 'Added row_version column to system_details table';
END
ELSE
BEGIN
    PRINT 'row_version column already exists on system_details table';
END
GO

-- Verify all columns were added
SELECT 
    OBJECT_NAME(c.object_id) AS table_name,
    c.name AS column_name,
    t.name AS data_type,
    c.is_nullable AS is_nullable
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.name = 'row_version'
  AND c.object_id IN (
      OBJECT_ID('maintenance_records'),
      OBJECT_ID('computer_histories'),
      OBJECT_ID('system_details')
  )
ORDER BY table_name;
GO
