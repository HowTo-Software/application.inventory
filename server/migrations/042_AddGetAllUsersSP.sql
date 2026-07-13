-- ============================================================================
-- Migration 042: Add sp_get_all_users
-- Replaces the EF Core LINQ query in UserRepository.GetAllAsync()
-- that uses 2 includes (DepartmentEntity, RoleLookup) with a stored
-- procedure that does server-side search, sorting, pagination, and
-- denormalized JOINs.
--
-- Returns two result sets:
--   1) Total count (INT) — filtered by search term + soft-delete
--   2) Paged user records with denormalized department/role names
-- ============================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_get_all_users]
    @search_term    NVARCHAR(100)   = NULL,
    @sort_by        NVARCHAR(50)    = NULL,
    @sort_direction NVARCHAR(4)     = 'ASC',
    @page           INT             = 1,
    @page_size      INT             = 25
AS
BEGIN
    SET NOCOUNT ON;

    -- Normalize search term for LIKE matching
    DECLARE @term NVARCHAR(102) = NULL;
    IF @search_term IS NOT NULL AND LEN(LTRIM(RTRIM(@search_term))) > 0
        SET @term = N'%' + LTRIM(RTRIM(@search_term)) + N'%';

    -- ── Count ───────────────────────────────────────────────────────────
    SELECT COUNT(*) AS [total_count]
    FROM [dbo].[users] u
    LEFT JOIN [dbo].[departments] d ON d.[id] = u.[department_id]
    WHERE u.[is_deleted] = 0
      AND (@term IS NULL
           OR u.[username]   LIKE @term
           OR u.[email]      LIKE @term
           OR u.[first_name] LIKE @term
           OR u.[last_name]  LIKE @term
           OR d.[name]       LIKE @term);

    -- ── Paged data ──────────────────────────────────────────────────────
    SELECT
        u.[id],
        u.[username],
        u.[email],
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
        -- Denormalized names for in-memory navigation property hydration
        d.[name]  AS [_department_name],
        r.[name]  AS [_role_name]
    FROM [dbo].[users] u
    LEFT  JOIN [dbo].[departments] d  ON d.[id] = u.[department_id]
    INNER JOIN [dbo].[user_roles]  r  ON r.[id] = u.[role_id]
    WHERE u.[is_deleted] = 0
      AND (@term IS NULL
           OR u.[username]   LIKE @term
           OR u.[email]      LIKE @term
           OR u.[first_name] LIKE @term
           OR u.[last_name]  LIKE @term
           OR d.[name]       LIKE @term)
    ORDER BY
        -- String sorts
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'username'   THEN u.[username]
                WHEN 'email'      THEN u.[email]
                WHEN 'firstname'  THEN u.[first_name]
                WHEN 'lastname'   THEN u.[last_name]
                WHEN 'fullname'   THEN u.[last_name]
                WHEN 'role'       THEN r.[name]
                WHEN 'department' THEN d.[name]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'username'   THEN u.[username]
                WHEN 'email'      THEN u.[email]
                WHEN 'firstname'  THEN u.[first_name]
                WHEN 'lastname'   THEN u.[last_name]
                WHEN 'fullname'   THEN u.[last_name]
                WHEN 'role'       THEN r.[name]
                WHEN 'department' THEN d.[name]
            END
        END DESC,
        -- Date sorts
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'createdat'   THEN u.[created_at]
                WHEN 'lastloginat' THEN u.[last_login_at]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'createdat'   THEN u.[created_at]
                WHEN 'lastloginat' THEN u.[last_login_at]
            END
        END DESC,
        -- Bit sorts
        CASE WHEN @sort_direction = 'ASC' THEN
            CASE @sort_by
                WHEN 'isactive' THEN u.[is_active]
            END
        END ASC,
        CASE WHEN @sort_direction = 'DESC' THEN
            CASE @sort_by
                WHEN 'isactive' THEN u.[is_active]
            END
        END DESC,
        u.[last_name] ASC  -- default tiebreaker
    OFFSET (@page - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO
