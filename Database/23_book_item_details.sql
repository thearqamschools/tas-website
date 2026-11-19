-- =============================================================================
-- THE ARQAM SCHOOLS: BOOK DETAILS FOR THE OFFICE (19 November 2026)
-- Run after 22. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Owner's request: each book can have a vendor, the retail price printed on the book, a discount
-- percentage (the sale price is the retail price less that percentage, worked out by the website)
-- and its own minimum stock level for the low-stock warning.
-- All four are optional (NULL), so the 78 books already entered keep their sale price until the
-- office fills in the retail price and discount. Customers never see vendor, retail price or discount.
-- Only new columns are added; nothing is changed or deleted, so no archive copy is needed.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'23_book_item_details')
BEGIN
    PRINT N'23_book_item_details is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.book_items', N'vendor') IS NULL
    ALTER TABLE dbo.book_items ADD vendor NVARCHAR(150) NULL;
IF COL_LENGTH(N'dbo.book_items', N'retail_price') IS NULL
    ALTER TABLE dbo.book_items ADD retail_price DECIMAL(12,2) NULL
        CONSTRAINT CK_book_items_retail CHECK (retail_price IS NULL OR retail_price >= 0);
IF COL_LENGTH(N'dbo.book_items', N'discount_percent') IS NULL
    ALTER TABLE dbo.book_items ADD discount_percent DECIMAL(5,2) NULL
        CONSTRAINT CK_book_items_discount CHECK (discount_percent IS NULL OR (discount_percent >= 0 AND discount_percent <= 100));
IF COL_LENGTH(N'dbo.book_items', N'reorder_level') IS NULL
    ALTER TABLE dbo.book_items ADD reorder_level INT NULL
        CONSTRAINT CK_book_items_reorder CHECK (reorder_level IS NULL OR (reorder_level >= 0 AND reorder_level <= 99999));

INSERT INTO dbo.schema_migrations (id) VALUES (N'23_book_item_details');
COMMIT TRANSACTION;
PRINT N'23_book_item_details applied.';
GO
SET NOEXEC OFF;
GO
