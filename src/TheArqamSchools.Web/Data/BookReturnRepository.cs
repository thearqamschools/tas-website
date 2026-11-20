using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Returns of books per customer (owner's decision, 20 November 2026): the books may come from several orders.
    /// - Only books the customer has received (order handed over, not cancelled) can come back, never more than
    ///   received and not yet returned. Each book is taken from the customer's NEWEST order first.
    /// - Each piece is valued at the price of that order line less the order's discount share
    ///   (discount × value ÷ order subtotal); when the last books of an order come back, the order's remainder is used.
    /// - The value first reduces what is still owed on those orders. Anything beyond that ("excess") either becomes
    ///   the customer's credit (used automatically on the next order) or is paid back. A payback never exceeds the
    ///   money actually received on those orders, nor what is left after the customer's other unpaid orders; the rest
    ///   becomes credit. A payback can wait ("refund pending")
    ///   until the office records how it was paid.
    /// - The books go back into stock. Lock order: the customer's orders (by id), then the customer, then books (by id).
    /// </summary>
    public static class BookReturnRepository
    {
        public class Returnable
        {
            public Guid ItemId { get; set; } public string ClassName { get; set; } public string ItemName { get; set; }
            public int Available { get; set; } public decimal UnitPrice { get; set; }
        }

        /// <summary>Books the customer can return, with the price of the newest order line (before any discount share).</summary>
        public static IList<Returnable> ReturnableBooks(Guid customerId)
        {
            using (var db = Db.Open())
            {
                var rows = db.Query<dynamic>(@"
                    SELECT l.item_id, l.class_name, l.item_name, l.quantity - l.returned_qty AS available, l.unit_price, o.created_at
                    FROM dbo.book_order_lines l JOIN dbo.book_orders o ON o.id = l.order_id
                    WHERE o.customer_id = @customerId AND o.status IN (N'delivered', N'collected') AND l.quantity > l.returned_qty",
                    new { customerId }).ToList();
                return rows.GroupBy(r => (Guid)r.item_id).Select(g =>
                {
                    var newest = g.OrderByDescending(r => (DateTimeOffset)r.created_at).First();
                    return new Returnable { ItemId = g.Key, ClassName = newest.class_name, ItemName = newest.item_name, Available = g.Sum(r => (int)r.available), UnitPrice = newest.unit_price };
                }).OrderBy(r => BookRepository.ClassRank(r.ClassName)).ThenBy(r => r.ClassName).ThenBy(r => r.ItemName).ToList();
            }
        }

        private class LineRow
        {
            public Guid Id { get; set; } public Guid OrderId { get; set; } public long OrderNo { get; set; } public Guid ItemId { get; set; }
            public string ClassName { get; set; } public string ItemName { get; set; } public decimal UnitPrice { get; set; }
            public int Quantity { get; set; } public int ReturnedQty { get; set; } public DateTimeOffset CreatedAt { get; set; }
        }

        /// <summary>
        /// Records a return. <paramref name="choice"/> is "credit" or "refund". <paramref name="returnNo"/> is given when
        /// the return completes a customer's request (same number), otherwise a new number is taken. Returns the number.
        /// </summary>
        internal static long Record(IDbConnection db, IDbTransaction tx, Guid customerId, IDictionary<Guid, int> quantities, string choice,
                                    DateTime returnedOn, string note, Guid actorId, long? returnNo,
                                    Func<KeyValuePair<string, string>> afterLocks = null)
        {
            var wanted = (quantities ?? new Dictionary<Guid, int>()).Where(q => q.Value > 0).ToDictionary(q => q.Key, q => q.Value);
            if (wanted.Count == 0) throw new BookOrderException("Please enter how many of each book come back.");
            if (wanted.Values.Any(v => v > BookRepository.MaxQuantityPerBook)) throw new BookOrderException("Please check the quantities.");
            choice = choice == "refund" ? "refund" : "credit";
            if (returnedOn.Date > DateTime.UtcNow.AddHours(5).Date) throw new BookOrderException("The return date cannot be in the future.");

            // 1. Lock the customer's handed-over orders (by id), then the customer.
            var orders = db.Query<BookOrder>(@"SELECT id, order_no, customer_id, status, subtotal, discount_total, credit_applied, paid_total, returned_total, refunded_total, stock_taken_at
                                               FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK)
                                               WHERE customer_id = @customerId AND status <> N'cancelled' ORDER BY id",
                new { customerId }, tx).ToDictionary(o => o.Id);
            var customer = db.QuerySingleOrDefault<BookCustomer>("SELECT id, name FROM dbo.book_customers WITH (UPDLOCK, HOLDLOCK) WHERE id = @customerId", new { customerId }, tx);
            if (customer == null) throw new BookOrderException("The customer was not found.");
            // A request locks its own row only after the orders and the customer (the lock order), and gives its choice and note.
            if (afterLocks != null) { var cn = afterLocks(); choice = cn.Key == "refund" ? "refund" : "credit"; note = cn.Value; }
            // Only books the customer has received (order delivered or collected) can come back.
            var orderIds = orders.Values.Where(o => o.Status == "delivered" || o.Status == "collected").Select(o => o.Id).ToList();
            var lines = orderIds.Count == 0 ? new List<LineRow>() : db.Query<LineRow>(@"
                SELECT l.id, l.order_id, o.order_no, l.item_id, l.class_name, l.item_name, l.unit_price, l.quantity, l.returned_qty, o.created_at
                FROM dbo.book_order_lines l JOIN dbo.book_orders o ON o.id = l.order_id
                WHERE l.order_id IN @orderIds AND l.quantity > l.returned_qty", new { orderIds }, tx).ToList();

            // 2. Take each book from the newest order first.
            var take = new List<KeyValuePair<LineRow, int>>();
            var problems = new List<string>();
            foreach (var w in wanted)
            {
                var candidates = lines.Where(l => l.ItemId == w.Key).OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.OrderNo).ToList();
                var left = w.Value;
                foreach (var l in candidates)
                {
                    if (left == 0) break;
                    var n = Math.Min(left, l.Quantity - l.ReturnedQty);
                    take.Add(new KeyValuePair<LineRow, int>(l, n));
                    left -= n;
                }
                if (left > 0)
                {
                    var any = candidates.FirstOrDefault();
                    problems.Add((any != null ? any.ClassName + " – " + any.ItemName : "A book") + ": at most " + (w.Value - left) + " can come back.");
                }
            }
            if (problems.Count > 0) throw new BookOrderException(string.Join(" ", problems));

            // 3. Value per order (net of the order's discount share), what it settles, and the excess.
            var perOrder = take.GroupBy(t => t.Key.OrderId).ToList();
            decimal totalValue = 0, totalExcess = 0, cashCap = 0;
            var orderValue = new Dictionary<Guid, decimal>();
            var lineNet = new Dictionary<Guid, decimal>();
            foreach (var g in perOrder)
            {
                var o = orders[g.Key];
                var gross = g.Sum(t => t.Key.UnitPrice * t.Value);
                var remaining = Math.Max(0m, o.Subtotal - o.ReturnedTotal - o.DiscountTotal);
                var orderLines = db.Query<BookOrderLine>("SELECT id, quantity, returned_qty FROM dbo.book_order_lines WHERE order_id = @Id", new { o.Id }, tx).ToList();
                var allBack = orderLines.All(l => l.Quantity - l.ReturnedQty - g.Where(t => t.Key.Id == l.Id).Sum(t => t.Value) == 0);
                var value = allBack ? remaining
                    : o.Subtotal <= 0 ? 0m
                    : Math.Min(remaining, Math.Round(gross * (o.Subtotal - o.DiscountTotal) / o.Subtotal, 2, MidpointRounding.AwayFromZero));
                value = Math.Max(0m, value);
                orderValue[o.Id] = value;
                // Spread the order's value over its lines (by gross), the last line takes the rounding.
                var parts = g.ToList();
                decimal spread = 0;
                for (var i = 0; i < parts.Count; i++)
                {
                    var lg = parts[i].Key.UnitPrice * parts[i].Value;
                    var net = i == parts.Count - 1 ? Math.Max(0m, value - spread) : (gross == 0 ? 0 : Math.Min(value - spread, Math.Round(value * lg / gross, 2, MidpointRounding.AwayFromZero)));
                    lineNet[parts[i].Key.Id] = (lineNet.ContainsKey(parts[i].Key.Id) ? lineNet[parts[i].Key.Id] : 0) + net;
                    spread += net;
                }
                var excess = Math.Max(0m, value - o.Balance);
                totalValue += value;
                totalExcess += excess;
                cashCap += Math.Min(excess, Math.Max(0m, o.PaidTotal - o.RefundedTotal));
            }
            // Money is paid back only beyond what the customer still owes on their other orders; the rest stays as credit.
            var otherOwed = orders.Values.Where(o => !orderValue.ContainsKey(o.Id) && o.StockTakenAt != null).Sum(o => Math.Max(0m, o.Balance));
            var refund = choice == "refund" ? Math.Max(0m, Math.Min(cashCap, totalExcess - otherOwed)) : 0m;
            var credit = totalExcess - refund;

            // 4. Write the return, its lines, the orders, stock and credit.
            var no = returnNo ?? db.ExecuteScalar<long>("SELECT NEXT VALUE FOR dbo.book_return_no_seq", null, tx);
            var firstOrder = perOrder.Count == 1 ? perOrder[0].Key : (Guid?)null;
            var returnId = db.ExecuteScalar<Guid>(@"
                INSERT INTO dbo.book_returns (return_no, order_id, customer_id, amount, refund_amount, credit_amount, refunded_on, note, created_by)
                OUTPUT inserted.id VALUES (@no, @firstOrder, @customerId, @totalValue, @refund, @credit, @on, @note, @actorId)",
                new { no, firstOrder, customerId, totalValue, refund, credit, on = returnedOn.Date, note, actorId }, tx);
            foreach (var t in take)
            {
                db.Execute("INSERT INTO dbo.book_return_lines (return_id, order_line_id, quantity, unit_price, net_amount) VALUES (@returnId, @lineId, @qty, @UnitPrice, @net)",
                    new { returnId, lineId = t.Key.Id, qty = t.Value, t.Key.UnitPrice, net = lineNet.ContainsKey(t.Key.Id) ? lineNet[t.Key.Id] : 0m }, tx);
                lineNet.Remove(t.Key.Id);   // a line appears once; its net amount was summed above
                db.Execute("UPDATE dbo.book_order_lines SET returned_qty = returned_qty + @qty WHERE id = @Id", new { qty = t.Value, t.Key.Id }, tx);
            }
            foreach (var g in perOrder)
            {
                var o = orders[g.Key];
                var excess = Math.Max(0m, orderValue[o.Id] - o.Balance);
                db.Execute(@"UPDATE dbo.book_orders SET returned_total = returned_total + @value, refunded_total = refunded_total + @excess,
                                    updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @Id",
                    new { value = orderValue[o.Id], excess, actorId, o.Id }, tx);
            }
            if (credit > 0)
                db.Execute("UPDATE dbo.book_customers SET credit = credit + @credit, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @customerId",
                    new { credit, actorId, customerId }, tx);
            var itemIds = take.Select(t => t.Key.ItemId).Distinct().OrderBy(i => i).ToList();
            var stock = db.Query<BookItem>("SELECT id, stock FROM dbo.book_items WITH (UPDLOCK, HOLDLOCK) WHERE id IN @itemIds ORDER BY id", new { itemIds }, tx).ToDictionary(i => i.Id, i => i.Stock);
            foreach (var g in take.GroupBy(t => t.Key.ItemId).OrderBy(g => g.Key))
            {
                var qty = g.Sum(t => t.Value);
                var after = stock[g.Key] + qty;
                BookRepository.Move(db, tx, g.Key, "return", qty, after, returnedOn, firstOrder, returnId,
                    "Return " + BookFormat.ReturnLabel(no) + " (" + string.Join(", ", g.Select(t => BookFormat.OrderLabel(t.Key.OrderNo)).Distinct()) + ")", actorId);
                stock[g.Key] = after;
            }
            AuditLog.Write(db, tx, actorId, "insert", "book_returns", returnId.ToString(),
                new { return_no = no, customer_id = customerId, value = totalValue, credit, refund, choice, lines = take.Select(t => new { order_no = t.Key.OrderNo, item = t.Key.ItemId, qty = t.Value }) });
            BookMessageRepository.ForReturn(db, tx, customerId, no, "return_received", totalValue, credit, refund);
            return no;
        }

        public static long RecordReturn(Guid customerId, IDictionary<Guid, int> quantities, string choice, DateTime returnedOn, string note, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var no = Record(db, tx, customerId, quantities, choice, returnedOn, note, actorId, null);
                tx.Commit();
                return no;
            }
        }

        /// <summary>The office pays back a pending refund (cash or bank account of the same kind).</summary>
        public static void PayRefund(long returnNo, string method, Guid? accountId, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var r = db.QuerySingleOrDefault<BookReturn>("SELECT id, return_no, customer_id, refund_amount, refund_paid_at FROM dbo.book_returns WITH (UPDLOCK, HOLDLOCK) WHERE return_no = @returnNo", new { returnNo }, tx);
                if (r == null) throw new BookOrderException("The return was not found.");
                if (r.RefundAmount <= 0 || r.RefundPaidAt != null) throw new BookOrderException("Nothing is waiting to be paid back on this return.");
                method = method == "bank" ? "bank" : "cash";
                var account = db.QuerySingleOrDefault<BookAccount>("SELECT id, name, kind, is_active FROM dbo.book_accounts WHERE id = @accountId", new { accountId }, tx);
                if (account == null || !account.IsActive) throw new BookOrderException("Please choose the account the money is paid from.");
                if (account.Kind != method) throw new BookOrderException("The account \"" + account.Name + "\" is a " + account.Kind + " account; please choose a " + method + " account.");
                db.Execute(@"UPDATE dbo.book_returns SET refund_method = @method, refund_account_id = @accountId, refund_paid_at = SYSDATETIMEOFFSET(), refund_paid_by = @actorId
                             WHERE id = @Id", new { method, accountId, actorId, r.Id }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_returns", r.Id.ToString(), new { return_no = returnNo, refund_paid = r.RefundAmount, method, account_id = accountId });
                BookMessageRepository.ForReturn(db, tx, r.CustomerId, returnNo, "refund_paid", 0, 0, r.RefundAmount, method == "bank" ? "bank transfer" : "cash");
                tx.Commit();
            }
        }

        private const string ReturnColumns = @"r.id, r.return_no, r.order_id, ro.order_no, r.customer_id, c.customer_no, c.name AS customer_name, c.mobile AS customer_mobile,
            r.amount, r.refund_amount, r.credit_amount, r.refund_method, r.refund_paid_at, a.name AS account_name, r.refunded_on, r.note, u.full_name AS created_by_name, r.created_at,
            (SELECT STRING_AGG(x.label, N', ') FROM (SELECT DISTINCT N'BK-' + CAST(o2.order_no AS NVARCHAR(20)) AS label FROM dbo.book_return_lines rl2
                JOIN dbo.book_order_lines l2 ON l2.id = rl2.order_line_id JOIN dbo.book_orders o2 ON o2.id = l2.order_id WHERE rl2.return_id = r.id) x) AS order_labels";

        private const string ReturnFrom = @" FROM dbo.book_returns r
            JOIN dbo.book_customers c ON c.id = r.customer_id
            LEFT JOIN dbo.book_orders ro ON ro.id = r.order_id
            LEFT JOIN dbo.book_accounts a ON a.id = r.refund_account_id
            LEFT JOIN dbo.users u ON u.id = r.created_by";

        /// <summary>Returns that include books from this order.</summary>
        public static IList<BookReturn> ForOrder(Guid orderId)
        {
            using (var db = Db.Open())
            {
                var list = db.Query<BookReturn>("SELECT " + ReturnColumns + ReturnFrom + @" WHERE EXISTS (SELECT 1 FROM dbo.book_return_lines rl JOIN dbo.book_order_lines l ON l.id = rl.order_line_id
                                                   WHERE rl.return_id = r.id AND l.order_id = @orderId) ORDER BY r.created_at", new { orderId }).ToList();
                foreach (var r in list) r.Lines = Lines(db, r.Id);
                return list;
            }
        }

        public static IList<BookReturn> ForCustomer(Guid customerId)
        {
            using (var db = Db.Open())
            {
                var list = db.Query<BookReturn>("SELECT " + ReturnColumns + ReturnFrom + " WHERE r.customer_id = @customerId ORDER BY r.created_at DESC", new { customerId }).ToList();
                foreach (var r in list) r.Lines = Lines(db, r.Id);
                return list;
            }
        }

        public static IList<BookReturn> PendingRefunds()
        {
            using (var db = Db.Open())
                return db.Query<BookReturn>("SELECT " + ReturnColumns + ReturnFrom + " WHERE r.refund_amount > 0 AND r.refund_paid_at IS NULL ORDER BY r.created_at").ToList();
        }

        public static BookReturn Get(long returnNo)
        {
            using (var db = Db.Open())
            {
                var r = db.QuerySingleOrDefault<BookReturn>("SELECT " + ReturnColumns + ReturnFrom + " WHERE r.return_no = @returnNo", new { returnNo });
                if (r != null) r.Lines = Lines(db, r.Id);
                return r;
            }
        }

        private static IList<BookReturnLine> Lines(IDbConnection db, Guid returnId)
        {
            return db.Query<BookReturnLine>(@"SELECT o.order_no, l.class_name, l.item_name, rl.quantity, rl.unit_price, rl.net_amount
                                              FROM dbo.book_return_lines rl JOIN dbo.book_order_lines l ON l.id = rl.order_line_id JOIN dbo.book_orders o ON o.id = l.order_id
                                              WHERE rl.return_id = @returnId ORDER BY o.order_no DESC, l.class_name, l.item_name", new { returnId }).ToList();
        }
    }
}
