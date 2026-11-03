-- =============================================================================
-- THE ARQAM SCHOOLS: OWNER'S SECOND REVIEW (3 November 2026)
-- Run after 10. Safe to run twice: it records itself in dbo.schema_migrations.
--
--  1. Motto: "Learn Today. Lead Tomorrow." is not the school's tagline. The motto
--     فِي الدُّنْيَا حَسَنَةً وَفِي الْآخِرَةِ حَسَنَةً (Al-Baqarah 2:201) replaces it, and the homepage title no longer uses it.
--  2. Facebook: https://www.facebook.com/TASHeadOffice (the Faisalabad page shows
--     "content not available"), once in the list.
--  3. Classes: "Class 9 (Matric)" and "Class 10 (Matric)" become "Class 9" and "Class 10".
--  4. Academics: the levels one under another (no tabs), with ages from the owner
--     (Early Years 3-5, Primary 5-10, Middle 11-12, Matric 13-15, Hifz 8+) and key points
--     taken from the existing level text. The same ages on the Admissions classes list.
--     Only empty ages and missing points are filled; admin edits are kept.
--  5. About: "Our journey" starts with 1993 (given by the owner) when the school has not
--     filled it in yet; "Our leadership" gets an intro line.
--  Changed rows are backed up in [archive].*_20261103b. Stops without changes if a
--  changed page has a waiting draft.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'11_owner_second_review')
BEGIN
    PRINT N'11_owner_second_review is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @about UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/about' AND deleted_at IS NULL);
DECLARE @academics UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/academics' AND deleted_at IS NULL);
DECLARE @admissions UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/admissions' AND deleted_at IS NULL);
IF @about IS NULL OR @academics IS NULL OR @admissions IS NULL
    THROW 50011, N'11: the About, Academics or Admissions page was not found. Nothing was changed.', 1;
IF EXISTS (SELECT 1 FROM dbo.page_sections WHERE draft_data IS NOT NULL AND page_id IN (@about, @academics, @admissions)
           AND [type] IN (N'academic_levels', N'timeline', N'leadership'))
    THROW 50012, N'11: a levels, journey or leadership section has a waiting draft. Publish or discard it in the admin panel, then run 11 again. Nothing was changed.', 1;

-- Backups
IF OBJECT_ID(N'archive.site_settings_20261103b') IS NULL
    SELECT * INTO archive.site_settings_20261103b FROM dbo.site_settings WHERE [key] IN (N'site.tagline', N'social');
IF OBJECT_ID(N'archive.class_levels_20261103b') IS NULL
    SELECT * INTO archive.class_levels_20261103b FROM dbo.class_levels;
IF OBJECT_ID(N'archive.page_sections_20261103b') IS NULL
    SELECT * INTO archive.page_sections_20261103b FROM dbo.page_sections WHERE page_id IN (@about, @academics, @admissions);
IF OBJECT_ID(N'archive.seo_metadata_20261103b') IS NULL
    SELECT * INTO archive.seo_metadata_20261103b FROM dbo.seo_metadata WHERE route_path = N'/';

-- 1. Motto
UPDATE dbo.site_settings SET [value] = N'"فِي الدُّنْيَا حَسَنَةً وَفِي الْآخِرَةِ حَسَنَةً"', updated_at = SYSDATETIMEOFFSET()
WHERE [key] = N'site.tagline' AND [value] = N'"Learn Today. Lead Tomorrow."';
UPDATE dbo.seo_metadata SET meta_title = N'The Arqam Schools | Knowledge with Values. Education with Purpose.', updated_at = SYSDATETIMEOFFSET()
WHERE route_path = N'/' AND meta_title = N'The Arqam Schools | Learn Today. Lead Tomorrow.';

-- 2. Facebook: the Lahore head office page, once
DECLARE @fb NVARCHAR(200) = N'https://www.facebook.com/TASHeadOffice';
DECLARE @social NVARCHAR(MAX) = (SELECT [value] FROM dbo.site_settings WHERE [key] = N'social');
IF @social IS NOT NULL AND ISJSON(@social, ARRAY) = 1 AND @social LIKE N'%thearqamfsdOfficial%'
BEGIN
    DECLARE @hasFb BIT = CASE WHEN EXISTS (SELECT 1 FROM OPENJSON(@social) j
                                           WHERE JSON_VALUE(j.[value], '$.url') LIKE N'https://www.facebook.com/TASHeadOffice%') THEN 1 ELSE 0 END;
    UPDATE dbo.site_settings
    SET [value] = N'[' + COALESCE((
            SELECT STRING_AGG(CAST(CASE WHEN j.[type] = 5 AND JSON_VALUE(j.[value], '$.url') LIKE N'%thearqamfsdOfficial%'
                                        THEN CASE WHEN @hasFb = 1 THEN NULL ELSE JSON_MODIFY(j.[value], '$.url', @fb) END
                                        ELSE j.[value] END AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT))
            FROM OPENJSON(@social) j), N'') + N']',
        updated_at = SYSDATETIMEOFFSET()
    WHERE [key] = N'social';
