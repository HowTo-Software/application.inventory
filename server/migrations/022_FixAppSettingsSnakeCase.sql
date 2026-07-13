-- Migration: Rename AppSettings table and columns to snake_case
-- The original migration (011) created this table with PascalCase names,
-- but EF Core's UseSnakeCaseNamingConvention() expects snake_case.

-- Step 1: Rename columns
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AppSettings')
BEGIN
    EXEC sp_rename 'AppSettings.Key', 'key', 'COLUMN';
    EXEC sp_rename 'AppSettings.Value', 'value', 'COLUMN';
    EXEC sp_rename 'AppSettings.UpdatedAt', 'updated_at', 'COLUMN';
    PRINT 'Renamed AppSettings columns to snake_case';
END
GO

-- Step 2: Rename primary key constraint
IF EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_AppSettings')
BEGIN
    EXEC sp_rename 'PK_AppSettings', 'pk_app_settings', 'OBJECT';
    PRINT 'Renamed PK_AppSettings constraint to pk_app_settings';
END
GO

-- Step 3: Rename table
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AppSettings')
BEGIN
    EXEC sp_rename 'AppSettings', 'app_settings';
    PRINT 'Renamed table AppSettings to app_settings';
END
GO
