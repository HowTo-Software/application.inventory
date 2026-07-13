-- Migration: Add RowVersion for optimistic concurrency control
-- Date: 2026-01-06
-- Description: Adds a timestamp/rowversion column to the Computers table for optimistic concurrency control.
--              This helps prevent lost updates when multiple agents or users update the same computer simultaneously.

-- Add RowVersion column to Computers table
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Computers') AND name = 'RowVersion'
)
BEGIN
    ALTER TABLE [Computers] ADD [RowVersion] rowversion NOT NULL;
    PRINT 'Added RowVersion column to Computers table';
END
ELSE
BEGIN
    PRINT 'RowVersion column already exists on Computers table';
END
GO

-- Verify the column was added
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.is_nullable AS IsNullable,
    c.is_rowguidcol AS IsRowGuid
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('Computers') AND c.name = 'RowVersion';
GO
