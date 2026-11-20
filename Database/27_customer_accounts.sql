-- =============================================================================
-- THE ARQAM SCHOOLS: CUSTOMER ACCOUNTS, CUSTOMER RETURNS AND MESSAGES (20 November 2026)
-- Run after 26. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Owner's requests:
-- - Every book customer gets a customer number (C-1001, C-1002 ...) and may get a credit limit (rupees)
--   and credit days (how long a handed-over order may stay unpaid). Both are optional.
-- - Returns are recorded per customer, not per order: the books may come from several orders. Each return
--   line still points to the order line it reduces. A return's money either becomes the customer's credit
--   or is paid back; a payback can wait ("refund pending") until the office records how it was paid.
-- - Customers can ask for a return on the website (book_return_requests); the office approves or rejects,
--   and records it as received when the books arrive. The request and the return share one number (RN-...).
-- - book_messages keeps ready-made messages (order placed, confirmed, payment, packed ...) that the office
--   copies into WhatsApp or SMS; the website never sends anything itself.
-- Existing rows: returns get their customer from their order; old paid-back returns are marked as paid.
-- Nothing is deleted. Changed rows are copied first to archive.book_customers_20261120 and
-- archive.book_returns_20261120.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'27_customer_accounts')
BEGIN
    PRINT N'27_customer_accounts is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF OBJECT_ID(N'archive.book_customers_20261120') IS NULL SELECT * INTO archive.book_customers_20261120 FROM dbo.book_customers;
IF OBJECT_ID(N'archive.book_returns_20261120') IS NULL SELECT * INTO archive.book_returns_20261120 FROM dbo.book_returns;

-- Customers: number, credit limit, credit days.
IF OBJECT_ID(N'dbo.book_customer_no_seq') IS NULL
    EXEC (N'CREATE SEQUENCE dbo.book_customer_no_seq AS BIGINT START WITH 1001 INCREMENT BY 1;');
IF COL_LENGTH(N'dbo.book_customers', N'customer_no') IS NULL
    ALTER TABLE dbo.book_customers ADD customer_no BIGINT NULL;
IF COL_LENGTH(N'dbo.book_customers', N'credit_limit') IS NULL
    ALTER TABLE dbo.book_customers ADD credit_limit DECIMAL(12,2) NULL CONSTRAINT CK_book_customers_limit CHECK (credit_limit IS NULL OR credit_limit >= 0);
IF COL_LENGTH(N'dbo.book_customers', N'credit_days') IS NULL
    ALTER TABLE dbo.book_customers ADD credit_days INT NULL CONSTRAINT CK_book_customers_days CHECK (credit_days IS NULL OR (credit_days >= 0 AND credit_days <= 3650));

EXEC sp_executesql N'
DECLARE @id UNIQUEIDENTIFIER;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT id FROM dbo.book_customers WHERE customer_no IS NULL ORDER BY created_at, id;
OPEN c; FETCH NEXT FROM c INTO @id;
WHILE @@FETCH_STATUS = 0
BEGIN
    UPDATE dbo.book_customers SET customer_no = NEXT VALUE FOR dbo.book_customer_no_seq WHERE id = @id;
    FETCH NEXT FROM c INTO @id;
END
CLOSE c; DEALLOCATE c;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N''DF_book_customers_no'')
    ALTER TABLE dbo.book_customers ADD CONSTRAINT DF_book_customers_no DEFAULT (NEXT VALUE FOR dbo.book_customer_no_seq) FOR customer_no;
ALTER TABLE dbo.book_customers ALTER COLUMN customer_no BIGINT NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''UQ_book_customers_no'')
    CREATE UNIQUE NONCLUSTERED INDEX UQ_book_customers_no ON dbo.book_customers (customer_no);';

-- Returns per customer.
IF COL_LENGTH(N'dbo.book_returns', N'customer_id') IS NULL
    ALTER TABLE dbo.book_returns ADD customer_id UNIQUEIDENTIFIER NULL CONSTRAINT FK_book_returns_customer FOREIGN KEY REFERENCES dbo.book_customers (id);
IF COL_LENGTH(N'dbo.book_returns', N'credit_amount') IS NULL
    ALTER TABLE dbo.book_returns ADD credit_amount DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_returns_credit DEFAULT 0 CONSTRAINT CK_book_returns_credit CHECK (credit_amount >= 0);
IF COL_LENGTH(N'dbo.book_returns', N'refund_paid_at') IS NULL
    ALTER TABLE dbo.book_returns ADD refund_paid_at DATETIMEOFFSET(3) NULL;
IF COL_LENGTH(N'dbo.book_returns', N'refund_paid_by') IS NULL
    ALTER TABLE dbo.book_returns ADD refund_paid_by UNIQUEIDENTIFIER NULL CONSTRAINT FK_book_returns_paid_by FOREIGN KEY REFERENCES dbo.users (id);
IF COL_LENGTH(N'dbo.book_return_lines', N'net_amount') IS NULL
    ALTER TABLE dbo.book_return_lines ADD net_amount DECIMAL(12,2) NULL CONSTRAINT CK_book_return_lines_net CHECK (net_amount IS NULL OR net_amount >= 0);

