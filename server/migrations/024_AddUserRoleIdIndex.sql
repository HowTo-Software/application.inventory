-- Migration: 024_AddUserRoleIdIndex
-- Date: 2026-03-13
-- Description: Add missing index on users.role_id FK column

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_RoleId' AND object_id = OBJECT_ID('users'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Users_RoleId
    ON users (role_id);
    PRINT 'Created IX_Users_RoleId index';
END
ELSE
BEGIN
    PRINT 'IX_Users_RoleId index already exists';
END
GO
