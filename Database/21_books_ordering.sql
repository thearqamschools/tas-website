-- =============================================================================
-- THE ARQAM SCHOOLS: BOOKS ORDERING (19 November 2026)
-- Run after 20. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- A separate books system (owner's specification "Books ordering on the website"):
--   book_items          books by class, with stock, purchase and sale price (entered by the office)
--   book_stock_moves    every stock change (purchase, sale, return, correction) with user and date
--   book_customers      customers registered by the office: mobile (main key), name, campus, address
--   book_orders         orders (number from dbo.book_order_no_seq), status, totals
--   book_order_lines    the books of an order, with the price at the time of the order
--   book_order_edits    every change made to an order, and by whom
--   book_accounts       cash and bank accounts that payments go into
--   book_payments       payments and discounts (stock is taken off at the first payment)
--   book_returns, book_return_lines   returns; the amount is paid back at once
-- Adds "Books" to the small menu at the top of the website (before Careers).
-- Also adds the permissions books.view / books.orders / books.payments / books.stock /
-- books.customers / books.reports and a "Books Manager" role that has all of them.
-- Only new tables, a new role and new permissions: no existing row is changed or deleted.
-- No book names, prices or customers are added: the office enters them.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'21_books_ordering')
BEGIN
    PRINT N'21_books_ordering is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.book_order_no_seq') IS NULL
    EXEC (N'CREATE SEQUENCE dbo.book_order_no_seq AS BIGINT START WITH 1001 INCREMENT BY 1 NO CACHE;');
IF OBJECT_ID(N'dbo.book_return_no_seq') IS NULL
    EXEC (N'CREATE SEQUENCE dbo.book_return_no_seq AS BIGINT START WITH 101 INCREMENT BY 1 NO CACHE;');

