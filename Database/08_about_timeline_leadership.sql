-- =============================================================================
-- THE ARQAM SCHOOLS: ABOUT PAGE "OUR JOURNEY" AND "OUR LEADERSHIP" (3 November 2026)
-- Run after 07. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- What it does:
--  Adds two EMPTY and HIDDEN sections to /about: a timeline ("Our journey": year, title,
--  text) and leadership cards (name, role, message). No years, names or facts are added:
--  the school fills them in the admin panel (Pages > About Us) and then presses
--  "Show on website". Items left empty are not shown.
--  The closing call-to-action band on /about moves below the two new sections
--  (its row is backed up first in [archive].page_sections_20261103).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'08_about_timeline_leadership')
BEGIN
    PRINT N'08_about_timeline_leadership is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @about UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/about' AND deleted_at IS NULL);
IF @about IS NULL
    THROW 50008, N'08: the /about page was not found. Nothing was changed.', 1;

IF OBJECT_ID(N'archive.page_sections_20261103') IS NULL
    SELECT * INTO archive.page_sections_20261103 FROM dbo.page_sections WHERE page_id = @about;

DECLARE @next INT = (SELECT COALESCE(MAX(sort_order), 0) FROM dbo.page_sections WHERE page_id = @about);

IF NOT EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @about AND [type] = N'timeline')
    INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible) VALUES
      (@about, N'timeline',
       N'{"anchor":"journey","tone":"white","heading":"Our journey","intro":"","items":[{"year":"","title":"","text":""},{"year":"","title":"","text":""},{"year":"","title":"","text":""},{"year":"","title":"","text":""},{"year":"","title":"","text":""},{"year":"","title":"","text":""}]}',
       @next + 10, 0);

IF NOT EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @about AND [type] = N'leadership')
    INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible) VALUES
      (@about, N'leadership',
       N'{"anchor":"leadership","tone":"grey","heading":"Our leadership","intro":"","items":[{"name":"","role":"","message":""},{"name":"","role":"","message":""},{"name":"","role":"","message":""},{"name":"","role":"","message":""}]}',
       @next + 20, 0);

-- Keep the call-to-action band last on the page (only when it was the last section).
UPDATE dbo.page_sections
SET sort_order = @next + 30, updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'cta_band' AND sort_order = @next;

INSERT INTO dbo.schema_migrations (id) VALUES (N'08_about_timeline_leadership');
COMMIT TRANSACTION;
PRINT N'08_about_timeline_leadership applied.';
GO
SET NOEXEC OFF;
GO
