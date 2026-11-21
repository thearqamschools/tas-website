using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Ready-made messages for the office to copy into WhatsApp or SMS (owner's request, 20 November 2026). The website
    /// never sends anything: a message is written when something happens (order placed, confirmed, payment, packed,
    /// delivered, return, refund) and kept in the customer's message history until the office marks it as sent.
    /// Messages never mention discounts or purchase prices.
    /// </summary>
    public static class BookMessageRepository
    {
        private const string Sign = "\nThe Arqam Schools, Faisalabad. Phone 041-2660265, WhatsApp 0339-2406479";

        /// <summary>Writes a message inside the caller's transaction. Never throws for a missing customer: messages are a convenience.</summary>
        internal static void Add(IDbConnection db, IDbTransaction tx, Guid customerId, Guid? orderId, long? returnNo, string kind, string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return;
            if (body.Length > 1000) body = body.Substring(0, 1000);
            db.Execute(@"INSERT INTO dbo.book_messages (customer_id, order_id, return_no, kind, body) VALUES (@customerId, @orderId, @returnNo, @kind, @body)",
                new { customerId, orderId, returnNo, kind, body }, tx);
        }

        private class OrderInfo { public Guid Id { get; set; } public long OrderNo { get; set; } public Guid CustomerId { get; set; } public string Name { get; set; } public string Fulfilment { get; set; }
                                  public decimal Subtotal { get; set; } public decimal CreditApplied { get; set; } public decimal Balance { get; set; } }

        private static OrderInfo Info(IDbConnection db, IDbTransaction tx, Guid orderId)
        {
            return db.QuerySingleOrDefault<OrderInfo>(@"SELECT o.id, o.order_no, o.customer_id, c.name, o.fulfilment, o.subtotal, o.credit_applied,
                                                               (o.subtotal - o.returned_total - o.discount_total - o.credit_applied - o.paid_total + o.refunded_total) AS balance
                                                        FROM dbo.book_orders o JOIN dbo.book_customers c ON c.id = o.customer_id WHERE o.id = @orderId", new { orderId }, tx);
        }

        /// <summary>A message about an order, written from its current figures.</summary>
        internal static void ForOrder(IDbConnection db, IDbTransaction tx, Guid orderId, string kind, decimal amount = 0m, string reason = null)
        {
            var o = Info(db, tx, orderId);
            if (o == null) return;
            var no = BookFormat.OrderLabel(o.OrderNo);
            var hi = "Assalamualaikum " + o.Name + ",\n";
            string text;
            switch (kind)
            {
                case "order_placed":
                    text = hi + "Thank you. Your book order " + no + " for " + BookFormat.Money(o.Subtotal - o.CreditApplied) + " has been received. "
                         + "Please pay into one of our bank accounts and attach the receipt on the website (Books > My orders)."; break;
                case "order_confirmed": text = hi + "Your book order " + no + " is confirmed."; break;
                case "order_packed":
                    text = hi + "Your book order " + no + " is packed" + (o.Fulfilment == "delivery" ? " and will be delivered soon." : " and ready to collect from the office."); break;
                case "order_delivered": text = hi + "Your book order " + no + " has been delivered. Thank you."; break;
                case "order_collected": text = hi + "Your book order " + no + " has been collected. Thank you."; break;
                case "order_cancelled": text = hi + "Your book order " + no + " has been cancelled."; break;
                case "payment_received":
                    text = hi + "We have received your payment of " + BookFormat.Money(amount) + " for order " + no + ". "
                         + (o.Balance > 0 ? "Still to pay: " + BookFormat.Money(o.Balance) + "." : "The order is fully paid."); break;
                case "payment_not_accepted":
                    text = hi + "Your payment of " + BookFormat.Money(amount) + " for order " + no + " could not be accepted"
                         + (string.IsNullOrEmpty(reason) ? "." : ": " + reason + ".") + " Please contact the office."; break;
                default: return;
            }
            Add(db, tx, o.CustomerId, o.Id, null, kind, text + Sign);
        }

        /// <summary>A message about a payment the customer sent towards their whole balance (Database/28).</summary>
        internal static void ForBalance(IDbConnection db, IDbTransaction tx, Guid customerId, string kind, decimal amount, decimal credit, string reason = null)
        {
            var name = db.ExecuteScalar<string>("SELECT name FROM dbo.book_customers WHERE id = @customerId", new { customerId }, tx);
            var hi = "Assalamualaikum " + name + ",\n";
            string text;
            switch (kind)
            {
                case "payment_not_accepted":
                    text = hi + "Your payment of " + BookFormat.Money(amount) + " could not be accepted"
                         + (string.IsNullOrEmpty(reason) ? "." : ": " + reason + ".") + " Please contact the office."; break;
                case "payment_credit":
                    text = hi + BookFormat.Money(credit) + " from your payment of " + BookFormat.Money(amount) + " is added to your credit for the next order."; break;
                default: return;
            }
            Add(db, tx, customerId, null, null, kind, text + Sign);
        }

        internal static void ForReturn(IDbConnection db, IDbTransaction tx, Guid customerId, long returnNo, string kind, decimal value, decimal credit, decimal refund, string extra = null)
        {
            var name = db.ExecuteScalar<string>("SELECT name FROM dbo.book_customers WHERE id = @customerId", new { customerId }, tx);
            var hi = "Assalamualaikum " + name + ",\n";
            var rn = BookFormat.ReturnLabel(returnNo);
            string text;
            switch (kind)
            {
                case "return_received":
                    text = hi + "We have received your returned books (" + rn + ") worth " + BookFormat.Money(value) + "."
                         + (credit > 0 ? " " + BookFormat.Money(credit) + " is added to your credit for the next order." : "")
                         + (refund > 0 ? " " + BookFormat.Money(refund) + " will be paid back to you." : ""); break;
                case "refund_paid": text = hi + BookFormat.Money(refund) + " for your returned books (" + rn + ") has been paid back" + (string.IsNullOrEmpty(extra) ? "." : " by " + extra + "."); break;
                case "return_approved": text = hi + "Your return request " + rn + " is approved. Please send the books to the office."; break;
                case "return_rejected": text = hi + "Your return request " + rn + " could not be approved" + (string.IsNullOrEmpty(extra) ? "." : ": " + extra + ".") + " Please contact the office."; break;
                default: return;
            }
            Add(db, tx, customerId, null, returnNo, kind, text + Sign);
        }

        public class Message
        {
            public Guid Id { get; set; } public Guid CustomerId { get; set; } public long? OrderNo { get; set; } public long? ReturnNo { get; set; }
            public string Kind { get; set; } public string Body { get; set; } public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset? SentAt { get; set; } public string SentByName { get; set; }
            public string CustomerName { get; set; } public string CustomerMobile { get; set; }
            public string KindLabel
            {
                get
                {
                    switch (Kind)
                    {
                        case "order_placed": return "Order placed"; case "order_confirmed": return "Order confirmed"; case "order_packed": return "Packed";
                        case "order_delivered": return "Delivered"; case "order_collected": return "Collected"; case "order_cancelled": return "Cancelled";
                        case "payment_received": return "Payment received"; case "payment_not_accepted": return "Payment not accepted";
                        case "payment_credit": return "Payment to credit"; case "return_received": return "Return received"; case "refund_paid": return "Refund paid";
                        case "return_approved": return "Return approved"; case "return_rejected": return "Return not approved";
                        default: return Kind;
                    }
                }
            }
            /// <summary>Opens WhatsApp with the message ready (wa.me needs the number as 92XXXXXXXXXX).</summary>
            public string WhatsAppUrl
            {
                get
                {
                    var m = CustomerMobile ?? "";
                    var intl = m.Length == 11 && m.StartsWith("0") ? "92" + m.Substring(1) : m;
                    return "https://wa.me/" + intl + "?text=" + Uri.EscapeDataString(Body ?? "");
                }
            }
        }

        private const string Columns = @"m.id, m.customer_id, o.order_no, m.return_no, m.kind, m.body, m.created_at, m.sent_at, u.full_name AS sent_by_name,
            c.name AS customer_name, c.mobile AS customer_mobile";
        private const string From = @" FROM dbo.book_messages m JOIN dbo.book_customers c ON c.id = m.customer_id
            LEFT JOIN dbo.book_orders o ON o.id = m.order_id LEFT JOIN dbo.users u ON u.id = m.sent_by";

        public static IList<Message> ForCustomer(Guid customerId)
        {
            using (var db = Db.Open())
                return db.Query<Message>("SELECT TOP (200) " + Columns + From + " WHERE m.customer_id = @customerId AND (o.id IS NULL OR o.archived_at IS NULL) ORDER BY m.created_at DESC", new { customerId }).ToList();
        }

        /// <summary>Every kind of message, in the order the office meets them (for the Messages filter).</summary>
        public static readonly string[] Kinds =
        {
            "order_placed", "order_confirmed", "order_packed", "order_delivered", "order_collected", "order_cancelled",
            "payment_received", "payment_not_accepted", "payment_credit",
            "return_approved", "return_rejected", "return_received", "refund_paid"
        };

        public static string LabelFor(string kind) { return new Message { Kind = kind }.KindLabel; }

        public class Filter
        {
            /// <summary>"unsent" (default), "sent" or "all".</summary>
            public string Status { get; set; }
            public string Query { get; set; }
            public string Kind { get; set; }
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
        }

        /// <summary>The Messages tab (owner's request, 21 November 2026): all customers' messages with filters. At most 500 rows.</summary>
        public static IList<Message> Search(Filter f)
        {
            var where = new List<string> { "(o.id IS NULL OR o.archived_at IS NULL)" };
            if (f.Status == "sent") where.Add("m.sent_at IS NOT NULL");
            else if (f.Status != "all") where.Add("m.sent_at IS NULL");
            if (!string.IsNullOrEmpty(f.Kind) && Kinds.Contains(f.Kind)) where.Add("m.kind = @Kind");
            long no = 0;
            var q = (f.Query ?? "").Trim();
            if (q.Length > 0)
            {
                var digits = q.StartsWith("C-", StringComparison.OrdinalIgnoreCase) || q.StartsWith("C", StringComparison.OrdinalIgnoreCase) ? q.Substring(1).TrimStart('-', ' ') : q;
                long.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out no);
                where.Add("(c.name LIKE @like ESCAPE '\\' OR c.mobile LIKE @mobileLike ESCAPE '\\'" + (no > 0 ? " OR c.customer_no = @no" : "") + ")");
            }
            var pk = TimeSpan.FromHours(5);
            if (f.From.HasValue) where.Add("m.created_at >= @fromUtc");
            if (f.To.HasValue) where.Add("m.created_at < @toUtc");
            using (var db = Db.Open())
                return db.Query<Message>("SELECT TOP (500) " + Columns + From + " WHERE " + string.Join(" AND ", where) + " ORDER BY m.created_at DESC",
                    new
                    {
                        f.Kind, no,
                        like = "%" + CampusRepository.EscapeLike(q) + "%",
                        mobileLike = "%" + CampusRepository.EscapeLike(Helpers.Mobile.Normalize(q) ?? q) + "%",
                        fromUtc = f.From.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(f.From.Value.Date, DateTimeKind.Unspecified), pk) : (DateTimeOffset?)null,
                        toUtc = f.To.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(f.To.Value.Date.AddDays(1), DateTimeKind.Unspecified), pk) : (DateTimeOffset?)null
                    }).ToList();
        }

        public static IList<Message> Unsent(int max)
        {
            using (var db = Db.Open())
                return db.Query<Message>("SELECT TOP (@max) " + Columns + From + " WHERE m.sent_at IS NULL AND (o.id IS NULL OR o.archived_at IS NULL) ORDER BY m.created_at DESC", new { max }).ToList();
        }

        public static int UnsentCount()
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.book_messages m LEFT JOIN dbo.book_orders o ON o.id = m.order_id WHERE m.sent_at IS NULL AND (o.id IS NULL OR o.archived_at IS NULL)");
        }

        /// <summary>Marks a message as sent (or not sent again). Returns the customer, for the redirect.</summary>
        public static Guid? MarkSent(Guid id, bool sent, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var customerId = db.ExecuteScalar<Guid?>("SELECT customer_id FROM dbo.book_messages WITH (UPDLOCK) WHERE id = @id", new { id }, tx);
                if (customerId == null) return null;
                db.Execute(sent ? "UPDATE dbo.book_messages SET sent_at = SYSDATETIMEOFFSET(), sent_by = @actorId WHERE id = @id"
                                : "UPDATE dbo.book_messages SET sent_at = NULL, sent_by = NULL WHERE id = @id", new { id, actorId }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_messages", id.ToString(), new { sent });
                tx.Commit();
                return customerId;
            }
        }
    }
}
