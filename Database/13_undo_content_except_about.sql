-- =============================================================================
-- THE ARQAM SCHOOLS: UNDO THE 6 NOVEMBER CONTENT UPDATE, EXCEPT ABOUT US (6 November 2026)
-- Run after 12. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- The owner asked to reverse the content v1.4 update everywhere except the About Us page.
--  1. Academics, Admissions, FAQ and Student life: every section, and the page titles and
--     summaries, as they were before 12 (from [archive].*_20261106). Sections that 12 added
--     on those pages are hidden.
--  2. Head offices, website settings (footer text and call-to-action band, description,
--     social links), search titles and descriptions (except About) and the stored homepage
--     content: as before 12. The new head office columns stay, empty.
--  3. About Us keeps the approved text from 12 but in the earlier layout: the Mission, Vision
--     and Values cards (text from the approved document, without the Arabic motto), then
--     Core Values, the journey, Founder, Leadership, Name and its significance, Our Basic Idea
--     and the closing band.
--  Changed rows are backed up in [archive].*_20261106b. Nothing is deleted.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'13_undo_content_except_about')
BEGIN
    PRINT N'13_undo_content_except_about is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'12_approved_content_v1_4')
   OR OBJECT_ID(N'archive.page_sections_20261106') IS NULL OR OBJECT_ID(N'archive.site_settings_20261106') IS NULL
   OR OBJECT_ID(N'archive.head_offices_20261106') IS NULL OR OBJECT_ID(N'archive.pages_20261106') IS NULL
   OR OBJECT_ID(N'archive.seo_metadata_20261106') IS NULL OR OBJECT_ID(N'archive.homepage_content_20261106') IS NULL
    THROW 50018, N'13: script 12 or its backups were not found. Nothing was changed.', 1;

