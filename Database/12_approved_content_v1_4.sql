-- =============================================================================
-- THE ARQAM SCHOOLS: APPROVED WEBSITE CONTENT v1.4 (6 November 2026)
-- Run after 11. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Source: "TAS-Website-Content.md" v1.4, the single approved source for website text.
--  1. Head offices: Lahore first; names, addresses, phones, mobile, email and Facebook pages
--     as in the file (new columns head_offices.mobile and head_offices.facebook_url).
--  2. Settings: footer text (motto in English), no footer call-to-action band, site
--     description, contact e-mail, Facebook pages (Lahore and Faisalabad), no YouTube link
--     (not yet available).
--  3. About: Our Journey, Vision and Mission, Core Values, Our Founder, Leadership, Name and
--     its significance, Our Basic Idea. Sections not in the file are hidden.
--  4. Academics: Curriculum, Classes, Quranic Education, Co-curricular Activities, Facilities.
--     Level summaries, ages and key points removed (not in the file).
--  5. Admissions: key information from the file; placeholders for the admission process and
--     the new admission form; sections not in the file are hidden.
--  6. FAQ: transport question removed; head office contact answer as in the file.
--     Student life: the activities list is replaced by the co-curricular text from the file.
--  7. "Learn Today. Lead Tomorrow." removed from the homepage title and stored homepage content.
--  8. Search-result descriptions of the changed pages from the approved wording; old /about and
--     /academics addresses point to sections that still show.
--  Changed rows are backed up in [archive].*_20261106. Nothing is deleted; old sections are hidden.
--  Stops without changes if a changed page has a waiting draft.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'12_approved_content_v1_4')
BEGIN
    PRINT N'12_approved_content_v1_4 is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
-- New head office columns (own batch, so the next batch can use them)
IF COL_LENGTH(N'dbo.head_offices', N'mobile') IS NULL
    ALTER TABLE dbo.head_offices ADD mobile NVARCHAR(20) NULL
        CONSTRAINT CK_head_offices_mobile_format CHECK (REGEXP_LIKE(mobile, N'^\+?[0-9 ()-]{7,20}$'));
IF COL_LENGTH(N'dbo.head_offices', N'facebook_url') IS NULL
    ALTER TABLE dbo.head_offices ADD facebook_url NVARCHAR(300) NULL
        CONSTRAINT CK_head_offices_facebook_url_check CHECK (REGEXP_LIKE(facebook_url, N'^https://(www\.)?facebook\.com/'));
GO
BEGIN TRANSACTION;

DECLARE @about UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/about' AND deleted_at IS NULL);
DECLARE @academics UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/academics' AND deleted_at IS NULL);
DECLARE @admissions UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/admissions' AND deleted_at IS NULL);
DECLARE @faq UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/faq' AND deleted_at IS NULL);
DECLARE @life UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/student-life' AND deleted_at IS NULL);
IF @about IS NULL OR @academics IS NULL OR @admissions IS NULL
    THROW 50014, N'12: the About, Academics or Admissions page was not found. Nothing was changed.', 1;
IF EXISTS (SELECT 1 FROM dbo.page_sections WHERE draft_data IS NOT NULL AND page_id IN (@about, @academics, @admissions, @faq, @life))
    THROW 50015, N'12: a section on About, Academics, Admissions, FAQ or Student life has a waiting draft. Publish or discard it in the admin panel, then run 12 again. Nothing was changed.', 1;
IF (SELECT COUNT(*) FROM dbo.head_offices WHERE office_number IN (1, 2)) <> 2
    THROW 50016, N'12: head offices 1 and 2 were not found. Nothing was changed.', 1;

