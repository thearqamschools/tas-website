-- =============================================================================
-- THE ARQAM SCHOOLS: ABOUT US PHOTOS AND CORE VALUES (7 November 2026)
-- Run after 14. Safe to run twice: it records itself in dbo.schema_migrations.
--
--  1. Photos supplied by the owner: Prof. Sakhawat Ali (Late) on the founder card and
--     Mr. Zubair Siddiqui on his leadership card (media/public-media/network/content/...).
--     Mr. Asad Sakhawat Ali keeps the "Photo to be added" space until his photo arrives.
--  2. Core Values: the "We believe in:" line and the "Knowledge with values. Education with
--     purpose." statement are removed from this section (the badge says "We believe in").
--  Changed rows are backed up in [archive].page_sections_20261107. Nothing is deleted.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'15_about_photos_and_core_values')
BEGIN
    PRINT N'15_about_photos_and_core_values is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @about UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/about' AND deleted_at IS NULL);
IF @about IS NULL
    THROW 50025, N'15: the /about page was not found. Nothing was changed.', 1;
IF EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @about AND draft_data IS NOT NULL)
    THROW 50026, N'15: a section on About Us has a waiting draft. Publish or discard it in the admin panel, then run 15 again. Nothing was changed.', 1;
IF OBJECT_ID(N'archive.page_sections_20261107') IS NULL
    SELECT * INTO archive.page_sections_20261107 FROM dbo.page_sections WHERE page_id = @about;

-- 1. Photos, matched by name so the order of the cards does not matter
UPDATE s SET updated_at = SYSDATETIMEOFFSET(),
    data = JSON_MODIFY(s.data, '$.items', JSON_QUERY(N'[' + COALESCE((SELECT STRING_AGG(CAST(
        CASE WHEN j.[type] = 5 AND JSON_VALUE(j.[value], '$.name') = N'Prof. Sakhawat Ali (Late)'
                  THEN JSON_MODIFY(j.[value], '$.image', JSON_QUERY(N'{"bucket":"public-media","path":"network/content/founder-prof-sakhawat-ali.jpg","alt":"Prof. Sakhawat Ali (Late), Founder of The Arqam Schools","width":600,"height":785}'))
             WHEN j.[type] = 5 AND JSON_VALUE(j.[value], '$.name') = N'Mr. Zubair Siddiqui'
                  THEN JSON_MODIFY(j.[value], '$.image', JSON_QUERY(N'{"bucket":"public-media","path":"network/content/leadership-zubair-siddiqui.jpg","alt":"Mr. Zubair Siddiqui, Chief Executive Officer, The Arqam Schools Pakistan","width":600,"height":745}'))
             ELSE j.[value] END AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT))
        FROM OPENJSON(s.data, '$.items') j), N'') + N']'))
FROM dbo.page_sections s
WHERE s.page_id = @about AND s.[type] = N'leadership' AND JSON_QUERY(s.data, '$.items') IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @about AND [type] = N'leadership' AND data LIKE N'%founder-prof-sakhawat-ali.jpg%')
    THROW 50027, N'15: the founder card (Prof. Sakhawat Ali (Late)) was not found. Nothing was changed.', 1;

-- 2. Core Values without the intro line and the statement
UPDATE dbo.page_sections SET data = JSON_MODIFY(JSON_MODIFY(data, '$.body', N''), '$.quote', NULL), updated_at = SYSDATETIMEOFFSET()
WHERE page_id = @about AND [type] = N'values' AND JSON_VALUE(data, '$.anchor') = N'core-values';
IF @@ROWCOUNT <> 1 THROW 50028, N'15: the About core values section was not found. Nothing was changed.', 1;

INSERT INTO dbo.schema_migrations (id) VALUES (N'15_about_photos_and_core_values');
COMMIT TRANSACTION;
PRINT N'15_about_photos_and_core_values applied.';
GO
SET NOEXEC OFF;
GO
