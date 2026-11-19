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
    /// - The return value is the price charged. The refund is what the customer has paid beyond the new
    ///   total: refund = max(0, value − balance before the return). It is recorded with method, account,
    ///   date and the signed-in user. A refund needs a cash or bank account of the same kind.
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
                    SELECT id, order_no, status, subtotal, discount_total, credit_applied, paid_total, returned_total, refunded_total, stock_taken_at
                    FROM dbo.book_orders WITH (UPDLOCK, HOLDLOCK) WHERE order_no = @orderNo", new { orderNo }, tx);
                if (o == null) throw new BookOrderException("The order was not found.");
                if (o.StockTakenAt == null || o.Status == "cancelled")
                    throw new BookOrderException("Books can be returned only after they were handed over (paid, delivered or collected).");
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

                var value = wanted.Sum(w => lines[w.Key].UnitPrice * w.Value);
                var refund = Math.Max(0m, value - o.Balance);
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
                    new { returnNo, orderId = o.Id, value, refund, method = refund > 0 ? method : null, accountId = refund > 0 ? accountId : null, on = refundedOn.Date, note, actorId }, tx);

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
                                    updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @Id",
                    new { value, refund, actorId, o.Id }, tx);
                AuditLog.Write(db, tx, actorId, "insert", "book_returns", returnId.ToString(),
                    new { return_no = returnNo, order_no = orderNo, value, refund, method, account_id = accountId, lines = wanted });
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
