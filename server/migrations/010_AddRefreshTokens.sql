-- Migration: 010_AddRefreshTokens.sql
-- Adds refresh_tokens table for secure token rotation
-- Note: EF Core uses snake_case naming convention (EFCore.NamingConventions)

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'refresh_tokens')
BEGIN
    CREATE TABLE refresh_tokens (
        id          INT IDENTITY(1,1) PRIMARY KEY,
        user_id     INT             NOT NULL,
        token_hash  NVARCHAR(128)   NOT NULL,
        expires_at  DATETIME2       NOT NULL,
        created_at  DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        consumed_at DATETIME2       NULL,
        revoked_at  DATETIME2       NULL,
        family      NVARCHAR(64)    NOT NULL,

        CONSTRAINT fk_refresh_tokens_users_user_id FOREIGN KEY (user_id)
            REFERENCES users(id) ON DELETE CASCADE
    );

    CREATE INDEX ix_refresh_tokens_token_hash ON refresh_tokens(token_hash);
    CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens(user_id);
    CREATE INDEX ix_refresh_tokens_family ON refresh_tokens(family);
END
