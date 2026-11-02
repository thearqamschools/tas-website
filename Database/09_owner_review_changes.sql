-- =============================================================================
-- THE ARQAM SCHOOLS: OWNER'S REVIEW OF THE TESTING SITE (3 November 2026)
-- Run after 08. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- What it does (nothing is deleted; hidden rows can be shown again in the admin panel):
--  1. Backs up the changed rows into [archive].*_20261104.
--  2. Menus: "Home" first in the main menu; "Contact" hidden in the top bar; "Student life"
--     and "Parent information" hidden in the footer's Explore column.
--  3. Social links: the Lahore Facebook page (TASHeadOffice) is replaced by the Faisalabad
--     page (thearqamfsdOfficial), as the owner asked.
--  4. About: "One network, one purpose" becomes the page heading; Mission, Vision and Values
--     as three cards (text taken from the existing sections); "Educational philosophy",
--     "Why choose" and "Islamic values and character building" hidden; journey and leadership
--     switched on (they show only once the school fills them in).
--  5. Academics: academic levels as tabs, then "What Sets Us Apart" (from the existing
--     teaching methodology text), then Student development; the two sections above hidden.
--  6. Admissions: process first, then requirements, classes, inquiry form, why choose us, FAQ;
--     an empty "ages" field per stage for the school to fill in.
--  7. Franchise: "Contact the Head Office".
--  8. Privacy Policy published and Terms of Service added (marked as under review).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'09_owner_review_changes')
BEGIN
    PRINT N'09_owner_review_changes is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @header UNIQUEIDENTIFIER = (SELECT id FROM dbo.navigation_menus WHERE [key] = N'header');
DECLARE @utility UNIQUEIDENTIFIER = (SELECT id FROM dbo.navigation_menus WHERE [key] = N'utility');
DECLARE @footer UNIQUEIDENTIFIER = (SELECT id FROM dbo.navigation_menus WHERE [key] = N'footer');
DECLARE @about UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/about' AND deleted_at IS NULL);
DECLARE @academics UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/academics' AND deleted_at IS NULL);
DECLARE @admissions UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/admissions' AND deleted_at IS NULL);
DECLARE @franchise UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/franchise' AND deleted_at IS NULL);
IF @header IS NULL OR @footer IS NULL OR @about IS NULL OR @academics IS NULL OR @admissions IS NULL
    THROW 50009, N'09: a menu or page that this script changes was not found. Nothing was changed.', 1;

-- 1. Backups
IF OBJECT_ID(N'archive.navigation_items_20261104') IS NULL
    SELECT * INTO archive.navigation_items_20261104 FROM dbo.navigation_items;
IF OBJECT_ID(N'archive.page_sections_20261104') IS NULL
    SELECT * INTO archive.page_sections_20261104 FROM dbo.page_sections WHERE page_id IN (@about, @academics, @admissions, @franchise);
IF OBJECT_ID(N'archive.pages_20261104') IS NULL
    SELECT * INTO archive.pages_20261104 FROM dbo.pages WHERE path IN (N'/privacy', N'/terms');
IF OBJECT_ID(N'archive.site_settings_20261104') IS NULL
    SELECT * INTO archive.site_settings_20261104 FROM dbo.site_settings WHERE [key] = N'social';

-- 2. Menus
IF NOT EXISTS (SELECT 1 FROM dbo.navigation_items WHERE menu_id = @header AND parent_id IS NULL AND url = N'/')
    INSERT INTO dbo.navigation_items (menu_id, label, url, target, sort_order, is_visible)
    VALUES (@header, N'Home', N'/', N'self', 0, 1);

IF @utility IS NOT NULL
    UPDATE i SET is_visible = 0, updated_at = SYSDATETIMEOFFSET()
    FROM dbo.navigation_items i LEFT JOIN dbo.pages p ON p.id = i.page_id
    WHERE i.menu_id = @utility AND i.is_visible = 1 AND COALESCE(i.url, p.path) = N'/contact';

UPDATE c SET is_visible = 0, updated_at = SYSDATETIMEOFFSET()
FROM dbo.navigation_items c JOIN dbo.navigation_items g ON g.id = c.parent_id
WHERE c.menu_id = @footer AND g.label = N'Explore' AND c.url IN (N'/student-life', N'/parents') AND c.is_visible = 1;

-- 3. Social links: Faisalabad Facebook page instead of the Lahore one
UPDATE dbo.site_settings
SET [value] = REPLACE(REPLACE([value], N'https://www.facebook.com/TASHeadOffice/', N'https://www.facebook.com/thearqamfsdOfficial'),
                      N'https://www.facebook.com/TASHeadOffice', N'https://www.facebook.com/thearqamfsdOfficial'),
    updated_at = SYSDATETIMEOFFSET()
WHERE [key] = N'social' AND [value] LIKE N'%facebook.com/TASHeadOffice%';

-- 4. About
UPDATE dbo.page_sections SET data = JSON_MODIFY(data, '$.page_intro', CAST(1 AS BIT)), updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'split_feature' AND JSON_VALUE(data, '$.anchor') = N'who-we-are';

