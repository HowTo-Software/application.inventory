-- Migration: Add invite_codes table for gating user registration
-- Admins generate invite codes; users must provide a valid code to register.

CREATE TABLE [invite_codes] (
    [id]          INT            IDENTITY(1,1) NOT NULL,
    [code_hash]   NVARCHAR(128)  NOT NULL,
    [expires_at]  DATETIME2      NOT NULL,
    [created_at]  DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    [max_uses]    INT            NOT NULL DEFAULT 1,
    [use_count]   INT            NOT NULL DEFAULT 0,
    [created_by]  NVARCHAR(256)  NOT NULL,
    [note]        NVARCHAR(500)  NULL,
    [is_revoked]  BIT            NOT NULL DEFAULT 0,
    CONSTRAINT [pk_invite_codes] PRIMARY KEY CLUSTERED ([id])
);

CREATE INDEX [ix_invite_codes_code_hash] ON [invite_codes] ([code_hash]);