-- Backups
IF OBJECT_ID(N'archive.head_offices_20261106') IS NULL SELECT * INTO archive.head_offices_20261106 FROM dbo.head_offices;
IF OBJECT_ID(N'archive.site_settings_20261106') IS NULL SELECT * INTO archive.site_settings_20261106 FROM dbo.site_settings;
IF OBJECT_ID(N'archive.pages_20261106') IS NULL SELECT * INTO archive.pages_20261106 FROM dbo.pages WHERE id IN (@about, @academics, @admissions, @faq, @life);
IF OBJECT_ID(N'archive.page_sections_20261106') IS NULL SELECT * INTO archive.page_sections_20261106 FROM dbo.page_sections WHERE page_id IN (@about, @academics, @admissions, @faq, @life);
IF OBJECT_ID(N'archive.seo_metadata_20261106') IS NULL SELECT * INTO archive.seo_metadata_20261106 FROM dbo.seo_metadata
    WHERE route_path = N'/' OR id IN (SELECT seo_id FROM dbo.pages WHERE path IN (N'/about', N'/academics', N'/admissions', N'/contact', N'/student-life'));
IF OBJECT_ID(N'archive.redirects_20261106') IS NULL SELECT * INTO archive.redirects_20261106 FROM dbo.redirects WHERE to_path LIKE N'/about#%' OR to_path LIKE N'/academics#%';
IF OBJECT_ID(N'archive.homepage_content_20261106') IS NULL SELECT * INTO archive.homepage_content_20261106 FROM dbo.homepage_content;

-- 1. Head offices (Lahore first, as in the file)
DECLARE @lhr UNIQUEIDENTIFIER = (SELECT TOP 1 id FROM dbo.head_offices WHERE name LIKE N'%Lahore%' OR city LIKE N'Lahore%' ORDER BY office_number);
DECLARE @fsd UNIQUEIDENTIFIER = (SELECT TOP 1 id FROM dbo.head_offices WHERE name LIKE N'%Faisalabad%' OR city LIKE N'Faisalabad%' ORDER BY office_number);
IF @lhr IS NULL OR @fsd IS NULL OR @lhr = @fsd
    THROW 50017, N'12: the Lahore and Faisalabad head offices could not be told apart. Nothing was changed.', 1;
UPDATE dbo.head_offices SET office_number = 9 WHERE id = @fsd;
UPDATE dbo.head_offices SET office_number = 1, name = N'The Arqam Schools — Head Office, Lahore', city = N'Lahore',
       address = N'163-Tufail Block, Canal Bank Housing Scheme, Lahore 54000', phone = N'042-36520008', phone_alt = NULL,
       mobile = N'0331-1304001', whatsapp = NULL, email = N'thearqamschools@gmail.com',
       facebook_url = N'https://www.facebook.com/TASHeadOffice', is_published = 1, updated_at = SYSDATETIMEOFFSET()
WHERE id = @lhr;
UPDATE dbo.head_offices SET office_number = 2, name = N'The Arqam Schools — Head Office, Faisalabad', city = N'Faisalabad',
       address = N'Sanora Colony, Samanabad, Faisalabad', phone = N'041-2660265', phone_alt = N'041-2406479',
       mobile = NULL, whatsapp = N'0339-2406479', email = N'thearqamschools@gmail.com',
       facebook_url = N'https://www.facebook.com/thearqamschoolsfsd', is_published = 1, updated_at = SYSDATETIMEOFFSET()
WHERE id = @fsd;

-- 2. Settings
UPDATE dbo.site_settings SET [value] = N'"Excellence in this world and excellence in the Hereafter"', updated_at = SYSDATETIMEOFFSET() WHERE [key] = N'footer.mission';
UPDATE dbo.site_settings SET [value] = N'null', updated_at = SYSDATETIMEOFFSET() WHERE [key] = N'footer.cta';
UPDATE dbo.site_settings SET [value] = N'"A trusted educational institution combining modern education with Islamic values, ethics, discipline and character development. Play Group to Matric, plus Hifz-e-Quran."', updated_at = SYSDATETIMEOFFSET()
WHERE [key] = N'site.description';
IF NOT EXISTS (SELECT 1 FROM dbo.site_settings WHERE [key] = N'contact.email')
    INSERT INTO dbo.site_settings ([key], [value], is_public) VALUES (N'contact.email', N'"thearqamschools@gmail.com"', 1);
