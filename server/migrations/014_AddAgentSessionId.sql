-- Migration: Add agent_session_id to computers table
-- Tracks agent session across registration, heartbeat, and update-progress API calls.

ALTER TABLE [computers]
ADD [agent_session_id] NVARCHAR(64) NULL;
