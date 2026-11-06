-- =============================================================================
-- THE ARQAM SCHOOLS: OWNER'S NOTES ON ABOUT US AND SAMPLE NEWS (6 November 2026)
-- Run after 13. Safe to run twice: it records itself in dbo.schema_migrations.
--
--  1. About Us:
--     - "Our Journey" is the page heading (no "About Us" above it); first sentence without
--       "in Faisalabad, with a single campus"; "Today," without the year.
--     - Two cards: Our Vision (with the Hasana seal) and Our Mission; the separate values
--       card is hidden (Core Values follow below).
--     - Core Values show the Core Values badge in the centre.
--     - The separate "Our journey" section lower down is hidden.
--  2. News & Events: five posts marked "Sample:" (three news, two events) so the owner can see
--     the layout. They say they are samples; archive them in the admin panel when real news exists.
--  Changed rows are backed up in [archive].page_sections_20261106c. Nothing is deleted.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'14_about_notes_and_sample_news')
BEGIN
    PRINT N'14_about_notes_and_sample_news is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @about UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/about' AND deleted_at IS NULL);
IF @about IS NULL
    THROW 50020, N'14: the /about page was not found. Nothing was changed.', 1;
IF EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @about AND draft_data IS NOT NULL)
    THROW 50021, N'14: a section on About Us has a waiting draft. Publish or discard it in the admin panel, then run 14 again. Nothing was changed.', 1;
IF OBJECT_ID(N'archive.page_sections_20261106c') IS NULL
    SELECT * INTO archive.page_sections_20261106c FROM dbo.page_sections WHERE page_id = @about;

-- 1. About Us
UPDATE dbo.page_sections
SET data = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(data, '$.eyebrow', N''), '$.heading', N'Our Journey'), '$.body', JSON_QUERY(N'["The Arqam Schools began in 1993 with a clear purpose: to give children a sound modern education rooted in the Quran and Sunnah, and to earn the pleasure of Allah through the work of teaching.","As parents'' trust grew, the school grew with it. From Faisalabad, The Arqam Schools expanded step by step to other cities, opening campuses in Lahore, Karachi and beyond, with head offices in both Faisalabad and Lahore.","Today, The Arqam Schools is a network of more than 350 branches across Pakistan, educating over 250,000 students from Play Group to Matric, including more than 20,000 Hifz-e-Quran students. The purpose remains what it was in 1993: excellence in this world and excellence in the Hereafter."]')),
    updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'split_feature' AND JSON_VALUE(data, '$.anchor') = N'who-we-are';

UPDATE dbo.page_sections SET data = N'{"anchor":"vision-mission","heading":"Our Vision and Mission","tone":"white","vision":{"title":"Our Vision","seal":{"bucket":"public-media","path":"network/content/hasana-seal.png","alt":"The Hasana seal: فِي الدُّنْيَا حَسَنَةً وَفِي الْآخِرَةِ حَسَنَةً, Excellence in this world and excellence in the Hereafter","width":938,"height":938},"body":"To raise a generation that excels in knowledge and character, and succeeds in this world and the Hereafter."},"mission":{"title":"Our Mission","body":"To earn the pleasure of Allah through education, training and service, by giving every child a quality modern education within the framework of the Quran and Sunnah, and by building the discipline, confidence and noble character they need to lead responsibly in every walk of life."}}', is_visible = 1, sort_order = 2, updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'vision_mission';
UPDATE dbo.page_sections SET is_visible = 0, updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND (JSON_VALUE(data, '$.anchor') IN (N'mission-vision-values', N'journey'));

UPDATE dbo.page_sections SET data = JSON_MODIFY(data, '$.badge', JSON_QUERY(N'{"bucket":"public-media","path":"network/content/core-values-badge.png","alt":"Core Values badge: we believe in citizenship, commitment, leadership, respect, excellence and responsibility","width":600,"height":600}')), updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'values' AND JSON_VALUE(data, '$.anchor') = N'core-values';

