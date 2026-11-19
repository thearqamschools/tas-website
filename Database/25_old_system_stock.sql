-- =============================================================================
-- THE ARQAM SCHOOLS: BOOKS AND STATIONERY FROM THE OLD STOCK SYSTEM (19 November 2026)
-- Run after 24. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Source: the owner's "Product Stock" report from the old system (Crystal Reports, printed 19-Nov-26):
-- category, class, company (vendor), name, P.Price (retail price), Dis. % (discount), Dis.Price (sale price),
-- Stock I (in), Stock O (out) and Stock L (left). Owner's decisions:
-- - "Stock L" is the stock in hand today and becomes each item's stock. The change is kept in the stock
--   history as a correction with the note "Opening stock from the old system".
-- - Retail price, discount % and vendor come from the report; the sale price is retail less the discount
--   (it equals the report's Dis.Price on every line). Arqam Urdu 2 takes the report's price (Rs. 408.75).
-- - STATIONARY items get the category "stationery"; "Misc. Exp." lines (carriage) are not added.
-- - "S.V.W.B." is written as "Summer Vacation Work Book 26-27" (category books).
-- - Names are written the website's way: "Art and Design 1" in Class 1 (the report adds " - One"), the
--   report's typos "EVS Setp" and "Shashes" are corrected, "Mutala a Islam" is written "Mutala-a-Islam".
-- 104 report lines: 78 books already on the website are updated, 23 items are new, 3 Misc lines are skipped.
-- An item with a sale price of 0 (School Certificate) is added hidden, so it cannot be ordered for free.
-- The book_items rows are copied to archive.book_items_20261119 first. Nothing is deleted.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'25_old_system_stock')
BEGIN
    PRINT N'25_old_system_stock is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF OBJECT_ID(N'archive.book_items_20261119') IS NULL
    SELECT * INTO archive.book_items_20261119 FROM dbo.book_items;

DECLARE @s TABLE (category NVARCHAR(20), class_name NVARCHAR(60), name NVARCHAR(200), vendor NVARCHAR(150),
                  retail_price DECIMAL(12,2), discount_percent DECIMAL(5,2), sale_price DECIMAL(12,2), stock_left INT,
                  row_no INT IDENTITY(1,1));
