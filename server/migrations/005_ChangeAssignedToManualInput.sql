-- Change AssignedUserId foreign key to AssignedTo text field
-- This allows manual entry of assigned user names instead of selecting from users table

-- Step 1: Add the new AssignedTo column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Computers') AND name = 'AssignedTo')
BEGIN
    ALTER TABLE Computers ADD AssignedTo NVARCHAR(200) NULL;
    PRINT 'Added AssignedTo column';
END
ELSE
BEGIN
    PRINT 'AssignedTo column already exists';
END

-- Step 2: Migrate existing assigned user data (copy FullName from Users table)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Computers') AND name = 'AssignedUserId')
BEGIN
    UPDATE c
    SET c.AssignedTo = u.FirstName + ' ' + u.LastName
    FROM Computers c
    INNER JOIN Users u ON c.AssignedUserId = u.Id
    WHERE c.AssignedUserId IS NOT NULL AND c.AssignedTo IS NULL;
    PRINT 'Migrated existing AssignedUserId values to AssignedTo';
END

-- Step 3: Drop the foreign key constraint if it exists
DECLARE @FKName NVARCHAR(128);
SELECT @FKName = fk.name
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns c ON fkc.parent_column_id = c.column_id AND fkc.parent_object_id = c.object_id
WHERE fk.parent_object_id = OBJECT_ID('Computers') AND c.name = 'AssignedUserId';

IF @FKName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE Computers DROP CONSTRAINT ' + @FKName);
    PRINT 'Dropped foreign key constraint: ' + @FKName;
END
ELSE
BEGIN
    PRINT 'No foreign key constraint found for AssignedUserId';
END

-- Step 4: Drop the AssignedUserId column
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Computers') AND name = 'AssignedUserId')
BEGIN
    ALTER TABLE Computers DROP COLUMN AssignedUserId;
    PRINT 'Dropped AssignedUserId column';
END
ELSE
BEGIN
    PRINT 'AssignedUserId column does not exist';
END

PRINT 'Migration 005_ChangeAssignedToManualInput completed successfully';
