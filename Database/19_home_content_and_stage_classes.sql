-- =============================================================================
-- THE ARQAM SCHOOLS: HOMEPAGE CONTENT AND STAGE CLASSES IN THE ADMIN PANEL (13 November 2026)
-- Run after 18. Safe to run twice: it records itself in dbo.schema_migrations.
-- 1. Adds the site setting "home.content": the homepage's words and pictures as they are now,
--    so they can be changed in the admin panel (Home Page). The website shows the same as before.
-- 2. Gives every stage of the two "academic levels" sections (Academics and Admissions) a
--    "classes" line (for example "Play Group, Nursery, Prep"), filled from dbo.class_levels,
--    so the line can be edited in the admin panel. The website shows the same classes as before.
-- The academic levels sections are backed up in [archive].page_sections_20261113 first.
-- Nothing is deleted.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'19_home_content_and_stage_classes')
BEGIN
    PRINT N'19_home_content_and_stage_classes is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM dbo.page_sections WHERE [type] = N'academic_levels' AND draft_data IS NOT NULL)
    THROW 50032, N'19: an academic levels section has a waiting draft. Publish or discard it in the admin panel, then run 19 again. Nothing was changed.', 1;

-- 1. Homepage content (only when it is not there yet).
IF NOT EXISTS (SELECT 1 FROM dbo.site_settings WHERE [key] = N'home.content')
    INSERT INTO dbo.site_settings ([key], [value], is_public, [description])
    VALUES (N'home.content', N'{"hero_badge":"Admissions Open 2026-27","hero_line_1":"Knowledge with values.","hero_line_2":"Education with purpose.","hero_intro":"Modern education rooted in Islamic values, from Play Group to Matric, with Hifz classes, at 350+ campuses across Pakistan.","hero_primary_cta":{"label":"Apply for Admission","href":"/admissions"},"hero_secondary_cta":{"label":"Find a Campus","href":"/campuses"},"slides":[{"image":{"bucket":"public-media","path":"network/content/hero-students-classroom--20261014075037.jpg","alt":"Students learning together in a classroom","width":1958,"height":803},"mobile_image":{"bucket":"public-media","path":"network/content/hero-mobile--20261014075037.jpg","alt":"Students learning together in a classroom","width":660,"height":803}}],"figures":[{"value":"34","label":"Years of Excellence"},{"value":"350+","label":"Campuses"},{"value":"250,000+","label":"Students"},{"value":"20,000+","label":"Hafiz-e-Quran"}],"explore_heading":"Explore The Arqam Schools","tiles":[{"icon":"users","title":"About Us","text":"Who we are, our vision and mission, and our values.","href":"/about"},{"icon":"book-open","title":"Academics","text":"Teaching, academic levels and student development.","href":"/academics"},{"icon":"graduation-cap","title":"Admissions","text":"The admission process and an online inquiry form.","href":"/admissions"},{"icon":"map-pin","title":"Campuses","text":"Find a campus by name, city, district or area.","href":"/campuses"},{"icon":"award","title":"News & Events","text":"Achievements, events and announcements.","href":"/news-events"},{"icon":"headset","title":"Contact Us","text":"Call, send a WhatsApp message or write to us.","href":"/contact"}],"whole_child_heading":"Educating the whole child","whole_child_lead":"At The Arqam Schools, modern education and Islamic values go together, to help children grow in knowledge, character, discipline and confidence, from Play Group to Matric.","whole_child_points":[{"icon":"book-open","title":"Academic learning","text":"Modern teaching that builds strong foundations, from Play Group to Matric."},{"icon":"moon-star","title":"Islamic education","text":"Islamic values at the heart of school life, with Hifz classes."},{"icon":"shield-check","title":"Ethics and discipline","text":"Good manners and discipline as part of everyday learning."},{"icon":"heart-handshake","title":"Confidence and character","text":"Supporting each child to grow in confidence and strong character."}],"whole_child_link":{"label":"About The Arqam Schools","href":"/about"},"whole_child_image":{"bucket":"public-media","path":"network/content/home-girl-painting.jpg","alt":"A smiling girl painting with watercolours in an art class","width":1200,"height":805},"news_heading":"Latest news & events","finder_heading":"Find a campus","finder_lead":"Find a campus by name, city, district or area.","admissions_heading":"Admissions Open for 2026-27","admissions_body":"Give your child a strong start with modern education rooted in Islamic values. Send an admission inquiry and the campus team will guide you through the next steps.","admissions_cta":{"label":"Start Your Application","href":"/admissions#inquiry"}}', 1, N'Homepage words and pictures (admin panel: Home Page).');

-- 2. Classes line for each stage.
IF OBJECT_ID(N'archive.page_sections_20261113') IS NULL
    SELECT * INTO archive.page_sections_20261113 FROM dbo.page_sections WHERE [type] = N'academic_levels';

UPDATE s SET updated_at = SYSDATETIMEOFFSET(),
    data = JSON_MODIFY(s.data, '$.items', JSON_QUERY(N'[' + COALESCE((SELECT STRING_AGG(CAST(
        CASE WHEN j.[type] = 5 AND JSON_VALUE(j.[value], '$.classes') IS NULL
                  THEN JSON_MODIFY(j.[value], '$.classes', COALESCE(x.classes, N''))
             ELSE j.[value] END AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT))
        FROM OPENJSON(s.data, '$.items') j
        OUTER APPLY (SELECT STRING_AGG(c.name, N', ') WITHIN GROUP (ORDER BY c.sort_order) AS classes
                     FROM dbo.class_levels c WHERE c.stage = JSON_VALUE(j.[value], '$.stage')) x), N'') + N']'))
FROM dbo.page_sections s
WHERE s.[type] = N'academic_levels' AND ISJSON(s.data) = 1 AND JSON_QUERY(s.data, '$.items') IS NOT NULL;

INSERT INTO dbo.schema_migrations (id) VALUES (N'19_home_content_and_stage_classes');
COMMIT TRANSACTION;
PRINT N'19_home_content_and_stage_classes applied.';
GO
SET NOEXEC OFF;
GO
