-- =============================================================================
-- THE ARQAM SCHOOLS: BOOKS LIST FROM THE "TAS BOOKS QUANTITY FORM" (19 November 2026)
-- Run after 21. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Source: the owner's Word file "TAS_Books_Quantity_Form.docx" (class, product name, sale price).
-- 78 items: Play Group, Nursery, Prep, Class 1 to Class 7 and "All Classes" (diary, attendance
-- register, admission form, prospectus). The file's class names One…Seven are written as
-- Class 1…Class 7 (owner's choice) so the website sorts them in order.
-- Only the sale price is in the file: purchase price and stock start at 0 and are entered by the
-- office in Admin > Books. A book with no stock is not shown to customers, so nothing can be
-- ordered until stock is entered.
-- Nothing is deleted or changed; a book that already exists (same class and name, not archived)
-- is skipped.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'22_books_list')
BEGIN
    PRINT N'22_books_list is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @b TABLE (class_name NVARCHAR(60), name NVARCHAR(200), sale_price DECIMAL(12,2), sort_order INT);
INSERT INTO @b (class_name, name, sale_price, sort_order) VALUES
    (N'Play Group', N'English', 452, 10),
    (N'Play Group', N'EVS Step 1', 440, 20),
    (N'Play Group', N'Math', 440, 30),
    (N'Play Group', N'Kids Book', 444, 40),
    (N'Play Group', N'Urdu', 452, 50),
    (N'Play Group', N'Note Book English', 240, 60),
    (N'Play Group', N'Note Book Urdu', 240, 70),
    (N'Play Group', N'Note Book Math', 240, 80),
    (N'Nursery', N'English', 444, 10),
    (N'Nursery', N'EVS Step 2', 440, 20),
    (N'Nursery', N'Math', 444, 30),
    (N'Nursery', N'Kids Book', 444, 40),
    (N'Nursery', N'Urdu', 452, 50),
    (N'Nursery', N'Note Book English', 256, 60),
    (N'Nursery', N'Note Book Urdu', 256, 70),
    (N'Nursery', N'Note Book Math', 240, 80),
    (N'Prep', N'English', 452, 10),
    (N'Prep', N'EVS Step 3', 440, 20),
    (N'Prep', N'Kids Book', 444, 30),
    (N'Prep', N'Math', 440, 40),
    (N'Prep', N'Urdu', 448, 50),
    (N'Prep', N'Note Book English', 240, 60),
    (N'Prep', N'Note Book Urdu', 240, 70),
    (N'Prep', N'Note Book Math', 240, 80),
    (N'Class 1', N'Art and Design 1', 292.5, 10),
    (N'Class 1', N'Arqam Computer 1', 281.25, 20),
    (N'Class 1', N'Arqam English 1', 371.25, 30),
    (N'Class 1', N'Arqam General Knowledge 1', 363.75, 40),
    (N'Class 1', N'Arqam Mutala-a-Islam 1', 266.25, 50),
    (N'Class 1', N'Arqam Maths 1', 408.75, 60),
    (N'Class 1', N'Arqam Urdu 1', 371.25, 70),
    (N'Class 2', N'Art and Design 2', 292.5, 10),
    (N'Class 2', N'Arqam Computer 2', 318.75, 20),
    (N'Class 2', N'Arqam English 2', 371.25, 30),
    (N'Class 2', N'Arqam General Knowledge 2', 431.25, 40),
    (N'Class 2', N'Arqam Mutala-a-Islam 2', 356.25, 50),
    (N'Class 2', N'Arqam Math 2', 431.25, 60),
    (N'Class 2', N'Arqam Urdu 2', 436, 70),
    (N'Class 3', N'Art and Design 3', 292.5, 10),
    (N'Class 3', N'Arqam Computer 3', 356.25, 20),
    (N'Class 3', N'Arqam English 3', 371.25, 30),
    (N'Class 3', N'Arqam General Knowledge 3', 468.75, 40),
    (N'Class 3', N'Arqam Mutala-a-Islam 3', 393.75, 50),
    (N'Class 3', N'Arqam Math 3', 431.25, 60),
    (N'Class 3', N'Arqam Urdu 3', 408.75, 70),
    (N'Class 4', N'Art and Design 4', 292.5, 10),
    (N'Class 4', N'Arqam Computer 4', 371.25, 20),
    (N'Class 4', N'Arqam English 4', 408.75, 30),
    (N'Class 4', N'Arqam Mutala-a-Islam 4', 393.75, 40),
    (N'Class 4', N'Arqam Moasrti Aloom 4', 360, 50),
    (N'Class 4', N'Arqam Math 4', 435, 60),
    (N'Class 5', N'Art and Design 5', 292.5, 10),
    (N'Class 5', N'Arqam Computer 5', 431.25, 20),
    (N'Class 5', N'Arqam English 5', 446.25, 30),
    (N'Class 5', N'Arqam Mutala-a-Islam 5', 431.25, 40),
    (N'Class 5', N'Arqam Moasrti Aloom 5', 360, 50),
    (N'Class 5', N'Arqam Math 5', 435, 60),
    (N'Class 5', N'Arqam Science 5', 468.75, 70),
    (N'Class 5', N'Arqam Urdu 5', 446.25, 80),
    (N'Class 6', N'Nazra Quran 6', 300, 10),
    (N'Class 6', N'Arqam Computer 6', 408.75, 20),
    (N'Class 6', N'Arqam English 6', 446.25, 30),
    (N'Class 6', N'Arqam Geo 6', 408.75, 40),
    (N'Class 6', N'Arqam History 6', 408.75, 50),
    (N'Class 6', N'Arqam Mutala-a-Islam 6', 468.75, 60),
    (N'Class 6', N'Arqam Science 6', 506.25, 70),
    (N'Class 6', N'Arqam Urdu 6', 446.25, 80),
    (N'Class 7', N'Arqam Urdu 7', 446.25, 10),
    (N'Class 7', N'Arqam Computer 7', 483.75, 20),
    (N'Class 7', N'Arqam English 7', 446.25, 30),
    (N'Class 7', N'Arqam Geo 7', 408.75, 40),
    (N'Class 7', N'Arqam History 7', 408.75, 50),
    (N'Class 7', N'Arqam Mutala-a-Islam 7', 468.75, 60),
    (N'Class 7', N'Arqam Science 7', 506.25, 70),
    (N'All Classes', N'Diary', 110, 10),
    (N'All Classes', N'Student Attendance Register', 300, 20),
    (N'All Classes', N'Admission Form', 10, 30),
    (N'All Classes', N'Prospectus', 100, 40);

INSERT INTO dbo.book_items (class_name, name, stock, purchase_price, sale_price, is_active, sort_order)
SELECT b.class_name, b.name, 0, 0, b.sale_price, 1, b.sort_order
FROM @b b
WHERE NOT EXISTS (SELECT 1 FROM dbo.book_items i WHERE i.class_name = b.class_name AND i.name = b.name AND i.archived_at IS NULL);

INSERT INTO dbo.schema_migrations (id) VALUES (N'22_books_list');
COMMIT TRANSACTION;
PRINT N'22_books_list applied.';
GO
SET NOEXEC OFF;
GO
