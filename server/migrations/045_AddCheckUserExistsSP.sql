-- 045_AddCheckUserExistsSP.sql
-- Combines UsernameExistsAsync + EmailExistsAsync into a single stored procedure call.
-- Either parameter may be NULL to skip that check.

CREATE OR ALTER PROCEDURE [dbo].[sp_check_user_exists]
    @username NVARCHAR(50)  = NULL,
    @email    NVARCHAR(256) = NULL,
    @username_exists BIT OUTPUT,
    @email_exists    BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @username_exists = CASE
        WHEN @username IS NOT NULL
             AND EXISTS (SELECT 1 FROM [dbo].[users] WHERE [username] = @username)
        THEN 1 ELSE 0 END;

    SET @email_exists = CASE
        WHEN @email IS NOT NULL
             AND EXISTS (SELECT 1 FROM [dbo].[users] WHERE [email] = @email)
        THEN 1 ELSE 0 END;
END
GO
