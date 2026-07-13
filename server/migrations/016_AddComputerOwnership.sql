-- Migration: 016_AddComputerOwnership
-- Purpose: Per-computer access control — tracks which users have explicit access to specific computers
-- Date: 2026-03-11

-- Create computer_ownerships table (snake_case to match EFCore.NamingConventions)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'computer_ownerships')
BEGIN
    CREATE TABLE [computer_ownerships] (
        [id]           INT            IDENTITY(1,1) NOT NULL,
        [computer_id]  INT            NOT NULL,
        [user_id]      INT            NOT NULL,
        [access_level] INT            NOT NULL DEFAULT 0,  -- 0=Viewer, 1=Manager, 2=Owner
        [granted_at]   DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        [granted_by]   NVARCHAR(100)  NULL,
        CONSTRAINT [pk_computer_ownerships] PRIMARY KEY CLUSTERED ([id]),
        CONSTRAINT [fk_computer_ownerships_computers] FOREIGN KEY ([computer_id])
            REFERENCES [computers] ([id]) ON DELETE CASCADE,
        CONSTRAINT [fk_computer_ownerships_users] FOREIGN KEY ([user_id])
            REFERENCES [users] ([id]) ON DELETE CASCADE
    );

    -- Each user can only have one ownership record per computer
    CREATE UNIQUE INDEX [ix_computer_ownerships_computer_id_user_id]
        ON [computer_ownerships] ([computer_id], [user_id]);

    -- Fast lookup: "which computers does this user own?"
    CREATE INDEX [ix_computer_ownerships_user_id]
        ON [computer_ownerships] ([user_id]);

    PRINT 'Created computer_ownerships table with indexes';
END
ELSE
BEGIN
    PRINT 'computer_ownerships table already exists — skipping';
END
GO
