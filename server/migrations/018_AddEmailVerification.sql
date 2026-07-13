-- Migration: Add email verification columns to users table
-- Users must verify their email address before they can log in.

ALTER TABLE [users] ADD
    [is_email_verified]                BIT           NOT NULL DEFAULT 0,
    [email_verification_token]         NVARCHAR(128) NULL,
    [email_verification_token_expiry]  DATETIME2     NULL;
GO

-- Mark all existing users as verified (they already have accounts)
UPDATE [users] SET [is_email_verified] = 1;
