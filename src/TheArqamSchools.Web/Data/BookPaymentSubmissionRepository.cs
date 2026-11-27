using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Payments that customers send online (owner's request, 20 November 2026): the customer chooses one of the
    /// school's bank accounts, types the amount and attaches the receipt. Nothing is counted as paid until the
    /// office checks the receipt and confirms; confirming records a normal book payment (the first one takes the
    /// books off stock) and the order becomes Confirmed. A rejected payment keeps its reason for the customer.
    /// Lock order as everywhere: order first, then the submission.
    /// </summary>
    public static class BookPaymentSubmissionRepository
    {
        /// <summary>Bank accounts the customer may choose: active bank accounts marked "show to customers".</summary>
        public static IList<BookAccount> CustomerAccounts()
        {
            return BookOrderRepository.Accounts(true).Where(a => a.Kind == "bank" && a.ShowToCustomers).ToList();
        }

        /// <summary>
        /// The customer sends a receipt for one order. Owner's decision (27 November 2026): the customer does not type an
        /// amount; the payment is taken as what is still to pay on the order, and the office enters what the receipt
        /// really shows when it confirms. More than one payment may wait for the office at the same time.
        /// </summary>
        public static Guid Submit(long orderNo, Guid customerId, Guid accountId, string receiptPath, string receiptMime)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var o = db.QuerySingleOrDefault<BookOrder>(@"
                    SELECT id, order_no, customer_id, status, subtotal, discount_total, credit_applied, paid_total, returned_total, refunded_total,
                           (SELECT COUNT(*) FROM dbo.book_payment_submissions s WHERE (s.order_id = o.id OR (s.order_id IS NULL AND s.customer_id = o.customer_id)) AND s.status = N'pending') AS pending_payments
                    FROM dbo.book_orders o WITH (UPDLOCK, HOLDLOCK) WHERE order_no = @orderNo", new { orderNo }, tx);
                if (o == null || o.CustomerId != customerId) throw new BookOrderException("The order was not found.");
                if (o.Status == "cancelled") throw new BookOrderException("This order is cancelled.");
                if (o.Balance <= 0) throw new BookOrderException("Nothing is left to pay on this order.");
                // What is still to pay less payments for this order already waiting for the office (no double counting).
                var amount = o.Balance - db.ExecuteScalar<decimal>("SELECT COALESCE(SUM(amount), 0) FROM dbo.book_payment_submissions WHERE order_id = @Id AND status = N'pending'", new { o.Id }, tx);
                if (amount <= 0) throw new BookOrderException("Your payment for this order is being checked by the office. Nothing more to pay for now.");
                var account = db.QuerySingleOrDefault<BookAccount>("SELECT id, kind, is_active, show_to_customers FROM dbo.book_accounts WHERE id = @accountId", new { accountId }, tx);
                if (account == null || !account.IsActive || account.Kind != "bank" || !account.ShowToCustomers)
                    throw new BookOrderException("Please choose the bank account you paid into.");
                var id = db.ExecuteScalar<Guid>(@"INSERT INTO dbo.book_payment_submissions (order_id, customer_id, account_id, amount, receipt_path, receipt_mime)
                                                  OUTPUT inserted.id VALUES (@Id, @CustomerId, @accountId, @amount, @receiptPath, @receiptMime)",
                    new { o.Id, o.CustomerId, accountId, amount, receiptPath, receiptMime }, tx);
                AuditLog.Write(db, tx, null, "insert", "book_payment_submissions", id.ToString(), new { order_no = orderNo, account_id = accountId, amount, by_customer = true });
                tx.Commit();
                return id;
            }
        }

        /// <summary>
        /// "Make a payment" on My payments (owner's decision, 21 November 2026): one payment towards everything the customer
        /// owes. It must not be more than they owe; only one such payment can wait for the office at a time.
        /// </summary>
        public static Guid SubmitForBalance(Guid customerId, Guid accountId, string receiptPath, string receiptMime)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                // The customer's orders first (by id), then the customer: the same lock order as everywhere.
                db.Execute("SELECT id FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE customer_id = @customerId AND status <> N'cancelled' ORDER BY id", new { customerId }, tx);
                db.Execute("SELECT id FROM dbo.book_customers WITH (UPDLOCK, HOLDLOCK) WHERE id = @customerId", new { customerId }, tx);
                // No amount is typed (27 November 2026): it is what is still to pay less payments already waiting for the office.
                var amount = Owed(db, tx, customerId) - Waiting(db, tx, customerId);
                if (amount <= 0) throw new BookOrderException("Nothing is left to pay at the moment. Payments you sent are being checked by the office.");
                var account = db.QuerySingleOrDefault<BookAccount>("SELECT id, kind, is_active, show_to_customers FROM dbo.book_accounts WHERE id = @accountId", new { accountId }, tx);
                if (account == null || !account.IsActive || account.Kind != "bank" || !account.ShowToCustomers)
                    throw new BookOrderException("Please choose the bank account you paid into.");
                var id = db.ExecuteScalar<Guid>(@"INSERT INTO dbo.book_payment_submissions (order_id, customer_id, account_id, amount, receipt_path, receipt_mime)
                                                  OUTPUT inserted.id VALUES (NULL, @customerId, @accountId, @amount, @receiptPath, @receiptMime)",
                    new { customerId, accountId, amount, receiptPath, receiptMime }, tx);
                AuditLog.Write(db, tx, null, "insert", "book_payment_submissions", id.ToString(), new { customer_id = customerId, account_id = accountId, amount, towards = "balance", by_customer = true });
                tx.Commit();
                return id;
            }
        }

        /// <summary>
        /// What the customer still has to pay: unpaid order balances less their credit (the same figure as the statement).
        /// The credit is used on the oldest orders when the office confirms the payment.
        /// </summary>
        public static decimal Owed(System.Data.IDbConnection db, System.Data.IDbTransaction tx, Guid customerId)
        {
            return db.ExecuteScalar<decimal>(@"SELECT CASE WHEN owed - credit > 0 THEN owed - credit ELSE 0 END FROM (
                    SELECT COALESCE((SELECT SUM(CASE WHEN b > 0 THEN b ELSE 0 END) FROM (
                        SELECT subtotal - returned_total - discount_total - credit_applied - paid_total + refunded_total AS b
                        FROM dbo.book_orders WHERE customer_id = @customerId AND status <> N'cancelled') x), 0) AS owed,
                           (SELECT credit FROM dbo.book_customers WHERE id = @customerId) AS credit) y", new { customerId }, tx);
        }

        /// <summary>Payments the customer sent that the office has not checked yet.</summary>
        public static decimal Waiting(System.Data.IDbConnection db, System.Data.IDbTransaction tx, Guid customerId)
        {
            return db.ExecuteScalar<decimal>("SELECT COALESCE(SUM(amount), 0) FROM dbo.book_payment_submissions WHERE customer_id = @customerId AND status = N'pending'", new { customerId }, tx);
        }

        public static decimal Waiting(Guid customerId)
        {
            using (var db = Db.Open()) return Waiting(db, null, customerId);
        }

        /// <summary>All payments waiting for the office, oldest first (Books overview).</summary>
        public static IList<BookPaymentSubmission> Pending(int max)
        {
            using (var db = Db.Open())
                return db.Query<BookPaymentSubmission>("SELECT TOP (@max) " + Columns + From + " WHERE s.status = N'pending' AND (o.id IS NULL OR o.archived_at IS NULL) ORDER BY s.created_at", new { max }).ToList();
        }

        public static decimal Owed(Guid customerId)
        {
            using (var db = Db.Open()) return Owed(db, null, customerId);
        }

        public static bool BalancePaymentWaiting(Guid customerId)
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.book_payment_submissions WHERE customer_id = @customerId AND order_id IS NULL AND status = N'pending'", new { customerId }) > 0;
        }

        /// <summary>
        /// Confirms a balance payment: the money goes to the customer's OLDEST unpaid orders first (each part is an ordinary
        /// payment; the first payment of an order takes its books off stock and a New order becomes Confirmed). Anything left
        /// becomes the customer's credit and is kept on the submission (credit_amount) so the statement shows it.
        /// </summary>
        private static void ConfirmBalancePayment(BookPaymentSubmission sub, Guid actorId, decimal sent, bool allowCredit)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var orders = db.Query<BookOrder>(@"SELECT id, order_no, status, subtotal, discount_total, credit_applied, paid_total, returned_total, refunded_total, created_at
                                                   FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE customer_id = @CustomerId AND status <> N'cancelled' ORDER BY id",
                    new { sub.CustomerId }, tx).ToList();
                db.Execute("SELECT id FROM dbo.book_customers WITH (UPDLOCK, HOLDLOCK) WHERE id = @CustomerId", new { sub.CustomerId }, tx);
                var status = db.ExecuteScalar<string>("SELECT status FROM dbo.book_payment_submissions WITH (UPDLOCK, HOLDLOCK) WHERE id = @Id", new { sub.Id }, tx);
                if (status != "pending") throw new BookOrderException("This payment has already been checked.");
                // First the customer's credit goes to the oldest unpaid orders (as it would on a new order), so the money
                // paid matches "Still to pay" on the statement.
                var credit = db.ExecuteScalar<decimal>("SELECT credit FROM dbo.book_customers WHERE id = @CustomerId", new { sub.CustomerId }, tx);
                foreach (var o in orders.Where(o => o.Balance > 0).OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderNo))
                {
                    if (credit <= 0) break;
                    var use = Math.Min(credit, o.Balance);
                    db.Execute("UPDATE dbo.book_orders SET credit_applied = credit_applied + @use, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @Id", new { use, actorId, o.Id }, tx);
                    db.Execute("UPDATE dbo.book_customers SET credit = credit - @use, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @CustomerId", new { use, actorId, sub.CustomerId }, tx);
                    AuditLog.Write(db, tx, actorId, "update", "book_orders", o.Id.ToString(), new { order_no = o.OrderNo, credit_applied_added = use, via = "customer balance payment confirmed" });
                    o.CreditApplied += use;
                    credit -= use;
                }
                var left = sub.Amount;
                Guid? firstPayment = null;
                var paid = new List<string>();
                foreach (var o in orders.Where(o => o.Balance > 0).OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderNo))
                {
                    if (left <= 0) break;
                    var part = Math.Min(left, o.Balance);
                    var form = new BookPaymentForm
                    {
                        Amount = part, Discount = 0, Method = "bank", AccountId = sub.AccountId,
                        ReceivedOn = sub.CreatedAt.ToOffset(TimeSpan.FromHours(5)).Date, Note = "Customer payment towards the balance"
                    };
                    Guid pid;
                    try { pid = BookOrderRepository.RecordPaymentTx(db, tx, o.OrderNo, form, sub.ReceiptPath, sub.ReceiptMime, actorId, true); }
                    catch (BookOrderException ex)
                    {
                        // Usually stock: the first payment of an order takes its books off stock. Nothing has been saved.
                        throw new BookOrderException("Order " + BookFormat.OrderLabel(o.OrderNo) + " (the oldest unpaid order this payment reaches): " + ex.Message
                            + " Please add the stock first, then confirm again.");
                    }
                    if (firstPayment == null) firstPayment = pid;
                    if (db.Execute("UPDATE dbo.book_orders SET status = N'checked', updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @Id AND status = N'new'", new { actorId, o.Id }, tx) > 0)
                        AuditLog.Write(db, tx, actorId, "update", "book_orders", o.Id.ToString(), new { order_no = o.OrderNo, status_from = "new", status_to = "checked", via = "customer payment confirmed" });
                    BookMessageRepository.ForOrder(db, tx, o.Id, "payment_received", part);
                    paid.Add(BookFormat.OrderLabel(o.OrderNo) + ": " + BookFormat.Money(part));
                    left -= part;
                }
                if (firstPayment == null)
                    throw new BookOrderException("This customer owes nothing at the moment, so the payment cannot be applied. Mark it as not accepted and pay the money back, or add it as credit by hand.");
                if (left > 0 && !allowCredit)
                    throw new BookOrderException(BookFormat.Money(left) + " of this amount is more than the customer owes. Check the amount; if it is right, tick \"Put the extra in the customer's credit\" and confirm again.");
                if (left > 0)
                    BookMessageRepository.ForBalance(db, tx, sub.CustomerId, "payment_credit", sub.Amount, left);
                if (left > 0)
                    db.Execute("UPDATE dbo.book_customers SET credit = credit + @left, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @CustomerId",
                        new { left, actorId, sub.CustomerId }, tx);
                db.Execute(@"UPDATE dbo.book_payment_submissions SET status = N'confirmed', amount = @Amount, payment_id = @firstPayment, credit_amount = @left, reviewed_by = @actorId, reviewed_at = SYSDATETIMEOFFSET()
                             WHERE id = @Id", new { sub.Amount, firstPayment, left, actorId, sub.Id }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_payment_submissions", sub.Id.ToString(),
                    new { status = "confirmed", amount = sub.Amount, amount_when_sent = sent, applied = paid, to_credit = left });
                tx.Commit();
            }
        }

        private const string Columns = @"s.id, s.order_id, o.order_no, s.customer_id, c.customer_no, s.account_id, a.name AS account_name, s.amount, s.credit_amount, s.receipt_path, s.receipt_mime,
            s.status, s.reject_reason, u.full_name AS reviewed_by_name, s.reviewed_at, s.created_at, c.name AS customer_name,
            (SELECT COUNT(*) FROM dbo.book_payment_submissions s2 WHERE s2.customer_id = s.customer_id AND s2.status = N'pending' AND s2.id <> s.id) AS other_waiting";
        private const string From = @" FROM dbo.book_payment_submissions s
            LEFT JOIN dbo.book_orders o ON o.id = s.order_id
            JOIN dbo.book_customers c ON c.id = s.customer_id
            JOIN dbo.book_accounts a ON a.id = s.account_id
            LEFT JOIN dbo.users u ON u.id = s.reviewed_by";

        public static IList<BookPaymentSubmission> ForOrder(Guid orderId)
        {
            using (var db = Db.Open())
                return db.Query<BookPaymentSubmission>("SELECT " + Columns + From + " WHERE s.order_id = @orderId ORDER BY s.created_at", new { orderId }).ToList();
        }

        public static IList<BookPaymentSubmission> ForCustomer(Guid customerId)
        {
            using (var db = Db.Open())
                return db.Query<BookPaymentSubmission>("SELECT " + Columns + From + " WHERE s.customer_id = @customerId ORDER BY s.created_at DESC", new { customerId }).ToList();
        }

        public static BookPaymentSubmission Get(Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<BookPaymentSubmission>("SELECT " + Columns + From + " WHERE s.id = @id", new { id });
        }

        /// <summary>
        /// The office confirms: the payment is recorded (bank, the chosen account, the date it was sent, the receipt),
        /// and a New order becomes Confirmed. Throws <see cref="BookOrderException"/> with a message for the office.
        /// </summary>
        /// <summary>
        /// The office confirms a payment. <paramref name="received"/> is what the receipt really shows (the customer types
        /// no amount, review of 27 November 2026): it must always be entered, and it is written inside the same transaction
        /// as the payment, after the locks, so a failed confirm changes nothing. For a balance payment, money beyond what the
        /// customer owes becomes credit only when <paramref name="allowCredit"/> is set (a typing mistake cannot make credit).
        /// </summary>
        public static void Confirm(Guid id, Guid actorId, decimal received, bool allowCredit = false)
        {
            if (received <= 0 || decimal.Round(received, 2) != received) throw new BookOrderException("Please enter the amount received, as shown on the receipt.");
            var sub = Get(id);
            if (sub == null) throw new BookOrderException("The payment was not found.");
            var sent = sub.Amount;
            sub.Amount = received;
            if (!sub.OrderId.HasValue) { ConfirmBalancePayment(sub, actorId, sent, allowCredit); return; }
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                // Order first (RecordPaymentTx locks it), then the submission.
                db.Execute("SELECT id FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE id = @OrderId", new { sub.OrderId }, tx);
                var status = db.ExecuteScalar<string>("SELECT status FROM dbo.book_payment_submissions WITH (UPDLOCK, HOLDLOCK) WHERE id = @id", new { id }, tx);
                if (status != "pending") throw new BookOrderException("This payment has already been checked.");
                var form = new BookPaymentForm
                {
                    Amount = sub.Amount, Discount = 0, Method = "bank", AccountId = sub.AccountId,
                    ReceivedOn = sub.CreatedAt.ToOffset(TimeSpan.FromHours(5)).Date, Note = "Sent by the customer online"
                };
                var paymentId = BookOrderRepository.RecordPaymentTx(db, tx, sub.OrderNo.Value, form, sub.ReceiptPath, sub.ReceiptMime, actorId, true);
                db.Execute(@"UPDATE dbo.book_payment_submissions SET status = N'confirmed', amount = @received, payment_id = @paymentId, reviewed_by = @actorId, reviewed_at = SYSDATETIMEOFFSET()
                             WHERE id = @id", new { received, paymentId, actorId, id }, tx);
                var moved = db.Execute("UPDATE dbo.book_orders SET status = N'checked', updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @OrderId AND status = N'new'",
                    new { actorId, sub.OrderId }, tx);
                if (moved > 0)
                    AuditLog.Write(db, tx, actorId, "update", "book_orders", sub.OrderId.ToString(),
                        new { order_no = sub.OrderNo, status_from = "new", status_to = "checked", via = "customer payment confirmed" });
                BookMessageRepository.ForOrder(db, tx, sub.OrderId.Value, "payment_received", sub.Amount);
                AuditLog.Write(db, tx, actorId, "update", "book_payment_submissions", id.ToString(),
                    new { order_no = sub.OrderNo, status = "confirmed", payment_id = paymentId, amount = sub.Amount, amount_when_sent = sent, order_confirmed = moved > 0 });
                tx.Commit();
            }
        }

        public static void Reject(Guid id, string reason, Guid actorId)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new BookOrderException("Please write why the payment is not accepted; the customer will see it.");
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var row = db.QuerySingleOrDefault("SELECT order_id, customer_id FROM dbo.book_payment_submissions WHERE id = @id", new { id }, tx);
                if (row == null) throw new BookOrderException("The payment was not found.");
                Guid? orderId = row.order_id; Guid customerId = row.customer_id;
                if (orderId.HasValue) db.Execute("SELECT id FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE id = @orderId", new { orderId }, tx);
                else db.Execute("SELECT id FROM dbo.book_customers WITH (UPDLOCK, HOLDLOCK) WHERE id = @customerId", new { customerId }, tx);
                var status = db.ExecuteScalar<string>("SELECT status FROM dbo.book_payment_submissions WITH (UPDLOCK, HOLDLOCK) WHERE id = @id", new { id }, tx);
                if (status != "pending") throw new BookOrderException("This payment has already been checked.");
                db.Execute(@"UPDATE dbo.book_payment_submissions SET status = N'rejected', reject_reason = @reason, reviewed_by = @actorId, reviewed_at = SYSDATETIMEOFFSET()
                             WHERE id = @id", new { reason, actorId, id }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_payment_submissions", id.ToString(), new { status = "rejected", reason });
                var amount = db.ExecuteScalar<decimal>("SELECT amount FROM dbo.book_payment_submissions WHERE id = @id", new { id }, tx);
                if (orderId.HasValue) BookMessageRepository.ForOrder(db, tx, orderId.Value, "payment_not_accepted", amount, reason);
                else BookMessageRepository.ForBalance(db, tx, customerId, "payment_not_accepted", amount, 0m, reason);
                tx.Commit();
            }
        }
    }
}
