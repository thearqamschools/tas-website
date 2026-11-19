-- =============================================================================
-- THE ARQAM SCHOOLS: BOOKS AND STATIONERY CATEGORIES (19 November 2026)
-- Run after 23. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Owner's request: besides books, the office sells stationery (diary, registers, forms and so on).
-- Each item gets a category, "books" or "stationery". Every existing item starts as "books";
-- Database/25 sets the stationery items. Only a new column is added; nothing is changed or deleted.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'24_book_categories')
BEGIN
    PRINT N'24_book_categories is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.book_items', N'category') IS NULL
    ALTER TABLE dbo.book_items ADD category NVARCHAR(20) NOT NULL
        CONSTRAINT DF_book_items_category DEFAULT N'books'
        CONSTRAINT CK_book_items_category CHECK (category IN (N'books', N'stationery'));

INSERT INTO dbo.schema_migrations (id) VALUES (N'24_book_categories');
COMMIT TRANSACTION;
PRINT N'24_book_categories applied.';
GO
SET NOEXEC OFF;
GO
