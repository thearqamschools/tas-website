-- =============================================================================
-- THE ARQAM SCHOOLS: VENUE FOR NEWS POSTS (11 November 2026)
-- Run after 17. Safe to run twice: it records itself in dbo.schema_migrations.
-- Adds dbo.news.venue (optional, like dbo.events.venue) so news posts can show where
-- they happened. The news date is the existing news.published_at, now editable in the
-- admin panel. No existing rows are changed and nothing is deleted.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'18_news_venue')
BEGIN
    PRINT N'18_news_venue is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.news', N'venue') IS NULL
    ALTER TABLE dbo.news ADD [venue] NVARCHAR(200) NULL;

INSERT INTO dbo.schema_migrations (id) VALUES (N'18_news_venue');
COMMIT TRANSACTION;
PRINT N'18_news_venue applied.';
GO
SET NOEXEC OFF;
GO
