-- Migration: 026_AddComputerUpdateRequestComputerIdIndex
-- Date: 2026-03-13
-- Description: Add standalone index on ComputerId for FK lookups and cascade deletes

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'ix_computer_update_requests_computer_id'
      AND object_id = OBJECT_ID('dbo.computer_update_requests')
)
BEGIN
    CREATE INDEX [ix_computer_update_requests_computer_id]
        ON [dbo].[computer_update_requests] ([computer_id]);
END
GO