EXEC sp_executesql N'
UPDATE r SET r.customer_id = o.customer_id FROM dbo.book_returns r JOIN dbo.book_orders o ON o.id = r.order_id WHERE r.customer_id IS NULL;
UPDATE dbo.book_returns SET refund_paid_at = created_at, refund_paid_by = created_by WHERE refund_amount > 0 AND refund_paid_at IS NULL;
ALTER TABLE dbo.book_returns ALTER COLUMN customer_id UNIQUEIDENTIFIER NOT NULL;
ALTER TABLE dbo.book_returns ALTER COLUMN order_id UNIQUEIDENTIFIER NULL;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N''CK_book_returns_method_needed'')
    ALTER TABLE dbo.book_returns DROP CONSTRAINT CK_book_returns_method_needed;
ALTER TABLE dbo.book_returns ADD CONSTRAINT CK_book_returns_method_needed
    CHECK (refund_amount = 0 OR refund_paid_at IS NULL OR (refund_method IS NOT NULL AND refund_account_id IS NOT NULL));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''book_returns_customer_idx'')
    CREATE NONCLUSTERED INDEX book_returns_customer_idx ON dbo.book_returns (customer_id, refunded_on);';

-- Return requests from the website.
IF OBJECT_ID(N'dbo.book_return_requests') IS NULL
BEGIN
    CREATE TABLE dbo.book_return_requests (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_rr_id DEFAULT NEWSEQUENTIALID(),
        return_no BIGINT NOT NULL,
        customer_id UNIQUEIDENTIFIER NOT NULL,
        status NVARCHAR(12) NOT NULL CONSTRAINT DF_book_rr_status DEFAULT N'requested',
        choice NVARCHAR(10) NOT NULL,
        note NVARCHAR(500) NULL,
        reject_reason NVARCHAR(300) NULL,
        return_id UNIQUEIDENTIFIER NULL,
        reviewed_by UNIQUEIDENTIFIER NULL,
        reviewed_at DATETIMEOFFSET(3) NULL,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_rr_created DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_book_return_requests PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_rr_status CHECK (status IN (N'requested', N'approved', N'rejected', N'received')),
        CONSTRAINT CK_book_rr_choice CHECK (choice IN (N'credit', N'refund')),
        CONSTRAINT CK_book_rr_received CHECK ((status = N'received' AND return_id IS NOT NULL) OR (status <> N'received' AND return_id IS NULL)),
        CONSTRAINT FK_book_rr_customer FOREIGN KEY (customer_id) REFERENCES dbo.book_customers (id),
        CONSTRAINT FK_book_rr_return FOREIGN KEY (return_id) REFERENCES dbo.book_returns (id),
        CONSTRAINT FK_book_rr_user FOREIGN KEY (reviewed_by) REFERENCES dbo.users (id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_book_rr_no ON dbo.book_return_requests (return_no);
    CREATE NONCLUSTERED INDEX book_rr_customer_idx ON dbo.book_return_requests (customer_id, created_at);
END
IF OBJECT_ID(N'dbo.book_return_request_lines') IS NULL
    CREATE TABLE dbo.book_return_request_lines (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_rrl_id DEFAULT NEWSEQUENTIALID(),
        request_id UNIQUEIDENTIFIER NOT NULL,
        item_id UNIQUEIDENTIFIER NOT NULL,
        quantity INT NOT NULL,
        CONSTRAINT PK_book_return_request_lines PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_rrl_qty CHECK (quantity > 0 AND quantity <= 9999),
        CONSTRAINT FK_book_rrl_request FOREIGN KEY (request_id) REFERENCES dbo.book_return_requests (id),
        CONSTRAINT FK_book_rrl_item FOREIGN KEY (item_id) REFERENCES dbo.book_items (id)
    );

-- Ready-made messages for WhatsApp / SMS (sent by the office by hand).
IF OBJECT_ID(N'dbo.book_messages') IS NULL
BEGIN
    CREATE TABLE dbo.book_messages (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_msg_id DEFAULT NEWSEQUENTIALID(),
        customer_id UNIQUEIDENTIFIER NOT NULL,
        order_id UNIQUEIDENTIFIER NULL,
        return_no BIGINT NULL,
        kind NVARCHAR(30) NOT NULL,
        body NVARCHAR(1000) NOT NULL,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_msg_created DEFAULT SYSDATETIMEOFFSET(),
        sent_at DATETIMEOFFSET(3) NULL,
        sent_by UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_book_messages PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_msg_body CHECK (LEN(body) >= 1),
        CONSTRAINT FK_book_msg_customer FOREIGN KEY (customer_id) REFERENCES dbo.book_customers (id),
        CONSTRAINT FK_book_msg_order FOREIGN KEY (order_id) REFERENCES dbo.book_orders (id),
        CONSTRAINT FK_book_msg_user FOREIGN KEY (sent_by) REFERENCES dbo.users (id)
    );
    CREATE NONCLUSTERED INDEX book_msg_customer_idx ON dbo.book_messages (customer_id, created_at);
    CREATE NONCLUSTERED INDEX book_msg_unsent_idx ON dbo.book_messages (created_at) WHERE sent_at IS NULL;
END

INSERT INTO dbo.schema_migrations (id) VALUES (N'27_customer_accounts');
COMMIT TRANSACTION;
PRINT N'27_customer_accounts applied.';
GO
SET NOEXEC OFF;
GO
