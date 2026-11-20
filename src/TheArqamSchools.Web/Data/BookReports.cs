using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Book reports (owner's specification, section 8). A sale counts on the day its books came off stock
    /// (first payment, or hand-over); payments on the date received; returns and refunds on the return date;
    /// discounts on the date of the payment that gave them. Dates are Pakistan time.
    /// </summary>
    public static class BookReports
    {
        public static readonly string[][] Kinds =
        {
            new[] { "stock", "Stock" }, new[] { "sales-books", "Sales by book and class" }, new[] { "sales-customers", "Sales by customer" },
            new[] { "payments", "Payments received" }, new[] { "unpaid", "Unpaid and part-paid orders" }, new[] { "discounts", "Discounts given" },
            new[] { "returns", "Returns" }, new[] { "profit", "Profit" },
            new[] { "aging", "Money owed by age (aging)" }, new[] { "campus", "Campus-wise summary" }
        };

        public static bool NeedsDates(string kind) { return kind != "stock" && kind != "unpaid" && kind != "aging"; }

        public static ReportTable Build(string kind, DateTime from, DateTime to)
        {
            var pk = TimeSpan.FromHours(5);
            var fromUtc = new DateTimeOffset(DateTime.SpecifyKind(from.Date, DateTimeKind.Unspecified), pk);
            var toUtc = new DateTimeOffset(DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Unspecified), pk);
            var period = from.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + " to " + to.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            using (var db = Db.Open())
            {
                switch (kind)
                {
                    case "stock": return Stock(db);
                    case "sales-books": return SalesByBook(db, fromUtc, toUtc, from, to, period);
                    case "sales-customers": return SalesByCustomer(db, fromUtc, toUtc, from, to, period);
                    case "payments": return Payments(db, from, to, period);
                    case "unpaid": return Unpaid(db);
                    case "discounts": return Discounts(db, from, to, period);
                    case "returns": return Returns(db, from, to, period);
                    case "profit": return Profit(db, fromUtc, toUtc, from, to, period);
                    case "aging": return Aging(db);
                    case "campus": return Campus(db, fromUtc, toUtc, from, to, period);
                    default: return null;
                }
            }
        }

        private class StockRow
        {
            public Guid Id { get; set; } public string ClassName { get; set; } public string Name { get; set; } public string Vendor { get; set; }
            public bool IsActive { get; set; } public int SortOrder { get; set; } public int Stock { get; set; }
            public decimal? RetailPrice { get; set; } public decimal? DiscountPercent { get; set; } public decimal SalePrice { get; set; }
            public int StockIn { get; set; } public int StockOut { get; set; } public int SoldQty { get; set; } public decimal SoldValue { get; set; }
        }

        /// <summary>
        /// Owner's layout (20 November 2026), like the old system's "Product Stock": class, book, vendor, shown, retail price,
        /// discount %, discounted (sale) price, then stock in, stock out and stock left (in - out = left, all movements),
        /// books sold (net of returns) and their value. No purchase prices.
        /// </summary>
        private static ReportTable Stock(System.Data.IDbConnection db)
        {
            var t = new ReportTable { Title = "Stock", Subtitle = "Stock on " + DateTime.UtcNow.AddHours(5).ToString("d MMMM yyyy", CultureInfo.InvariantCulture) }
                .Col("Class", "text", 0.9).Col("Book", "text", 2).Col("Vendor", "text", 1.3).Col("Shown", "text", 0.6)
                .Col("Retail price", "money", 0.8).Col("Disc. %", "number", 0.55).Col("Disc. price", "money", 0.8)
                .Col("Stock in", "number", 0.7).Col("Stock out", "number", 0.7).Col("Stock left", "number", 0.7)
                .Col("Sold", "number", 0.6).Col("Sold value", "money", 0.95);
            var rows = db.Query<StockRow>(@"
                SELECT i.id, i.class_name, i.name, i.vendor, i.is_active, i.sort_order, i.stock, i.retail_price, i.discount_percent, i.sale_price,
                       COALESCE((SELECT SUM(m.change) FROM dbo.book_stock_moves m WHERE m.item_id = i.id AND m.change > 0), 0) AS stock_in,
                       COALESCE((SELECT -SUM(m.change) FROM dbo.book_stock_moves m WHERE m.item_id = i.id AND m.change < 0), 0) AS stock_out,
                       COALESCE((SELECT SUM(l.quantity - l.returned_qty) FROM dbo.book_order_lines l JOIN dbo.book_orders o ON o.id = l.order_id
                                 WHERE l.item_id = i.id AND o.stock_taken_at IS NOT NULL AND o.status <> N'cancelled'), 0) AS sold_qty,
                       COALESCE((SELECT SUM((l.quantity - l.returned_qty) * l.unit_price) FROM dbo.book_order_lines l JOIN dbo.book_orders o ON o.id = l.order_id
                                 WHERE l.item_id = i.id AND o.stock_taken_at IS NOT NULL AND o.status <> N'cancelled'), 0) AS sold_value
                FROM dbo.book_items i WHERE i.archived_at IS NULL").ToList()
                .OrderBy(r => BookRepository.ClassRank(r.ClassName)).ThenBy(r => r.ClassName).ThenBy(r => r.SortOrder).ThenBy(r => r.Name).ToList();
            foreach (var r in rows)
                t.Rows.Add(new object[] { r.ClassName, r.Name, r.Vendor, r.IsActive ? "Yes" : "Hidden", r.RetailPrice, r.DiscountPercent, r.SalePrice,
                                          r.StockIn, r.StockOut, r.Stock, r.SoldQty, r.SoldValue });
            t.Totals = new object[] { "Total", null, null, null, null, null, null, rows.Sum(r => r.StockIn), rows.Sum(r => r.StockOut), rows.Sum(r => r.Stock),
                                      rows.Sum(r => r.SoldQty), rows.Sum(r => r.SoldValue) };
            t.Notes.Add("Stock in = books received, returned and added by correction. Stock out = books handed over and taken off by correction. Stock in - Stock out = Stock left.");
            t.Notes.Add("Sold = books handed over to customers less books returned. Disc. price is the price customers pay.");
            return t;
        }

        private class SoldRow { public Guid ItemId { get; set; } public string ClassName { get; set; } public string ItemName { get; set; } public int Qty { get; set; } public decimal Sales { get; set; } public decimal Cost { get; set; } }

        private static IList<SoldRow> Sold(System.Data.IDbConnection db, DateTimeOffset fromUtc, DateTimeOffset toUtc)
        {
            return db.Query<SoldRow>(@"
                SELECT l.item_id, l.class_name, l.item_name, SUM(l.quantity) AS qty, SUM(l.quantity * l.unit_price) AS sales, SUM(l.quantity * l.purchase_price) AS cost
                FROM dbo.book_order_lines l JOIN dbo.book_orders o ON o.id = l.order_id
                WHERE o.stock_taken_at >= @fromUtc AND o.stock_taken_at < @toUtc AND o.status <> N'cancelled' AND l.quantity > 0
                GROUP BY l.item_id, l.class_name, l.item_name", new { fromUtc, toUtc }).ToList();
        }

        private static IList<SoldRow> Returned(System.Data.IDbConnection db, DateTime from, DateTime to)
        {
            return db.Query<SoldRow>(@"
                SELECT l.item_id, l.class_name, l.item_name, SUM(rl.quantity) AS qty, SUM(rl.quantity * rl.unit_price) AS sales, SUM(rl.quantity * l.purchase_price) AS cost
                FROM dbo.book_return_lines rl JOIN dbo.book_returns r ON r.id = rl.return_id JOIN dbo.book_order_lines l ON l.id = rl.order_line_id
                WHERE r.refunded_on >= @from AND r.refunded_on <= @to
                GROUP BY l.item_id, l.class_name, l.item_name", new { from = from.Date, to = to.Date }).ToList();
        }

        private static ReportTable SalesByBook(System.Data.IDbConnection db, DateTimeOffset fromUtc, DateTimeOffset toUtc, DateTime from, DateTime to, string period)
        {
            var t = new ReportTable { Title = "Sales by book and class", Subtitle = period }
                .Col("Class", "text", 1).Col("Book", "text", 2.2).Col("Sold", "number", 0.7).Col("Returned", "number", 0.8).Col("Net copies", "number", 0.8)
                .Col("Sales value", "money", 1.1).Col("Returned value", "money", 1.1).Col("Net sales", "money", 1.1);
            var sold = Sold(db, fromUtc, toUtc);
            var ret = Returned(db, from, to);
            var keys = sold.Select(s => s.ItemId).Union(ret.Select(r => r.ItemId)).ToList();
            var rows = keys.Select(k =>
            {
                var s = sold.FirstOrDefault(x => x.ItemId == k);
                var r = ret.FirstOrDefault(x => x.ItemId == k);
                var any = s ?? r;
                return new { any.ClassName, any.ItemName, Sold = s == null ? 0 : s.Qty, Ret = r == null ? 0 : r.Qty, Sales = s == null ? 0 : s.Sales, RetVal = r == null ? 0 : r.Sales };
            }).OrderBy(x => BookRepository.ClassRank(x.ClassName)).ThenBy(x => x.ClassName).ThenBy(x => x.ItemName).ToList();
            foreach (var x in rows) t.Rows.Add(new object[] { x.ClassName, x.ItemName, x.Sold, x.Ret, x.Sold - x.Ret, x.Sales, x.RetVal, x.Sales - x.RetVal });
            t.Totals = new object[] { "Total", null, rows.Sum(x => x.Sold), rows.Sum(x => x.Ret), rows.Sum(x => x.Sold - x.Ret), rows.Sum(x => x.Sales), rows.Sum(x => x.RetVal), rows.Sum(x => x.Sales - x.RetVal) };
            t.Notes.Add("A sale counts on the day its books came off stock (first payment or hand-over). Returns count on the return date.");
            return t;
        }

        private static ReportTable SalesByCustomer(System.Data.IDbConnection db, DateTimeOffset fromUtc, DateTimeOffset toUtc, DateTime from, DateTime to, string period)
        {
            var t = new ReportTable { Title = "Sales by customer", Subtitle = period }
                .Col("Customer", "text", 1.8).Col("Mobile", "text", 1).Col("Campus", "text", 1.4).Col("Orders", "number", 0.6)
                .Col("Sales value", "money", 1.1).Col("Returned value", "money", 1.1).Col("Net sales", "money", 1.1);
            var rows = db.Query(@"
                SELECT c.id, c.name, c.mobile, c.campus_name,
                       (SELECT COUNT(*) FROM dbo.book_orders o WHERE o.customer_id = c.id AND o.status <> N'cancelled' AND o.stock_taken_at >= @fromUtc AND o.stock_taken_at < @toUtc) AS orders,
                       (SELECT COALESCE(SUM(o.subtotal), 0) FROM dbo.book_orders o WHERE o.customer_id = c.id AND o.status <> N'cancelled' AND o.stock_taken_at >= @fromUtc AND o.stock_taken_at < @toUtc) AS sales,
                       (SELECT COALESCE(SUM(rl.quantity * rl.unit_price), 0) FROM dbo.book_return_lines rl JOIN dbo.book_returns r ON r.id = rl.return_id
                        WHERE r.customer_id = c.id AND r.refunded_on >= @from AND r.refunded_on <= @to) AS returned
                FROM dbo.book_customers c", new { fromUtc, toUtc, from = from.Date, to = to.Date }).ToList()
                .Where(r => (int)r.orders > 0 || (decimal)r.returned > 0).OrderByDescending(r => (decimal)r.sales).ToList();
            foreach (var r in rows) t.Rows.Add(new object[] { (string)r.name, Helpers.Mobile.Format((string)r.mobile), (string)r.campus_name, (int)r.orders, (decimal)r.sales, (decimal)r.returned, (decimal)r.sales - (decimal)r.returned });
            t.Totals = new object[] { "Total", null, null, rows.Sum(r => (int)r.orders), rows.Sum(r => (decimal)r.sales), rows.Sum(r => (decimal)r.returned), rows.Sum(r => (decimal)r.sales - (decimal)r.returned) };
            return t;
        }

        private static ReportTable Payments(System.Data.IDbConnection db, DateTime from, DateTime to, string period)
        {
            var t = new ReportTable { Title = "Payments received", Subtitle = period }
                .Col("Date", "text", 0.9).Col("Order", "text", 0.8).Col("Customer", "text", 1.6).Col("Method", "text", 0.6).Col("Account", "text", 1.2)
                .Col("Received by", "text", 1.2).Col("Amount", "money", 1);
            var rows = db.Query(@"
                SELECT p.received_on, o.order_no, c.name, p.method, a.name AS account, u.full_name AS by_name, p.amount
                FROM dbo.book_payments p JOIN dbo.book_orders o ON o.id = p.order_id JOIN dbo.book_customers c ON c.id = o.customer_id
                LEFT JOIN dbo.book_accounts a ON a.id = p.account_id LEFT JOIN dbo.users u ON u.id = p.received_by
                WHERE p.amount > 0 AND p.received_on >= @from AND p.received_on <= @to ORDER BY p.received_on, p.created_at",
                new { from = from.Date, to = to.Date }).ToList();
            foreach (var r in rows)
                t.Rows.Add(new object[] { (DateTime)r.received_on, BookFormat.OrderLabel((long)r.order_no), (string)r.name, (string)r.method == "bank" ? "Bank" : "Cash", (string)r.account, (string)r.by_name, (decimal)r.amount });
            t.Totals = new object[] { "Total", null, null, null, null, null, rows.Sum(r => (decimal)r.amount) };
            var paidBack = db.Query(@"SELECT a.name AS account, SUM(r.refund_amount) AS refunded FROM dbo.book_returns r JOIN dbo.book_accounts a ON a.id = r.refund_account_id
                                      WHERE r.refund_amount > 0 AND r.refunded_on >= @from AND r.refunded_on <= @to GROUP BY a.name", new { from = from.Date, to = to.Date })
                              .ToDictionary(r => (string)r.account, r => (decimal)r.refunded);
            foreach (var name in rows.Select(r => (string)r.account).Union(paidBack.Keys).Distinct())
            {
                var received = rows.Where(r => (string)r.account == name).Sum(r => (decimal)r.amount);
                decimal back;
                paidBack.TryGetValue(name, out back);
                t.Notes.Add("Account " + name + ": received Rs. " + received.ToString("#,##0.##", CultureInfo.InvariantCulture)
                    + (back > 0 ? ", paid back for returns Rs. " + back.ToString("#,##0.##", CultureInfo.InvariantCulture) + ", net Rs. " + (received - back).ToString("#,##0.##", CultureInfo.InvariantCulture) : ""));
            }
            foreach (var g in rows.GroupBy(r => (string)r.by_name)) t.Notes.Add("Received by " + g.Key + ": Rs. " + g.Sum(r => (decimal)r.amount).ToString("#,##0.##", CultureInfo.InvariantCulture));
            return t;
        }

        private static ReportTable Unpaid(System.Data.IDbConnection db)
        {
            var t = new ReportTable { Title = "Unpaid and part-paid orders", Subtitle = "On " + DateTime.UtcNow.AddHours(5).ToString("d MMMM yyyy", CultureInfo.InvariantCulture) }
                .Col("Order", "text", 0.8).Col("Date", "text", 0.9).Col("Customer", "text", 1.6).Col("Mobile", "text", 1).Col("Status", "text", 0.8)
                .Col("To pay", "money").Col("Paid", "money").Col("Balance", "money");
            var rows = db.Query<BookOrder>(@"
                SELECT o.order_no, o.created_at, c.name AS customer_name, c.mobile AS customer_mobile, o.status, o.subtotal, o.discount_total, o.credit_applied,
                       o.paid_total, o.returned_total, o.refunded_total
                FROM dbo.book_orders o JOIN dbo.book_customers c ON c.id = o.customer_id
                WHERE o.status <> N'cancelled' AND (o.subtotal - o.returned_total - o.discount_total - o.credit_applied - o.paid_total + o.refunded_total) > 0
                ORDER BY o.created_at").ToList();
            foreach (var o in rows)
                t.Rows.Add(new object[] { o.OrderLabel, o.CreatedAt.ToOffset(TimeSpan.FromHours(5)).DateTime, o.CustomerName, Helpers.Mobile.Format(o.CustomerMobile), BookFormat.StatusLabel(o.Status) + (o.PaidTotal > 0 ? " · part paid" : ""), Net(o), o.PaidTotal - o.RefundedTotal, o.Balance });
            t.Totals = new object[] { "Total", null, null, null, null, rows.Sum(o => Net(o)), rows.Sum(o => o.PaidTotal - o.RefundedTotal), rows.Sum(o => o.Balance) };
            t.Notes.Add("To pay = order total less returns, discounts and credit. Paid = money received less money paid back.");
            return t;
        }

        private static decimal Net(BookOrder o) { return o.Subtotal - o.ReturnedTotal - o.DiscountTotal - o.CreditApplied; }

        private static ReportTable Discounts(System.Data.IDbConnection db, DateTime from, DateTime to, string period)
        {
            var t = new ReportTable { Title = "Discounts given", Subtitle = period }
                .Col("Date", "text", 0.9).Col("Order", "text", 0.8).Col("Customer", "text", 1.8).Col("Given by", "text", 1.3).Col("Note", "text", 1.6).Col("Discount", "money", 1);
            var rows = db.Query(@"
                SELECT p.received_on, o.order_no, c.name, u.full_name AS by_name, p.note, p.discount
                FROM dbo.book_payments p JOIN dbo.book_orders o ON o.id = p.order_id JOIN dbo.book_customers c ON c.id = o.customer_id
                LEFT JOIN dbo.users u ON u.id = p.received_by
                WHERE p.discount > 0 AND p.received_on >= @from AND p.received_on <= @to ORDER BY p.received_on", new { from = from.Date, to = to.Date }).ToList();
            foreach (var r in rows) t.Rows.Add(new object[] { (DateTime)r.received_on, BookFormat.OrderLabel((long)r.order_no), (string)r.name, (string)r.by_name, (string)r.note, (decimal)r.discount });
            t.Totals = new object[] { "Total", null, null, null, null, rows.Sum(r => (decimal)r.discount) };
            return t;
        }

        private static ReportTable Returns(System.Data.IDbConnection db, DateTime from, DateTime to, string period)
        {
            var t = new ReportTable { Title = "Returns", Subtitle = period }
                .Col("Date", "text", 0.8).Col("Return", "text", 0.6).Col("Customer", "text", 1.5).Col("Orders", "text", 1).Col("Books", "number", 0.5)
                .Col("Value", "money", 0.8).Col("To credit", "money", 0.8).Col("Paid back", "money", 0.8).Col("Refund", "text", 0.8);
            var rows = db.Query(@"
                SELECT r.refunded_on, r.return_no, c.name, (SELECT SUM(quantity) FROM dbo.book_return_lines WHERE return_id = r.id) AS books,
                       r.amount, r.credit_amount, r.refund_amount, r.refund_paid_at,
                       (SELECT STRING_AGG(x.label, N', ') FROM (SELECT DISTINCT N'BK-' + CAST(o2.order_no AS NVARCHAR(20)) AS label FROM dbo.book_return_lines rl2
                           JOIN dbo.book_order_lines l2 ON l2.id = rl2.order_line_id JOIN dbo.book_orders o2 ON o2.id = l2.order_id WHERE rl2.return_id = r.id) x) AS orders
                FROM dbo.book_returns r JOIN dbo.book_customers c ON c.id = r.customer_id
                WHERE r.refunded_on >= @from AND r.refunded_on <= @to ORDER BY r.refunded_on, r.return_no", new { from = from.Date, to = to.Date }).ToList();
            foreach (var r in rows)
                t.Rows.Add(new object[] { (DateTime)r.refunded_on, BookFormat.ReturnLabel((long)r.return_no), (string)r.name, (string)r.orders, (int)r.books, (decimal)r.amount,
                                          (decimal)r.credit_amount, (decimal)r.refund_amount, (decimal)r.refund_amount == 0 ? "" : r.refund_paid_at == null ? "Pending" : "Paid" });
            t.Totals = new object[] { "Total", null, null, null, rows.Sum(r => (int)r.books), rows.Sum(r => (decimal)r.amount), rows.Sum(r => (decimal)r.credit_amount), rows.Sum(r => (decimal)r.refund_amount), null };
            return t;
        }

        /// <summary>What each customer owes, split by how long ago the order was placed (unpaid orders that are not cancelled).</summary>
        private static ReportTable Aging(System.Data.IDbConnection db)
        {
            var t = new ReportTable { Title = "Money owed by age", Subtitle = "On " + DateTime.UtcNow.AddHours(5).ToString("d MMMM yyyy", CultureInfo.InvariantCulture) }
                .Col("Customer", "text", 1.6).Col("No.", "text", 0.6).Col("Campus", "text", 1.3).Col("Mobile", "text", 0.9)
                .Col("0-30 days", "money", 0.8).Col("31-60 days", "money", 0.8).Col("61-90 days", "money", 0.8).Col("Over 90", "money", 0.8).Col("Total owed", "money", 0.9);
            var rows = db.Query(@"
                SELECT c.name, c.customer_no, c.campus_name, c.mobile, DATEDIFF(day, o.created_at, SYSDATETIMEOFFSET()) AS age,
                       (o.subtotal - o.returned_total - o.discount_total - o.credit_applied - o.paid_total + o.refunded_total) AS owed
                FROM dbo.book_orders o JOIN dbo.book_customers c ON c.id = o.customer_id
                WHERE o.status <> N'cancelled' AND (o.subtotal - o.returned_total - o.discount_total - o.credit_applied - o.paid_total + o.refunded_total) > 0").ToList();
            var groups = rows.GroupBy(r => new { name = (string)r.name, no = (long)r.customer_no, campus = (string)r.campus_name, mobile = (string)r.mobile })
                .Select(g => new
                {
                    g.Key, A = g.Where(r => (int)r.age <= 30).Sum(r => (decimal)r.owed), B = g.Where(r => (int)r.age > 30 && (int)r.age <= 60).Sum(r => (decimal)r.owed),
                    C = g.Where(r => (int)r.age > 60 && (int)r.age <= 90).Sum(r => (decimal)r.owed), D = g.Where(r => (int)r.age > 90).Sum(r => (decimal)r.owed), T = g.Sum(r => (decimal)r.owed)
                }).OrderByDescending(x => x.D).ThenByDescending(x => x.T).ToList();
            foreach (var x in groups)
                t.Rows.Add(new object[] { x.Key.name, BookFormat.CustomerLabel(x.Key.no), x.Key.campus, Helpers.Mobile.Format(x.Key.mobile), x.A, x.B, x.C, x.D, x.T });
            t.Totals = new object[] { "Total", null, null, null, groups.Sum(x => x.A), groups.Sum(x => x.B), groups.Sum(x => x.C), groups.Sum(x => x.D), groups.Sum(x => x.T) };
            t.Notes.Add("Age is counted from the day the order was placed. Credit is not taken off here; see each customer's account for the balance after credit.");
            return t;
        }

        /// <summary>Per campus: customers, orders placed in the period, their value, money received in the period, and what is owed now.</summary>
        private static ReportTable Campus(System.Data.IDbConnection db, DateTimeOffset fromUtc, DateTimeOffset toUtc, DateTime from, DateTime to, string period)
        {
            var t = new ReportTable { Title = "Campus-wise summary", Subtitle = period }
                .Col("Campus", "text", 1.8).Col("Customers", "number", 0.7).Col("Orders", "number", 0.6).Col("Order value", "money", 1)
                .Col("Received", "money", 1).Col("Returns", "money", 0.9).Col("Owed now", "money", 1);
            var rows = db.Query(@"
                SELECT COALESCE(NULLIF(c.campus_name, N''), N'(no campus)') AS campus, COUNT(DISTINCT c.id) AS customers,
                       COALESCE(SUM(x.orders), 0) AS orders, COALESCE(SUM(x.value), 0) AS value, COALESCE(SUM(y.received), 0) AS received,
                       COALESCE(SUM(z.returned), 0) AS returned, COALESCE(SUM(w.owed), 0) AS owed
                FROM dbo.book_customers c
                OUTER APPLY (SELECT COUNT(*) AS orders, SUM(o.subtotal) AS value FROM dbo.book_orders o WHERE o.customer_id = c.id AND o.status <> N'cancelled'
                             AND o.created_at >= @fromUtc AND o.created_at < @toUtc) x
                OUTER APPLY (SELECT SUM(p.amount) AS received FROM dbo.book_payments p JOIN dbo.book_orders o ON o.id = p.order_id
                             WHERE o.customer_id = c.id AND p.received_on >= @from AND p.received_on <= @to) y
                OUTER APPLY (SELECT SUM(r.amount) AS returned FROM dbo.book_returns r WHERE r.customer_id = c.id AND r.refunded_on >= @from AND r.refunded_on <= @to) z
                OUTER APPLY (SELECT SUM(o.subtotal - o.returned_total - o.discount_total - o.credit_applied - o.paid_total + o.refunded_total) AS owed
                             FROM dbo.book_orders o WHERE o.customer_id = c.id AND o.status <> N'cancelled') w
                WHERE c.archived_at IS NULL
                GROUP BY COALESCE(NULLIF(c.campus_name, N''), N'(no campus)') ORDER BY campus", new { fromUtc, toUtc, from = from.Date, to = to.Date }).ToList();
            foreach (var r in rows)
                t.Rows.Add(new object[] { (string)r.campus, (int)r.customers, (int)r.orders, (decimal)r.value, (decimal)r.received, (decimal)r.returned, (decimal)r.owed });
            t.Totals = new object[] { "Total", rows.Sum(r => (int)r.customers), rows.Sum(r => (int)r.orders), rows.Sum(r => (decimal)r.value), rows.Sum(r => (decimal)r.received),
                                      rows.Sum(r => (decimal)r.returned), rows.Sum(r => (decimal)r.owed) };
            return t;
        }

        private static ReportTable Profit(System.Data.IDbConnection db, DateTimeOffset fromUtc, DateTimeOffset toUtc, DateTime from, DateTime to, string period)
        {
            var sold = Sold(db, fromUtc, toUtc);
            var ret = Returned(db, from, to);
            var discounts = db.ExecuteScalar<decimal>("SELECT COALESCE(SUM(discount), 0) FROM dbo.book_payments WHERE received_on >= @from AND received_on <= @to",
                new { from = from.Date, to = to.Date });
            var sales = sold.Sum(s => s.Sales);
            var cost = sold.Sum(s => s.Cost);
            var retSales = ret.Sum(r => r.Sales);
            var retCost = ret.Sum(r => r.Cost);
            // Returns are valued after their share of the discount; that share is no longer a discount given.
            var discountBack = db.ExecuteScalar<decimal>(@"SELECT COALESCE(SUM(g.gross - r.amount), 0) FROM dbo.book_returns r
                CROSS APPLY (SELECT SUM(rl.quantity * rl.unit_price) AS gross FROM dbo.book_return_lines rl WHERE rl.return_id = r.id) g
                WHERE r.refunded_on >= @from AND r.refunded_on <= @to", new { from = from.Date, to = to.Date });
            var t = new ReportTable { Title = "Profit", Subtitle = period }.Col("", "text", 3).Col("Amount (Rs.)", "money", 1);
            t.Rows.Add(new object[] { "Sale value of books sold", sales });
            t.Rows.Add(new object[] { "Less: sale value of books returned", -retSales });
            t.Rows.Add(new object[] { "Net sales", sales - retSales });
            t.Rows.Add(new object[] { "Less: purchase value of books sold", -cost });
            t.Rows.Add(new object[] { "Add: purchase value of books returned", retCost });
            t.Rows.Add(new object[] { "Less: discounts given", -discounts });
            if (discountBack != 0) t.Rows.Add(new object[] { "Add: discount on books returned", discountBack });
            t.Totals = new object[] { "Profit", sales - retSales - (cost - retCost) - discounts + discountBack };
            t.Notes.Add("Sale and purchase values are the prices stored on each order line. A sale counts on the day its books came off stock.");
            return t;
        }
    }
}
