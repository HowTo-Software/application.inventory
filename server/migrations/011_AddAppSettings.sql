-- Migration: Add AppSettings table for persisting application state (e.g., last cleanup timestamp)

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppSettings')
BEGIN
    CREATE TABLE [AppSettings] (
        [Key]       NVARCHAR(128)  NOT NULL,
        [Value]     NVARCHAR(1024) NOT NULL,
        [UpdatedAt] DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_AppSettings] PRIMARY KEY ([Key])
    );

    PRINT 'Created table: AppSettings';
END
ELSE
BEGIN
    PRINT 'Table AppSettings already exists — skipping.';
END
