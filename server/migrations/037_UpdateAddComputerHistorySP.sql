-- Migration 037: Update sp_add_computer_history to include old_value and new_value columns
-- Previously only inserted computer_id, action, notes, changed_by, changed_at, created_at.
-- Now also inserts old_value and new_value for full history tracking.

CREATE OR ALTER PROCEDURE [dbo].[sp_add_computer_history]
    @computer_id INT,
    @action      NVARCHAR(100),
    @changed_by  NVARCHAR(100) = NULL,
    @old_value   NVARCHAR(500) = NULL,
    @new_value   NVARCHAR(500) = NULL,
    @notes       NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[computer_histories]
        ([computer_id], [action], [old_value], [new_value], [notes], [changed_by], [changed_at], [created_at])
    VALUES
        (@computer_id, @action, @old_value, @new_value, @notes, @changed_by, SYSUTCDATETIME(), SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS [id];
END
GO