INSERT INTO @s (category, class_name, name, vendor, retail_price, discount_percent, sale_price, stock_left) VALUES
    (N'books', N'Class 1', N'Art and Design 1', N'Print Expert LHR', 390.00, 25.00, 292.50, 581),
    (N'books', N'Class 2', N'Art and Design 2', N'Print Expert LHR', 390.00, 25.00, 292.50, 463),
    (N'books', N'Class 3', N'Art and Design 3', N'Print Expert LHR', 390.00, 25.00, 292.50, 40),
    (N'books', N'Class 4', N'Art and Design 4', N'Print Expert LHR', 390.00, 25.00, 292.50, 177),
    (N'books', N'Class 5', N'Art and Design 5', N'Print Expert LHR', 390.00, 25.00, 292.50, 113),
    (N'books', N'Nursery', N'English', N'Print Expert LHR', 555.00, 20.00, 444.00, 29),
    (N'books', N'Play Group', N'English', N'Print Expert LHR', 565.00, 20.00, 452.00, 370),
    (N'books', N'Prep', N'English', N'Print Expert LHR', 565.00, 20.00, 452.00, 431),
    (N'books', N'Play Group', N'EVS Step 1', N'Print Expert LHR', 550.00, 20.00, 440.00, 5),
    (N'books', N'Prep', N'EVS Step 3', N'Print Expert LHR', 550.00, 20.00, 440.00, 522),
    (N'books', N'Nursery', N'EVS Step 2', N'Print Expert LHR', 550.00, 20.00, 440.00, 43),
    (N'books', N'Nursery', N'Kids Book', N'Print Expert LHR', 555.00, 20.00, 444.00, 491),
    (N'books', N'Play Group', N'Kids Book', N'Print Expert LHR', 555.00, 20.00, 444.00, 96),
    (N'books', N'Prep', N'Kids Book', N'Print Expert LHR', 555.00, 20.00, 444.00, 573),
    (N'books', N'Nursery', N'Math', N'Print Expert LHR', 555.00, 20.00, 444.00, 31),
    (N'books', N'Play Group', N'Math', N'Print Expert LHR', 550.00, 20.00, 440.00, 119),
    (N'books', N'Prep', N'Math', N'Print Expert LHR', 550.00, 20.00, 440.00, 416),
    (N'books', N'Prep', N'Noorani Qaida', N'Print Expert LHR', 150.00, 20.00, 120.00, 1644),
    (N'books', N'Nursery', N'Note Book English', N'Print Expert LHR', 320.00, 20.00, 256.00, 138),
    (N'books', N'Play Group', N'Note Book English', N'Print Expert LHR', 300.00, 20.00, 240.00, 35),
    (N'books', N'Prep', N'Note Book English', N'Print Expert LHR', 300.00, 20.00, 240.00, 315),
    (N'books', N'Nursery', N'Note Book Math', N'Print Expert LHR', 300.00, 20.00, 240.00, 153),
    (N'books', N'Play Group', N'Note Book Math', N'Print Expert LHR', 300.00, 20.00, 240.00, 31),
    (N'books', N'Prep', N'Note Book Math', N'Print Expert LHR', 300.00, 20.00, 240.00, 327),
    (N'books', N'Nursery', N'Note Book Urdu', N'Print Expert LHR', 320.00, 20.00, 256.00, 298),
    (N'books', N'Play Group', N'Note Book Urdu', N'Print Expert LHR', 300.00, 20.00, 240.00, 39),
    (N'books', N'Prep', N'Note Book Urdu', N'Print Expert LHR', 300.00, 20.00, 240.00, 339),
    (N'books', N'Nursery', N'Urdu', N'Print Expert LHR', 565.00, 20.00, 452.00, 53),
    (N'books', N'Play Group', N'Urdu', N'Print Expert LHR', 565.00, 20.00, 452.00, 116),
    (N'books', N'Prep', N'Urdu', N'Print Expert LHR', 560.00, 20.00, 448.00, 814),
    (N'books', N'Class 1', N'Arqam Computer 1', N'Al Bakio LHR', 375.00, 25.00, 281.25, 687),
    (N'books', N'Class 2', N'Arqam Computer 2', N'Al Bakio LHR', 425.00, 25.00, 318.75, 95),
    (N'books', N'Class 3', N'Arqam Computer 3', N'Al Bakio LHR', 475.00, 25.00, 356.25, 243),
    (N'books', N'Class 4', N'Arqam Computer 4', N'Al Bakio LHR', 495.00, 25.00, 371.25, 414),
    (N'books', N'Class 5', N'Arqam Computer 5', N'Al Bakio LHR', 575.00, 25.00, 431.25, 192),
    (N'books', N'Class 6', N'Arqam Computer 6', N'Al Bakio LHR', 545.00, 25.00, 408.75, 111),
    (N'books', N'Class 7', N'Arqam Computer 7', N'Al Bakio LHR', 645.00, 25.00, 483.75, 100),
    (N'books', N'Class 1', N'Arqam English 1', N'Al Bakio LHR', 495.00, 25.00, 371.25, 663),
    (N'books', N'Class 2', N'Arqam English 2', N'Al Bakio LHR', 495.00, 25.00, 371.25, 503),
    (N'books', N'Class 3', N'Arqam English 3', N'Al Bakio LHR', 495.00, 25.00, 371.25, 765),
    (N'books', N'Class 4', N'Arqam English 4', N'Al Bakio LHR', 545.00, 25.00, 408.75, 723),
    (N'books', N'Class 5', N'Arqam English 5', N'Al Bakio LHR', 595.00, 25.00, 446.25, 231),
    (N'books', N'Class 6', N'Arqam English 6', N'Al Bakio LHR', 595.00, 25.00, 446.25, 122),
    (N'books', N'Class 7', N'Arqam English 7', N'Al Bakio LHR', 595.00, 25.00, 446.25, 104),
    (N'books', N'Class 1', N'Arqam General Knowledge 1', N'Al Bakio LHR', 485.00, 25.00, 363.75, 590),
    (N'books', N'Class 2', N'Arqam General Knowledge 2', N'Al Bakio LHR', 575.00, 25.00, 431.25, 641),
    (N'books', N'Class 3', N'Arqam General Knowledge 3', N'Al Bakio LHR', 625.00, 25.00, 468.75, 689),
    (N'books', N'Class 6', N'Arqam Geo 6', N'Al Bakio LHR', 545.00, 25.00, 408.75, 105),
    (N'books', N'Class 7', N'Arqam Geo 7', N'Al Bakio LHR', 545.00, 25.00, 408.75, 165),
    (N'books', N'Class 6', N'Arqam History 6', N'Al Bakio LHR', 545.00, 25.00, 408.75, 176),
    (N'books', N'Class 7', N'Arqam History 7', N'Al Bakio LHR', 545.00, 25.00, 408.75, 142),
    (N'books', N'Class 2', N'Arqam Math 2', N'Al Bakio LHR', 575.00, 25.00, 431.25, 203),
    (N'books', N'Class 3', N'Arqam Math 3', N'Al Bakio LHR', 575.00, 25.00, 431.25, 563),
    (N'books', N'Class 4', N'Arqam Math 4', N'Al Bakio LHR', 580.00, 25.00, 435.00, 779),
    (N'books', N'Class 5', N'Arqam Math 5', N'Al Bakio LHR', 580.00, 25.00, 435.00, 323),
    (N'books', N'Class 1', N'Arqam Maths 1', N'Al Bakio LHR', 545.00, 25.00, 408.75, 861),
    (N'books', N'Class 4', N'Arqam Moasrti Aloom 4', N'Al Bakio LHR', 480.00, 25.00, 360.00, 933),
    (N'books', N'Class 5', N'Arqam Moasrti Aloom 5', N'Al Bakio LHR', 480.00, 25.00, 360.00, 615),
    (N'books', N'Class 1', N'Arqam Mutala-a-Islam 1', N'Al Bakio LHR', 355.00, 25.00, 266.25, 533),
    (N'books', N'Class 2', N'Arqam Mutala-a-Islam 2', N'Al Bakio LHR', 475.00, 25.00, 356.25, 526),
    (N'books', N'Class 3', N'Arqam Mutala-a-Islam 3', N'Al Bakio LHR', 525.00, 25.00, 393.75, 359),
    (N'books', N'Class 4', N'Arqam Mutala-a-Islam 4', N'Al Bakio LHR', 525.00, 25.00, 393.75, 454),
    (N'books', N'Class 5', N'Arqam Mutala-a-Islam 5', N'Al Bakio LHR', 575.00, 25.00, 431.25, 456),
    (N'books', N'Class 6', N'Arqam Mutala-a-Islam 6', N'Al Bakio LHR', 625.00, 25.00, 468.75, 134),
    (N'books', N'Class 7', N'Arqam Mutala-a-Islam 7', N'Al Bakio LHR', 625.00, 25.00, 468.75, 257),
    (N'books', N'Class 4', N'Arqam Science 4', N'Al Bakio LHR', 625.00, 25.00, 468.75, 518),
    (N'books', N'Class 5', N'Arqam Science 5', N'Al Bakio LHR', 625.00, 25.00, 468.75, 209),
    (N'books', N'Class 6', N'Arqam Science 6', N'Al Bakio LHR', 675.00, 25.00, 506.25, 85),
    (N'books', N'Class 7', N'Arqam Science 7', N'Al Bakio LHR', 675.00, 25.00, 506.25, 75),
    (N'books', N'Class 1', N'Arqam Urdu 1', N'Al Bakio LHR', 495.00, 25.00, 371.25, 549),
    (N'books', N'Class 2', N'Arqam Urdu 2', N'Al Bakio LHR', 545.00, 25.00, 408.75, 641),
    (N'books', N'Class 3', N'Arqam Urdu 3', N'Al Bakio LHR', 545.00, 25.00, 408.75, 635),
    (N'books', N'Class 4', N'Arqam Urdu 4', N'Al Bakio LHR', 595.00, 25.00, 446.25, 213),
    (N'books', N'Class 5', N'Arqam Urdu 5', N'Al Bakio LHR', 595.00, 25.00, 446.25, 217),
    (N'books', N'Class 6', N'Arqam Urdu 6', N'Al Bakio LHR', 595.00, 25.00, 446.25, 162),
    (N'books', N'Class 7', N'Arqam Urdu 7', N'Al Bakio LHR', 595.00, 25.00, 446.25, 90),
    (N'books', N'Class 1', N'Nazra Quran 1', N'CEF Publications', 300.00, 25.00, 225.00, 0),
    (N'books', N'Class 2', N'Nazra Quran 2', N'CEF Publications', 380.00, 25.00, 285.00, 0),
    (N'books', N'Class 3', N'Nazra Quran 3', N'CEF Publications', 390.00, 25.00, 292.50, 0),
    (N'books', N'Class 4', N'Nazra Quran 4', N'CEF Publications', 430.00, 25.00, 322.50, 0),
    (N'books', N'Class 5', N'Nazra Quran 5', N'CEF Publications', 475.00, 25.00, 356.25, 0),
    (N'books', N'Class 6', N'Nazra Quran 6', N'CEF Publications', 400.00, 25.00, 300.00, 305),
    (N'books', N'Class 7', N'Nazra Quran 7', N'CEF Publications', 510.00, 25.00, 382.50, 223),
    (N'stationery', N'All Classes', N'Diary', N'Print Expert LHR', 110.00, 0.00, 110.00, 3656),
    (N'stationery', N'All Classes', N'Note Book 3 in 1', N'Print Expert LHR', 165.00, 9.00, 150.15, 942),
    (N'stationery', N'All Classes', N'Admission Form', N'The Arqam Publications', 10.00, 0.00, 10.00, 974),
    (N'stationery', N'All Classes', N'Monogram TAS', N'The Arqam Publications', 50.00, 0.00, 50.00, 130),
    (N'stationery', N'All Classes', N'Prospectus', N'The Arqam Publications', 100.00, 0.00, 100.00, 4),
    (N'stationery', N'All Classes', N'School Certificate', N'The Arqam Publications', 0.00, 0.00, 0.00, 0),
    (N'stationery', N'All Classes', N'Sash (Prefect)', N'The Arqam Publications', 200.00, 0.00, 200.00, 0),
    (N'stationery', N'All Classes', N'Student Attendance Register', N'The Arqam Publications', 300.00, 0.00, 300.00, 130),
    (N'stationery', N'All Classes', N'TAS Ball Point', N'The Arqam Publications', 15.00, 0.00, 15.00, 0),
    (N'stationery', N'All Classes', N'TAS Pointer', N'The Arqam Publications', 18.00, 0.00, 18.00, 0),
    (N'books', N'Class 5', N'Summer Vacation Work Book 26-27', N'Print Expert LHR', 300.00, 0.00, 300.00, 3),
    (N'books', N'Class 4', N'Summer Vacation Work Book 26-27', N'Print Expert LHR', 300.00, 0.00, 300.00, 36),
    (N'books', N'Nursery', N'Summer Vacation Work Book 26-27', N'Print Expert LHR', 300.00, 0.00, 300.00, 1),
    (N'books', N'Class 1', N'Summer Vacation Work Book 26-27', N'Print Expert LHR', 300.00, 0.00, 300.00, 6),
    (N'books', N'Play Group', N'Summer Vacation Work Book 26-27', N'Print Expert LHR', 300.00, 0.00, 300.00, 14),
    (N'books', N'Prep', N'Summer Vacation Work Book 26-27', N'Print Expert LHR', 300.00, 0.00, 300.00, 3),
    (N'books', N'Class 3', N'Summer Vacation Work Book 26-27', N'Print Expert LHR', 300.00, 0.00, 300.00, 11),
    (N'books', N'Class 2', N'Summer Vacation Work Book 26-27', N'Print Expert LHR', 300.00, 0.00, 300.00, 1);

