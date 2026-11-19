using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Returns of books (part 3; owner's decision: the money is paid back at once, not kept as credit).
    /// - Only books that were handed over (stock taken) can be returned, and never more than were taken
    ///   and not yet returned.
    /// - The returned books go back into stock (book_stock_moves kind "return").
    /// - The return value (book_returns.amount, added to returned_total) is the price charged less the
    ///   order's discount share on these books (discount × value ÷ subtotal), so a discount is never paid out
    ///   as cash. When the last books of an order come back, the value is what remains of the order after
    ///   discount, so a full return always ends at a balance of 0.
    /// - What is due back = max(0, value − balance before the return). It is paid in cash or bank up to the
    ///   money actually received (paid − already refunded); anything beyond that was the customer's own credit
    ///   and goes back to their credit. The cash refund needs a cash or bank account of the same kind.
    /// - The return date cannot be in the future or before the books were handed over.
    /// </summary>
    public static class BookReturnRepository
    {
        public static long RecordReturn(long orderNo, IDictionary<Guid, int> quantities, string method, Guid? accountId, DateTime refundedOn, string note, Guid actorId)
        {
            var wanted = (quantities ?? new Dictionary<Guid, int>()).Where(q => q.Value > 0).ToDictionary(q => q.Key, q => q.Value);
            if (wanted.Count == 0) throw new BookOrderException("Please enter how many of each book come back.");
            if (wanted.Values.Any(v => v > BookRepository.MaxQuantityPerBook)) throw new BookOrderException("Please check the quantities.");
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var o = db.QuerySingleOrDefault<BookOrder>(@"
                    SELECT id, order_no, customer_id, status, subtotal, discount_total, credit_applied, paid_total, returned_total, refunded_total, stock_taken_at
                    FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE order_no = @orderNo", new { orderNo }, tx);
                if (o == null) throw new BookOrderException("The order was not found.");
                if (o.StockTakenAt == null || o.Status == "cancelled")
                    throw new BookOrderException("Books can be returned only after they were handed over (paid, delivered or collected).");
                var handedOver = o.StockTakenAt.Value.ToOffset(TimeSpan.FromHours(5)).Date;
                var todayPk = DateTime.UtcNow.AddHours(5).Date;
                if (refundedOn.Date > todayPk) throw new BookOrderException("The return date cannot be in the future.");
                if (refundedOn.Date < handedOver) throw new BookOrderException("The return date cannot be before the books were handed over (" + handedOver.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture) + ").");
                // Lock order: order, then customer, then books by id.
                db.Execute("SELECT id FROM dbo.book_customers WITH (UPDLOCK, HOLDLOCK) WHERE id = @CustomerId", new { o.CustomerId }, tx);
                var lines = db.Query<BookOrderLine>("SELECT id, item_id, class_name, item_name, unit_price, quantity, returned_qty FROM dbo.book_order_lines WHERE order_id = @Id",
                    new { o.Id }, tx).ToDictionary(l => l.Id);
                var problems = new List<string>();
                foreach (var w in wanted)
                {
                    BookOrderLine l;
                    if (!lines.TryGetValue(w.Key, out l)) { problems.Add("A book is not part of this order."); continue; }
                    if (w.Value > l.Quantity - l.ReturnedQty)
                        problems.Add(l.ClassName + " – " + l.ItemName + ": at most " + (l.Quantity - l.ReturnedQty) + " can come back.");
                }
                if (problems.Count > 0) throw new BookOrderException(string.Join(" ", problems.Distinct()));

                var gross = wanted.Sum(w => lines[w.Key].UnitPrice * w.Value);
                var remaining = Math.Max(0m, o.Subtotal - o.ReturnedTotal - o.DiscountTotal);
                var allBack = lines.Values.All(l => l.Quantity - l.ReturnedQty - (wanted.ContainsKey(l.Id) ? wanted[l.Id] : 0) == 0);
                var value = allBack ? remaining
                    : o.Subtotal <= 0 ? 0m
                    : Math.Min(remaining, Math.Round(gross * (o.Subtotal - o.DiscountTotal) / o.Subtotal, 2, MidpointRounding.AwayFromZero));
                value = Math.Max(0m, value);
                var due = Math.Max(0m, value - o.Balance);
                var refund = Math.Min(due, Math.Max(0m, o.PaidTotal - o.RefundedTotal));
                var creditBack = Math.Min(due - refund, o.CreditApplied);
                if (refund > 0)
                {
                    if (method != "cash" && method != "bank") throw new BookOrderException("Please choose how the money is paid back (cash or bank).");
                    var account = db.QuerySingleOrDefault<BookAccount>("SELECT id, name, kind, is_active FROM dbo.book_accounts WHERE id = @accountId", new { accountId }, tx);
                    if (account == null || !account.IsActive) throw new BookOrderException("Please choose the account the money is paid back from.");
                    if (account.Kind != method) throw new BookOrderException("The account \"" + account.Name + "\" is a " + account.Kind + " account; please choose a " + method + " account.");
                }

                var returnNo = db.ExecuteScalar<long>("SELECT NEXT VALUE FOR dbo.book_return_no_seq", null, tx);
                var returnId = db.ExecuteScalar<Guid>(@"
                    INSERT INTO dbo.book_returns (return_no, order_id, amount, refund_amount, refund_method, refund_account_id, refunded_on, note, created_by)
                    OUTPUT inserted.id
                    VALUES (@returnNo, @orderId, @value, @refund, @method, @accountId, @on, @note, @actorId)",
                    new { returnNo, orderId = o.Id, value, refund, method = refund > 0 ? method : null, accountId = refund > 0 ? accountId : null, on = refundedOn.Date,
                          note = creditBack > 0 ? ((note == null ? "" : note + " ") + "(" + BookFormat.Money(creditBack) + " back to the customer's credit)") : note, actorId }, tx);

                // Books back into stock, in book-id order (the same lock order as everywhere else).
                var itemIds = wanted.Keys.Select(k => lines[k].ItemId).Distinct().OrderBy(i => i).ToList();
                var stock = db.Query<BookItem>("SELECT id, stock FROM dbo.book_items WITH (UPDLOCK, HOLDLOCK) WHERE id IN @itemIds ORDER BY id", new { itemIds }, tx)
                              .ToDictionary(i => i.Id, i => i.Stock);
                foreach (var w in wanted.OrderBy(w => lines[w.Key].ItemId))
                {
                    var l = lines[w.Key];
                    db.Execute("INSERT INTO dbo.book_return_lines (return_id, order_line_id, quantity, unit_price) VALUES (@returnId, @lineId, @qty, @UnitPrice)",
                        new { returnId, lineId = l.Id, qty = w.Value, l.UnitPrice }, tx);
                    db.Execute("UPDATE dbo.book_order_lines SET returned_qty = returned_qty + @qty WHERE id = @Id", new { qty = w.Value, l.Id }, tx);
                    var after = stock[l.ItemId] + w.Value;
                    BookRepository.Move(db, tx, l.ItemId, "return", w.Value, after, refundedOn, o.Id, returnId, "Return " + "RN-" + returnNo + " of order " + BookFormat.OrderLabel(orderNo), actorId);
                    stock[l.ItemId] = after;
                }
                db.Execute(@"UPDATE dbo.book_orders SET returned_total = returned_total + @value, refunded_total = refunded_total + @refund,
                                    credit_applied = credit_applied - @creditBack, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @Id",
                    new { value, refund, creditBack, actorId, o.Id }, tx);
                if (creditBack > 0)
                    db.Execute("UPDATE dbo.book_customers SET credit = credit + @creditBack, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @CustomerId",
                        new { creditBack, actorId, o.CustomerId }, tx);
                AuditLog.Write(db, tx, actorId, "insert", "book_returns", returnId.ToString(),
                    new { return_no = returnNo, order_no = orderNo, gross, value, refund, credit_back = creditBack,
                          method = refund > 0 ? method : null, account_id = refund > 0 ? accountId : null, lines = wanted });
                tx.Commit();
                return returnNo;
            }
        }

        private const string ReturnColumns = @"r.id, r.return_no, r.order_id, o.order_no, c.name AS customer_name, c.mobile AS customer_mobile,
            r.amount, r.refund_amount, r.refund_method, a.name AS account_name, r.refunded_on, r.note, u.full_name AS created_by_name, r.created_at";

        private const string ReturnFrom = @" FROM dbo.book_returns r
            JOIN dbo.book_orders o ON o.id = r.order_id
            JOIN dbo.book_customers c ON c.id = o.customer_id
            LEFT JOIN dbo.book_accounts a ON a.id = r.refund_account_id
            LEFT JOIN dbo.users u ON u.id = r.created_by";

        public static IList<BookReturn> ForOrder(Guid orderId)
        {
            using (var db = Db.Open())
            {
                var list = db.Query<BookReturn>("SELECT " + ReturnColumns + ReturnFrom + " WHERE r.order_id = @orderId ORDER BY r.created_at", new { orderId }).ToList();
                foreach (var r in list) r.Lines = Lines(db, r.Id);
                return list;
            }
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

        private static IList<BookReturnLine> Lines(System.Data.IDbConnection db, Guid returnId)
        {
            return db.Query<BookReturnLine>(@"SELECT l.class_name, l.item_name, rl.quantity, rl.unit_price
                                              FROM dbo.book_return_lines rl JOIN dbo.book_order_lines l ON l.id = rl.order_line_id
                                              WHERE rl.return_id = @returnId", new { returnId }).ToList();
        }
    }
}