ELSE
    UPDATE dbo.site_settings SET [value] = N'"thearqamschools@gmail.com"', updated_at = SYSDATETIMEOFFSET() WHERE [key] = N'contact.email';
-- Social links: keep the owner's links except YouTube (not yet available), one entry per address,
-- and both Facebook pages from the file.
DECLARE @social NVARCHAR(MAX) = (SELECT [value] FROM dbo.site_settings WHERE [key] = N'social');
IF @social IS NULL OR ISJSON(@social, ARRAY) <> 1 SET @social = N'[]';
DECLARE @kept NVARCHAR(MAX) = (
    SELECT STRING_AGG(CAST(x.v AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY x.k)
    FROM (SELECT MIN(CAST(j.[key] AS INT)) AS k, MIN(j.[value]) AS v
          FROM OPENJSON(@social) j
          WHERE j.[type] = 5 AND COALESCE(JSON_VALUE(j.[value], '$.platform'), N'') <> N'youtube'
            AND COALESCE(JSON_VALUE(j.[value], '$.url'), N'') NOT LIKE N'%thearqamfsdOfficial%'
          GROUP BY LOWER(RTRIM(REPLACE(JSON_VALUE(j.[value], '$.url'), N'/', N'')))) x);
SET @social = N'[' + COALESCE(@kept, N'') + N']';
IF @social NOT LIKE N'%facebook.com\/TASHeadOffice%' AND @social NOT LIKE N'%facebook.com/TASHeadOffice%'
    SET @social = JSON_MODIFY(@social, 'append $', JSON_QUERY(N'{"url":"https://www.facebook.com/TASHeadOffice","platform":"facebook"}'));
IF @social NOT LIKE N'%thearqamschoolsfsd%'
    SET @social = JSON_MODIFY(@social, 'append $', JSON_QUERY(N'{"url":"https://www.facebook.com/thearqamschoolsfsd","platform":"facebook"}'));
UPDATE dbo.site_settings SET [value] = @social, updated_at = SYSDATETIMEOFFSET() WHERE [key] = N'social';

-- 7. Homepage title and stored homepage content without "Learn Today. Lead Tomorrow."
UPDATE dbo.seo_metadata SET meta_title = N'The Arqam Schools | Play Group to Matric and Hifz-e-Quran', updated_at = SYSDATETIMEOFFSET() WHERE route_path = N'/';
UPDATE dbo.homepage_content SET data = REPLACE(REPLACE(CAST(data AS NVARCHAR(MAX)), N'Learn Today. Lead Tomorrow.', N'Excellence in this world and excellence in the Hereafter.'), N'Learn Today, Lead Tomorrow', N'Excellence in this world and excellence in the Hereafter')
WHERE CAST(data AS NVARCHAR(MAX)) LIKE N'%Learn Today%';

-- 3. About
UPDATE dbo.pages SET summary = N'The Arqam Schools began in 1993 in Faisalabad. Today it is a network of more than 350 branches across Pakistan.', updated_at = SYSDATETIMEOFFSET() WHERE id = @about;
UPDATE dbo.page_sections SET is_visible = 0, updated_at = SYSDATETIMEOFFSET() WHERE page_id = @about;
UPDATE dbo.page_sections SET is_visible = 1, sort_order = 1, updated_at = SYSDATETIMEOFFSET(),
       data = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(data, '$.eyebrow', N'About Us'), '$.heading', N'Our Journey'),
                          '$.body', JSON_QUERY(N'["The Arqam Schools began in 1993 in Faisalabad, with a single campus and a clear purpose: to give children a sound modern education rooted in the Quran and Sunnah, and to earn the pleasure of Allah through the work of teaching.","As parents'' trust grew, the school grew with it. From Faisalabad, The Arqam Schools expanded step by step to other cities, opening campuses in Lahore, Karachi and beyond, with head offices in both Faisalabad and Lahore.","Today, in 2026, The Arqam Schools is a network of more than 350 branches across Pakistan, educating over 250,000 students from Play Group to Matric, including more than 20,000 Hifz-e-Quran students. The purpose remains what it was in 1993: excellence in this world and excellence in the Hereafter."]')), '$.page_intro', CAST(1 AS BIT))