-- 1. Items already on the website: vendor, prices and category from the report.
UPDATE i SET i.category = s.category, i.vendor = s.vendor, i.retail_price = s.retail_price,
             i.discount_percent = s.discount_percent, i.sale_price = s.sale_price, i.updated_at = SYSDATETIMEOFFSET()
FROM dbo.book_items i JOIN @s s ON s.class_name = i.class_name AND s.name = i.name
WHERE i.archived_at IS NULL;

-- 2. New items (stock 0 for now; step 3 sets it).
INSERT INTO dbo.book_items (category, class_name, name, vendor, retail_price, discount_percent, sale_price, purchase_price, stock, is_active, sort_order)
SELECT s.category, s.class_name, s.name, s.vendor, s.retail_price, s.discount_percent, s.sale_price, 0, 0,
       CASE WHEN s.sale_price > 0 THEN 1 ELSE 0 END, 100 + s.row_no
FROM @s s
WHERE NOT EXISTS (SELECT 1 FROM dbo.book_items i WHERE i.class_name = s.class_name AND i.name = s.name AND i.archived_at IS NULL);

-- 3. Stock in hand = the report's "Stock L", recorded as a correction in the stock history.
DECLARE @today DATE = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:00') AS DATE);
INSERT INTO dbo.book_stock_moves (item_id, kind, change, stock_after, moved_on, note)
SELECT i.id, N'correction', s.stock_left - i.stock, s.stock_left, @today, N'Opening stock from the old system (Stock Left on 19 Nov 2026)'
FROM dbo.book_items i WITH (UPDLOCK, HOLDLOCK) JOIN @s s ON s.class_name = i.class_name AND s.name = i.name
WHERE i.archived_at IS NULL AND i.stock <> s.stock_left;

UPDATE i SET i.stock = s.stock_left, i.updated_at = SYSDATETIMEOFFSET()
FROM dbo.book_items i JOIN @s s ON s.class_name = i.class_name AND s.name = i.name
WHERE i.archived_at IS NULL AND i.stock <> s.stock_left;

INSERT INTO dbo.schema_migrations (id) VALUES (N'25_old_system_stock');
COMMIT TRANSACTION;
PRINT N'25_old_system_stock applied.';
GO
SET NOEXEC OFF;
GO