UPDATE dbo.page_sections SET is_visible = 0, updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND is_visible = 1
  AND ([type] = N'vision_mission'
       OR ([type] = N'split_feature' AND JSON_VALUE(data, '$.anchor') = N'philosophy')
       OR ([type] = N'card_grid' AND JSON_VALUE(data, '$.anchor') = N'why-choose-us')
       OR ([type] = N'values' AND JSON_VALUE(data, '$.anchor') = N'values'));

IF NOT EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @about AND JSON_VALUE(data, '$.anchor') = N'mission-vision-values')
    INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible)
    VALUES (@about, N'card_grid', N'{"anchor":"mission-vision-values","heading":"Our Mission, Vision and Values","tone":"white","columns":3,"items":[{"icon":"target","title":"Our Mission","summary":"Every campus of The Arqam Schools works to provide quality modern education, teach the Holy Quran and Islamic studies, develop discipline, honesty, good manners and responsibility, give children chances to speak, create, take part and lead, and keep a close partnership between home and school."},{"icon":"eye","title":"Our Vision","summary":"To raise a generation who combine knowledge with values: confident, capable and caring young Muslims who lead with good character, serve their families, their communities and Pakistan, and succeed in this world and the Hereafter."},{"icon":"heart","title":"Our Values","summary":"Faith and worship, honesty, respect and good manners, discipline and responsibility guide everyday life at our schools, so that children practise good character at school and at home."}]}', 2, 1);

UPDATE dbo.page_sections SET is_visible = 1, updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] IN (N'timeline', N'leadership') AND is_visible = 0;

-- 5. Academics
UPDATE dbo.page_sections SET is_visible = 0, updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @academics AND is_visible = 1
  AND (([type] = N'split_feature' AND JSON_VALUE(data, '$.anchor') = N'excellence')
       OR ([type] = N'card_grid' AND JSON_VALUE(data, '$.anchor') = N'teaching'));

UPDATE s SET
    data = JSON_MODIFY(JSON_MODIFY(s.data, '$.layout', N'tabs'), '$.items', JSON_QUERY(
        N'[' + (SELECT STRING_AGG(CAST(
                    JSON_MODIFY(JSON_MODIFY(j.[value], '$.ages', COALESCE(JSON_VALUE(j.[value], '$.ages'), N'')),
                                '$.icon', COALESCE(JSON_VALUE(j.[value], '$.icon'),
                                    CASE JSON_VALUE(j.[value], '$.stage') WHEN N'Early Years' THEN N'smile' WHEN N'Primary' THEN N'pencil'
                                         WHEN N'Middle' THEN N'flask' WHEN N'Matric' THEN N'graduation-cap' WHEN N'Hifz' THEN N'moon-star'
                                         ELSE N'book-open' END)) AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT))
                FROM OPENJSON(s.data, '$.items') j) + N']')),
    sort_order = 10, updated_at = SYSDATETIMEOFFSET()
FROM dbo.page_sections s
WHERE s.page_id = @academics AND s.[type] = N'academic_levels' AND JSON_VALUE(s.data, '$.layout') IS NULL
  AND JSON_QUERY(s.data, '$.items') IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @academics AND JSON_VALUE(data, '$.anchor') = N'what-sets-us-apart')
    INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible)
    VALUES (@academics, N'card_grid', N'{"anchor":"what-sets-us-apart","heading":"What Sets Us Apart","tone":"grey","columns":4,"intro":"Lessons follow a simple rhythm that children come to know and trust: explain clearly, practise regularly, check understanding and give feedback.","items":[{"icon":"book-open","title":"Clear Explanation","summary":"New ideas are introduced step by step, in calm and orderly classrooms."},{"icon":"sparkles","title":"Active, Hands-on Learning","summary":"Discussion, group work, activities and projects bring lessons to life."},{"icon":"pencil","title":"Practice and Feedback","summary":"Classwork and homework help children remember; feedback shows them how to improve."},{"icon":"heart","title":"Values in Every Lesson","summary":"Teachers model honesty, respect and good manners as they teach."}]}', 20, 1);

UPDATE dbo.page_sections SET sort_order = 30, updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @academics AND [type] = N'values' AND JSON_VALUE(data, '$.anchor') = N'development';
UPDATE dbo.page_sections SET sort_order = 40, updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @academics AND [type] = N'cta_band';

-- 6. Admissions: new order, and an "ages" field for each stage in the classes list
UPDATE dbo.page_sections SET sort_order =
    CASE WHEN [type] = N'page_header' THEN 1
         WHEN [type] = N'process_steps' THEN 2
         WHEN [type] = N'info_list' THEN 3
         WHEN [type] = N'academic_levels' THEN 4
         WHEN [type] = N'admission_inquiry' THEN 5
         WHEN [type] = N'card_grid' AND JSON_VALUE(data, '$.anchor') = N'why-choose-us' THEN 6
         WHEN [type] = N'faq' THEN 7
         ELSE sort_order + 10 END,
    updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @admissions;

