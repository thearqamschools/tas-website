-- =============================================================================
-- THE ARQAM SCHOOLS: PHOTO OF MR. ASAD SAKHAWAT ALI (7 November 2026)
-- Run after 15. Safe to run twice: it records itself in dbo.schema_migrations.
-- Adds the photo supplied by the owner to Mr. Asad Sakhawat Ali's card in About Us > Leadership
-- (media/public-media/network/content/leadership-asad-sakhawat-ali.jpg).
-- The About sections are backed up in [archive].page_sections_20261107b. Nothing is deleted.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'16_photo_asad_sakhawat_ali')
BEGIN
    PRINT N'16_photo_asad_sakhawat_ali is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @about UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/about' AND deleted_at IS NULL);
IF @about IS NULL
    THROW 50029, N'16: the /about page was not found. Nothing was changed.', 1;
IF EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @about AND [type] = N'leadership' AND draft_data IS NOT NULL)
    THROW 50030, N'16: a leadership section on About Us has a waiting draft. Publish or discard it in the admin panel, then run 16 again. Nothing was changed.', 1;
IF OBJECT_ID(N'archive.page_sections_20261107b') IS NULL
    SELECT * INTO archive.page_sections_20261107b FROM dbo.page_sections WHERE page_id = @about;

UPDATE s SET updated_at = SYSDATETIMEOFFSET(),
    data = JSON_MODIFY(s.data, '$.items', JSON_QUERY(N'[' + COALESCE((SELECT STRING_AGG(CAST(
        CASE WHEN j.[type] = 5 AND JSON_VALUE(j.[value], '$.name') = N'Mr. Asad Sakhawat Ali'
                  THEN JSON_MODIFY(j.[value], '$.image', JSON_QUERY(N'{"bucket":"public-media","path":"network/content/leadership-asad-sakhawat-ali.jpg","alt":"Mr. Asad Sakhawat Ali, Executive Director, The Arqam Schools Pakistan","width":600,"height":745}'))
             ELSE j.[value] END AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY CAST(j.[key] AS INT))
        FROM OPENJSON(s.data, '$.items') j), N'') + N']'))
FROM dbo.page_sections s
WHERE s.page_id = @about AND s.[type] = N'leadership' AND s.data LIKE N'%Mr. Asad Sakhawat Ali%';
IF @@ROWCOUNT <> 1 THROW 50031, N'16: the leadership card of Mr. Asad Sakhawat Ali was not found. Nothing was changed.', 1;

INSERT INTO dbo.schema_migrations (id) VALUES (N'16_photo_asad_sakhawat_ali');
COMMIT TRANSACTION;
PRINT N'16_photo_asad_sakhawat_ali applied.';
GO
SET NOEXEC OFF;
GO