IF OBJECT_ID(N'dbo.book_items') IS NULL
BEGIN
    CREATE TABLE dbo.book_items (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_items_id DEFAULT NEWSEQUENTIALID(),
        class_name NVARCHAR(60) NOT NULL,
        name NVARCHAR(200) NOT NULL,
        stock INT NOT NULL CONSTRAINT DF_book_items_stock DEFAULT 0,
        purchase_price DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_items_purchase DEFAULT 0,
        sale_price DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_items_sale DEFAULT 0,
        is_active BIT NOT NULL CONSTRAINT DF_book_items_active DEFAULT 1,
        sort_order INT NOT NULL CONSTRAINT DF_book_items_sort DEFAULT 0,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_items_created DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_items_updated DEFAULT SYSDATETIMEOFFSET(),
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        archived_at DATETIMEOFFSET(3) NULL,
        CONSTRAINT PK_book_items PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_items_stock CHECK (stock >= 0),
        CONSTRAINT CK_book_items_prices CHECK (purchase_price >= 0 AND sale_price >= 0),
        CONSTRAINT CK_book_items_name CHECK (LEN(name) >= 2 AND LEN(class_name) >= 1),
        CONSTRAINT FK_book_items_created_by FOREIGN KEY (created_by) REFERENCES dbo.users (id),
        CONSTRAINT FK_book_items_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.users (id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_book_items_class_name ON dbo.book_items (class_name, name) WHERE archived_at IS NULL;
END

IF OBJECT_ID(N'dbo.book_customers') IS NULL
BEGIN
    CREATE TABLE dbo.book_customers (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_customers_id DEFAULT NEWSEQUENTIALID(),
        mobile NVARCHAR(11) NOT NULL,
        name NVARCHAR(150) NOT NULL,
        campus_name NVARCHAR(150) NULL,
        address NVARCHAR(400) NULL,
        credit DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_customers_credit DEFAULT 0,
        is_active BIT NOT NULL CONSTRAINT DF_book_customers_active DEFAULT 1,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_customers_created DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_customers_updated DEFAULT SYSDATETIMEOFFSET(),
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        archived_at DATETIMEOFFSET(3) NULL,
        CONSTRAINT PK_book_customers PRIMARY KEY CLUSTERED (id),
        -- One format: 11 digits starting 03 (for example 03001234567); shown as 0300-1234567.
        CONSTRAINT CK_book_customers_mobile CHECK (REGEXP_LIKE(mobile, N'^03[0-9]{9}$')),
        CONSTRAINT CK_book_customers_name CHECK (LEN(name) >= 2),
        CONSTRAINT CK_book_customers_credit CHECK (credit >= 0),
        CONSTRAINT FK_book_customers_created_by FOREIGN KEY (created_by) REFERENCES dbo.users (id),
        CONSTRAINT FK_book_customers_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.users (id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_book_customers_mobile ON dbo.book_customers (mobile);
END

IF OBJECT_ID(N'dbo.book_accounts') IS NULL
BEGIN
    CREATE TABLE dbo.book_accounts (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_accounts_id DEFAULT NEWSEQUENTIALID(),
        name NVARCHAR(120) NOT NULL,
        kind NVARCHAR(10) NOT NULL,
        details NVARCHAR(300) NULL,
        is_active BIT NOT NULL CONSTRAINT DF_book_accounts_active DEFAULT 1,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_accounts_created DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_accounts_updated DEFAULT SYSDATETIMEOFFSET(),
        created_by UNIQUEIDENTIFIER NULL,
        updated_by UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_book_accounts PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_accounts_kind CHECK (kind IN (N'cash', N'bank')),
        CONSTRAINT CK_book_accounts_name CHECK (LEN(name) >= 2),
        CONSTRAINT FK_book_accounts_created_by FOREIGN KEY (created_by) REFERENCES dbo.users (id),
        CONSTRAINT FK_book_accounts_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.users (id)
    );
END

IF OBJECT_ID(N'dbo.book_orders') IS NULL
BEGIN
    CREATE TABLE dbo.book_orders (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_orders_id DEFAULT NEWSEQUENTIALID(),
        order_no BIGINT NOT NULL,
        customer_id UNIQUEIDENTIFIER NOT NULL,
        fulfilment NVARCHAR(10) NOT NULL,
        delivery_address NVARCHAR(400) NULL,
        status NVARCHAR(12) NOT NULL CONSTRAINT DF_book_orders_status DEFAULT N'new',
        source NVARCHAR(10) NOT NULL CONSTRAINT DF_book_orders_source DEFAULT N'web',
        -- Money: subtotal of the lines; balance = subtotal - returned_total - discount_total - credit_applied - paid_total + refunded_total
        subtotal DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_orders_subtotal DEFAULT 0,
        discount_total DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_orders_discount DEFAULT 0,
        credit_applied DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_orders_credit DEFAULT 0,
        paid_total DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_orders_paid DEFAULT 0,
        returned_total DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_orders_returned DEFAULT 0,
        refunded_total DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_orders_refunded DEFAULT 0,
        edit_count INT NOT NULL CONSTRAINT DF_book_orders_edits DEFAULT 0,
        stock_taken_at DATETIMEOFFSET(3) NULL,
        placed_by UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_orders_created DEFAULT SYSDATETIMEOFFSET(),
        updated_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_orders_updated DEFAULT SYSDATETIMEOFFSET(),
        updated_by UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_book_orders PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_orders_fulfilment CHECK (fulfilment IN (N'pickup', N'delivery')),
        CONSTRAINT CK_book_orders_status CHECK (status IN (N'new', N'checked', N'packed', N'delivered', N'collected', N'cancelled')),
        CONSTRAINT CK_book_orders_source CHECK (source IN (N'web', N'office')),
        CONSTRAINT CK_book_orders_money CHECK (subtotal >= 0 AND discount_total >= 0 AND credit_applied >= 0 AND paid_total >= 0
                                               AND returned_total >= 0 AND refunded_total >= 0),
        CONSTRAINT FK_book_orders_customer FOREIGN KEY (customer_id) REFERENCES dbo.book_customers (id),
        CONSTRAINT FK_book_orders_placed_by FOREIGN KEY (placed_by) REFERENCES dbo.users (id),
        CONSTRAINT FK_book_orders_updated_by FOREIGN KEY (updated_by) REFERENCES dbo.users (id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_book_orders_no ON dbo.book_orders (order_no);
    CREATE NONCLUSTERED INDEX book_orders_customer_idx ON dbo.book_orders (customer_id, created_at DESC);
    CREATE NONCLUSTERED INDEX book_orders_status_idx ON dbo.book_orders (status, created_at DESC);
END

IF OBJECT_ID(N'dbo.book_order_lines') IS NULL
BEGIN
    CREATE TABLE dbo.book_order_lines (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_order_lines_id DEFAULT NEWSEQUENTIALID(),
        order_id UNIQUEIDENTIFIER NOT NULL,
        item_id UNIQUEIDENTIFIER NOT NULL,
        class_name NVARCHAR(60) NOT NULL,
        item_name NVARCHAR(200) NOT NULL,
        unit_price DECIMAL(12,2) NOT NULL,
        purchase_price DECIMAL(12,2) NOT NULL,
        quantity INT NOT NULL,
        returned_qty INT NOT NULL CONSTRAINT DF_book_order_lines_returned DEFAULT 0,
        CONSTRAINT PK_book_order_lines PRIMARY KEY CLUSTERED (id),
        -- A book taken out of an order while editing keeps its row with quantity 0 (the edit history shows it).
        CONSTRAINT CK_book_order_lines_qty CHECK (quantity >= 0 AND returned_qty >= 0 AND returned_qty <= quantity),
        CONSTRAINT CK_book_order_lines_price CHECK (unit_price >= 0 AND purchase_price >= 0),
        CONSTRAINT FK_book_order_lines_order FOREIGN KEY (order_id) REFERENCES dbo.book_orders (id),
        CONSTRAINT FK_book_order_lines_item FOREIGN KEY (item_id) REFERENCES dbo.book_items (id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_book_order_lines_item ON dbo.book_order_lines (order_id, item_id);
END

IF OBJECT_ID(N'dbo.book_order_edits') IS NULL
BEGIN
    CREATE TABLE dbo.book_order_edits (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_order_edits_id DEFAULT NEWSEQUENTIALID(),
        order_id UNIQUEIDENTIFIER NOT NULL,
        changes NVARCHAR(MAX) NOT NULL,
        old_subtotal DECIMAL(12,2) NOT NULL,
        new_subtotal DECIMAL(12,2) NOT NULL,
        by_customer BIT NOT NULL,
        by_user UNIQUEIDENTIFIER NULL,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_order_edits_created DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_book_order_edits PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_order_edits_json CHECK (ISJSON(changes) = 1),
        CONSTRAINT FK_book_order_edits_order FOREIGN KEY (order_id) REFERENCES dbo.book_orders (id),
        CONSTRAINT FK_book_order_edits_user FOREIGN KEY (by_user) REFERENCES dbo.users (id)
    );
    CREATE NONCLUSTERED INDEX book_order_edits_order_idx ON dbo.book_order_edits (order_id, created_at);
END

IF OBJECT_ID(N'dbo.book_payments') IS NULL
BEGIN
    CREATE TABLE dbo.book_payments (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_payments_id DEFAULT NEWSEQUENTIALID(),
        order_id UNIQUEIDENTIFIER NOT NULL,
        amount DECIMAL(12,2) NOT NULL,
        discount DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_payments_discount DEFAULT 0,
        method NVARCHAR(10) NULL,
        account_id UNIQUEIDENTIFIER NULL,
        received_by UNIQUEIDENTIFIER NOT NULL,
        received_on DATE NOT NULL,
        receipt_path NVARCHAR(300) NULL,
        receipt_mime NVARCHAR(100) NULL,
        note NVARCHAR(500) NULL,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_payments_created DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_book_payments PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_payments_money CHECK (amount >= 0 AND discount >= 0 AND (amount > 0 OR discount > 0)),
        CONSTRAINT CK_book_payments_method CHECK (method IS NULL OR method IN (N'cash', N'bank')),
        CONSTRAINT CK_book_payments_method_needed CHECK (amount = 0 OR (method IS NOT NULL AND account_id IS NOT NULL)),
        CONSTRAINT CK_book_payments_path CHECK (receipt_path IS NULL OR NOT REGEXP_LIKE(receipt_path, N'\.\.')),
        CONSTRAINT FK_book_payments_order FOREIGN KEY (order_id) REFERENCES dbo.book_orders (id),
        CONSTRAINT FK_book_payments_account FOREIGN KEY (account_id) REFERENCES dbo.book_accounts (id),
        CONSTRAINT FK_book_payments_user FOREIGN KEY (received_by) REFERENCES dbo.users (id)
    );
    CREATE NONCLUSTERED INDEX book_payments_order_idx ON dbo.book_payments (order_id, created_at);
    CREATE NONCLUSTERED INDEX book_payments_date_idx ON dbo.book_payments (received_on);
END

IF OBJECT_ID(N'dbo.book_returns') IS NULL
BEGIN
    CREATE TABLE dbo.book_returns (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_returns_id DEFAULT NEWSEQUENTIALID(),
        return_no BIGINT NOT NULL,
        order_id UNIQUEIDENTIFIER NOT NULL,
        amount DECIMAL(12,2) NOT NULL,
        refund_amount DECIMAL(12,2) NOT NULL CONSTRAINT DF_book_returns_refund DEFAULT 0,
        refund_method NVARCHAR(10) NULL,
        refund_account_id UNIQUEIDENTIFIER NULL,
        refunded_on DATE NOT NULL,
        note NVARCHAR(500) NULL,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_returns_created DEFAULT SYSDATETIMEOFFSET(),
        created_by UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_book_returns PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_returns_money CHECK (amount >= 0 AND refund_amount >= 0),
        CONSTRAINT CK_book_returns_method CHECK (refund_method IS NULL OR refund_method IN (N'cash', N'bank')),
        CONSTRAINT FK_book_returns_order FOREIGN KEY (order_id) REFERENCES dbo.book_orders (id),
        CONSTRAINT FK_book_returns_account FOREIGN KEY (refund_account_id) REFERENCES dbo.book_accounts (id),
        CONSTRAINT FK_book_returns_user FOREIGN KEY (created_by) REFERENCES dbo.users (id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_book_returns_no ON dbo.book_returns (return_no);
END

IF OBJECT_ID(N'dbo.book_return_lines') IS NULL
BEGIN
    CREATE TABLE dbo.book_return_lines (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_return_lines_id DEFAULT NEWSEQUENTIALID(),
        return_id UNIQUEIDENTIFIER NOT NULL,
        order_line_id UNIQUEIDENTIFIER NOT NULL,
        quantity INT NOT NULL,
        unit_price DECIMAL(12,2) NOT NULL,
        CONSTRAINT PK_book_return_lines PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_return_lines CHECK (quantity > 0 AND unit_price >= 0),
        CONSTRAINT FK_book_return_lines_return FOREIGN KEY (return_id) REFERENCES dbo.book_returns (id),
        CONSTRAINT FK_book_return_lines_line FOREIGN KEY (order_line_id) REFERENCES dbo.book_order_lines (id)
    );
END

IF OBJECT_ID(N'dbo.book_stock_moves') IS NULL
BEGIN
    CREATE TABLE dbo.book_stock_moves (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_book_stock_moves_id DEFAULT NEWSEQUENTIALID(),
        item_id UNIQUEIDENTIFIER NOT NULL,
        kind NVARCHAR(12) NOT NULL,
        change INT NOT NULL,
        stock_after INT NOT NULL,
        moved_on DATE NOT NULL,
        order_id UNIQUEIDENTIFIER NULL,
        return_id UNIQUEIDENTIFIER NULL,
        note NVARCHAR(500) NULL,
        created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_book_stock_moves_created DEFAULT SYSDATETIMEOFFSET(),
        created_by UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_book_stock_moves PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_book_stock_moves_kind CHECK (kind IN (N'purchase', N'sale', N'return', N'correction')),
        CONSTRAINT CK_book_stock_moves_change CHECK (change <> 0 AND stock_after >= 0),
        CONSTRAINT FK_book_stock_moves_item FOREIGN KEY (item_id) REFERENCES dbo.book_items (id),
        CONSTRAINT FK_book_stock_moves_order FOREIGN KEY (order_id) REFERENCES dbo.book_orders (id),
        CONSTRAINT FK_book_stock_moves_return FOREIGN KEY (return_id) REFERENCES dbo.book_returns (id),
        CONSTRAINT FK_book_stock_moves_user FOREIGN KEY (created_by) REFERENCES dbo.users (id)
    );
    CREATE NONCLUSTERED INDEX book_stock_moves_item_idx ON dbo.book_stock_moves (item_id, created_at);
END

-- Permissions and the Books Manager role.
INSERT INTO dbo.permissions ([key], description)
SELECT p.k, p.d FROM (VALUES
    (N'books.view', N'View books, customers and orders'),
    (N'books.orders', N'Manage book orders (status, office orders)'),
    (N'books.payments', N'Record book payments, discounts and refunds'),
    (N'books.stock', N'Manage books, prices and stock'),
    (N'books.customers', N'Manage book customers'),
    (N'books.reports', N'View and download book reports')) p(k, d)
WHERE NOT EXISTS (SELECT 1 FROM dbo.permissions x WHERE x.[key] = p.k);

IF NOT EXISTS (SELECT 1 FROM dbo.roles WHERE [key] = N'books_manager')
    INSERT INTO dbo.roles ([key], name, description, is_system)
    VALUES (N'books_manager', N'Books Manager', N'Handles book stock, customers, orders, payments and returns.', 0);

INSERT INTO dbo.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM dbo.roles r CROSS JOIN dbo.permissions p
WHERE r.[key] = N'books_manager' AND p.[key] LIKE N'books.%'
  AND NOT EXISTS (SELECT 1 FROM dbo.role_permissions x WHERE x.role_id = r.id AND x.permission_id = p.id);

-- "Books" in the small menu at the top of the website, before Careers (owner).
INSERT INTO dbo.navigation_items (menu_id, label, url, sort_order, is_visible)
SELECT m.id, N'Books', N'/books', 0, 1
FROM dbo.navigation_menus m
WHERE m.[key] = N'utility'
  AND NOT EXISTS (SELECT 1 FROM dbo.navigation_items n WHERE n.menu_id = m.id AND n.url = N'/books');

INSERT INTO dbo.schema_migrations (id) VALUES (N'21_books_ordering');
COMMIT TRANSACTION;
PRINT N'21_books_ordering applied.';
GO
SET NOEXEC OFF;
GO