WHERE page_id = @about AND [type] = N'split_feature' AND JSON_VALUE(data, '$.anchor') = N'who-we-are';
UPDATE dbo.page_sections SET is_visible = 1, sort_order = 2, data = N'{"anchor":"vision-mission","heading":"Vision and Mission","tone":"warm","vision":{"title":"Our Vision","motto":"فِي الدُّنْيَا حَسَنَةً وَفِي الْآخِرَةِ حَسَنَةً","motto_en":"Excellence in this world and excellence in the Hereafter.","body":"To raise a generation that excels in knowledge and character, and succeeds in this world and the Hereafter."},"mission":{"title":"Our Mission","body":"To earn the pleasure of Allah through education, training and service, by giving every child a quality modern education within the framework of the Quran and Sunnah, and by building the discipline, confidence and noble character they need to lead responsibly in every walk of life."}}', updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'vision_mission';
UPDATE dbo.page_sections SET is_visible = 1, sort_order = 5, data = N'{"anchor":"leadership","tone":"white","heading":"Leadership","intro":"Leadership messages and photos will be added soon.","items":[{"name":"Mr. Zubair Siddiqui","role":"Chief Executive Officer, The Arqam Schools Pakistan","message":""},{"name":"Mr. Asad Sakhawat Ali","role":"Executive Director, The Arqam Schools Pakistan","message":""}]}', updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'leadership' AND JSON_VALUE(data, '$.anchor') = N'leadership';
INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible)
SELECT @about, v.t, v.d, v.s, 1 FROM (VALUES
    (N'values', N'{"anchor":"core-values","heading":"Our Core Values","tone":"white","body":"We believe in:","items":[{"icon":"map-pin","title":"Citizenship","description":"Being loyal, law-abiding and useful members of Pakistan and the wider community."},{"icon":"heart-handshake","title":"Commitment","description":"Dedication to learning, to our duties and to the promises we make."},{"icon":"compass","title":"Leadership","description":"The confidence to take initiative and set a good example for others."},{"icon":"users","title":"Respect","description":"For Allah''s commands, for parents, teachers and elders, and for one another."},{"icon":"award","title":"Excellence","description":"Giving our best in study, character and conduct, in the spirit of Ihsan."},{"icon":"shield-check","title":"Responsibility","description":"Owning our actions, our learning and our duties to others."}],"quote":{"text":"Knowledge with values. Education with purpose.","source":""}}', 3, N'core-values'),
    (N'leadership', N'{"anchor":"founder","tone":"grey","heading":"Our Founder","intro":"","items":[{"name":"Prof. Sakhawat Ali (Late)","role":"Founder of The Arqam Schools","message":""}]}', 4, N'founder'),
    (N'split_feature', N'{"anchor":"name","tone":"warm","heading":"Name and its significance","body":["\"The Arqam\" has special importance and unique grace in the history of Islam. It was the pioneer educational institution of the Muslim society, which came into existence under the leadership of Rasool Allah ﷺ. It was the basic school of Islamic thought, established at the residence of Hazrat Zaid Bin Arqam (R.A.), the well-known companion of the Holy Prophet ﷺ, with a view to building the character of the people coming into the circle of Islam. Keeping that first glorious Islamic educational academy in view, this institution has been named \"The Arqam Schools\". The basic aim of the school is to teach and train the present Muslim generation with the Islamic spirit. May Allah accept it and bless us with strength for betterment."]}', 6, N'name'),
    (N'split_feature', N'{"anchor":"basic-idea","tone":"white","heading":"Our Basic Idea","body":["The basic aim and idea of The Arqam Schools is to earn the blessing of Allah through the acts of education, training and human service, in accordance with the teachings of Allah and the sayings of the Holy Prophet ﷺ."]}', 7, N'basic-idea')) v(t, d, s, a)
