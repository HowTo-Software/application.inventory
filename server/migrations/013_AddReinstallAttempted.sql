-- Migration: Add reinstall_attempted flag to computers table
-- Tracks whether a reinstall was attempted (via auto-escalation or manual trigger)
-- so the system can detect compound failures and offer manual override options.

ALTER TABLE [computers]
ADD [reinstall_attempted] BIT NOT NULL DEFAULT 0;
