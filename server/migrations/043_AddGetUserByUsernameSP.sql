-- ============================================================================
-- Migration 043: Update sp_get_user_by_username
-- Replaces the EF Core compiled query in UserRepository.GetByUsernameAsync()
-- that uses 2 includes (DepartmentEntity, RoleLookup) with a stored procedure.
--
-- Returns a single user row with denormalized department/role names,
-- including password_hash for authentication and is_email_verified for
-- login validation.
-- ============================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_get_user_by_username]
    @username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.[id],
        u.[username],
        u.[email],
        u.[password_hash],
        u.[first_name],
        u.[last_name],
        u.[department_id],
        u.[phone],
        u.[role_id],
        u.[is_active],
        u.[is_email_verified],
        u.[created_at],
        u.[updated_at],
        u.[last_login_at],
        d.[name]  AS [_department_name],
        r.[name]  AS [_role_name]
    FROM [dbo].[users] u
    LEFT  JOIN [dbo].[departments] d ON d.[id] = u.[department_id]
    INNER JOIN [dbo].[user_roles]  r ON r.[id] = u.[role_id]
    WHERE u.[username] = @username
      AND u.[is_deleted] = 0;
END
GO
