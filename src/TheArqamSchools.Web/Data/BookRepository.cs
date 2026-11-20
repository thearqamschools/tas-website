using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>Thrown when an order cannot be placed or changed; the message is shown to the user.</summary>
    public class BookOrderException : Exception
    {
        public BookOrderException(string message) : base(message) { }
    }

    /// <summary>
    /// The books system (Database/21): books and stock, customers, and orders. Money and stock rules:
    /// - prices are taken from the book when the order is placed or edited, and kept on the order line;
    /// - an order may not ask for more than the stock in hand (checked again under a lock when placed);
    /// - stock is taken off at the first payment (see payments, part 2), never below zero;
    /// - customer credit is used up automatically on the next order;
    /// - every change is written to the audit log (and stock changes to book_stock_moves) in the same transaction.
    /// </summary>
    public static class BookRepository
    {
        public const int MaxQuantityPerBook = 9999;

        // ---- Classes ------------------------------------------------------------------

        private static readonly string[] ClassOrder =
        {
            "play group", "nursery", "prep", "kg", "class 1", "class 2", "class 3", "class 4", "class 5", "class 6",
            "class 7", "class 8", "class 9", "class 10", "matric", "hifz", "hifz class", "all classes"
        };

        /// <summary>Sort key so classes appear Play Group, Nursery, Prep, Class 1 … Class 10, Matric, then others A–Z.</summary>
        public static int ClassRank(string className)
        {
            var i = Array.IndexOf(ClassOrder, (className ?? "").Trim().ToLowerInvariant());
            return i < 0 ? 100 : i;
        }

        public static IList<BookItem> Sort(IEnumerable<BookItem> items)
        {
            return items.OrderBy(i => ClassRank(i.ClassName)).ThenBy(i => i.ClassName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(i => i.SortOrder).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ---- Books and stock ----------------------------------------------------------

        public static IList<BookItem> Items(bool includeHidden)
        {
            using (var db = Db.Open())
                return Sort(db.Query<BookItem>(
                    "SELECT id, class_name, name, stock, purchase_price, sale_price, is_active, sort_order, updated_at, vendor, retail_price, discount_percent, reorder_level, category FROM dbo.book_items " +
                    "WHERE archived_at IS NULL" + (includeHidden ? "" : " AND is_active = 1")));
        }

        /// <summary>Sets one book's minimum stock level (null = the default).</summary>
        public static void SetReorderLevel(Guid id, int? level, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var before = db.ExecuteScalar<int?>("SELECT reorder_level FROM dbo.book_items WITH (UPDLOCK) WHERE id = @id", new { id }, tx);
                db.Execute("UPDATE dbo.book_items SET reorder_level = @level, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @id AND archived_at IS NULL",
                    new { id, level, actorId }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_items", id.ToString(), new { reorder_level = new { before, after = level } });
                tx.Commit();
            }
        }

        /// <summary>Books that have had at least one stock entry; a book never stocked is not "running low".</summary>
        public static HashSet<Guid> StockedItemIds()
        {
            using (var db = Db.Open())
                return new HashSet<Guid>(db.Query<Guid>("SELECT DISTINCT item_id FROM dbo.book_stock_moves"));
        }

        /// <summary>What a customer may order: active books with stock.</summary>
        public static IList<BookItem> OrderableItems()
        {
            return Items(false).Where(i => i.Stock > 0).ToList();
        }

        public static BookItem Item(Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<BookItem>(
                    "SELECT id, class_name, name, stock, purchase_price, sale_price, is_active, sort_order, updated_at, vendor, retail_price, discount_percent, reorder_level, category FROM dbo.book_items WHERE id = @id AND archived_at IS NULL", new { id });
        }

        public static IList<string> Classes()
        {
            using (var db = Db.Open())
                return db.Query<string>("SELECT DISTINCT class_name FROM dbo.book_items WHERE archived_at IS NULL").ToList()
                         .OrderBy(ClassRank).ThenBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static bool ItemNameTaken(string className, string name, Guid exceptId)
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM dbo.book_items WHERE class_name = @className AND name = @name AND archived_at IS NULL AND id <> @exceptId",
                    new { className, name, exceptId }) > 0;
        }

        public static Guid SaveItem(BookItemForm f, bool isNew, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                object before = null;
                if (isNew)
                    f.Id = db.ExecuteScalar<Guid>(@"
                        INSERT INTO dbo.book_items (class_name, name, purchase_price, sale_price, is_active, sort_order, vendor, retail_price, discount_percent, category, created_by, updated_by)
                        OUTPUT inserted.id
                        VALUES (@ClassName, @Name, @PurchasePrice, @SalePrice, @IsActive, @SortOrder, @Vendor, @RetailPrice, @DiscountPercent, @Category, @actorId, @actorId)",
                        new { f.ClassName, f.Name, f.PurchasePrice, f.SalePrice, f.IsActive, f.SortOrder, f.Vendor, f.RetailPrice, f.DiscountPercent, f.Category, actorId }, tx);
                else
                {
                    before = db.QuerySingleOrDefault(
                        "SELECT class_name, name, purchase_price, sale_price, is_active, sort_order, vendor, retail_price, discount_percent, category FROM dbo.book_items WITH (UPDLOCK) WHERE id = @Id", new { f.Id }, tx);
                    db.Execute(@"
                        UPDATE dbo.book_items SET class_name = @ClassName, name = @Name, purchase_price = @PurchasePrice, sale_price = @SalePrice,
                               is_active = @IsActive, sort_order = @SortOrder, vendor = @Vendor, retail_price = @RetailPrice, discount_percent = @DiscountPercent, category = @Category,
                               updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                        WHERE id = @Id AND archived_at IS NULL",
                        new { f.Id, f.ClassName, f.Name, f.PurchasePrice, f.SalePrice, f.IsActive, f.SortOrder, f.Vendor, f.RetailPrice, f.DiscountPercent, f.Category, actorId }, tx);
                }
                AuditLog.Write(db, tx, actorId, isNew ? "insert" : "update", "book_items", f.Id.ToString(),
                    new { before, after = new { f.ClassName, f.Name, f.PurchasePrice, f.SalePrice, f.IsActive, f.SortOrder, f.Vendor, f.RetailPrice, f.DiscountPercent, f.Category } });
                tx.Commit();
                return f.Id;
            }
        }

        /// <summary>
        /// Adds (purchase) or corrects stock. A correction may be negative, but stock never goes below zero.
        /// Returns the new stock, or throws <see cref="BookOrderException"/> with a message for the user.
        /// </summary>
        public static int ChangeStock(Guid itemId, string kind, int change, DateTime movedOn, string note, Guid actorId)
        {
            if (kind != "purchase" && kind != "correction") throw new ArgumentException("kind");
            if (change == 0) throw new BookOrderException("Please enter a number of books other than 0.");
            if (kind == "purchase" && change < 0) throw new BookOrderException("A purchase adds books; please enter a positive number.");
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var stock = db.QuerySingleOrDefault<int?>("SELECT stock FROM dbo.book_items WITH (UPDLOCK, HOLDLOCK) WHERE id = @itemId AND archived_at IS NULL", new { itemId }, tx);
                if (stock == null) throw new BookOrderException("The book was not found.");
                var after = stock.Value + change;
                if (after < 0) throw new BookOrderException("Stock cannot go below zero. There are only " + stock.Value + " in stock.");
                Move(db, tx, itemId, kind, change, after, movedOn, null, null, note, actorId);
                AuditLog.Write(db, tx, actorId, "update", "book_items", itemId.ToString(), new { stock_change = change, kind, stock_before = stock.Value, stock_after = after, note });
                tx.Commit();
                return after;
            }
        }

        /// <summary>Writes one stock change: updates book_items.stock and adds the history row. Callers hold a lock on the book.</summary>
        internal static void Move(IDbConnection db, IDbTransaction tx, Guid itemId, string kind, int change, int after, DateTime movedOn,
                                  Guid? orderId, Guid? returnId, string note, Guid? actorId)
        {
            db.Execute("UPDATE dbo.book_items SET stock = @after, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @itemId",
                new { after, actorId, itemId }, tx);
            db.Execute(@"INSERT INTO dbo.book_stock_moves (item_id, kind, change, stock_after, moved_on, order_id, return_id, note, created_by)
                         VALUES (@itemId, @kind, @change, @after, @movedOn, @orderId, @returnId, @note, @actorId)",
                new { itemId, kind, change, after, movedOn = movedOn.Date, orderId, returnId, note, actorId }, tx);
        }

        public static IList<BookStockMove> Moves(Guid itemId)
        {
            using (var db = Db.Open())
                return db.Query<BookStockMove>(@"
                    SELECT m.kind, m.change, m.stock_after, m.moved_on, m.note, o.order_no, u.full_name AS user_name, m.created_at
                    FROM dbo.book_stock_moves m
                    LEFT JOIN dbo.book_orders o ON o.id = m.order_id
                    LEFT JOIN dbo.users u ON u.id = m.created_by
                    WHERE m.item_id = @itemId ORDER BY m.created_at DESC", new { itemId }).ToList();
        }

        // ---- Customers ----------------------------------------------------------------

        private const string CustomerColumns = @"c.id, c.mobile, c.name, c.campus_name, c.address, c.credit, c.is_active,
            (SELECT COUNT(*) FROM dbo.book_orders o WHERE o.customer_id = c.id) AS order_count";

        public static IList<BookCustomer> Customers(string q)
        {
            using (var db = Db.Open())
                return db.Query<BookCustomer>(
                    "SELECT TOP (500) " + CustomerColumns + " FROM dbo.book_customers c WHERE c.archived_at IS NULL" +
                    (string.IsNullOrEmpty(q) ? "" : " AND (c.mobile LIKE @like ESCAPE '\\' OR c.name LIKE @like ESCAPE '\\' OR c.campus_name LIKE @like ESCAPE '\\')") +
                    " ORDER BY c.name",
                    new { like = "%" + CampusRepository.EscapeLike(q ?? "") + "%" }).ToList();
        }

        public static int CustomerCount()
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.book_customers WHERE archived_at IS NULL");
        }

        public static BookCustomer Customer(Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<BookCustomer>("SELECT " + CustomerColumns + " FROM dbo.book_customers c WHERE c.id = @id AND c.archived_at IS NULL", new { id });
        }

        /// <summary>The active customer with this mobile number (stored format), or null.</summary>
        public static BookCustomer CustomerByMobile(string mobile)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<BookCustomer>(
                    "SELECT " + CustomerColumns + " FROM dbo.book_customers c WHERE c.mobile = @mobile AND c.archived_at IS NULL AND c.is_active = 1", new { mobile });
        }

        public static bool MobileTaken(string mobile, Guid exceptId)
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.book_customers WHERE mobile = @mobile AND id <> @exceptId", new { mobile, exceptId }) > 0;
        }

        public static Guid SaveCustomer(BookCustomerForm f, bool isNew, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                object before = null;
                if (isNew)
                    f.Id = db.ExecuteScalar<Guid>(@"
                        INSERT INTO dbo.book_customers (mobile, name, campus_name, address, is_active, created_by, updated_by)
                        OUTPUT inserted.id VALUES (@Mobile, @Name, @CampusName, @Address, @IsActive, @actorId, @actorId)",
                        new { f.Mobile, f.Name, f.CampusName, f.Address, f.IsActive, actorId }, tx);
                else
                {
                    before = db.QuerySingleOrDefault("SELECT mobile, name, campus_name, address, is_active FROM dbo.book_customers WITH (UPDLOCK) WHERE id = @Id", new { f.Id }, tx);
                    db.Execute(@"
                        UPDATE dbo.book_customers SET mobile = @Mobile, name = @Name, campus_name = @CampusName, address = @Address, is_active = @IsActive,
                               updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                        WHERE id = @Id AND archived_at IS NULL", new { f.Id, f.Mobile, f.Name, f.CampusName, f.Address, f.IsActive, actorId }, tx);
                }
                AuditLog.Write(db, tx, actorId, isNew ? "insert" : "update", "book_customers", f.Id.ToString(),
                    new { before, after = new { f.Mobile, f.Name, f.CampusName, f.Address, f.IsActive } });
                tx.Commit();
                return f.Id;
            }
        }

        // ---- Orders -------------------------------------------------------------------

        private const string OrderColumns = @"o.id, o.order_no, o.customer_id, c.name AS customer_name, c.mobile AS customer_mobile, c.campus_name,
            o.fulfilment, o.delivery_address, o.status, o.source, o.subtotal, o.discount_total, o.credit_applied, o.paid_total,
            o.returned_total, o.refunded_total, o.edit_count, o.stock_taken_at, o.created_at,
            (SELECT COUNT(*) FROM dbo.book_payment_submissions s WHERE s.order_id = o.id AND s.status <> N'rejected') AS sent_payments,
            (SELECT COUNT(*) FROM dbo.book_payment_submissions s WHERE s.order_id = o.id AND s.status = N'pending') AS pending_payments";

        public static IList<BookOrder> CustomerOrders(Guid customerId)
        {
            using (var db = Db.Open())
            {
                var orders = db.Query<BookOrder>("SELECT " + OrderColumns + @" FROM dbo.book_orders o JOIN dbo.book_customers c ON c.id = o.customer_id
                                                   WHERE o.customer_id = @customerId ORDER BY o.created_at DESC", new { customerId }).ToList();
                var lines = db.Query<BookOrderLine>(@"SELECT l.order_id AS id, l.item_id, l.class_name, l.item_name, l.unit_price, l.quantity, l.returned_qty
                                                      FROM dbo.book_order_lines l JOIN dbo.book_orders o ON o.id = l.order_id
                                                      WHERE o.customer_id = @customerId AND l.quantity > 0", new { customerId }).ToList();
                foreach (var o in orders) o.Lines = lines.Where(l => l.Id == o.Id).OrderBy(l => ClassRank(l.ClassName)).ThenBy(l => l.ItemName).ToList();
                return orders;
            }
        }

        public static BookOrder Order(long orderNo)
        {
            using (var db = Db.Open())
            {
                var o = db.QuerySingleOrDefault<BookOrder>("SELECT " + OrderColumns + " FROM dbo.book_orders o JOIN dbo.book_customers c ON c.id = o.customer_id WHERE o.order_no = @orderNo", new { orderNo });
                if (o == null) return null;
                o.Lines = Lines(db, null, o.Id);
                o.Edits = db.Query<BookOrderEdit>(@"SELECT e.changes, e.old_subtotal, e.new_subtotal, e.by_customer, u.full_name AS user_name, e.created_at
                                                    FROM dbo.book_order_edits e LEFT JOIN dbo.users u ON u.id = e.by_user
                                                    WHERE e.order_id = @Id ORDER BY e.created_at", new { o.Id }).ToList();
                return o;
            }
        }

        private static IList<BookOrderLine> Lines(IDbConnection db, IDbTransaction tx, Guid orderId)
        {
            return db.Query<BookOrderLine>(@"SELECT id, item_id, class_name, item_name, unit_price, quantity, returned_qty
                                             FROM dbo.book_order_lines WHERE order_id = @orderId AND quantity > 0", new { orderId }, tx)
                     .OrderBy(l => ClassRank(l.ClassName)).ThenBy(l => l.ClassName).ThenBy(l => l.ItemName).ToList();
        }

        /// <summary>
        /// Places a new order. Quantities above zero only; each book must be active and have enough stock.
        /// Customer credit is used automatically. Returns the order number.
        /// </summary>
        public static long PlaceOrder(Guid customerId, IDictionary<Guid, int> quantities, string fulfilment, bool byCustomer, Guid? actorId)
        {
            var wanted = Clean(quantities);
            if (wanted.Count == 0) throw new BookOrderException("Please choose at least one book.");
            fulfilment = fulfilment == "delivery" ? "delivery" : "pickup";
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                var customer = db.QuerySingleOrDefault<BookCustomer>(
                    "SELECT id, mobile, name, campus_name, address, credit, is_active FROM dbo.book_customers WITH (UPDLOCK, HOLDLOCK) WHERE id = @customerId AND archived_at IS NULL",
                    new { customerId }, tx);
                if (customer == null || !customer.IsActive) throw new BookOrderException("This customer cannot order at the moment. Please contact the office.");
                if (fulfilment == "delivery" && string.IsNullOrWhiteSpace(customer.Address))
                    throw new BookOrderException("There is no delivery address for this number. Please choose Pick up, or ask the office to add your address.");

                var items = LockItems(db, tx, wanted.Keys);
                CheckStock(items, wanted, null);
                var subtotal = wanted.Sum(w => items[w.Key].SalePrice * w.Value);
                var credit = Math.Min(customer.Credit, subtotal);

                var orderNo = db.ExecuteScalar<long>("SELECT NEXT VALUE FOR dbo.book_order_no_seq", null, tx);
                var orderId = db.ExecuteScalar<Guid>(@"
                    INSERT INTO dbo.book_orders (order_no, customer_id, fulfilment, delivery_address, status, source, subtotal, credit_applied, placed_by, updated_by)
                    OUTPUT inserted.id
                    VALUES (@orderNo, @customerId, @fulfilment, @address, N'new', @source, @subtotal, @credit, @actorId, @actorId)",
                    new { orderNo, customerId, fulfilment, address = fulfilment == "delivery" ? customer.Address : null, source = byCustomer ? "web" : "office", subtotal, credit, actorId }, tx);
                foreach (var w in wanted)
                {
                    var it = items[w.Key];
                    db.Execute(@"INSERT INTO dbo.book_order_lines (order_id, item_id, class_name, item_name, unit_price, purchase_price, quantity)
                                 VALUES (@orderId, @ItemId, @ClassName, @Name, @SalePrice, @PurchasePrice, @qty)",
                        new { orderId, ItemId = it.Id, it.ClassName, it.Name, it.SalePrice, it.PurchasePrice, qty = w.Value }, tx);
                }
                if (credit > 0)
                    db.Execute("UPDATE dbo.book_customers SET credit = credit - @credit, updated_at = SYSDATETIMEOFFSET() WHERE id = @customerId", new { credit, customerId }, tx);
                AuditLog.Write(db, tx, actorId, "insert", "book_orders", orderId.ToString(),
                    new { order_no = orderNo, customer_id = customerId, fulfilment, subtotal, credit_applied = credit, by_customer = byCustomer, lines = wanted });
                tx.Commit();
                return orderNo;
            }
        }

        /// <summary>
        /// The customer cancels their own order while it is still New (owner's rule). The order is kept with status
        /// Cancelled (never deleted), any credit it used goes back to the customer, and the cancellation is kept in
        /// book_order_edits like any other change. No stock was taken yet, so none is given back.
        /// </summary>
        public static void CancelByCustomer(long orderNo, Guid customerId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var o = db.QuerySingleOrDefault<BookOrder>(@"SELECT id, order_no, customer_id, status, subtotal, discount_total, credit_applied, paid_total, stock_taken_at,
                                                                    (SELECT COUNT(*) FROM dbo.book_payment_submissions s WHERE s.order_id = o.id AND s.status <> N'rejected') AS sent_payments
                                                             FROM dbo.book_orders o WITH (UPDLOCK, HOLDLOCK) WHERE order_no = @orderNo", new { orderNo }, tx);
                if (o == null || o.CustomerId != customerId) throw new BookOrderException("The order was not found.");
                if (!o.CustomerCanEdit) throw new BookOrderException("This order can no longer be cancelled online, because a payment has been sent or the office has confirmed it. Please contact the office.");
                db.Execute("SELECT id FROM dbo.book_customers WITH (UPDLOCK, HOLDLOCK) WHERE id = @CustomerId", new { o.CustomerId }, tx);
                db.Execute("UPDATE dbo.book_orders SET status = N'cancelled', credit_applied = 0, edit_count = edit_count + 1, updated_at = SYSDATETIMEOFFSET(), updated_by = NULL WHERE id = @Id", new { o.Id }, tx);
                if (o.CreditApplied > 0)
                    db.Execute("UPDATE dbo.book_customers SET credit = credit + @CreditApplied, updated_at = SYSDATETIMEOFFSET() WHERE id = @CustomerId", new { o.CreditApplied, o.CustomerId }, tx);
                var changes = JsonConvert.SerializeObject(new object[] { new { cancelled = true, credit_returned = o.CreditApplied } });
                db.Execute(@"INSERT INTO dbo.book_order_edits (order_id, changes, old_subtotal, new_subtotal, by_customer, by_user)
                             VALUES (@Id, @changes, @Subtotal, @Subtotal, 1, NULL)", new { o.Id, changes, o.Subtotal }, tx);
                AuditLog.Write(db, tx, null, "update", "book_orders", o.Id.ToString(), new { order_no = orderNo, status_from = o.Status, status_to = "cancelled", by_customer = true, credit_returned = o.CreditApplied });
                tx.Commit();
            }
        }

        /// <summary>
        /// Changes the books of an order that is not paid yet. Every change is recorded in book_order_edits.
        /// <paramref name="customerId"/> (when the customer edits) must own the order.
        /// </summary>
        public static void EditOrder(long orderNo, Guid? customerId, IDictionary<Guid, int> quantities, string fulfilment, Guid? actorId)
        {
            var wanted = Clean(quantities);
            if (wanted.Count == 0) throw new BookOrderException("Please keep at least one book in the order. To cancel the whole order, use Cancel this order on the order page.");
            fulfilment = fulfilment == "delivery" ? "delivery" : "pickup";
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                var o = db.QuerySingleOrDefault<BookOrder>(@"SELECT id, order_no, customer_id, fulfilment, status, subtotal, discount_total, credit_applied, paid_total, stock_taken_at,
                                                                    (SELECT COUNT(*) FROM dbo.book_payment_submissions s WHERE s.order_id = o.id AND s.status <> N'rejected') AS sent_payments
                                                             FROM dbo.book_orders o WITH (UPDLOCK, HOLDLOCK) WHERE order_no = @orderNo", new { orderNo }, tx);
                if (o == null || (customerId.HasValue && o.CustomerId != customerId.Value)) throw new BookOrderException("The order was not found.");
                if (customerId.HasValue ? !o.CustomerCanEdit : !o.OfficeCanEdit)
                    throw new BookOrderException("This order can no longer be changed, because a payment has been sent or the office has confirmed it. Please contact the office.");
                var customer = db.QuerySingle<BookCustomer>("SELECT id, address, credit FROM dbo.book_customers WITH (UPDLOCK, HOLDLOCK) WHERE id = @CustomerId", new { o.CustomerId }, tx);
                if (fulfilment == "delivery" && string.IsNullOrWhiteSpace(customer.Address))
                    throw new BookOrderException("There is no delivery address for this number. Please choose Pick up, or ask the office to add your address.");

                var oldLines = db.Query<BookOrderLine>("SELECT id, item_id, class_name, item_name, unit_price, quantity FROM dbo.book_order_lines WHERE order_id = @Id", new { o.Id }, tx).ToList();
                var oldQty = oldLines.ToDictionary(l => l.ItemId, l => l.Quantity);
                var items = LockItems(db, tx, wanted.Keys.Union(oldLines.Select(l => l.ItemId)));
                // Only added or increased books need stock; books already in the order may keep their quantity.
                CheckStock(items, wanted, oldQty);

                var changes = new List<object>();
                foreach (var id in wanted.Keys.Union(oldQty.Keys))
                {
                    int was, now;
                    oldQty.TryGetValue(id, out was);
                    wanted.TryGetValue(id, out now);
                    if (was == now) continue;
                    var it = items.ContainsKey(id) ? items[id] : null;
                    var line = oldLines.FirstOrDefault(l => l.ItemId == id);
                    changes.Add(new { book = (it != null ? it.ClassName + " \u2013 " + it.Name : line.ClassName + " \u2013 " + line.ItemName), from = was, to = now });
                    if (line == null)
                        db.Execute(@"INSERT INTO dbo.book_order_lines (order_id, item_id, class_name, item_name, unit_price, purchase_price, quantity)
                                     VALUES (@orderId, @ItemId, @ClassName, @Name, @SalePrice, @PurchasePrice, @now)",
                            new { orderId = o.Id, ItemId = it.Id, it.ClassName, it.Name, it.SalePrice, it.PurchasePrice, now }, tx);
                    else
                        db.Execute("UPDATE dbo.book_order_lines SET quantity = @now WHERE id = @Id", new { now, line.Id }, tx);
                }
                var fulfilmentChanged = fulfilment != o.Fulfilment;
                if (changes.Count == 0 && !fulfilmentChanged) { tx.Rollback(); return; }
                if (fulfilmentChanged) changes.Add(new { fulfilment = new { from = o.Fulfilment, to = fulfilment } });

                var newSubtotal = db.ExecuteScalar<decimal>("SELECT COALESCE(SUM(unit_price * quantity), 0) FROM dbo.book_order_lines WHERE order_id = @Id", new { o.Id }, tx);
                // Credit: give back what this order used, then use it again on the new total.
                var available = customer.Credit + o.CreditApplied;
                var credit = Math.Min(available, newSubtotal);
                db.Execute(@"UPDATE dbo.book_orders SET subtotal = @newSubtotal, credit_applied = @credit, fulfilment = @fulfilment,
                                    delivery_address = @address, edit_count = edit_count + 1, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                             WHERE id = @Id",
                    new { newSubtotal, credit, fulfilment, address = fulfilment == "delivery" ? customer.Address : null, actorId, o.Id }, tx);
                db.Execute("UPDATE dbo.book_customers SET credit = @rest, updated_at = SYSDATETIMEOFFSET() WHERE id = @CustomerId", new { rest = available - credit, o.CustomerId }, tx);
                db.Execute(@"INSERT INTO dbo.book_order_edits (order_id, changes, old_subtotal, new_subtotal, by_customer, by_user)
                             VALUES (@Id, @changes, @old, @newSubtotal, @byCustomer, @actorId)",
                    new { o.Id, changes = JsonConvert.SerializeObject(changes), old = o.Subtotal, newSubtotal, byCustomer = customerId.HasValue, actorId }, tx);
                AuditLog.Write(db, tx, actorId, "update", "book_orders", o.Id.ToString(), new { order_no = orderNo, edit = changes, old_subtotal = o.Subtotal, new_subtotal = newSubtotal, by_customer = customerId.HasValue });
                tx.Commit();
            }
        }

        private static Dictionary<Guid, int> Clean(IDictionary<Guid, int> quantities)
        {
            var result = new Dictionary<Guid, int>();
            if (quantities == null) return result;
            foreach (var q in quantities)
            {
                if (q.Value < 0 || q.Value > MaxQuantityPerBook) throw new BookOrderException("Please enter a quantity from 0 to " + MaxQuantityPerBook + ".");
                if (q.Value > 0) result[q.Key] = q.Value;
            }
            return result;
        }

        private static Dictionary<Guid, BookItem> LockItems(IDbConnection db, IDbTransaction tx, IEnumerable<Guid> ids)
        {
            var list = ids.Distinct().ToList();
            if (list.Count == 0) return new Dictionary<Guid, BookItem>();
            return db.Query<BookItem>(@"SELECT id, class_name, name, stock, purchase_price, sale_price, is_active, sort_order
                                        FROM dbo.book_items WITH (UPDLOCK, HOLDLOCK) WHERE id IN @list AND archived_at IS NULL ORDER BY id",
                                      new { list }, tx).ToDictionary(i => i.Id);
        }

        /// <summary>
        /// Each wanted book must exist and be active, and the quantity may not be more than the stock in hand.
        /// When editing, a quantity that is not raised is always allowed (the book is already in the order).
        /// </summary>
        private static void CheckStock(IDictionary<Guid, BookItem> items, IDictionary<Guid, int> wanted, IDictionary<Guid, int> already)
        {
            var problems = new List<string>();
            foreach (var w in wanted)
            {
                BookItem it;
                int had = 0;
                if (already != null) already.TryGetValue(w.Key, out had);
                if (!items.TryGetValue(w.Key, out it) || (!it.IsActive && w.Value > had))
                {
                    problems.Add("One of the books is no longer available. Please remove it from the order.");
                    continue;
                }
                if (w.Value > had && w.Value > it.Stock)
                    problems.Add(it.ClassName + " – " + it.Name + ": only " + it.Stock.ToString(CultureInfo.InvariantCulture) + " in stock.");
            }
            if (problems.Count > 0) throw new BookOrderException(string.Join(" ", problems.Distinct()));
        }
    }
}
