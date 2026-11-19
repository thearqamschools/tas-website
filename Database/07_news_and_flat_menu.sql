-- =============================================================================
-- THE ARQAM SCHOOLS: NEWS & EVENTS, FLAT MAIN MENU, FOOTER "EXPLORE" (3 November 2026)
-- Run after 06. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- What it does:
--  1. Backs up dbo.navigation_items into [archive].navigation_items_20261103.
--  2. Adds three news categories (Achievement, Campus news, Announcement) used by the
--     admin panel's News & Events screen, if they do not exist yet. No posts are added.
--  3. Main menu becomes one flat line: About Us, Academics, Admissions, Campuses,
--     News & Events, Contact. The "Student Life" and "Parents" dropdowns are hidden
--     (is_visible = 0), not deleted; their pages stay reachable from the footer.
--  4. Footer gets an "Explore" column: News & Events, Student life, Gallery,
--     Parent information, Downloads, Frequently asked questions.
--  Nothing is deleted. Every change can be undone in the admin panel (Menus).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'07_news_and_flat_menu')
BEGIN
    PRINT N'07_news_and_flat_menu is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @header UNIQUEIDENTIFIER = (SELECT id FROM dbo.navigation_menus WHERE [key] = N'header');
DECLARE @footer UNIQUEIDENTIFIER = (SELECT id FROM dbo.navigation_menus WHERE [key] = N'footer');
IF @header IS NULL OR @footer IS NULL
    THROW 50007, N'07: the header or footer menu was not found. Nothing was changed.', 1;

IF OBJECT_ID(N'archive.navigation_items_20261103') IS NULL
    SELECT * INTO archive.navigation_items_20261103 FROM dbo.navigation_items;

-- 2. News categories
INSERT INTO dbo.categories ([type], name, slug, sort_order)
SELECT v.[type], v.name, v.slug, v.sort_order
FROM (VALUES (N'news', N'Achievement', N'achievement', 10),
             (N'news', N'Campus news', N'campus', 20),
             (N'news', N'Announcement', N'announcement', 30)) v([type], name, slug, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM dbo.categories c WHERE c.[type] = v.[type] AND c.slug = v.slug);

-- 3. Flat main menu: hide the two dropdown parents, add News & Events and Contact.
UPDATE dbo.navigation_items
SET is_visible = 0, updated_at = SYSDATETIMEOFFSET()
WHERE menu_id = @header AND parent_id IS NULL AND url IN (N'/student-life', N'/parents')
  AND id IN (SELECT DISTINCT parent_id FROM dbo.navigation_items WHERE menu_id = @header AND parent_id IS NOT NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.navigation_items WHERE menu_id = @header AND parent_id IS NULL AND url = N'/news-events')
    INSERT INTO dbo.navigation_items (menu_id, label, url, target, sort_order, is_visible)
    VALUES (@header, N'News & Events', N'/news-events', N'self', 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.navigation_items i LEFT JOIN dbo.pages p ON p.id = i.page_id
               WHERE i.menu_id = @header AND i.parent_id IS NULL AND i.is_visible = 1 AND COALESCE(i.url, p.path) = N'/contact')
    INSERT INTO dbo.navigation_items (menu_id, label, url, target, sort_order, is_visible)
    VALUES (@header, N'Contact', N'/contact', N'self', 6, 1);

-- 4. Footer "Explore" column
IF NOT EXISTS (SELECT 1 FROM dbo.navigation_items WHERE menu_id = @footer AND parent_id IS NULL AND label = N'Explore')
BEGIN
    DECLARE @explore TABLE (id UNIQUEIDENTIFIER);
    INSERT INTO dbo.navigation_items (menu_id, label, url, target, sort_order, is_visible)
    OUTPUT inserted.id INTO @explore
    VALUES (@footer, N'Explore', N'/news-events', N'self', 2, 1);
    DECLARE @exploreId UNIQUEIDENTIFIER = (SELECT id FROM @explore);

    INSERT INTO dbo.navigation_items (menu_id, parent_id, label, url, target, sort_order, is_visible)
    VALUES (@footer, @exploreId, N'News & Events', N'/news-events', N'self', 1, 1),
           (@footer, @exploreId, N'Student life', N'/student-life', N'self', 2, 1),
           (@footer, @exploreId, N'Gallery', N'/gallery', N'self', 3, 1),
           (@footer, @exploreId, N'Parent information', N'/parents', N'self', 4, 1),
           (@footer, @exploreId, N'Downloads', N'/downloads', N'self', 5, 1),
           (@footer, @exploreId, N'Frequently asked questions', N'/faq', N'self', 6, 1);
END

INSERT INTO dbo.schema_migrations (id) VALUES (N'07_news_and_flat_menu');
COMMIT TRANSACTION;
PRINT N'07_news_and_flat_menu applied.';
GO
SET NOEXEC OFF;
GO
