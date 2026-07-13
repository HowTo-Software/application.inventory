-- Migration: 025_AddMaintenanceRecordComputerIdIndex
-- Date: 2026-03-13
-- Description: Add missing standalone index on maintenance_records.computer_id FK column

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaintenanceRecords_ComputerId' AND object_id = OBJECT_ID('maintenance_records'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MaintenanceRecords_ComputerId
    ON maintenance_records (computer_id);
    PRINT 'Created IX_MaintenanceRecords_ComputerId index';
END
ELSE
BEGIN
    PRINT 'IX_MaintenanceRecords_ComputerId index already exists';
END
GO
