using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Customer accounts (owner's request, 20 November 2026): a summary, a ledger and reports for each book customer.
    /// The ledger is what the customer owes: + orders (not cancelled), − payments, − discounts, − returns (value after
    /// the discount share), + money paid back. Its last balance always equals the balance of all the customer's orders
    /// less their credit (checked by the tests).
    /// </summary>
    public static class BookCustomerAccountRepository
    {
        public class Summary
        {
            public int Orders { get; set; } public int Completed { get; set; } public int Open { get; set; } public int Cancelled { get; set; }
            public int BooksTaken { get; set; } public int BooksReturned { get; set; }
            public decimal OrderTotal { get; set; } public decimal Paid { get; set; } public decimal Discounts { get; set; }
            public decimal Returned { get; set; } public decimal PaidBack { get; set; } public decimal RefundPending { get; set; }
            public decimal Credit { get; set; } public decimal Owed { get; set; } public decimal Overdue { get; set; } public int OverdueOrders { get; set; }
            /// <summary>What the customer owes after their credit (may be negative when the school owes them).</summary>
            public decimal Balance { get { return Owed - Credit; } }
        }

        public class LedgerLine
        {
            public DateTime On { get; set; } public DateTimeOffset At { get; set; } public string Kind { get; set; } public string Reference { get; set; }
            public string Details { get; set; } public decimal Debit { get; set; } public decimal CreditAmount { get; set; } public decimal Balance { get; set; }
        }

        public class CustomerRow
        {
            public Guid Id { get; set; } public long CustomerNo { get; set; } public string Name { get; set; } public string Mobile { get; set; }
            public string CampusName { get; set; } public decimal? CreditLimit { get; set; } public int? CreditDays { get; set; } public decimal Credit { get; set; }
            public int Orders { get; set; } public decimal Owed { get; set; } public decimal Overdue { get; set; } public bool IsActive { get; set; }
            public decimal Balance { get { return Owed - Credit; } }
        }

        private const string Bal = "(o.subtotal - o.returned_total - o.discount_total - o.credit_applied - o.paid_total + o.refunded_total)";
        // An order is overdue when it was handed over, still has something to pay, and the customer's credit days have passed.
        private const string OverdueCond = "o.stock_taken_at IS NOT NULL AND c.credit_days IS NOT NULL AND DATEADD(day, c.credit_days, o.stock_taken_at) < SYSDATETIMEOFFSET()";

        public static IList<CustomerRow> Customers(string q)
        {
            q = (q ?? "").Trim();
            var mobile = Helpers.Mobile.Normalize(q);
            using (var db = Db.Open())
                return db.Query<CustomerRow>(@"
                    SELECT TOP (500) c.id, c.customer_no, c.name, c.mobile, c.campus_name, c.credit_limit, c.credit_days, c.credit, c.is_active,
                           (SELECT COUNT(*) FROM dbo.book_orders o WHERE o.customer_id = c.id AND o.status <> N'cancelled') AS orders,
                           (SELECT COALESCE(SUM(" + Bal + @"), 0) FROM dbo.book_orders o WHERE o.customer_id = c.id AND o.status <> N'cancelled') AS owed,
                           (SELECT COALESCE(SUM(" + Bal + @"), 0) FROM dbo.book_orders o WHERE o.customer_id = c.id AND o.status <> N'cancelled' AND " + Bal + " > 0 AND " + OverdueCond + @") AS overdue
                    FROM dbo.book_customers c
                    WHERE c.archived_at IS NULL" + (q.Length == 0 ? "" : @" AND (c.name LIKE @like ESCAPE '\' OR c.campus_name LIKE @like ESCAPE '\' OR c.mobile LIKE @mlike ESCAPE '\'
                          OR CAST(c.customer_no AS NVARCHAR(20)) = @no)") + " ORDER BY c.name",
                    new { like = "%" + CampusRepository.EscapeLike(q) + "%", mlike = "%" + CampusRepository.EscapeLike(mobile ?? q) + "%", no = q.TrimStart('C', 'c', '-') }).ToList();
        }

        public static Summary GetSummary(Guid customerId)
        {
            using (var db = Db.Open())
            {
                var s = db.QuerySingle<Summary>(@"
                    SELECT COUNT(*) AS orders,
                           COALESCE(SUM(CASE WHEN o.status IN (N'delivered', N'collected') THEN 1 ELSE 0 END), 0) AS completed,
                           COALESCE(SUM(CASE WHEN o.status IN (N'new', N'checked', N'packed') THEN 1 ELSE 0 END), 0) AS [open],
                           COALESCE(SUM(CASE WHEN o.status = N'cancelled' THEN 1 ELSE 0 END), 0) AS cancelled,
                           COALESCE(SUM(CASE WHEN o.status <> N'cancelled' THEN o.subtotal END), 0) AS order_total,
                           COALESCE(SUM(CASE WHEN o.status <> N'cancelled' THEN o.paid_total END), 0) AS paid,
                           COALESCE(SUM(CASE WHEN o.status <> N'cancelled' THEN o.discount_total END), 0) AS discounts,
                           COALESCE(SUM(CASE WHEN o.status <> N'cancelled' THEN " + Bal + @" END), 0) AS owed,
                           COALESCE(SUM(CASE WHEN o.status <> N'cancelled' AND " + Bal + " > 0 AND " + OverdueCond + " THEN " + Bal + @" END), 0) AS overdue,
                           COALESCE(SUM(CASE WHEN o.status <> N'cancelled' AND " + Bal + " > 0 AND " + OverdueCond + @" THEN 1 ELSE 0 END), 0) AS overdue_orders
                    FROM dbo.book_orders o JOIN dbo.book_customers c ON c.id = o.customer_id WHERE o.customer_id = @customerId", new { customerId });
                s.BooksTaken = db.ExecuteScalar<int>(@"SELECT COALESCE(SUM(l.quantity), 0) FROM dbo.book_order_lines l JOIN dbo.book_orders o ON o.id = l.order_id
                                                       WHERE o.customer_id = @customerId AND o.stock_taken_at IS NOT NULL AND o.status <> N'cancelled'", new { customerId });
                s.BooksReturned = db.ExecuteScalar<int>(@"SELECT COALESCE(SUM(rl.quantity), 0) FROM dbo.book_return_lines rl JOIN dbo.book_returns r ON r.id = rl.return_id
                                                          WHERE r.customer_id = @customerId", new { customerId });
                var r = db.QuerySingle(@"SELECT COALESCE(SUM(amount), 0) AS returned, COALESCE(SUM(CASE WHEN refund_paid_at IS NOT NULL THEN refund_amount END), 0) AS paid_back,
                                               COALESCE(SUM(CASE WHEN refund_paid_at IS NULL THEN refund_amount END), 0) AS pending
                                        FROM dbo.book_returns WHERE customer_id = @customerId", new { customerId });
                s.Returned = r.returned; s.PaidBack = r.paid_back; s.RefundPending = r.pending;
                s.Credit = db.ExecuteScalar<decimal>("SELECT credit FROM dbo.book_customers WHERE id = @customerId", new { customerId });
                return s;
            }
        }

        /// <summary>The ledger between two dates (inclusive), with the balance brought forward from before <paramref name="from"/>.</summary>
        public static IList<LedgerLine> Ledger(Guid customerId, DateTime? from, DateTime? to, bool forCustomer)
        {
            using (var db = Db.Open())
            {
                var pk = TimeSpan.FromHours(5);
                var lines = new List<LedgerLine>();
                foreach (var o in db.Query(@"SELECT order_no, subtotal, created_at, status FROM dbo.book_orders WHERE customer_id = @customerId AND status <> N'cancelled'", new { customerId }))
                {
                    var at = (DateTimeOffset)o.created_at;
                    lines.Add(new LedgerLine { At = at, On = at.ToOffset(pk).Date, Kind = "Order", Reference = BookFormat.OrderLabel((long)o.order_no), Details = BookFormat.StatusLabel((string)o.status), Debit = (decimal)o.subtotal });
                }
                foreach (var p in db.Query(@"SELECT o.order_no, p.amount, p.discount, p.method, a.name AS account, p.received_on, p.created_at
                                             FROM dbo.book_payments p JOIN dbo.book_orders o ON o.id = p.order_id LEFT JOIN dbo.book_accounts a ON a.id = p.account_id
                                             WHERE o.customer_id = @customerId AND o.status <> N'cancelled'", new { customerId }))
                {
                    var on = (DateTime)p.received_on; var at = (DateTimeOffset)p.created_at;
                    if ((decimal)p.amount > 0)
                        lines.Add(new LedgerLine { At = at, On = on, Kind = "Payment", Reference = BookFormat.OrderLabel((long)p.order_no),
                                                   Details = ((string)p.method == "bank" ? "Bank" : "Cash") + (p.account != null && !forCustomer ? " · " + (string)p.account : ""), CreditAmount = (decimal)p.amount });
                    if ((decimal)p.discount > 0)
                        lines.Add(new LedgerLine { At = at, On = on, Kind = forCustomer ? "Adjustment" : "Discount", Reference = BookFormat.OrderLabel((long)p.order_no), CreditAmount = (decimal)p.discount });
                }
                foreach (var r in db.Query(@"SELECT return_no, amount, refund_amount, credit_amount, refund_method, refund_paid_at, refunded_on, created_at FROM dbo.book_returns WHERE customer_id = @customerId", new { customerId }))
                {
                    var at = (DateTimeOffset)r.created_at; var on = (DateTime)r.refunded_on;
                    lines.Add(new LedgerLine { At = at, On = on, Kind = "Return", Reference = BookFormat.ReturnLabel((long)r.return_no),
                                               Details = (decimal)r.credit_amount > 0 ? BookFormat.Money((decimal)r.credit_amount) + " to credit" : "", CreditAmount = (decimal)r.amount });
                    if ((decimal)r.refund_amount > 0)
                        lines.Add(new LedgerLine { At = at.AddMilliseconds(1), On = on, Kind = r.refund_paid_at == null ? "Refund due" : "Refund paid", Reference = BookFormat.ReturnLabel((long)r.return_no),
                                                   Details = r.refund_paid_at == null ? "To be paid back" : ((string)r.refund_method == "bank" ? "Bank" : "Cash"), Debit = (decimal)r.refund_amount });
                }
                lines = lines.OrderBy(l => l.On).ThenBy(l => l.At).ToList();
                decimal running = 0;
                var result = new List<LedgerLine>();
                var opening = from.HasValue ? lines.Where(l => l.On < from.Value.Date).Sum(l => l.Debit - l.CreditAmount) : 0m;
                if (from.HasValue) result.Add(new LedgerLine { On = from.Value.Date, Kind = "Brought forward", Balance = opening });
                running = opening;
                foreach (var l in lines.Where(l => (!from.HasValue || l.On >= from.Value.Date) && (!to.HasValue || l.On <= to.Value.Date)))
                {
                    running += l.Debit - l.CreditAmount;
                    l.Balance = running;
                    result.Add(l);
                }
                return result;
            }
        }

        public class ToDo
        {
            public int NewOrders { get; set; } public int PaymentsToCheck { get; set; } public int ReturnRequests { get; set; } public int ReturnsToReceive { get; set; }
            public int RefundsPending { get; set; } public int MessagesToSend { get; set; } public int OverdueCustomers { get; set; }
            public int Total { get { return NewOrders + PaymentsToCheck + ReturnRequests + ReturnsToReceive + RefundsPending + MessagesToSend + OverdueCustomers; } }
        }

        public static ToDo GetToDo()
        {
            using (var db = Db.Open())
                return db.QuerySingle<ToDo>(@"
                    SELECT (SELECT COUNT(*) FROM dbo.book_orders WHERE status = N'new') AS new_orders,
                           (SELECT COUNT(*) FROM dbo.book_payment_submissions WHERE status = N'pending') AS payments_to_check,
                           (SELECT COUNT(*) FROM dbo.book_return_requests WHERE status = N'requested') AS return_requests,
                           (SELECT COUNT(*) FROM dbo.book_return_requests WHERE status = N'approved') AS returns_to_receive,
                           (SELECT COUNT(*) FROM dbo.book_returns WHERE refund_amount > 0 AND refund_paid_at IS NULL) AS refunds_pending,
                           (SELECT COUNT(*) FROM dbo.book_messages WHERE sent_at IS NULL) AS messages_to_send,
                           (SELECT COUNT(DISTINCT o.customer_id) FROM dbo.book_orders o JOIN dbo.book_customers c ON c.id = o.customer_id
                             WHERE o.status <> N'cancelled' AND " + Bal + " > 0 AND " + OverdueCond + ") AS overdue_customers");
        }
    }
}
