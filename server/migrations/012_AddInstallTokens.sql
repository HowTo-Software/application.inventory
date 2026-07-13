-- ============================================================
-- Migration 012: Add InstallTokens table
-- Short-lived, single-use tokens for secure install script access.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[install_tokens]') AND type = 'U')
BEGIN
    CREATE TABLE [install_tokens] (
        [id]          INT            IDENTITY(1,1) NOT NULL,
        [token_hash]  NVARCHAR(128)  NOT NULL,
        [expires_at]  DATETIME2      NOT NULL,
        [created_at]  DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [consumed_at] DATETIME2      NULL,
        [created_by]  NVARCHAR(256)  NOT NULL,
        CONSTRAINT [pk_install_tokens] PRIMARY KEY CLUSTERED ([id]),
    );

    CREATE INDEX [ix_install_tokens_token_hash] ON [install_tokens] ([token_hash]);

    PRINT 'Created install_tokens table';
END
ELSE
BEGIN
    PRINT 'install_tokens table already exists — skipping';
END
GO
