-- Migration: Add per-computer heartbeat interval
-- Default is 5 minutes (matching current system-wide default)

ALTER TABLE [computers]
ADD [heartbeat_interval_minutes] INT NOT NULL DEFAULT 5;