END

-- 3. Class names
UPDATE dbo.class_levels SET name = LTRIM(RTRIM(REPLACE(name, N'(Matric)', N''))), updated_at = SYSDATETIMEOFFSET()
WHERE name LIKE N'%(Matric)%';

-- 4. Levels: one under another, with ages and key points (only where empty)
UPDATE s SET
    data = JSON_MODIFY(CASE WHEN JSON_VALUE(s.data, '$.layout') = N'tabs' THEN JSON_MODIFY(s.data, '$.layout', N'stack') ELSE s.data END,
        '$.items', JSON_QUERY(N'[' + COALESCE((SELECT STRING_AGG(CAST(
            CASE WHEN j.[type] <> 5 THEN j.[value] ELSE
                JSON_MODIFY(
                    JSON_MODIFY(j.[value], '$.ages', COALESCE(NULLIF(JSON_VALUE(j.[value], '$.ages'), N''), CASE JSON_VALUE(j.[value], '$.stage') WHEN N'Early Years' THEN N'Ages 3–5' WHEN N'Primary' THEN N'Ages 5–10' WHEN N'Middle' THEN N'Ages 11–12' WHEN N'Matric' THEN N'Ages 13–15' WHEN N'Hifz' THEN N'Ages 8+' END, N'')),
                    '$.points', JSON_QUERY(COALESCE(JSON_QUERY(j.[value], '$.points'), CASE JSON_VALUE(j.[value], '$.stage') WHEN N'Early Years' THEN N'["Learning through play","Stories and daily routines","First duas and good manners","A happy, caring start to school"]' WHEN N'Primary' THEN N'["Fluent reading","Clear writing","Confident arithmetic","Early science"]' WHEN N'Middle' THEN N'["Deeper learning in every subject","Science and computer skills","The Holy Quran and Islamic studies","Growing independence"]' WHEN N'Matric' THEN N'["Focused preparation for the Matric examinations","Regular practice and feedback","Guidance for the next step"]' WHEN N'Hifz' THEN N'["Memorising the Holy Quran","Correct recitation","Guided by teachers"]' END, N'[]')))
            END AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT))
            FROM OPENJSON(s.data, '$.items') j), N'') + N']')),
    updated_at = SYSDATETIMEOFFSET()
FROM dbo.page_sections s
WHERE s.page_id = @academics AND s.[type] = N'academic_levels' AND JSON_QUERY(s.data, '$.items') IS NOT NULL;

UPDATE s SET
    data = JSON_MODIFY(s.data, '$.items', JSON_QUERY(N'[' + COALESCE((SELECT STRING_AGG(CAST(
            CASE WHEN j.[type] <> 5 THEN j.[value] ELSE
                JSON_MODIFY(j.[value], '$.ages', COALESCE(NULLIF(JSON_VALUE(j.[value], '$.ages'), N''), CASE JSON_VALUE(j.[value], '$.stage') WHEN N'Early Years' THEN N'Ages 3–5' WHEN N'Primary' THEN N'Ages 5–10' WHEN N'Middle' THEN N'Ages 11–12' WHEN N'Matric' THEN N'Ages 13–15' WHEN N'Hifz' THEN N'Ages 8+' END, N''))
            END AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT))
            FROM OPENJSON(s.data, '$.items') j), N'') + N']')),
    updated_at = SYSDATETIMEOFFSET()
FROM dbo.page_sections s
WHERE s.page_id = @admissions AND s.[type] = N'academic_levels' AND JSON_QUERY(s.data, '$.items') IS NOT NULL;

-- 5. About: journey from 1993 (only when no year has been filled in yet) and the leadership intro
UPDATE dbo.page_sections
SET data = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(data,
        '$.intro', COALESCE(NULLIF(JSON_VALUE(data, '$.intro'), N''), N'From its first school in 1993 to 350+ campuses across Pakistan.')),
        '$.items[0].year', N'1993'),
        '$.items[0].title', N'The Arqam Schools founded'),
    updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'timeline' AND JSON_QUERY(data, '$.items') IS NOT NULL AND JSON_VALUE(data, '$.items[0].year') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM OPENJSON(data, '$.items') j WHERE LEN(JSON_VALUE(j.[value], '$.year')) > 0 OR LEN(JSON_VALUE(j.[value], '$.title')) > 0);

UPDATE dbo.page_sections
SET data = JSON_MODIFY(data, '$.intro', N'Meet the people guiding The Arqam Schools.'), updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'leadership' AND COALESCE(JSON_VALUE(data, '$.intro'), N'') = N'';

INSERT INTO dbo.schema_migrations (id) VALUES (N'11_owner_second_review');
COMMIT TRANSACTION;
PRINT N'11_owner_second_review applied.';
GO
SET NOEXEC OFF;
GO
