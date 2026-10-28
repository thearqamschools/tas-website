-- =============================================================================
-- THE ARQAM SCHOOLS: FRANCHISE BUTTON IN THE HEADER (28 October 2026)
-- Run after 04. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- What it does:
--  1. Backs up the rows it changes into [archive] (*_20261028 tables).
--  2. The red header button (setting header.cta_apply) now says "Franchise"
--     and links to /franchise, in place of "Admissions".
--     Admissions stays in the main menu as a direct link.
--  3. The "Franchise" item in the main menu is hidden (is_visible = 0), not
--     deleted, so it does not appear twice. Show it again from the Menus editor.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'05_franchise_header_button')
BEGIN
    PRINT N'05_franchise_header_button is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

SELECT * INTO archive.navigation_items_20261028 FROM dbo.navigation_items;
SELECT * INTO archive.site_settings_20261028 FROM dbo.site_settings WHERE [key] LIKE N'header.%';

UPDATE dbo.site_settings
SET [value] = N'{"href":"/franchise","label":"Franchise"}', updated_at = SYSDATETIMEOFFSET()
WHERE [key] = N'header.cta_apply';

UPDATE n
SET n.is_visible = 0, n.updated_at = SYSDATETIMEOFFSET()
FROM dbo.navigation_items n
JOIN dbo.navigation_menus m ON m.id = n.menu_id
WHERE m.[key] = N'header' AND n.parent_id IS NULL AND n.url = N'/franchise';

INSERT INTO dbo.schema_migrations (id) VALUES (N'05_franchise_header_button');
COMMIT TRANSACTION;
PRINT N'05_franchise_header_button applied.';
GO
SET NOEXEC OFF;
GO
