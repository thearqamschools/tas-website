-- =============================================================================
-- THE ARQAM SCHOOLS: PHOTOS FOR NEWS POSTS AND EVENTS (10 November 2026)
-- Run after 16. Safe to run twice: it records itself in dbo.schema_migrations.
-- Adds dbo.post_images: the photo gallery of one news post or one event (20, 30 or more
-- photos each), uploaded in the admin panel. The main photo shown on the cards is the
-- existing news.cover_media_id / events.cover_media_id. Photos are removed by setting
-- deleted_at (soft delete); nothing is deleted. No existing rows are changed.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'17_news_event_photos')
BEGIN
    PRINT N'17_news_event_photos is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.post_images') IS NULL
BEGIN
    CREATE TABLE [dbo].[post_images] (
        [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_post_images_id] DEFAULT NEWSEQUENTIALID(),
        [news_id] UNIQUEIDENTIFIER NULL,
        [event_id] UNIQUEIDENTIFIER NULL,
        [media_id] UNIQUEIDENTIFIER NOT NULL,
        [caption] NVARCHAR(300) NULL,
        [sort_order] INT NOT NULL CONSTRAINT [DF_post_images_sort_order] DEFAULT 0,
        [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_post_images_created_at] DEFAULT SYSDATETIMEOFFSET(),
        [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_post_images_updated_at] DEFAULT SYSDATETIMEOFFSET(),
        [created_by] UNIQUEIDENTIFIER NULL,
        [updated_by] UNIQUEIDENTIFIER NULL,
        [deleted_at] DATETIMEOFFSET(3) NULL,
        CONSTRAINT [PK_post_images] PRIMARY KEY CLUSTERED ([id]),
        -- Each photo belongs to exactly one news post or one event.
        CONSTRAINT [CK_post_images_one_owner] CHECK (([news_id] IS NULL AND [event_id] IS NOT NULL) OR ([news_id] IS NOT NULL AND [event_id] IS NULL)),
        CONSTRAINT [CK_post_images_sort_order] CHECK ([sort_order] >= 0),
        CONSTRAINT [FK_post_images_news_id_fkey] FOREIGN KEY ([news_id]) REFERENCES [dbo].[news] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_post_images_event_id_fkey] FOREIGN KEY ([event_id]) REFERENCES [dbo].[events] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_post_images_media_id_fkey] FOREIGN KEY ([media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_post_images_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_post_images_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION
    );
    CREATE NONCLUSTERED INDEX [post_images_news_idx] ON [dbo].[post_images] ([news_id], [sort_order]) WHERE [news_id] IS NOT NULL AND [deleted_at] IS NULL;
    CREATE NONCLUSTERED INDEX [post_images_event_idx] ON [dbo].[post_images] ([event_id], [sort_order]) WHERE [event_id] IS NOT NULL AND [deleted_at] IS NULL;
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_post_images_media] ON [dbo].[post_images] ([media_id]);
END

INSERT INTO dbo.schema_migrations (id) VALUES (N'17_news_event_photos');
COMMIT TRANSACTION;
PRINT N'17_news_event_photos applied.';
GO
SET NOEXEC OFF;
GO
