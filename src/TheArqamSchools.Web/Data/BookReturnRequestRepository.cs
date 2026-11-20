using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Return requests from customers on the website (owner's request, 20 November 2026): the customer chooses books
    /// they received and whether the money should be kept as credit or paid back. The office approves or rejects;
    /// when the books arrive the office records them as received, which records the return itself with the same number
    /// (RN-...). Books already in an open request cannot be requested twice.
    /// </summary>
    public static class BookReturnRequestRepository
    {
        public class Request
        {
            public Guid Id { get; set; } public long ReturnNo { get; set; } public Guid CustomerId { get; set; } public long CustomerNo { get; set; }
            public string CustomerName { get; set; } public string CustomerMobile { get; set; } public string Status { get; set; } public string Choice { get; set; }
            public string Note { get; set; } public string RejectReason { get; set; } public string ReviewedByName { get; set; } public DateTimeOffset? ReviewedAt { get; set; }
            public DateTimeOffset CreatedAt { get; set; } public decimal? ReturnAmount { get; set; }
            public IList<Line> Lines { get; set; }
            public string Label { get { return BookFormat.ReturnLabel(ReturnNo); } }
            public string StatusLabel
            {
                get { return Status == "requested" ? "Waiting for the office" : Status == "approved" ? "Approved - please send the books" : Status == "rejected" ? "Not approved" : "Received"; }
            }
        }

        public class Line { public Guid ItemId { get; set; } public string ClassName { get; set; } public string ItemName { get; set; } public int Quantity { get; set; } }

        /// <summary>Books the customer may still ask to return: received, not returned, and not already in an open request.</summary>
        public static IList<BookReturnRepository.Returnable> Requestable(Guid customerId)
        {
            var list = BookReturnRepository.ReturnableBooks(customerId);
            using (var db = Db.Open())
            {
                var open = db.Query<Line>(@"SELECT l.item_id, SUM(l.quantity) AS quantity FROM dbo.book_return_request_lines l JOIN dbo.book_return_requests r ON r.id = l.request_id
                                            WHERE r.customer_id = @customerId AND r.status IN (N'requested', N'approved') GROUP BY l.item_id", new { customerId })
                             .ToDictionary(x => x.ItemId, x => x.Quantity);
                foreach (var b in list) { int n; if (open.TryGetValue(b.ItemId, out n)) b.Available = Math.Max(0, b.Available - n); }
            }
            return list.Where(b => b.Available > 0).ToList();
        }

        public static long Create(Guid customerId, IDictionary<Guid, int> quantities, string choice, string note)
        {
            var wanted = (quantities ?? new Dictionary<Guid, int>()).Where(q => q.Value > 0).ToDictionary(q => q.Key, q => q.Value);
            if (wanted.Count == 0) throw new BookOrderException("Please enter how many of each book you want to return.");
            if (wanted.Values.Any(v => v > BookRepository.MaxQuantityPerBook)) throw new BookOrderException("Please check the quantities.");
            choice = choice == "refund" ? "refund" : "credit";
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                // Customer row first: two requests from the same customer cannot race.
                db.Execute("SELECT id FROM dbo.book_customers WITH (UPDLOCK, HOLDLOCK) WHERE id = @customerId", new { customerId }, tx);
                var have = db.Query<Line>(@"SELECT l.item_id, MAX(l.class_name) AS class_name, MAX(l.item_name) AS item_name, SUM(l.quantity - l.returned_qty) AS quantity
                                            FROM dbo.book_order_lines l JOIN dbo.book_orders o ON o.id = l.order_id
                                            WHERE o.customer_id = @customerId AND o.status IN (N'delivered', N'collected') GROUP BY l.item_id", new { customerId }, tx)
                             .ToDictionary(x => x.ItemId);
                var open = db.Query<Line>(@"SELECT l.item_id, SUM(l.quantity) AS quantity FROM dbo.book_return_request_lines l JOIN dbo.book_return_requests r ON r.id = l.request_id
                                            WHERE r.customer_id = @customerId AND r.status IN (N'requested', N'approved') GROUP BY l.item_id", new { customerId }, tx)
                             .ToDictionary(x => x.ItemId, x => x.Quantity);
                var problems = new List<string>();
                foreach (var w in wanted)
                {
                    Line h;
                    int pending;
                    open.TryGetValue(w.Key, out pending);
                    var max = have.TryGetValue(w.Key, out h) ? h.Quantity - pending : 0;
                    if (w.Value > max) problems.Add((h != null ? h.ClassName + " – " + h.ItemName : "A book") + ": at most " + Math.Max(0, max) + ".");
                }
                if (problems.Count > 0) throw new BookOrderException("You cannot return more books than you received. " + string.Join(" ", problems));
                var no = db.ExecuteScalar<long>("SELECT NEXT VALUE FOR dbo.book_return_no_seq", null, tx);
                var id = db.ExecuteScalar<Guid>(@"INSERT INTO dbo.book_return_requests (return_no, customer_id, choice, note) OUTPUT inserted.id VALUES (@no, @customerId, @choice, @note)",
                    new { no, customerId, choice, note }, tx);
                foreach (var w in wanted)
                    db.Execute("INSERT INTO dbo.book_return_request_lines (request_id, item_id, quantity) VALUES (@id, @item, @qty)", new { id, item = w.Key, qty = w.Value }, tx);
                AuditLog.Write(db, tx, null, "insert", "book_return_requests", id.ToString(), new { return_no = no, customer_id = customerId, choice, lines = wanted, by_customer = true });
                tx.Commit();
                return no;
            }
        }

        private const string Columns = @"r.id, r.return_no, r.customer_id, c.customer_no, c.name AS customer_name, c.mobile AS customer_mobile, r.status, r.choice, r.note,
            r.reject_reason, u.full_name AS reviewed_by_name, r.reviewed_at, r.created_at, b.amount AS return_amount";
        private const string From = @" FROM dbo.book_return_requests r JOIN dbo.book_customers c ON c.id = r.customer_id
            LEFT JOIN dbo.users u ON u.id = r.reviewed_by LEFT JOIN dbo.book_returns b ON b.id = r.return_id";

        private static IList<Request> Fill(IDbConnection db, IList<Request> list)
        {
            if (list.Count == 0) return list;
            var ids = list.Select(r => r.Id).ToList();
            var lines = db.Query(@"SELECT l.request_id, l.item_id, i.class_name, i.name AS item_name, l.quantity FROM dbo.book_return_request_lines l
                                   JOIN dbo.book_items i ON i.id = l.item_id WHERE l.request_id IN @ids", new { ids }).ToList();
            foreach (var r in list)
                r.Lines = lines.Where(l => (Guid)l.request_id == r.Id)
                               .Select(l => new Line { ItemId = l.item_id, ClassName = l.class_name, ItemName = l.item_name, Quantity = l.quantity })
                               .OrderBy(l => BookRepository.ClassRank(l.ClassName)).ThenBy(l => l.ItemName).ToList();
            return list;
        }

        public static IList<Request> ForCustomer(Guid customerId)
        {
            using (var db = Db.Open())
                return Fill(db, db.Query<Request>("SELECT " + Columns + From + " WHERE r.customer_id = @customerId ORDER BY r.created_at DESC", new { customerId }).ToList());
        }

        public static IList<Request> List(string status)
        {
            using (var db = Db.Open())
                return Fill(db, db.Query<Request>("SELECT TOP (300) " + Columns + From + (string.IsNullOrEmpty(status) ? "" : " WHERE r.status = @status")
                                                  + " ORDER BY CASE r.status WHEN N'requested' THEN 0 WHEN N'approved' THEN 1 ELSE 2 END, r.created_at DESC", new { status }).ToList());
        }

        public static Request Get(long returnNo)
        {
            using (var db = Db.Open())
                return Fill(db, db.Query<Request>("SELECT " + Columns + From + " WHERE r.return_no = @returnNo", new { returnNo }).ToList()).FirstOrDefault();
        }

        public static void Approve(long returnNo, Guid actorId) { Review(returnNo, "approved", null, actorId); }

        public static void Reject(long returnNo, string reason, Guid actorId)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new BookOrderException("Please write why the return is not approved; the customer will see it.");
            Review(returnNo, "rejected", reason, actorId);
        }

        private static void Review(long returnNo, string status, string reason, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var r = db.QuerySingleOrDefault<Request>("SELECT id, return_no, customer_id, status FROM dbo.book_return_requests WITH (UPDLOCK, HOLDLOCK) WHERE return_no = @returnNo", new { returnNo }, tx);
                if (r == null) throw new BookOrderException("The return request was not found.");
                if (r.Status != "requested" && !(status == "rejected" && r.Status == "approved"))
                    throw new BookOrderException("This return request has already been " + r.Status + ".");
                db.Execute("UPDATE dbo.book_return_requests SET status = @status, reject_reason = @reason, reviewed_by = @actorId, reviewed_at = SYSDATETIMEOFFSET() WHERE id = @Id",
                    new { status, reason, actorId, r.Id }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_return_requests", r.Id.ToString(), new { return_no = returnNo, status_from = r.Status, status_to = status, reason });
                BookMessageRepository.ForReturn(db, tx, r.CustomerId, returnNo, status == "approved" ? "return_approved" : "return_rejected", 0, 0, 0, reason);
                tx.Commit();
            }
        }

        /// <summary>The books have arrived: records the return (same number) with the quantities that actually came.</summary>
        public static void Receive(long returnNo, IDictionary<Guid, int> quantities, DateTime receivedOn, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var customerId = db.ExecuteScalar<Guid?>("SELECT customer_id FROM dbo.book_return_requests WHERE return_no = @returnNo", new { returnNo }, tx);
                if (customerId == null) throw new BookOrderException("The return request was not found.");
                // Lock order as everywhere: the customer's orders, then the customer (both inside Record), then the request.
                var no = BookReturnRepository.Record(db, tx, customerId.Value, quantities, "credit", receivedOn, null, actorId, returnNo, () =>
                {
                    var r = db.QuerySingle<Request>("SELECT id, status, choice, note FROM dbo.book_return_requests WITH (UPDLOCK, HOLDLOCK) WHERE return_no = @returnNo", new { returnNo }, tx);
                    if (r.Status != "approved") throw new BookOrderException("Only an approved request can be received (this one is " + r.Status + ").");
                    return new KeyValuePair<string, string>(r.Choice, "Request " + BookFormat.ReturnLabel(returnNo) + (string.IsNullOrEmpty(r.Note) ? "" : ": " + r.Note));
                });
                var returnId = db.ExecuteScalar<Guid>("SELECT id FROM dbo.book_returns WHERE return_no = @no", new { no }, tx);
                db.Execute("UPDATE dbo.book_return_requests SET status = N'received', return_id = @returnId WHERE return_no = @returnNo", new { returnId, returnNo }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_return_requests", returnNo.ToString(System.Globalization.CultureInfo.InvariantCulture), new { status = "received", return_id = returnId });
                tx.Commit();
            }
        }

        public static int OpenCount(Guid customerId)
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.book_return_requests WHERE customer_id = @customerId AND status IN (N'requested', N'approved')", new { customerId });
        }
    }
}
