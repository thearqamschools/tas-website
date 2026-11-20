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

        public static Guid Submit(long orderNo, Guid customerId, Guid accountId, decimal amount, string receiptPath, string receiptMime)
        {
            if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new BookOrderException("Please enter the amount you paid, in rupees.");
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var o = db.QuerySingleOrDefault<BookOrder>(@"
                    SELECT id, order_no, customer_id, status, subtotal, discount_total, credit_applied, paid_total, returned_total, refunded_total,
                           (SELECT COUNT(*) FROM dbo.book_payment_submissions s WHERE s.order_id = o.id AND s.status = N'pending') AS pending_payments
                    FROM dbo.book_orders o WITH (UPDLOCK, HOLDLOCK) WHERE order_no = @orderNo", new { orderNo }, tx);
                if (o == null || o.CustomerId != customerId) throw new BookOrderException("The order was not found.");
                if (o.Status == "cancelled") throw new BookOrderException("This order is cancelled.");
                if (o.PendingPayments > 0) throw new BookOrderException("Your earlier payment is still being checked by the office. Please wait for it first.");
                if (o.Balance <= 0) throw new BookOrderException("Nothing is left to pay on this order.");
                if (amount > o.Balance) throw new BookOrderException("The amount is more than the " + BookFormat.Money(o.Balance) + " still to pay.");
                var account = db.QuerySingleOrDefault<BookAccount>("SELECT id, kind, is_active, show_to_customers FROM dbo.book_accounts WHERE id = @accountId", new { accountId }, tx);
                if (account == null || !account.IsActive || account.Kind != "bank" || !account.ShowToCustomers)
                    throw new BookOrderException("Please choose the bank account you paid into.");
                var id = db.ExecuteScalar<Guid>(@"INSERT INTO dbo.book_payment_submissions (order_id, account_id, amount, receipt_path, receipt_mime)
                                                  OUTPUT inserted.id VALUES (@Id, @accountId, @amount, @receiptPath, @receiptMime)",
                    new { o.Id, accountId, amount, receiptPath, receiptMime }, tx);
                AuditLog.Write(db, tx, null, "insert", "book_payment_submissions", id.ToString(), new { order_no = orderNo, account_id = accountId, amount, by_customer = true });
                tx.Commit();
                return id;
            }
        }

        private const string Columns = @"s.id, s.order_id, o.order_no, s.account_id, a.name AS account_name, s.amount, s.receipt_path, s.receipt_mime,
            s.status, s.reject_reason, u.full_name AS reviewed_by_name, s.reviewed_at, s.created_at, c.name AS customer_name";
        private const string From = @" FROM dbo.book_payment_submissions s
            JOIN dbo.book_orders o ON o.id = s.order_id
            JOIN dbo.book_customers c ON c.id = o.customer_id
            JOIN dbo.book_accounts a ON a.id = s.account_id
            LEFT JOIN dbo.users u ON u.id = s.reviewed_by";

        public static IList<BookPaymentSubmission> ForOrder(Guid orderId)
        {
            using (var db = Db.Open())
                return db.Query<BookPaymentSubmission>("SELECT " + Columns + From + " WHERE s.order_id = @orderId ORDER BY s.created_at", new { orderId }).ToList();
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
        public static void Confirm(Guid id, Guid actorId)
        {
            var sub = Get(id);
            if (sub == null) throw new BookOrderException("The payment was not found.");
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
                var paymentId = BookOrderRepository.RecordPaymentTx(db, tx, sub.OrderNo, form, sub.ReceiptPath, sub.ReceiptMime, actorId, true);
                db.Execute(@"UPDATE dbo.book_payment_submissions SET status = N'confirmed', payment_id = @paymentId, reviewed_by = @actorId, reviewed_at = SYSDATETIMEOFFSET()
                             WHERE id = @id", new { paymentId, actorId, id }, tx);
                var moved = db.Execute("UPDATE dbo.book_orders SET status = N'checked', updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @OrderId AND status = N'new'",
                    new { actorId, sub.OrderId }, tx);
                if (moved > 0)
                    AuditLog.Write(db, tx, actorId, "update", "book_orders", sub.OrderId.ToString(),
                        new { order_no = sub.OrderNo, status_from = "new", status_to = "checked", via = "customer payment confirmed" });
                BookMessageRepository.ForOrder(db, tx, sub.OrderId, "payment_received", sub.Amount);
                AuditLog.Write(db, tx, actorId, "update", "book_payment_submissions", id.ToString(),
                    new { order_no = sub.OrderNo, status = "confirmed", payment_id = paymentId, amount = sub.Amount, order_confirmed = moved > 0 });
                tx.Commit();
            }
        }

        public static void Reject(Guid id, string reason, Guid actorId)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new BookOrderException("Please write why the payment is not accepted; the customer will see it.");
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var orderId = db.ExecuteScalar<Guid?>("SELECT order_id FROM dbo.book_payment_submissions WHERE id = @id", new { id }, tx);
                if (orderId == null) throw new BookOrderException("The payment was not found.");
                db.Execute("SELECT id FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE id = @orderId", new { orderId }, tx);
                var status = db.ExecuteScalar<string>("SELECT status FROM dbo.book_payment_submissions WITH (UPDLOCK, HOLDLOCK) WHERE id = @id", new { id }, tx);
                if (status != "pending") throw new BookOrderException("This payment has already been checked.");
                db.Execute(@"UPDATE dbo.book_payment_submissions SET status = N'rejected', reject_reason = @reason, reviewed_by = @actorId, reviewed_at = SYSDATETIMEOFFSET()
                             WHERE id = @id", new { reason, actorId, id }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_payment_submissions", id.ToString(), new { status = "rejected", reason });
                var amount = db.ExecuteScalar<decimal>("SELECT amount FROM dbo.book_payment_submissions WHERE id = @id", new { id }, tx);
                BookMessageRepository.ForOrder(db, tx, orderId.Value, "payment_not_accepted", amount, reason);
                tx.Commit();
            }
        }
    }
}
