using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// The office side of book orders (part 2): the order list, status changes, accounts and payments.
    /// Rules (owner's specification and decisions of 19 November 2026):
    /// - a payment may be part of the balance; amount + discount may not be more than the balance;
    /// - the method (cash/bank) must match the kind of account the money went into;
    /// - stock is taken off at the FIRST payment of an order, for all its books, never below zero;
    ///   if a book does not have enough stock then, the payment is refused with a clear message;
    /// - "received by" is always the signed-in admin user;
    /// - a cancelled order takes no payments, and an order with payments cannot be cancelled (use a return);
    /// - every change is written to the audit log in the same transaction.
    /// </summary>
    public static class BookOrderRepository
    {
        public static readonly string[] Statuses = { "new", "checked", "packed", "delivered", "collected", "cancelled" };

        private const string BalanceSql = "(o.subtotal - o.returned_total - o.discount_total - o.credit_applied - o.paid_total + o.refunded_total)";

        // ---- Accounts ---------------------------------------------------------------------

        public static IList<BookAccount> Accounts(bool activeOnly)
        {
            using (var db = Db.Open())
                return db.Query<BookAccount>("SELECT id, name, kind, details, is_active, account_title, account_number, iban, show_to_customers FROM dbo.book_accounts" +
                                             (activeOnly ? " WHERE is_active = 1" : "") + " ORDER BY kind, name").ToList();
        }

        public static BookAccount Account(Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<BookAccount>("SELECT id, name, kind, details, is_active, account_title, account_number, iban, show_to_customers FROM dbo.book_accounts WHERE id = @id", new { id });
        }

        public static Guid SaveAccount(BookAccount a, bool isNew, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                if (isNew)
                    a.Id = db.ExecuteScalar<Guid>(@"INSERT INTO dbo.book_accounts (name, kind, details, is_active, account_title, account_number, iban, show_to_customers, created_by, updated_by)
                                                    OUTPUT inserted.id VALUES (@Name, @Kind, @Details, @IsActive, @AccountTitle, @AccountNumber, @Iban, @ShowToCustomers, @actorId, @actorId)",
                        new { a.Name, a.Kind, a.Details, a.IsActive, a.AccountTitle, a.AccountNumber, a.Iban, a.ShowToCustomers, actorId }, tx);
                else
                    db.Execute(@"UPDATE dbo.book_accounts SET name = @Name, kind = @Kind, details = @Details, is_active = @IsActive,
                                        account_title = @AccountTitle, account_number = @AccountNumber, iban = @Iban, show_to_customers = @ShowToCustomers,
                                        updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @Id",
                        new { a.Id, a.Name, a.Kind, a.Details, a.IsActive, a.AccountTitle, a.AccountNumber, a.Iban, a.ShowToCustomers, actorId }, tx);
                AuditLog.Write(db, tx, actorId, isNew ? "insert" : "update", "book_accounts", a.Id.ToString(), new { a.Name, a.Kind, a.Details, a.IsActive, a.AccountTitle, a.AccountNumber, a.Iban, a.ShowToCustomers });
                tx.Commit();
                return a.Id;
            }
        }

        // ---- Order list -------------------------------------------------------------------

        public static IList<BookOrder> List(BookOrderFilter f)
        {
            var where = new List<string> { "o.archived_at IS NULL" };   // removed cancelled orders (Database/28) are never listed
            if (!string.IsNullOrEmpty(f.Status)) where.Add("o.status = @Status");
            if (f.View == "done") where.Add("o.status IN (N'delivered', N'collected')");
            if (f.Payment == "pending") where.Add("o.paid_total = 0 AND o.discount_total = 0 AND o.status <> N'cancelled'");
            else if (f.Payment == "part") where.Add("(o.paid_total > 0 OR o.discount_total > 0) AND " + BalanceSql + " > 0");
            else if (f.Payment == "paid") where.Add("(o.paid_total > 0 OR o.discount_total > 0) AND " + BalanceSql + " <= 0");
            else if (f.Payment == "tocheck") where.Add("EXISTS (SELECT 1 FROM dbo.book_payment_submissions s WHERE (s.order_id = o.id OR (s.order_id IS NULL AND s.customer_id = o.customer_id)) AND s.status = N'pending')");
            else if (f.Payment == "unpaid") where.Add(BalanceSql + " > 0 AND o.status <> N'cancelled'");
            if (f.From.HasValue) where.Add("o.created_at >= @fromUtc");
            if (f.To.HasValue) where.Add("o.created_at < @toUtc");
            long no;
            var q = (f.Query ?? "").Trim();
            var qNo = q.StartsWith("BK-", StringComparison.OrdinalIgnoreCase) ? q.Substring(3) : q;
            if (q.Length > 0)
                where.Add(long.TryParse(qNo, out no) ? "(o.order_no = @no OR c.mobile LIKE @like ESCAPE '\\')"
                                                     : "(c.name LIKE @like ESCAPE '\\' OR c.mobile LIKE @like ESCAPE '\\' OR c.campus_name LIKE @like ESCAPE '\\')");
            long.TryParse(qNo, out no);
            var pk = TimeSpan.FromHours(5);
            using (var db = Db.Open())
                return db.Query<BookOrder>(@"
                    SELECT TOP (500) o.id, o.order_no, o.customer_id, c.name AS customer_name, c.mobile AS customer_mobile, c.campus_name,
                           o.fulfilment, o.delivery_address, o.status, o.source, o.subtotal, o.discount_total, o.credit_applied, o.paid_total,
                           o.returned_total, o.refunded_total, o.edit_count, o.stock_taken_at, o.created_at,
                           (SELECT COUNT(*) FROM dbo.book_payment_submissions s WHERE (s.order_id = o.id OR (s.order_id IS NULL AND s.customer_id = o.customer_id)) AND s.status = N'pending') AS pending_payments,
                           (SELECT COALESCE(SUM(l.quantity), 0) FROM dbo.book_order_lines l WHERE l.order_id = o.id) AS book_count
                    FROM dbo.book_orders o JOIN dbo.book_customers c ON c.id = o.customer_id" +
                    (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") + " ORDER BY o.created_at DESC",
                    new
                    {
                        f.Status, no, like = "%" + CampusRepository.EscapeLike(Helpers.Mobile.Normalize(q) ?? q) + "%",
                        fromUtc = f.From.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(f.From.Value.Date, DateTimeKind.Unspecified), pk) : (DateTimeOffset?)null,
                        toUtc = f.To.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(f.To.Value.Date.AddDays(1), DateTimeKind.Unspecified), pk) : (DateTimeOffset?)null
                    }).ToList();
        }

        /// <summary>
        /// Removes a cancelled order (owner's decision, 21 November 2026; Super Admin only, checked by the controller).
        /// Nothing is deleted: the order is archived and then hidden from every list, the customer's pages and the reports.
        /// Only an order that was cancelled before any money, stock or return touched it can be removed; the database
        /// checks the same (CK_book_orders_archived).
        /// </summary>
        public static void RemoveCancelled(long orderNo, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var o = db.QuerySingleOrDefault<BookOrder>(@"SELECT id, order_no, customer_id, status, paid_total, discount_total, returned_total, refunded_total, credit_applied, stock_taken_at
                                                             FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE order_no = @orderNo AND archived_at IS NULL", new { orderNo }, tx);
                if (o == null) throw new BookOrderException("The order was not found.");
                if (o.Status != "cancelled") throw new BookOrderException("Only a cancelled order can be removed.");
                var touched = db.ExecuteScalar<int>(@"SELECT (SELECT COUNT(*) FROM dbo.book_payments WHERE order_id = @Id)
                                                           + (SELECT COUNT(*) FROM dbo.book_payment_submissions WHERE order_id = @Id AND status <> N'rejected')
                                                           + (SELECT COUNT(*) FROM dbo.book_return_lines rl JOIN dbo.book_order_lines l ON l.id = rl.order_line_id WHERE l.order_id = @Id)", new { o.Id }, tx);
                if (o.PaidTotal != 0 || o.DiscountTotal != 0 || o.ReturnedTotal != 0 || o.RefundedTotal != 0 || o.StockTakenAt != null || touched > 0)
                    throw new BookOrderException("This order has a payment, a discount, a return or books taken off stock, so it must stay on record.");
                db.Execute("UPDATE dbo.book_orders SET archived_at = SYSDATETIMEOFFSET(), archived_by = @actorId, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @Id",
                    new { actorId, o.Id }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_orders", o.Id.ToString(), new { order_no = o.OrderNo, status = o.Status, archived = true, removed_by_super_admin = true });
                tx.Commit();
            }
        }

        /// <summary>How many orders each quick filter on the Orders page shows (removed orders never count).</summary>
        public static IDictionary<string, int> ViewCounts()
        {
            using (var db = Db.Open())
            {
                var r = db.QuerySingle(@"
                    SELECT (SELECT COUNT(*) FROM dbo.book_orders WHERE archived_at IS NULL) AS total,
                           (SELECT COUNT(*) FROM dbo.book_orders o WHERE o.archived_at IS NULL AND EXISTS (SELECT 1 FROM dbo.book_payment_submissions s WHERE (s.order_id = o.id OR (s.order_id IS NULL AND s.customer_id = o.customer_id)) AND s.status = N'pending')) AS tocheck,
                           (SELECT COUNT(*) FROM dbo.book_orders WHERE archived_at IS NULL AND status = N'new') AS new_orders,
                           (SELECT COUNT(*) FROM dbo.book_orders WHERE archived_at IS NULL AND status = N'checked') AS topack,
                           (SELECT COUNT(*) FROM dbo.book_orders WHERE archived_at IS NULL AND status = N'packed') AS ready,
                           (SELECT COUNT(*) FROM dbo.book_orders o WHERE o.archived_at IS NULL AND o.status <> N'cancelled' AND " + BalanceSql + @" > 0) AS unpaid,
                           (SELECT COUNT(*) FROM dbo.book_orders WHERE archived_at IS NULL AND status IN (N'delivered', N'collected')) AS done");
                return new Dictionary<string, int>
                {
                    { "", (int)r.total }, { "tocheck", (int)r.tocheck }, { "new", (int)r.new_orders }, { "topack", (int)r.topack },
                    { "ready", (int)r.ready }, { "unpaid", (int)r.unpaid }, { "done", (int)r.done }
                };
            }
        }

        /// <summary>Orders to pack (confirmed) or ready to hand over (packed), oldest first, for the Today page.</summary>
        public static IList<BookOrder> ByStatus(string status, int max)
        {
            return List(new BookOrderFilter { Status = status }).OrderBy(o => o.CreatedAt).Take(max).ToList();
        }

        /// <summary>Numbers for the dashboard: orders waiting to be checked, and orders not fully paid.</summary>
        public static BookCounts Counts()
        {
            using (var db = Db.Open())
                return db.QuerySingle<BookCounts>(@"
                    SELECT (SELECT COUNT(*) FROM dbo.book_orders WHERE status = N'new') AS new_orders,
                           (SELECT COUNT(*) FROM dbo.book_orders o WHERE o.status <> N'cancelled' AND " + BalanceSql + @" > 0) AS unpaid_orders,
                           (SELECT COUNT(*) FROM dbo.book_orders WHERE archived_at IS NULL AND created_at >= DATEADD(day, -1, SYSDATETIMEOFFSET())) AS last_day,
                           (SELECT COUNT(*) FROM dbo.book_payment_submissions WHERE status = N'pending') AS payments_to_check");
        }

        public static IList<BookPayment> Payments(Guid orderId)
        {
            using (var db = Db.Open())
                return db.Query<BookPayment>(@"
                    SELECT p.id, p.amount, p.discount, p.method, a.name AS account_name, u.full_name AS received_by_name, p.received_on,
                           p.receipt_path, p.receipt_mime, p.note, p.created_at
                    FROM dbo.book_payments p
                    LEFT JOIN dbo.book_accounts a ON a.id = p.account_id
                    LEFT JOIN dbo.users u ON u.id = p.received_by
                    WHERE p.order_id = @orderId ORDER BY p.created_at", new { orderId }).ToList();
        }

        public static BookPayment Payment(Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<BookPayment>("SELECT id, receipt_path, receipt_mime FROM dbo.book_payments WHERE id = @id", new { id });
        }

        // ---- Status -----------------------------------------------------------------------

        private static int Step(string status)
        {
            switch (status) { case "new": return 0; case "checked": return 1; case "packed": return 2; case "delivered": case "collected": return 3; default: return 4; }
        }

        /// <summary>Changes the order status (forward only). Returns an error message, or null when done.</summary>
        public static string SetStatus(long orderNo, string status, Guid actorId)
        {
            if (!Statuses.Contains(status)) return "Unknown status.";
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var o = db.QuerySingleOrDefault<BookOrder>(@"SELECT id, status, fulfilment, paid_total, discount_total, stock_taken_at FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK)
                                                             WHERE order_no = @orderNo", new { orderNo }, tx);
                if (o == null) return "The order was not found.";
                if (o.Status == status) { tx.Rollback(); return null; }
                if (o.Status == "cancelled") return "This order is cancelled and cannot be changed.";
                // Only forward: New → Checked → Packed → Delivered / Collected (or Cancelled before that).
                if (Step(status) <= Step(o.Status) && status != "cancelled") return "An order cannot go back to an earlier status.";
                if (status == "cancelled" && Step(o.Status) >= 3) return "A delivered or collected order cannot be cancelled. Record a return instead.";
                if (status == "cancelled" && (o.PaidTotal > 0 || o.DiscountTotal > 0))
                    return "A payment has been recorded for this order, so it cannot be cancelled. Record a return instead.";
                if (status == "cancelled" && db.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.book_payment_submissions WHERE order_id = @Id AND status = N'pending'", new { o.Id }, tx) > 0)
                    return "The customer has sent a payment for this order. Please confirm or reject it first.";
                if (status == "delivered" && o.Fulfilment != "delivery") return "This order is for pick up; mark it as Collected.";
                if (status == "collected" && o.Fulfilment != "pickup") return "This order is for delivery; mark it as Delivered.";
                // When the books leave the office before any payment, they come off stock now (not again at payment).
                var tookStock = false;
                if ((status == "delivered" || status == "collected") && o.StockTakenAt == null)
                {
                    try { TakeStock(db, tx, o.Id, orderNo, DateTime.UtcNow.AddHours(5).Date, actorId); tookStock = true; }
                    catch (BookOrderException ex) { return ex.Message; }
                }
                db.Execute("UPDATE dbo.book_orders SET status = @status, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @Id",
                    new { status, actorId, o.Id }, tx);
                // A cancelled order gives back any credit it used.
                if (status == "cancelled")
                    db.Execute(@"UPDATE c SET credit = c.credit + o.credit_applied, updated_at = SYSDATETIMEOFFSET()
                                 FROM dbo.book_customers c JOIN dbo.book_orders o ON o.customer_id = c.id WHERE o.id = @Id;
                                 UPDATE dbo.book_orders SET credit_applied = 0 WHERE id = @Id;", new { o.Id }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_orders", o.Id.ToString(), new { order_no = orderNo, status_from = o.Status, status_to = status, stock_taken = tookStock });
                BookMessageRepository.ForOrder(db, tx, o.Id, status == "checked" ? "order_confirmed" : "order_" + status);
                tx.Commit();
                return null;
            }
        }

        // ---- Payments ---------------------------------------------------------------------

        /// <summary>
        /// Records a payment and/or a discount. At the first payment the order's books are taken off stock.
        /// Throws <see cref="BookOrderException"/> with a message for the office when it cannot be recorded.
        /// </summary>
        public static Guid RecordPayment(long orderNo, BookPaymentForm p, string receiptPath, string receiptMime, Guid actorId)
        {
            if (p.Amount < 0 || p.Discount < 0) throw new BookOrderException("Amounts cannot be negative.");
            if (p.Amount == 0 && p.Discount == 0) throw new BookOrderException("Please enter the amount received, a discount, or both.");
            if (decimal.Round(p.Amount, 2) != p.Amount || decimal.Round(p.Discount, 2) != p.Discount) throw new BookOrderException("Please enter amounts with at most two decimals.");
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                // Order row first (the lock order everywhere), then: no manual payment while a customer's payment waits.
                db.Execute("SELECT id FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE order_no = @orderNo", new { orderNo }, tx);
                var waiting = db.ExecuteScalar<int>(@"SELECT COUNT(*) FROM dbo.book_payment_submissions s JOIN dbo.book_orders o
                                                        ON o.id = s.order_id OR (s.order_id IS NULL AND s.customer_id = o.customer_id)
                                                      WHERE o.order_no = @orderNo AND s.status = N'pending'", new { orderNo }, tx);
                if (waiting > 0) throw new BookOrderException("A payment sent by the customer is waiting under \"Payments sent by the customer\". Please confirm or reject it first, so the same money is not counted twice.");
                var id = RecordPaymentTx(db, tx, orderNo, p, receiptPath, receiptMime, actorId);
                if (p.Amount > 0)
                    BookMessageRepository.ForOrder(db, tx, db.ExecuteScalar<Guid>("SELECT id FROM dbo.book_orders WHERE order_no = @orderNo", new { orderNo }, tx), "payment_received", p.Amount);
                tx.Commit();
                return id;
            }
        }

        /// <summary>Records a payment inside the caller's transaction (also used when the office confirms a payment the customer sent).</summary>
        internal static Guid RecordPaymentTx(System.Data.IDbConnection db, System.Data.IDbTransaction tx, long orderNo, BookPaymentForm p, string receiptPath, string receiptMime, Guid actorId, bool accountMayBeInactive = false)
        {
            {
                var o = db.QuerySingleOrDefault<BookOrder>(@"
                    SELECT id, order_no, status, subtotal, discount_total, credit_applied, paid_total, returned_total, refunded_total, stock_taken_at
                    FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE order_no = @orderNo", new { orderNo }, tx);
                if (o == null) throw new BookOrderException("The order was not found.");
                if (o.Status == "cancelled") throw new BookOrderException("This order is cancelled; no payment can be recorded.");
                if (p.Amount + p.Discount > o.Balance)
                    throw new BookOrderException("The amount and discount together (" + BookFormat.Money(p.Amount + p.Discount) + ") are more than the balance of " + BookFormat.Money(o.Balance) + ".");
                if (p.Amount > 0)
                {
                    var account = db.QuerySingleOrDefault<BookAccount>("SELECT id, name, kind, is_active FROM dbo.book_accounts WHERE id = @AccountId", new { p.AccountId }, tx);
                    // A customer's payment stays confirmable even if the account was switched off after they paid into it.
                    if (account == null || (!account.IsActive && !accountMayBeInactive)) throw new BookOrderException("Please choose the account the money went into.");
                    if (account.Kind != p.Method) throw new BookOrderException("The account \"" + account.Name + "\" is a " + account.Kind + " account; please choose a " + p.Method + " account or change the method.");
                }

                var receivedOn = p.ReceivedOn.Date;
                var first = o.StockTakenAt == null;
                if (first) TakeStock(db, tx, o.Id, orderNo, receivedOn, actorId);

                var id = db.ExecuteScalar<Guid>(@"
                    INSERT INTO dbo.book_payments (order_id, amount, discount, method, account_id, received_by, received_on, receipt_path, receipt_mime, note)
                    OUTPUT inserted.id
                    VALUES (@orderId, @Amount, @Discount, @method, @accountId, @actorId, @receivedOn, @receiptPath, @receiptMime, @Note)",
                    new
                    {
                        orderId = o.Id, p.Amount, p.Discount, method = p.Amount > 0 ? p.Method : null, accountId = p.Amount > 0 ? p.AccountId : (Guid?)null,
                        actorId, receivedOn, receiptPath, receiptMime, p.Note
                    }, tx);
                db.Execute(@"UPDATE dbo.book_orders SET paid_total = paid_total + @Amount, discount_total = discount_total + @Discount,
                                    updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @Id",
                    new { p.Amount, p.Discount, actorId, o.Id }, tx);
                AuditLog.Write(db, tx, actorId, "insert", "book_payments", id.ToString(),
                    new { order_no = orderNo, p.Amount, p.Discount, p.Method, account_id = p.AccountId, received_on = receivedOn, stock_taken = first, receipt = receiptPath != null });
                return id;
            }
        }
    
        /// <summary>
        /// Takes every book of the order off stock (sorted by book id, the same lock order everywhere) and marks the
        /// order. Throws <see cref="BookOrderException"/> naming the books that do not have enough stock.
        /// </summary>
        private static void TakeStock(System.Data.IDbConnection db, System.Data.IDbTransaction tx, Guid orderId, long orderNo, DateTime on, Guid actorId)
        {
            var lines = db.Query<BookOrderLine>("SELECT id, item_id, class_name, item_name, quantity FROM dbo.book_order_lines WHERE order_id = @orderId AND quantity > 0",
                new { orderId }, tx).OrderBy(l => l.ItemId).ToList();
            var stock = db.Query<BookItem>("SELECT id, stock FROM dbo.book_items WITH (UPDLOCK, HOLDLOCK) WHERE id IN @ids ORDER BY id",
                new { ids = lines.Select(l => l.ItemId).ToList() }, tx).ToDictionary(i => i.Id, i => i.Stock);
            var shortList = lines.Where(l => !stock.ContainsKey(l.ItemId) || stock[l.ItemId] < l.Quantity)
                                 .Select(l => l.ClassName + " – " + l.ItemName + " (needs " + l.Quantity + ", in stock " + (stock.ContainsKey(l.ItemId) ? stock[l.ItemId] : 0) + ")").ToList();
            if (shortList.Count > 0)
                throw new BookOrderException("There is not enough stock to complete this order: " + string.Join("; ", shortList) + ". Add stock or change the order first.");
            foreach (var l in lines)
            {
                var after = stock[l.ItemId] - l.Quantity;
                BookRepository.Move(db, tx, l.ItemId, "sale", -l.Quantity, after, on, orderId, null, "Order " + BookFormat.OrderLabel(orderNo), actorId);
                stock[l.ItemId] = after;
            }
            db.Execute("UPDATE dbo.book_orders SET stock_taken_at = SYSDATETIMEOFFSET() WHERE id = @orderId", new { orderId }, tx);
        }
    }
}
