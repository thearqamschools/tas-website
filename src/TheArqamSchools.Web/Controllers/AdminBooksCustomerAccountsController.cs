using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/books/customer-accounts (owner's request, 20 November 2026): each customer's summary, ledger and reports,
    /// customer-wise returns (books.payments, because money may be paid back), pending refunds and ready-made messages.
    /// </summary>
    public partial class AdminBooksController
    {
        [HttpGet]
        public ActionResult CustomerAccounts()
        {
            var q = Trim(Request.Unvalidated.QueryString["q"]);
            if (q != null && q.Length > 60) q = q.Substring(0, 60);
            ViewBag.Query = q;
            ViewBag.Wide = true;
            return View("~/Views/Admin/Books/CustomerAccounts.cshtml", BookCustomerAccountRepository.Customers(q));
        }

        /// <summary>GET /admin/books/customer-accounts/{id}?view=ledger|orders|payments|returns|messages&amp;from&amp;to&amp;session&amp;format=pdf|xlsx</summary>
        [HttpGet]
        public ActionResult CustomerAccount(Guid id, string view, string from, string to, string session, string format, string notice)
        {
            var c = BookRepository.Customer(id);
            if (c == null) return HttpNotFound();
            view = new[] { "ledger", "orders", "payments", "returns", "messages" }.Contains(view) ? view : "ledger";
            DateTime? f = ParseDate(from), t = ParseDate(to);
            int year;
            if (int.TryParse(session, NumberStyles.None, CultureInfo.InvariantCulture, out year) && year > 2000 && year < 2100)
            {
                f = new DateTime(year, 4, 1);   // academic session: 1 April to 31 March
                t = new DateTime(year + 1, 3, 31);
            }
            if (f.HasValue && t.HasValue && t < f) { var x = f; f = t; t = x; }
            var table = view == "messages" ? null : BuildCustomerReport(c, view, f, t);
            if (table != null && (format == "pdf" || format == "xlsx"))
            {
                if (!AdminAuth.Current(HttpContext).Can("books.reports")) return new HttpStatusCodeResult(403);
                Response.Headers["Cache-Control"] = "private, no-store";
                var name = "customer-" + c.CustomerLabel + "-" + view;
                return format == "pdf" ? (ActionResult)File(table.ToPdf(), "application/pdf", name + ".pdf")
                                       : File(table.ToXlsx(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx");
            }
            var actor = AdminAuth.Current(HttpContext);
            ViewBag.Wide = true;
            return View("~/Views/Admin/Books/CustomerAccount.cshtml", new BookCustomerAccountViewModel
            {
                Customer = c, Summary = BookCustomerAccountRepository.GetSummary(id), View = view, From = f, To = t, Session = session,
                Report = table, Messages = view == "messages" ? BookMessageRepository.ForCustomer(id) : null,
                PendingRefunds = BookReturnRepository.ForCustomer(id).Where(r => r.RefundPending).ToList(),
                Submissions = BookPaymentSubmissionRepository.ForCustomer(id).Where(x => !x.OrderId.HasValue).Take(10).ToList(),
                Accounts = BookOrderRepository.Accounts(true), CanPay = actor.Can("books.payments"), CanOrders = actor.Can("books.orders"),
                Notice = notice == "returned" ? "Return " + Request.QueryString["rn"] + " recorded. The books are back in stock."
                       : notice == "refund" ? "Refund recorded as paid."
                       : notice == "sentconfirmed" ? "Payment confirmed and applied to the oldest unpaid orders first."
                       : notice == "sentrejected" ? "Payment marked as not accepted. A message for the customer is ready under Messages." : notice == "sent" ? "Message marked as sent." : null
            });
        }

        private ReportTable BuildCustomerReport(BookCustomer c, string view, DateTime? f, DateTime? t)
        {
            var who = c.Name + " (" + c.CustomerLabel + ")";
            var period = f.HasValue || t.HasValue
                ? (f.HasValue ? f.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "start") + " to " + (t.HasValue ? t.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "today")
                : "All dates";
            var sub = period + " · " + Helpers.Mobile.Format(c.Mobile) + (string.IsNullOrEmpty(c.CampusName) ? "" : " · " + c.CampusName);
            Func<DateTimeOffset, bool> inRange = at => { var d = at.ToOffset(PakistanTime).Date; return (!f.HasValue || d >= f.Value) && (!t.HasValue || d <= t.Value); };
            if (view == "ledger")
            {
                var tbl = new ReportTable { Title = "Statement of account – " + who, Subtitle = sub }
                    .Col("Date", "text", 0.8).Col("Type", "text", 0.8).Col("Reference", "text", 0.8).Col("Details", "text", 1.4)
                    .Col("Debit", "money", 0.8).Col("Credit", "money", 0.8).Col("Balance", "money", 0.9);
                var lines = BookCustomerAccountRepository.Ledger(c.Id, f, t, false);
                foreach (var l in lines)
                    tbl.Rows.Add(new object[] { l.On, l.Kind, l.Reference, l.Details, l.Debit == 0 ? (object)null : l.Debit, l.CreditAmount == 0 ? (object)null : l.CreditAmount, l.Balance });
                var s = BookCustomerAccountRepository.GetSummary(c.Id);
                tbl.Totals = new object[] { "Total", null, null, null, lines.Sum(l => l.Debit), lines.Sum(l => l.CreditAmount), lines.Count > 0 ? lines.Last().Balance : 0m };
                tbl.Notes.Add("Debit = what the customer owes for orders and money paid back. Credit = payments, discounts and returns. Balance = what the customer owes (a minus means the school owes the customer).");
                if (s.Credit > 0) tbl.Notes.Add("Credit kept for the next order: " + BookFormat.Money(s.Credit) + " (already included in the balance).");
                return tbl;
            }
            if (view == "orders")
            {
                var tbl = new ReportTable { Title = "Orders – " + who, Subtitle = sub }
                    .Col("Order", "text", 0.7).Col("Date", "text", 0.8).Col("Status", "text", 0.8).Col("Books", "number", 0.5).Col("Total", "money", 0.8)
                    .Col("Paid", "money", 0.8).Col("Discount", "money", 0.7).Col("Returns", "money", 0.8).Col("Balance", "money", 0.8);
                var orders = BookRepository.CustomerOrders(c.Id).Where(o => inRange(o.CreatedAt)).OrderBy(o => o.CreatedAt).ToList();
                foreach (var o in orders)
                    tbl.Rows.Add(new object[] { o.OrderLabel, o.CreatedAt.ToOffset(PakistanTime).DateTime, BookFormat.StatusLabel(o.Status), o.Lines.Sum(l => l.Quantity), o.Subtotal, o.PaidTotal, o.DiscountTotal, o.ReturnedTotal, o.Status == "cancelled" ? 0m : o.Balance });
                var live = orders.Where(o => o.Status != "cancelled").ToList();
                tbl.Totals = new object[] { "Total", null, null, live.Sum(o => o.Lines.Sum(l => l.Quantity)), live.Sum(o => o.Subtotal), live.Sum(o => o.PaidTotal), live.Sum(o => o.DiscountTotal), live.Sum(o => o.ReturnedTotal), live.Sum(o => o.Balance) };
                tbl.Notes.Add("Cancelled orders are listed but not counted in the totals.");
                return tbl;
            }
            if (view == "payments")
            {
                var tbl = new ReportTable { Title = "Payments – " + who, Subtitle = sub }
                    .Col("Date", "text", 0.8).Col("Order", "text", 0.7).Col("Method", "text", 0.6).Col("Account", "text", 1.1).Col("Amount", "money", 0.8).Col("Discount", "money", 0.7).Col("Received by", "text", 1.1);
                var rows = BookRepository.CustomerOrders(c.Id).SelectMany(o => BookOrderRepository.Payments(o.Id).Select(p => new { o, p }))
                    .Where(x => (!f.HasValue || x.p.ReceivedOn >= f.Value) && (!t.HasValue || x.p.ReceivedOn <= t.Value)).OrderBy(x => x.p.ReceivedOn).ToList();
                foreach (var x in rows)
                    tbl.Rows.Add(new object[] { x.p.ReceivedOn, x.o.OrderLabel, x.p.Method == "bank" ? "Bank" : x.p.Method == "cash" ? "Cash" : "", x.p.AccountName, x.p.Amount, x.p.Discount, x.p.ReceivedByName });
                tbl.Totals = new object[] { "Total", null, null, null, rows.Sum(x => x.p.Amount), rows.Sum(x => x.p.Discount), null };
                return tbl;
            }
            {
                var tbl = new ReportTable { Title = "Returns – " + who, Subtitle = sub }
                    .Col("Date", "text", 0.8).Col("Return", "text", 0.6).Col("Orders", "text", 1.1).Col("Books", "number", 0.5).Col("Value", "money", 0.8)
                    .Col("To credit", "money", 0.8).Col("Paid back", "money", 0.8).Col("Refund", "text", 0.7);
                var rows = BookReturnRepository.ForCustomer(c.Id).Where(r => (!f.HasValue || r.RefundedOn >= f.Value) && (!t.HasValue || r.RefundedOn <= t.Value)).OrderBy(r => r.RefundedOn).ToList();
                foreach (var r in rows)
                    tbl.Rows.Add(new object[] { r.RefundedOn, r.ReturnLabel, r.OrderLabels, r.Lines.Sum(l => l.Quantity), r.Amount, r.CreditAmount, r.RefundAmount, r.RefundAmount == 0 ? "" : r.RefundPending ? "Pending" : "Paid" });
                tbl.Totals = new object[] { "Total", null, null, rows.Sum(r => r.Lines.Sum(l => l.Quantity)), rows.Sum(r => r.Amount), rows.Sum(r => r.CreditAmount), rows.Sum(r => r.RefundAmount), null };
                return tbl;
            }
        }

        /// <summary>GET /admin/books/customer-accounts/{id}/return: the books the customer has and can bring back.</summary>
        [HttpGet]
        [AdminAuthorize("books.payments")]
        public ActionResult CustomerReturn(Guid id)
        {
            var c = BookRepository.Customer(id);
            if (c == null) return HttpNotFound();
            return CustomerReturnView(c, null, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.payments")]
        [ActionName("CustomerReturn")]
        public ActionResult CustomerReturnPost(Guid id)
        {
            var c = BookRepository.Customer(id);
            if (c == null) return HttpNotFound();
            var form = Request.Unvalidated.Form;
            var qty = new Dictionary<Guid, int>();
            foreach (var key in form.AllKeys.Where(k => k != null && k.StartsWith("r_", StringComparison.Ordinal)))
            {
                Guid itemId; int n;
                var raw = (form[key] ?? "").Trim();
                if (!Guid.TryParse(key.Substring(2), out itemId) || raw.Length == 0) continue;
                if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out n)) return CustomerReturnView(c, form, "Please enter whole numbers in the return boxes.");
                qty[itemId] = n;
            }
            var on = ParseDate(form["ReturnedOn"]) ?? DateTime.UtcNow.Add(PakistanTime).Date;
            var note = Trim(form["ReturnNote"]);
            if (note != null && note.Length > 400) return CustomerReturnView(c, form, "The note can be at most 400 characters.");
            try
            {
                var no = BookReturnRepository.RecordReturn(id, qty, form["Choice"] == "refund" ? "refund" : "credit", on, note, AdminAuth.Current(HttpContext).Id);
                return Redirect("/admin/books/customer-accounts/" + id.ToString("D") + "?notice=returned&rn=" + BookFormat.ReturnLabel(no) + "&view=returns");
            }
            catch (BookOrderException ex) { return CustomerReturnView(c, form, ex.Message); }
        }

        private ActionResult CustomerReturnView(BookCustomer c, System.Collections.Specialized.NameValueCollection form, string error)
        {
            ViewBag.Customer = c;
            ViewBag.Error = error;
            ViewBag.Form = form;
            return View("~/Views/Admin/Books/CustomerReturn.cshtml", BookReturnRepository.ReturnableBooks(c.Id));
        }

        /// <summary>POST /admin/books/returns/{no}/refund: a pending payback has been paid (cash or bank).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.payments")]
        public ActionResult PayRefund(long returnNo)
        {
            var r = BookReturnRepository.Get(returnNo);
            if (r == null) return HttpNotFound();
            Guid account;
            var accountId = Guid.TryParse(Request.Form["RefundAccountId"], out account) ? account : (Guid?)null;
            try { BookReturnRepository.PayRefund(returnNo, Request.Form["RefundMethod"] == "bank" ? "bank" : "cash", accountId, AdminAuth.Current(HttpContext).Id); }
            catch (BookOrderException ex)
            {
                return Redirect("/admin/books/customer-accounts/" + r.CustomerId.ToString("D") + "?view=returns&error=" + Uri.EscapeDataString(ex.Message));
            }
            return Redirect("/admin/books/customer-accounts/" + r.CustomerId.ToString("D") + "?notice=refund&view=returns");
        }

        /// <summary>POST /admin/books/messages/{id}/sent: the office has sent (or not) the message by WhatsApp or SMS.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.orders")]
        public ActionResult MessageSent(Guid id, bool sent = true, string back = null)
        {
            var customerId = BookMessageRepository.MarkSent(id, sent, AdminAuth.Current(HttpContext).Id);
            if (customerId == null) return HttpNotFound();
            if (back == "list")
            {
                // Back to the same filtered list (only our own query string, rebuilt from known keys).
                var r = Request.Unvalidated.Form;
                var qs = new List<string>();
                foreach (var k in new[] { "status", "q", "kind", "from", "to" })
                    if (!string.IsNullOrEmpty(r["f_" + k])) qs.Add(k + "=" + Uri.EscapeDataString(r["f_" + k].Length > 60 ? r["f_" + k].Substring(0, 60) : r["f_" + k]));
                return Redirect("/admin/books/messages" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "") + "#m-" + id.ToString("N"));
            }
            return Redirect("/admin/books/customer-accounts/" + customerId.Value.ToString("D") + "?view=messages&notice=sent");
        }

        /// <summary>GET /admin/books/messages?status=unsent|sent|all&amp;q&amp;kind&amp;from&amp;to: the Messages tab (all customers).</summary>
        [HttpGet]
        public ActionResult Messages(string status, string kind, string from, string to)
        {
            var q = Trim(Request.Unvalidated.QueryString["q"]);
            var f = new BookMessageRepository.Filter
            {
                Status = status == "sent" || status == "all" ? status : "unsent",
                Kind = BookMessageRepository.Kinds.Contains(kind) ? kind : null,
                Query = q == null ? null : (q.Length > 60 ? q.Substring(0, 60) : q),
                From = ParseDate(from), To = ParseDate(to)
            };
            if (f.From.HasValue && f.To.HasValue && f.To < f.From) { var x = f.From; f.From = f.To; f.To = x; }
            ViewBag.Filter = f;
            ViewBag.CanOrders = AdminAuth.Current(HttpContext).Can("books.orders");
            ViewBag.Wide = true;
            return View("~/Views/Admin/Books/Messages.cshtml", BookMessageRepository.Search(f));
        }
    }
}
