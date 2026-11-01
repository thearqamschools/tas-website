-- =============================================================================
-- THE ARQAM SCHOOLS: CONTACT AND FRANCHISE FORMS (1 November 2026)
-- Run after 05. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- What it does:
--  1. Backs up the page sections of /contact and /franchise/apply into
--     [archive].page_sections_20261101 (both pages had no sections before).
--  2. Adds a "contact_form" section to /contact and a "franchise_form" section
--     to /franchise/apply, unless the page already has one. The forms post to
--     /contact/send and /franchise/apply/send (ContactController).
--  If either page is missing, it stops with an error and changes nothing.
--  Nothing is deleted. Headings and intros can be edited in the admin panel
--  (Pages); the anchors ("contact", "apply"), which the forms' redirects use,
--  are not shown there. Contact details on /contact come from the website settings.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'06_contact_franchise_forms')
BEGIN
    PRINT N'06_contact_franchise_forms is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @p_contact UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/contact' AND deleted_at IS NULL);
DECLARE @p_apply UNIQUEIDENTIFIER = (SELECT id FROM dbo.pages WHERE path = N'/franchise/apply' AND deleted_at IS NULL);

IF @p_contact IS NULL OR @p_apply IS NULL
    THROW 50006, N'06: the /contact or /franchise/apply page was not found. Nothing was changed; check the pages and run again.', 1;

IF OBJECT_ID(N'archive.page_sections_20261101') IS NULL
    SELECT * INTO archive.page_sections_20261101 FROM dbo.page_sections WHERE page_id IN (@p_contact, @p_apply);

IF @p_contact IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @p_contact AND [type] = N'contact_form')
    INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible) VALUES
      (@p_contact, N'contact_form',
       N'{"anchor":"contact","tone":"white","heading":"Send us a message","intro":"Write to us with a question, feedback or a media request. We will reply by phone or email.","success":"Thank you. Your message has been received and we will reply soon."}',
       10, 1);

IF @p_apply IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.page_sections WHERE page_id = @p_apply AND [type] = N'franchise_form')
    INSERT INTO dbo.page_sections (page_id, [type], data, sort_order, is_visible) VALUES
      (@p_apply, N'franchise_form',
       N'{"anchor":"apply","tone":"white","heading":"Franchise application"}',
       10, 1);

INSERT INTO dbo.schema_migrations (id) VALUES (N'06_contact_franchise_forms');
COMMIT TRANSACTION;
PRINT N'06_contact_franchise_forms applied.';
GO
SET NOEXEC OFF;
GO