-- 2. Sample news and events (skipped when a post with the same web address already exists)
DECLARE @news TABLE (title NVARCHAR(160), slug NVARCHAR(120), category NVARCHAR(40), excerpt NVARCHAR(300), body NVARCHAR(MAX), n INT);
INSERT INTO @news VALUES
    (N'Sample: Students take part in an inter-school Quran recitation competition', N'sample-quran-recitation-competition', N'achievement', N'Students from our campuses recited the Holy Quran with beautiful tajweed. This is a sample post for the preview.', N'{"text":"Students from our campuses recited the Holy Quran with beautiful tajweed. This is a sample post for the preview.\n\nThis is a sample post to preview how news and events look on the website. It is not real news."}', 0),
    (N'Sample: A new reading corner opens at a campus', N'sample-new-reading-corner', N'campus', N'A quiet reading corner with storybooks for younger children. This is a sample post for the preview.', N'{"text":"A quiet reading corner with storybooks for younger children. This is a sample post for the preview.\n\nThis is a sample post to preview how news and events look on the website. It is not real news."}', 1),
    (N'Sample: Admissions for the new session', N'sample-admissions-new-session', N'announcement', N'Admissions open at the start of the academic session. For admission enquiries, call or WhatsApp 0339-2406479. This is a sample post for the preview.', N'{"text":"Admissions open at the start of the academic session. For admission enquiries, call or WhatsApp 0339-2406479. This is a sample post for the preview.\n\nThis is a sample post to preview how news and events look on the website. It is not real news."}', 2);
INSERT INTO dbo.news (title, slug, excerpt, body, category_id, is_featured, status, published_at)
SELECT x.title, x.slug, x.excerpt, x.body, c.id, 0, N'published', DATEADD(MINUTE, -x.n, SYSDATETIMEOFFSET())
FROM @news x LEFT JOIN dbo.categories c ON c.[type] = N'news' AND c.slug = x.category
WHERE NOT EXISTS (SELECT 1 FROM dbo.news n WHERE n.slug = x.slug AND n.deleted_at IS NULL)
  AND NOT EXISTS (SELECT 1 FROM dbo.events e WHERE e.slug = x.slug AND e.deleted_at IS NULL);

DECLARE @events TABLE (title NVARCHAR(160), slug NVARCHAR(120), starts_at DATETIMEOFFSET(3), ends_at DATETIMEOFFSET(3), venue NVARCHAR(200), summary NVARCHAR(300), body NVARCHAR(MAX));
INSERT INTO @events VALUES
    (N'Sample: Annual Sports Day', N'sample-annual-sports-day', '2026-12-10T09:00:00+05:00', '2026-12-10T13:00:00+05:00', N'At the campus', N'Races, team games and a prize ceremony for students. This is a sample event for the preview.', N'{"text":"Races, team games and a prize ceremony for students. This is a sample event for the preview.\n\nThis is a sample post to preview how news and events look on the website. It is not real news."}'),
    (N'Sample: Parent–Teacher Meeting', N'sample-parent-teacher-meeting', '2026-11-28T09:00:00+05:00', '2026-11-28T12:00:00+05:00', N'At the campus', N'Parents meet class teachers to talk about their child''s progress. This is a sample event for the preview.', N'{"text":"Parents meet class teachers to talk about their child''s progress. This is a sample event for the preview.\n\nThis is a sample post to preview how news and events look on the website. It is not real news."}');
INSERT INTO dbo.events (title, slug, summary, body, starts_at, ends_at, is_all_day, timezone, venue, status, published_at)
SELECT x.title, x.slug, x.summary, x.body, x.starts_at, x.ends_at, 0, N'Asia/Karachi', x.venue, N'published', SYSDATETIMEOFFSET()
FROM @events x
WHERE NOT EXISTS (SELECT 1 FROM dbo.events e WHERE e.slug = x.slug AND e.deleted_at IS NULL)
  AND NOT EXISTS (SELECT 1 FROM dbo.news n WHERE n.slug = x.slug AND n.deleted_at IS NULL);

INSERT INTO dbo.schema_migrations (id) VALUES (N'14_about_notes_and_sample_news');
COMMIT TRANSACTION;
PRINT N'14_about_notes_and_sample_news applied.';
GO
SET NOEXEC OFF;
GO
