-- =============================================================================
-- THE ARQAM SCHOOLS: PAYMENTS FOR A CUSTOMER'S BALANCE, AND REMOVING CANCELLED ORDERS (21 November 2026)
-- Run after 27. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Owner's requests:
-- - On "My payments" a customer can send one payment for what they owe, not for one order. When the office
--   confirms it, the money is applied to the customer's OLDEST unpaid orders first; anything left becomes credit.
--   book_payment_submissions therefore gets customer_id (filled from the order for existing rows), order_id may be
--   empty, and credit_amount keeps the part that became credit.
-- - A Super Admin can remove a cancelled order that has no payment, no stock taken and no return. The order is
--   hidden everywhere (archived_at), not deleted, so it can be brought back.
-- Changed rows are copied first to archive.book_payment_submissions_20261121. Nothing is deleted.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'28_payments_and_removed_orders')
BEGIN
    PRINT N'28_payments_and_removed_orders is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF OBJECT_ID(N'archive.book_payment_submissions_20261121') IS NULL
    SELECT * INTO archive.book_payment_submissions_20261121 FROM dbo.book_payment_submissions;

IF COL_LENGTH(N'dbo.book_payment_submissions', N'customer_id') IS NULL
    ALTER TABLE dbo.book_payment_submissions ADD customer_id UNIQUEIDENTIFIER NULL
        CONSTRAINT FK_book_pay_sub_customer FOREIGN KEY REFERENCES dbo.book_customers (id);
IF COL_LENGTH(N'dbo.book_payment_submissions', N'credit_amount') IS NULL
    ALTER TABLE dbo.book_payment_submissions ADD credit_amount DECIMAL(12,2) NOT NULL
        CONSTRAINT DF_book_pay_sub_credit DEFAULT 0 CONSTRAINT CK_book_pay_sub_credit CHECK (credit_amount >= 0);
IF COL_LENGTH(N'dbo.book_orders', N'archived_at') IS NULL
    ALTER TABLE dbo.book_orders ADD archived_at DATETIMEOFFSET(3) NULL;
IF COL_LENGTH(N'dbo.book_orders', N'archived_by') IS NULL
    ALTER TABLE dbo.book_orders ADD archived_by UNIQUEIDENTIFIER NULL CONSTRAINT FK_book_orders_archived_by FOREIGN KEY REFERENCES dbo.users (id);

EXEC sp_executesql N'
UPDATE s SET s.customer_id = o.customer_id FROM dbo.book_payment_submissions s JOIN dbo.book_orders o ON o.id = s.order_id WHERE s.customer_id IS NULL;
ALTER TABLE dbo.book_payment_submissions ALTER COLUMN customer_id UNIQUEIDENTIFIER NOT NULL;
ALTER TABLE dbo.book_payment_submissions ALTER COLUMN order_id UNIQUEIDENTIFIER NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''book_pay_sub_customer_idx'')
    CREATE NONCLUSTERED INDEX book_pay_sub_customer_idx ON dbo.book_payment_submissions (customer_id, created_at);
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N''CK_book_orders_archived'')
    ALTER TABLE dbo.book_orders DROP CONSTRAINT CK_book_orders_archived;
ALTER TABLE dbo.book_orders ADD CONSTRAINT CK_book_orders_archived
    CHECK (archived_at IS NULL OR (status = N''cancelled'' AND paid_total = 0 AND discount_total = 0 AND refunded_total = 0 AND stock_taken_at IS NULL AND returned_total = 0));';

INSERT INTO dbo.schema_migrations (id) VALUES (N'28_payments_and_removed_orders');
COMMIT TRANSACTION;
PRINT N'28_payments_and_removed_orders applied.';
GO
SET NOEXEC OFF;
GO