DECLARE @about UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/about' AND deleted_at IS NULL);
DECLARE @others TABLE (id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @others SELECT id FROM dbo.pages WHERE path IN (N'/academics', N'/admissions', N'/faq', N'/student-life') AND deleted_at IS NULL;
IF EXISTS (SELECT 1 FROM dbo.page_sections WHERE draft_data IS NOT NULL AND (page_id = @about OR page_id IN (SELECT id FROM @others)))
    THROW 50019, N'13: a section on About, Academics, Admissions, FAQ or Student life has a waiting draft. Publish or discard it in the admin panel, then run 13 again. Nothing was changed.', 1;

-- Backups of the current state
IF OBJECT_ID(N'archive.page_sections_20261106b') IS NULL
    SELECT * INTO archive.page_sections_20261106b FROM dbo.page_sections WHERE page_id = @about OR page_id IN (SELECT id FROM @others);
IF OBJECT_ID(N'archive.pages_20261106b') IS NULL SELECT * INTO archive.pages_20261106b FROM dbo.pages WHERE id IN (SELECT id FROM @others);
IF OBJECT_ID(N'archive.head_offices_20261106b') IS NULL SELECT * INTO archive.head_offices_20261106b FROM dbo.head_offices;
IF OBJECT_ID(N'archive.site_settings_20261106b') IS NULL SELECT * INTO archive.site_settings_20261106b FROM dbo.site_settings;
IF OBJECT_ID(N'archive.seo_metadata_20261106b') IS NULL SELECT * INTO archive.seo_metadata_20261106b FROM dbo.seo_metadata;
IF OBJECT_ID(N'archive.homepage_content_20261106b') IS NULL SELECT * INTO archive.homepage_content_20261106b FROM dbo.homepage_content;

-- 1. Academics, Admissions, FAQ, Student life as before 12
UPDATE s SET data = a.data, draft_data = a.draft_data, is_visible = a.is_visible, sort_order = a.sort_order, updated_at = SYSDATETIMEOFFSET()
FROM dbo.page_sections s JOIN archive.page_sections_20261106 a ON a.id = s.id
WHERE s.page_id IN (SELECT id FROM @others);
UPDATE s SET is_visible = 0, updated_at = SYSDATETIMEOFFSET()
FROM dbo.page_sections s
WHERE s.page_id IN (SELECT id FROM @others) AND NOT EXISTS (SELECT 1 FROM archive.page_sections_20261106 a WHERE a.id = s.id);
UPDATE p SET title = a.title, summary = a.summary, updated_at = SYSDATETIMEOFFSET()
FROM dbo.pages p JOIN archive.pages_20261106 a ON a.id = p.id
WHERE p.id IN (SELECT id FROM @others);

-- 2. Head offices, settings, search text, stored homepage content as before 12
-- office_number is unique: move both out of the way first, then put back the earlier numbers.
UPDATE h SET office_number = CASE WHEN h.office_number = 1 THEN 8 ELSE 9 END FROM dbo.head_offices h WHERE h.id IN (SELECT id FROM archive.head_offices_20261106);
UPDATE h SET office_number = a.office_number, name = a.name, city = a.city, address = a.address, phone = a.phone, phone_alt = a.phone_alt,
       whatsapp = a.whatsapp, email = a.email, google_maps_url = a.google_maps_url, hours = a.hours, is_published = a.is_published,
       mobile = NULL, facebook_url = NULL, updated_at = SYSDATETIMEOFFSET()
FROM dbo.head_offices h JOIN archive.head_offices_20261106 a ON a.id = h.id;
UPDATE s SET [value] = a.[value], updated_at = SYSDATETIMEOFFSET()
FROM dbo.site_settings s JOIN archive.site_settings_20261106 a ON a.[key] = s.[key]
WHERE s.[value] <> a.[value] OR (s.[value] IS NULL AND a.[value] IS NOT NULL);
UPDATE m SET meta_title = a.meta_title, meta_description = a.meta_description, og_description = a.og_description, updated_at = SYSDATETIMEOFFSET()
FROM dbo.seo_metadata m JOIN archive.seo_metadata_20261106 a ON a.id = m.id
WHERE m.id <> COALESCE((SELECT seo_id FROM dbo.pages WHERE id = @about), '00000000-0000-0000-0000-000000000000');
UPDATE h SET data = a.data, updated_at = SYSDATETIMEOFFSET()
FROM dbo.homepage_content h JOIN archive.homepage_content_20261106 a ON a.id = h.id;

-- 3. About Us: approved text, earlier layout
IF @about IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @about AND JSON_VALUE(data, '$.anchor') = N'mission-vision-values')
        INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible) VALUES (@about, N'card_grid', N'{"anchor":"mission-vision-values","heading":"Our Mission, Vision and Values","tone":"white","columns":3,"items":[{"icon":"target","title":"Our Mission","summary":"To earn the pleasure of Allah through education, training and service, by giving every child a quality modern education within the framework of the Quran and Sunnah, and by building the discipline, confidence and noble character they need to lead responsibly in every walk of life."},{"icon":"eye","title":"Our Vision","summary":"To raise a generation that excels in knowledge and character, and succeeds in this world and the Hereafter."},{"icon":"heart","title":"Our Values","summary":"We believe in citizenship, commitment, leadership, respect, excellence and responsibility."}]}', 2, 1);
    ELSE
        UPDATE dbo.page_sections SET data = N'{"anchor":"mission-vision-values","heading":"Our Mission, Vision and Values","tone":"white","columns":3,"items":[{"icon":"target","title":"Our Mission","summary":"To earn the pleasure of Allah through education, training and service, by giving every child a quality modern education within the framework of the Quran and Sunnah, and by building the discipline, confidence and noble character they need to lead responsibly in every walk of life."},{"icon":"eye","title":"Our Vision","summary":"To raise a generation that excels in knowledge and character, and succeeds in this world and the Hereafter."},{"icon":"heart","title":"Our Values","summary":"We believe in citizenship, commitment, leadership, respect, excellence and responsibility."}]}', updated_at = SYSDATETIMEOFFSET()
        WHERE page_id = @about AND JSON_VALUE(data, '$.anchor') = N'mission-vision-values';
    UPDATE dbo.page_sections SET is_visible = 0, updated_at = SYSDATETIMEOFFSET() WHERE page_id = @about AND [type] = N'vision_mission';
    UPDATE x SET is_visible = 1, sort_order = v.s, updated_at = SYSDATETIMEOFFSET()
    FROM dbo.page_sections x
    JOIN (VALUES (N'who-we-are', 1), (N'mission-vision-values', 2), (N'core-values', 3), (N'journey', 4), (N'founder', 5),
                 (N'leadership', 6), (N'name', 7), (N'basic-idea', 8)) v(a, s) ON JSON_VALUE(x.data, '$.anchor') = v.a
    WHERE x.page_id = @about;
    UPDATE dbo.page_sections SET is_visible = 1, sort_order = 9, updated_at = SYSDATETIMEOFFSET() WHERE page_id = @about AND [type] = N'cta_band';
    -- Leadership intro: the photo spaces now say "Photo to be added" on each card.
    UPDATE dbo.page_sections SET data = JSON_MODIFY(data, '$.intro', N''), updated_at = SYSDATETIMEOFFSET()
    WHERE page_id = @about AND [type] = N'leadership' AND JSON_VALUE(data, '$.intro') = N'Leadership messages and photos will be added soon.';
END

INSERT INTO dbo.schema_migrations (id) VALUES (N'13_undo_content_except_about');
COMMIT TRANSACTION;
PRINT N'13_undo_content_except_about applied.';
GO
SET NOEXEC OFF;
GO
