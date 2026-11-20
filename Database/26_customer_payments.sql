-- =============================================================================
-- THE ARQAM SCHOOLS: CUSTOMERS SEND THEIR BOOK PAYMENTS ONLINE (20 November 2026)
-- Run after 25. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Owner's request: after placing an order the customer chooses one of the school's bank accounts, pays,
-- types the amount and attaches the receipt. The office checks it and confirms; only then is the payment
-- recorded (dbo.book_payments, first payment takes the books off stock) and the order becomes Confirmed.
-- - dbo.book_accounts gets account_title, account_number, iban and show_to_customers (bank accounts the
--   customer may choose). The two accounts the owner gave are added (BankIslami, Bank Alfalah), unless an
--   account with the same IBAN already exists.
-- - dbo.book_payment_submissions keeps every payment a customer sends: pending, confirmed (with the
--   payment it became) or rejected (with the reason). Rows are never deleted.
-- Nothing existing is changed or deleted, so no archive copy is needed.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'26_customer_payments')
BEGIN
    PRINT N'26_customer_payments is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.book_accounts', N'account_title') IS NULL
    ALTER TABLE dbo.book_accounts ADD account_title NVARCHAR(150) NULL;
IF COL_LENGTH(N'dbo.book_accounts', N'account_number') IS NULL
    ALTER TABLE dbo.book_accounts ADD account_number NVARCHAR(40) NULL
        CONSTRAINT CK_book_accounts_number CHECK (account_number IS NULL OR REGEXP_LIKE(account_number, N'^[0-9 -]{4,40}$'));
IF COL_LENGTH(N'dbo.book_accounts', N'iban') IS NULL
    ALTER TABLE dbo.book_accounts ADD iban NVARCHAR(34) NULL
        CONSTRAINT CK_book_accounts_iban CHECK (iban IS NULL OR REGEXP_LIKE(iban, N'^[A-Z]{2}[0-9]{2}[A-Z0-9]{10,30}$'));
IF COL_LENGTH(N'dbo.book_accounts', N'show_to_customers') IS NULL
    ALTER TABLE dbo.book_accounts ADD show_to_customers BIT NOT NULL CONSTRAINT DF_book_accounts_show DEFAULT 0;

IF OBJECT_ID(N'dbo.book_payment_submissions') IS NULL
BEGIN
    CREATE TABLE dbo.book_payment_submissions (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_pay_sub_id DEFAULT NEWSEQUENTIALID(),
        order_id UNIQUEIDENTIFIER NOT NULL,
        account_id UNIQUEIDENTIFIER NOT NULL,
        amount DECIMAL(12,2) NOT NULL,
        receipt_path NVARCHAR(300) NOT NULL,
        receipt_mime NVARCHAR(100) NOT NULL,
        status NVARCHAR(12) NOT NULL CONSTRAINT DF_book_pay_sub_status DEFAULT N'pending',
        payment_id UNIQUEIDENTIFIER NULL,
        reject_reason NVARCHAR(300) NULL,
        reviewed_by UNIQUEIDENTIFIER NULL,
        reviewed_at DATETIMEOFFSET(3) NULL,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_pay_sub_created DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_book_payment_submissions PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_pay_sub_amount CHECK (amount > 0),
        CONSTRAINT CK_book_pay_sub_status CHECK (status IN (N'pending', N'confirmed', N'rejected')),
        CONSTRAINT CK_book_pay_sub_done CHECK ((status = N'confirmed' AND payment_id IS NOT NULL) OR (status <> N'confirmed' AND payment_id IS NULL)),
        CONSTRAINT CK_book_pay_sub_path CHECK (NOT REGEXP_LIKE(receipt_path, N'\.\.')),
        CONSTRAINT FK_book_pay_sub_order FOREIGN KEY (order_id) REFERENCES dbo.book_orders (id),
        CONSTRAINT FK_book_pay_sub_account FOREIGN KEY (account_id) REFERENCES dbo.book_accounts (id),
        CONSTRAINT FK_book_pay_sub_payment FOREIGN KEY (payment_id) REFERENCES dbo.book_payments (id),
        CONSTRAINT FK_book_pay_sub_user FOREIGN KEY (reviewed_by) REFERENCES dbo.users (id)
    );
    CREATE NONCLUSTERED INDEX book_pay_sub_order_idx ON dbo.book_payment_submissions (order_id, created_at);
    CREATE NONCLUSTERED INDEX book_pay_sub_pending_idx ON dbo.book_payment_submissions (status) WHERE status = N'pending';
END

-- The two bank accounts the owner gave (new columns, so through sp_executesql in this batch).
EXEC sp_executesql N'
IF NOT EXISTS (SELECT 1 FROM dbo.book_accounts WHERE iban = N''PK88BKIP0223900012520001'')
    INSERT INTO dbo.book_accounts (name, kind, details, is_active, account_title, account_number, iban, show_to_customers)
    VALUES (N''BankIslami'', N''bank'', NULL, 1, N''THE ARQAM SCHOOLS'', N''223900012520001'', N''PK88BKIP0223900012520001'', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.book_accounts WHERE iban = N''PK37ALFH5644005000834141'')
    INSERT INTO dbo.book_accounts (name, kind, details, is_active, account_title, account_number, iban, show_to_customers)
    VALUES (N''Bank Alfalah'', N''bank'', NULL, 1, N''THE ARQAM SCHOOLS'', N''56445000834141'', N''PK37ALFH5644005000834141'', 1);';

INSERT INTO dbo.schema_migrations (id) VALUES (N'26_customer_payments');
COMMIT TRANSACTION;
PRINT N'26_customer_payments applied.';
GO
SET NOEXEC OFF;
GO