WHERE NOT EXISTS (SELECT 1 FROM dbo.page_sections x WHERE x.page_id = @about AND JSON_VALUE(x.data, '$.anchor') = v.a);
UPDATE x SET is_visible = 1, sort_order = v.s, updated_at = SYSDATETIMEOFFSET()
FROM dbo.page_sections x JOIN (VALUES (N'core-values', 3), (N'founder', 4), (N'name', 6), (N'basic-idea', 7)) v(a, s)
  ON JSON_VALUE(x.data, '$.anchor') = v.a
WHERE x.page_id = @about;

-- 4. Academics
UPDATE dbo.pages SET summary = N'The Arqam Schools follows its own curriculum, approved by the Government of Pakistan education boards.', updated_at = SYSDATETIMEOFFSET() WHERE id = @academics;
UPDATE dbo.page_sections SET is_visible = 0, updated_at = SYSDATETIMEOFFSET() WHERE page_id = @academics;
UPDATE s SET is_visible = 1, sort_order = 2, updated_at = SYSDATETIMEOFFSET(),
    data = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(s.data, '$.heading', N'Classes'), '$.intro', N'Play Group to Matric, plus Hifz-e-Quran.'),
        '$.items', JSON_QUERY(N'[' + COALESCE((SELECT STRING_AGG(CAST(
            CASE WHEN j.[type] <> 5 THEN j.[value]
                 ELSE JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(j.[value], '$.summary', N''), '$.ages', N''), '$.points', JSON_QUERY(N'[]')) END
            AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT)) FROM OPENJSON(s.data, '$.items') j), N'') + N']'))
FROM dbo.page_sections s
WHERE s.page_id = @academics AND s.[type] = N'academic_levels' AND JSON_QUERY(s.data, '$.items') IS NOT NULL;
INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible)
SELECT @academics, v.t, v.d, v.s, 1 FROM (VALUES
    (N'card_grid', N'{"anchor":"curriculum","heading":"Academics at a glance","tone":"white","columns":3,"items":[{"icon":"book-open","title":"Curriculum","summary":"The Arqam Schools follows its own curriculum, approved by the Government of Pakistan education boards."},{"icon":"graduation-cap","title":"Classes","summary":"Play Group to Matric."},{"icon":"languages","title":"Medium of instruction","summary":"English, alongside Urdu."}]}', 1, N'curriculum'),
    (N'card_grid', N'{"anchor":"quranic-education","heading":"Quranic Education","tone":"grey","columns":4,"intro":"Taught alongside general education.","items":[{"icon":"moon-star","title":"Hifz-e-Quran","summary":"Memorisation of the Holy Quran with experienced, devoted teachers."},{"icon":"sparkles","title":"Tajweed","summary":"Correct pronunciation and recitation of the Quran."},{"icon":"book-open","title":"Nazra","summary":"Quran reading for every student."},{"icon":"heart","title":"Islamic practice","summary":"Namaz, Qira''at and the fundamentals of Islam, taught through easy methods and practised daily."}]}', 3, N'quranic-education'),
    (N'split_feature', N'{"anchor":"co-curricular","tone":"white","heading":"Co-curricular Activities","image_side":"right","body":["Husn-e-Qira''at, Naat Khawani, morning assembly presentations, Urdu and English debates and speech competitions, art and craft, theme days and educational trips — all designed to build confidence and help students overcome shyness."],"image":{"bucket":"public-media","path":"network/content/creative-projects--20261014075037.jpg","alt":"Children painting pictures outdoors on the school lawn with their teacher","width":1365,"height":1024}}', 4, N'co-curricular'),
    (N'card_grid', N'{"anchor":"facilities","heading":"Facilities","tone":"grey","columns":4,"items":[{"icon":"monitor","title":"Computer Lab","summary":""},{"icon":"flask","title":"Science Laboratory","summary":""},{"icon":"book-open","title":"Library","summary":""},{"icon":"video","title":"CCTV-monitored campuses","summary":""}]}', 5, N'facilities')) v(t, d, s, a)
