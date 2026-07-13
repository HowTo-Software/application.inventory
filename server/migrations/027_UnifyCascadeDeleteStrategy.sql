-- Migration: 027_UnifyCascadeDeleteStrategy
-- Date: 2026-03-13
-- Description: Unify cascade-delete strategy — change all SetNull FK constraints
--              to Restrict (NO ACTION in T-SQL). This ensures lookup/reference table
--              rows cannot be deleted while still referenced, preventing silent data loss.
--
-- Affected FKs:
--   computers.operating_system_id    → operating_systems   (SET NULL → NO ACTION)
--   computers.location_id            → locations           (SET NULL → NO ACTION)
--   computers.department_id          → departments         (SET NULL → NO ACTION)
--   users.department_id              → departments         (SET NULL → NO ACTION)
--   computer_geo_locations.country_id → countries          (SET NULL → NO ACTION)

-- ============================================================
-- 1. computers.operating_system_id → operating_systems
-- ============================================================
DECLARE @fk NVARCHAR(256);

SELECT @fk = fk.name
FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
WHERE fk.parent_object_id = OBJECT_ID('dbo.computers')
  AND c.name = 'operating_system_id';

IF @fk IS NOT NULL AND EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = @fk AND delete_referential_action = 1 -- SET NULL
)
BEGIN
    EXEC('ALTER TABLE [dbo].[computers] DROP CONSTRAINT [' + @fk + ']');
    ALTER TABLE [dbo].[computers]
        ADD CONSTRAINT [fk_computers_operating_system_id]
        FOREIGN KEY ([operating_system_id]) REFERENCES [dbo].[operating_systems] ([id])
        ON DELETE NO ACTION;
    PRINT 'Updated: computers.operating_system_id → NO ACTION';
END
ELSE
    PRINT 'Skipped: computers.operating_system_id (already NO ACTION or not found)';
GO

-- ============================================================
-- 2. computers.location_id → locations
-- ============================================================
DECLARE @fk NVARCHAR(256);

SELECT @fk = fk.name
FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
WHERE fk.parent_object_id = OBJECT_ID('dbo.computers')
  AND c.name = 'location_id';

IF @fk IS NOT NULL AND EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = @fk AND delete_referential_action = 1
)
BEGIN
    EXEC('ALTER TABLE [dbo].[computers] DROP CONSTRAINT [' + @fk + ']');
    ALTER TABLE [dbo].[computers]
        ADD CONSTRAINT [fk_computers_location_id]
        FOREIGN KEY ([location_id]) REFERENCES [dbo].[locations] ([id])
        ON DELETE NO ACTION;
    PRINT 'Updated: computers.location_id → NO ACTION';
END
ELSE
    PRINT 'Skipped: computers.location_id (already NO ACTION or not found)';
GO

-- ============================================================
-- 3. computers.department_id → departments
-- ============================================================
DECLARE @fk NVARCHAR(256);

SELECT @fk = fk.name
FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
WHERE fk.parent_object_id = OBJECT_ID('dbo.computers')
  AND c.name = 'department_id';

IF @fk IS NOT NULL AND EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = @fk AND delete_referential_action = 1
)
BEGIN
    EXEC('ALTER TABLE [dbo].[computers] DROP CONSTRAINT [' + @fk + ']');
    ALTER TABLE [dbo].[computers]
        ADD CONSTRAINT [fk_computers_department_id]
        FOREIGN KEY ([department_id]) REFERENCES [dbo].[departments] ([id])
        ON DELETE NO ACTION;
    PRINT 'Updated: computers.department_id → NO ACTION';
END
ELSE
    PRINT 'Skipped: computers.department_id (already NO ACTION or not found)';
GO

-- ============================================================
-- 4. users.department_id → departments
-- ============================================================
DECLARE @fk NVARCHAR(256);

SELECT @fk = fk.name
FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
WHERE fk.parent_object_id = OBJECT_ID('dbo.users')
  AND c.name = 'department_id';

IF @fk IS NOT NULL AND EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = @fk AND delete_referential_action = 1
)
BEGIN
    EXEC('ALTER TABLE [dbo].[users] DROP CONSTRAINT [' + @fk + ']');
    ALTER TABLE [dbo].[users]
        ADD CONSTRAINT [fk_users_department_id]
        FOREIGN KEY ([department_id]) REFERENCES [dbo].[departments] ([id])
        ON DELETE NO ACTION;
    PRINT 'Updated: users.department_id → NO ACTION';
END
ELSE
    PRINT 'Skipped: users.department_id (already NO ACTION or not found)';
GO

-- ============================================================
-- 5. computer_geo_locations.country_id → countries
-- ============================================================
DECLARE @fk NVARCHAR(256);

SELECT @fk = fk.name
FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
WHERE fk.parent_object_id = OBJECT_ID('dbo.computer_geo_locations')
  AND c.name = 'country_id';

IF @fk IS NOT NULL AND EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = @fk AND delete_referential_action = 1
)
BEGIN
    EXEC('ALTER TABLE [dbo].[computer_geo_locations] DROP CONSTRAINT [' + @fk + ']');
    ALTER TABLE [dbo].[computer_geo_locations]
        ADD CONSTRAINT [fk_computer_geo_locations_country_id]
        FOREIGN KEY ([country_id]) REFERENCES [dbo].[countries] ([id])
        ON DELETE NO ACTION;
    PRINT 'Updated: computer_geo_locations.country_id → NO ACTION';
END
ELSE
    PRINT 'Skipped: computer_geo_locations.country_id (already NO ACTION or not found)';
GO
