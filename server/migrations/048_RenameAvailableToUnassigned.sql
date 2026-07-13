-- Rename the 'Available' computer status to 'Unassigned'.
-- The computers table uses status_id (FK to computer_statuses), so only the lookup table needs updating.

UPDATE [dbo].[computer_statuses]
SET [name] = N'Unassigned'
WHERE [name] = N'Available';

IF @@ROWCOUNT > 0
    PRINT 'Updated computer_statuses: Available -> Unassigned';
ELSE
    PRINT 'computer_statuses: Available not found (may already be renamed)';
