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
            new[] { "returns", "Returns" }, new[] { "profit", "Profit" }
        };

        public static bool NeedsDates(string kind) { return kind != "stock" && kind != "unpaid"; }

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
                    default: return null;
                }
            }
        }

        private static ReportTable Stock(System.Data.IDbConnection db)
        {
            var t = new ReportTable { Title = "Stock", Subtitle = "Stock in hand on " + DateTime.UtcNow.AddHours(5).ToString("d MMMM yyyy", CultureInfo.InvariantCulture) }
                .Col("Class", "text", 1).Col("Book", "text", 2.2).Col("Shown", "text", 0.6).Col("In stock", "number", 0.8)
                .Col("Purchase price", "money").Col("Sale price", "money").Col("Value at purchase", "money", 1.2).Col("Value at sale", "money", 1.2);
            var items = BookRepository.Sort(db.Query<BookItem>("SELECT id, class_name, name, stock, purchase_price, sale_price, is_active, sort_order FROM dbo.book_items WHERE archived_at IS NULL"));
            foreach (var i in items)
                t.Rows.Add(new object[] { i.ClassName, i.Name, i.IsActive ? "Yes" : "Hidden", i.Stock, i.PurchasePrice, i.SalePrice, i.PurchasePrice * i.Stock, i.SalePrice * i.Stock });
            t.Totals = new object[] { "Total", null, null, items.Sum(i => i.Stock), null, null, items.Sum(i => i.PurchasePrice * i.Stock), items.Sum(i => i.SalePrice * i.Stock) };
            return t;
        }

        private class SoldRow { public Guid ItemId { get; set; } public string ClassName { get; set; } public string ItemName { get; set; } public int Qty { get; set; } public decimal Sales { get; set; } public decimal Cost { get; set; } }

        private static IList<SoldRow> Sold(System.Data.IDbConnection db, DateTimeOffset fromUtc, DateTimeOffset toUtc)
        {
            return db.Query<SoldRow>(@"
                SELECT l.item_id, l.class_name, l.item_name, SUM(l.quantity) AS qty, SUM(l.quantity * l.unit_price) AS sales, SUM(l.quantity * l.purchase_price) AS cost
                FROM dbo.book_order_lines l JOIN dbo.book_orders o ON o.id = l.order_id
                WHERE o.stock_taken_at >= @fromUtc AND o.stock_taken_at < @toUtc AND l.quantity > 0
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
                       (SELECT COUNT(*) FROM dbo.book_orders o WHERE o.customer_id = c.id AND o.stock_taken_at >= @fromUtc AND o.stock_taken_at < @toUtc) AS orders,
                       (SELECT COALESCE(SUM(o.subtotal), 0) FROM dbo.book_orders o WHERE o.customer_id = c.id AND o.stock_taken_at >= @fromUtc AND o.stock_taken_at < @toUtc) AS sales,
                       (SELECT COALESCE(SUM(r.amount), 0) FROM dbo.book_returns r JOIN dbo.book_orders o ON o.id = r.order_id
                        WHERE o.customer_id = c.id AND r.refunded_on >= @from AND r.refunded_on <= @to) AS returned
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
            foreach (var g in rows.GroupBy(r => (string)r.account)) t.Notes.Add("Account " + g.Key + ": Rs. " + g.Sum(r => (decimal)r.amount).ToString("#,##0.##", CultureInfo.InvariantCulture));
            foreach (var g in rows.GroupBy(r => (string)r.by_name)) t.Notes.Add("Received by " + g.Key + ": Rs. " + g.Sum(r => (decimal)r.amount).ToString("#,##0.##", CultureInfo.InvariantCulture));
            return t;
        }

        private static ReportTable Unpaid(System.Data.IDbConnection db)
        {
            var t = new ReportTable { Title = "Unpaid and part-paid orders", Subtitle = "On " + DateTime.UtcNow.AddHours(5).ToString("d MMMM yyyy", CultureInfo.InvariantCulture) }
                .Col("Order", "text", 0.8).Col("Date", "text", 0.9).Col("Customer", "text", 1.6).Col("Mobile", "text", 1).Col("Status", "text", 0.8)
                .Col("Total", "money").Col("Paid", "money").Col("Balance", "money");
            var rows = db.Query<BookOrder>(@"
                SELECT o.order_no, o.created_at, c.name AS customer_name, c.mobile AS customer_mobile, o.status, o.subtotal, o.discount_total, o.credit_applied,
                       o.paid_total, o.returned_total, o.refunded_total
                FROM dbo.book_orders o JOIN dbo.book_customers c ON c.id = o.customer_id
                WHERE o.status <> N'cancelled' AND (o.subtotal - o.returned_total - o.discount_total - o.credit_applied - o.paid_total + o.refunded_total) > 0
                ORDER BY o.created_at").ToList();
            foreach (var o in rows)
                t.Rows.Add(new object[] { o.OrderLabel, o.CreatedAt.ToOffset(TimeSpan.FromHours(5)).DateTime, o.CustomerName, Helpers.Mobile.Format(o.CustomerMobile), BookFormat.StatusLabel(o.Status) + (o.PaidTotal > 0 ? " · part paid" : ""), o.Subtotal, o.PaidTotal, o.Balance });
            t.Totals = new object[] { "Total", null, null, null, null, rows.Sum(o => o.Subtotal), rows.Sum(o => o.PaidTotal), rows.Sum(o => o.Balance) };
            return t;
        }

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
                .Col("Date", "text", 0.9).Col("Return", "text", 0.7).Col("Order", "text", 0.8).Col("Customer", "text", 1.5).Col("Books", "number", 0.6)
                .Col("Value", "money", 0.9).Col("Paid back", "money", 0.9).Col("Account", "text", 1.1).Col("By", "text", 1.1);
            var rows = db.Query(@"
                SELECT r.refunded_on, r.return_no, o.order_no, c.name, (SELECT SUM(quantity) FROM dbo.book_return_lines WHERE return_id = r.id) AS books,
                       r.amount, r.refund_amount, a.name AS account, u.full_name AS by_name
                FROM dbo.book_returns r JOIN dbo.book_orders o ON o.id = r.order_id JOIN dbo.book_customers c ON c.id = o.customer_id
                LEFT JOIN dbo.book_accounts a ON a.id = r.refund_account_id LEFT JOIN dbo.users u ON u.id = r.created_by
                WHERE r.refunded_on >= @from AND r.refunded_on <= @to ORDER BY r.refunded_on, r.return_no", new { from = from.Date, to = to.Date }).ToList();
            foreach (var r in rows)
                t.Rows.Add(new object[] { (DateTime)r.refunded_on, "RN-" + ((long)r.return_no).ToString(CultureInfo.InvariantCulture), BookFormat.OrderLabel((long)r.order_no), (string)r.name, (int)r.books, (decimal)r.amount, (decimal)r.refund_amount, (string)r.account, (string)r.by_name });
            t.Totals = new object[] { "Total", null, null, null, rows.Sum(r => (int)r.books), rows.Sum(r => (decimal)r.amount), rows.Sum(r => (decimal)r.refund_amount), null, null };
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
            var t = new ReportTable { Title = "Profit", Subtitle = period }.Col("", "text", 3).Col("Amount (Rs.)", "money", 1);
            t.Rows.Add(new object[] { "Sale value of books sold", sales });
            t.Rows.Add(new object[] { "Less: sale value of books returned", -retSales });
            t.Rows.Add(new object[] { "Net sales", sales - retSales });
            t.Rows.Add(new object[] { "Less: purchase value of books sold", -cost });
            t.Rows.Add(new object[] { "Add: purchase value of books returned", retCost });
            t.Rows.Add(new object[] { "Less: discounts given", -discounts });
            t.Totals = new object[] { "Profit", sales - retSales - (cost - retCost) - discounts };
            t.Notes.Add("Sale and purchase values are the prices stored on each order line. A sale counts on the day its books came off stock.");
            return t;
        }
    }
}