UPDATE s SET
    data = JSON_MODIFY(s.data, '$.items', JSON_QUERY(
        N'[' + (SELECT STRING_AGG(CAST(JSON_MODIFY(j.[value], '$.ages', COALESCE(JSON_VALUE(j.[value], '$.ages'), N'')) AS NVARCHAR(MAX)), N',')
                       WITHIN GROUP (ORDER BY CAST(j.[key] AS INT))
                FROM OPENJSON(s.data, '$.items') j) + N']')),
    updated_at = SYSDATETIMEOFFSET()
FROM dbo.page_sections s
WHERE s.page_id = @admissions AND s.[type] = N'academic_levels' AND JSON_QUERY(s.data, '$.items') IS NOT NULL;

-- 7. Franchise button text
IF @franchise IS NOT NULL
    UPDATE dbo.page_sections SET data = JSON_MODIFY(data, '$.secondary_cta.label', N'Contact the Head Office'), updated_at = SYSDATETIMEOFFSET()
    WHERE page_id = @franchise AND [type] = N'cta_band' AND JSON_VALUE(data, '$.secondary_cta.label') = N'Contact the head office';

-- 8. Privacy Policy and Terms of Service (linked from the footer)
DECLARE @privacy UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/privacy' AND deleted_at IS NULL);
IF @privacy IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.pages WHERE id = @privacy AND status = N'draft')
BEGIN
    UPDATE dbo.pages SET title = N'Privacy Policy', status = N'published', published_at = COALESCE(published_at, SYSDATETIMEOFFSET()), updated_at = SYSDATETIMEOFFSET()
    WHERE id = @privacy;
    UPDATE dbo.page_sections SET data = N'{"body":{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"This page is being reviewed by The Arqam Schools and may change."}]},{"type":"paragraph","content":[{"type":"text","text":"This notice explains how the website of The Arqam Schools uses the information you send us."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"What we collect"}]},{"type":"bulletList","content":[{"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Admission inquiries: the parent or guardian''s name, phone number, email address (if given), the child''s details, the campus and your message."}]}]},{"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Contact messages: your name, phone number or email address, and your message."}]}]},{"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Franchise applications: the details you enter about yourself and the proposed location."}]}]}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"How we use it"}]},{"type":"paragraph","content":[{"type":"text","text":"We use this information only to reply to you, to process admission inquiries and franchise applications, and to run our schools. We do not sell it, and we do not use it for advertising."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Who can see it"}]},{"type":"paragraph","content":[{"type":"text","text":"Only staff who need it for their work can see it, for example the admissions team of the campus you chose."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"How long we keep it"}]},{"type":"paragraph","content":[{"type":"text","text":"We keep information only for as long as it is needed for these purposes. When it is no longer needed, it is deleted."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Your choices"}]},{"type":"paragraph","content":[{"type":"text","text":"To ask about, correct or remove your information, contact us on 041-2660265 or 041-2406479, or write to us at Sanora Colony, Samanabad, Faisalabad."}]}]}}', updated_at = SYSDATETIMEOFFSET()
    WHERE page_id = @privacy AND [type] = N'rich_text';
END

IF NOT EXISTS (SELECT 1 FROM dbo.pages WHERE path = N'/terms' AND deleted_at IS NULL)
BEGIN
    DECLARE @terms TABLE (id UNIQUEIDENTIFIER);
    INSERT INTO dbo.pages (title, slug, path, template, summary, status, published_at)
    OUTPUT inserted.id INTO @terms
    VALUES (N'Terms of Service', N'terms', N'/terms', N'default', N'The terms for using the website of The Arqam Schools.', N'published', SYSDATETIMEOFFSET());
    INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible)
    SELECT id, N'rich_text', N'{"body":{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"This page is being reviewed by The Arqam Schools and may change."}]},{"type":"paragraph","content":[{"type":"text","text":"These terms apply to the use of the website of The Arqam Schools. By using the website, you agree to them."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Information on this website"}]},{"type":"paragraph","content":[{"type":"text","text":"We try to keep the information on this website correct and up to date. Classes, admissions and other details can vary by campus and can change, so please confirm them with the campus before making a decision."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Forms"}]},{"type":"paragraph","content":[{"type":"text","text":"When you send an admission inquiry, a message or a franchise application, please give correct information. How we use it is explained in our Privacy Policy."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Name, logo and content"}]},{"type":"paragraph","content":[{"type":"text","text":"The name The Arqam Schools, the logo, and the text and pictures on this website belong to The Arqam Schools. Please do not copy or use them without our written permission."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Links to other websites"}]},{"type":"paragraph","content":[{"type":"text","text":"This website links to other websites, such as our social media pages and Google Maps. We are not responsible for the content of other websites."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Changes to these terms"}]},{"type":"paragraph","content":[{"type":"text","text":"We may update these terms from time to time. The latest version is always on this page."}]},{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Contact"}]},{"type":"paragraph","content":[{"type":"text","text":"For any question about these terms, contact us on 041-2660265 or 041-2406479, or WhatsApp 0339-2406479."}]}]}}', 1, 1 FROM @terms;
END

INSERT INTO dbo.schema_migrations (id) VALUES (N'09_owner_review_changes');
COMMIT TRANSACTION;
PRINT N'09_owner_review_changes applied.';
GO
SET NOEXEC OFF;
GO