WHERE NOT EXISTS (SELECT 1 FROM dbo.page_sections x WHERE x.page_id = @academics AND JSON_VALUE(x.data, '$.anchor') = v.a);
UPDATE x SET is_visible = 1, sort_order = v.s, updated_at = SYSDATETIMEOFFSET()
FROM dbo.page_sections x JOIN (VALUES (N'curriculum', 1), (N'quranic-education', 3), (N'co-curricular', 4), (N'facilities', 5)) v(a, s)
  ON JSON_VALUE(x.data, '$.anchor') = v.a
WHERE x.page_id = @academics;

-- 5. Admissions
UPDATE dbo.pages SET summary = N'Admissions open at the start of the academic session; the new session begins in the first week of March.', updated_at = SYSDATETIMEOFFSET() WHERE id = @admissions;
UPDATE dbo.page_sections SET is_visible = 0, updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @admissions AND [type] IN (N'process_steps', N'faq', N'card_grid');
UPDATE dbo.page_sections SET sort_order = 1, updated_at = SYSDATETIMEOFFSET(),
    data = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(data, '$.eyebrow', N''), '$.title', N'Admissions'),
        '$.intro', N'Admissions open at the start of the academic session; the new session begins in the first week of March.'),
        '$.primary_cta', JSON_QUERY(N'{"href":"https://wa.me/923392406479","label":"WhatsApp 0339-2406479"}')),
        '$.secondary_cta', JSON_QUERY(N'{"href":"tel:03392406479","label":"Call 0339-2406479"}'))
WHERE page_id = @admissions AND [type] = N'page_header';
UPDATE dbo.page_sections SET sort_order = 2, data = N'{"anchor":"key-information","heading":"Key information","tone":"white","intro":"","items":["Admissions open at the start of the academic session; the new session begins in the first week of March.","Play Group: from 3.5 years of age.","Academic year: 1st term March to September, 2nd term October to February.","For admission enquiries: call or WhatsApp 0339-2406479."]}', updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @admissions AND [type] = N'info_list';
UPDATE s SET sort_order = 5, updated_at = SYSDATETIMEOFFSET(),
    data = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(s.data, '$.heading', N'Classes'), '$.intro', N'Play Group to Matric, plus Hifz-e-Quran.'), '$.items', JSON_QUERY(N'[' + COALESCE((SELECT STRING_AGG(CAST(
            CASE WHEN j.[type] <> 5 THEN j.[value] ELSE JSON_MODIFY(j.[value], '$.ages', N'') END
            AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT)) FROM OPENJSON(s.data, '$.items') j), N'') + N']'))
FROM dbo.page_sections s
WHERE s.page_id = @admissions AND s.[type] = N'academic_levels' AND JSON_QUERY(s.data, '$.items') IS NOT NULL;
UPDATE dbo.page_sections SET sort_order = 6, data = JSON_MODIFY(data, '$.heading', N'Admission enquiry'), updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @admissions AND [type] = N'admission_inquiry';
INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible)
SELECT @admissions, N'split_feature', v.d, v.s, 1 FROM (VALUES (N'{"anchor":"process","tone":"grey","heading":"Admission process","body":["The step-by-step admission process will be published here soon. For admission enquiries, call or WhatsApp 0339-2406479."]}', 3, N'process'), (N'{"anchor":"admission-form","tone":"white","heading":"Admission form","body":["The new admission form will be published here soon. For admission enquiries, call or WhatsApp 0339-2406479."]}', 4, N'admission-form')) v(d, s, a)
WHERE NOT EXISTS (SELECT 1 FROM dbo.page_sections x WHERE x.page_id = @admissions AND x.[type] = N'split_feature' AND JSON_VALUE(x.data, '$.anchor') = v.a);

-- 6. FAQ and Student life
IF @faq IS NOT NULL
    UPDATE s SET updated_at = SYSDATETIMEOFFSET(),
        data = JSON_MODIFY(s.data, '$.items', JSON_QUERY(N'[' + COALESCE((SELECT STRING_AGG(CAST(
            CASE WHEN JSON_VALUE(j.[value], '$.question') = N'How can I contact the head office?'
                 THEN JSON_MODIFY(j.[value], '$.answer', N'Head Office, Lahore: 042-36520008, mobile 0331-1304001. Head Office, Faisalabad: 041-2660265, 041-2406479, mobile and WhatsApp 0339-2406479. Email: thearqamschools@gmail.com. You can also use the form on the Contact Us page.')
                 ELSE j.[value] END AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT))
            FROM OPENJSON(s.data, '$.items') j
            WHERE j.[type] <> 5 OR COALESCE(JSON_VALUE(j.[value], '$.question'), N'') NOT LIKE N'%transport%'), N'') + N']'))
    FROM dbo.page_sections s WHERE s.page_id = @faq AND s.[type] = N'faq' AND JSON_QUERY(s.data, '$.items') IS NOT NULL;
IF @life IS NOT NULL
BEGIN
    UPDATE dbo.pages SET summary = N'Co-curricular activities that build confidence and help students overcome shyness.', updated_at = SYSDATETIMEOFFSET() WHERE id = @life;
    UPDATE dbo.page_sections SET is_visible = 0, updated_at = SYSDATETIMEOFFSET() WHERE page_id = @life AND [type] = N'card_grid';
    IF NOT EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @life AND JSON_VALUE(data, '$.anchor') = N'co-curricular')
        INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible) VALUES (@life, N'split_feature', N'{"anchor":"co-curricular","tone":"white","heading":"Co-curricular Activities","image_side":"right","body":["Husn-e-Qira''at, Naat Khawani, morning assembly presentations, Urdu and English debates and speech competitions, art and craft, theme days and educational trips — all designed to build confidence and help students overcome shyness."],"image":{"bucket":"public-media","path":"network/content/creative-projects--20261014075037.jpg","alt":"Children painting pictures outdoors on the school lawn with their teacher","width":1365,"height":1024}}', 0, 1);
END

-- 8. Search-result descriptions from the approved wording
UPDATE m SET meta_description = v.d, og_description = CASE WHEN m.og_description IS NULL THEN NULL ELSE v.d END, updated_at = SYSDATETIMEOFFSET()
FROM dbo.seo_metadata m
JOIN (VALUES
    (N'/', N'The Arqam Schools: a trusted educational institution combining modern education with Islamic values. Play Group to Matric and Hifz-e-Quran.'),
    (N'/about', N'The Arqam Schools began in 1993 in Faisalabad. Today: 350+ branches, 250,000+ students and 20,000+ Hifz-e-Quran students across Pakistan.'),
    (N'/contact', N'Head Office, Lahore: 042-36520008. Head Office, Faisalabad: 041-2660265, 041-2406479. WhatsApp 0339-2406479. thearqamschools@gmail.com'),
    (N'/admissions', N'Admissions open at the start of the academic session; the new session begins in the first week of March. Enquiries: call or WhatsApp 0339-2406479.'),
    (N'/academics', N'Our own curriculum, approved by the Government of Pakistan education boards: Play Group to Matric, Quranic education and co-curricular activities.'),
    (N'/student-life', N'Co-curricular activities at The Arqam Schools that build confidence and help students overcome shyness.')) v(p, d)
  ON (v.p = N'/' AND m.route_path = N'/') OR m.id = (SELECT seo_id FROM dbo.pages WHERE path = v.p AND deleted_at IS NULL);

-- 9. Old addresses that pointed at sections which are now hidden
UPDATE dbo.redirects SET to_path = N'/about#core-values', updated_at = SYSDATETIMEOFFSET() WHERE to_path = N'/about#values';
UPDATE dbo.redirects SET to_path = N'/about', updated_at = SYSDATETIMEOFFSET() WHERE to_path IN (N'/about#philosophy', N'/about#why-choose-us');
UPDATE dbo.redirects SET to_path = N'/academics', updated_at = SYSDATETIMEOFFSET() WHERE to_path = N'/academics#teaching';

INSERT INTO dbo.schema_migrations (id) VALUES (N'12_approved_content_v1_4');
COMMIT TRANSACTION;
PRINT N'12_approved_content_v1_4 applied.';
GO
SET NOEXEC OFF;
GO
