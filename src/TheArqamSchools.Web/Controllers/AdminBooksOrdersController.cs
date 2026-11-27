using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Helpers;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/books part 2: orders (books.orders), payments and accounts (books.payments), invoices.
    /// Same controller as the books and customers pages (partial class), so the Books menu stays one area.
    /// </summary>
    public partial class AdminBooksController
    {
        private static readonly TimeSpan PakistanTime = TimeSpan.FromHours(5);

        // ---- Order list and order page ----------------------------------------------------

        [HttpGet]
        public ActionResult Orders(string view, string status, string payment, string from, string to)
        {
            var q = Request.Unvalidated.QueryString["q"];
            // Quick filters (30 November 2026); the older status / payment addresses still work.
            if (view == "tocheck") payment = "tocheck";
            else if (view == "new") status = "new";
            else if (view == "topack") status = "checked";
            else if (view == "ready") status = "packed";
            else if (view == "unpaid") payment = "unpaid";
            else if (view != "done") view = payment == "tocheck" ? "tocheck" : status == "new" ? "new" : payment == "unpaid" ? "unpaid" : null;
            var f = new BookOrderFilter
            {
                View = view,
                Status = BookOrderRepository.Statuses.Contains(status) ? status : null,
                Payment = new[] { "pending", "part", "paid", "unpaid", "tocheck" }.Contains(payment) ? payment : null,
                From = ParseDate(from), To = ParseDate(to),
                Query = Trim(q) == null ? null : (Trim(q).Length > 60 ? Trim(q).Substring(0, 60) : Trim(q))
            };
            ViewBag.Filter = f;
            ViewBag.Counts = BookOrderRepository.ViewCounts();
            ViewBag.CanOrders = AdminAuth.Current(HttpContext).Can("books.orders");
            return View("~/Views/Admin/Books/Orders.cshtml", BookOrderRepository.List(f));
        }

        /// <summary>GET /admin/books/find?code=BK-1001: what a barcode scanner types. Opens the order at its payment form.</summary>
        [HttpGet]
        public ActionResult Find()
        {
            var code = Request.Unvalidated.QueryString["code"];
            var c = (Trim(code) ?? "").ToUpperInvariant();
            if (c.StartsWith("BK")) c = c.Substring(2).TrimStart('-', ' ');
            long no;
            if (long.TryParse(c, NumberStyles.None, CultureInfo.InvariantCulture, out no) && BookRepository.Order(no) != null)
                return Redirect("/admin/books/orders/" + no.ToString(CultureInfo.InvariantCulture) + "#payment");
            return Redirect("/admin/books/orders?q=" + Uri.EscapeDataString(Trim(code) ?? ""));
        }

        [HttpGet]
        public ActionResult Order(long orderNo, string notice, long? rn)
        {
            var o = BookRepository.Order(orderNo);
            if (o == null) return HttpNotFound();
            var msg = notice == "status" ? "Status changed." : notice == "paid" ? "Payment recorded." : notice == "placed" ? "Order placed." : notice == "edited" ? "Order changed."
                : notice == "sentconfirmed" ? "Payment confirmed and recorded. The order is confirmed."
                : notice == "sentrejected" ? "Payment marked as not accepted. The customer sees the reason."
                : notice == "returned" ? "Return " + (rn.HasValue ? "RN-" + rn.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " : "") + "recorded. The books are back in stock." : null;
            return OrderView(o, null, msg, null);
        }

        private ActionResult OrderView(BookOrder o, BookPaymentForm form, string notice, string error)
        {
            var actor = AdminAuth.Current(HttpContext);
            return View("~/Views/Admin/Books/Order.cshtml", new BookOrderAdminViewModel
            {
                Order = o, Payments = BookOrderRepository.Payments(o.Id), Accounts = BookOrderRepository.Accounts(true), Returns = BookReturnRepository.ForOrder(o.Id),
                Submissions = BookPaymentSubmissionRepository.ForOrder(o.Id)
                    .Concat(BookPaymentSubmissionRepository.ForCustomer(o.CustomerId).Where(x => !x.OrderId.HasValue && x.Status == "pending")).ToList(),
                PaymentForm = form ?? new BookPaymentForm { ReceivedOn = DateTime.UtcNow.Add(PakistanTime).Date, Method = "cash" },
                Notice = notice, Error = error, CanOrders = actor.Can("books.orders"), CanPay = actor.Can("books.payments"),
                CanRemove = actor.IsSuperAdmin && o.Status == "cancelled" && o.PaidTotal == 0 && o.DiscountTotal == 0 && o.ReturnedTotal == 0 && o.StockTakenAt == null
            });
        }

        /// <summary>POST /admin/books/sent-payments/{id}/confirm: the receipt is right; record the payment and confirm the order.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.payments")]
        public ActionResult ConfirmSent(Guid id, string back)
        {
            var sub = BookPaymentSubmissionRepository.Get(id);
            if (sub == null) return HttpNotFound();
            // The amount on the receipt must always be typed (review of 27 November 2026): the customer types none.
            decimal received;
            var raw = (Request.Unvalidated.Form["Amount"] ?? "").Replace(",", "").Trim();
            if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out received) || received <= 0)
                return SentError(sub, back, "Please enter the amount shown on the receipt.");
            var allowCredit = (Request.Form["AllowCredit"] ?? "").Split(',').Contains("true");
            try { BookPaymentSubmissionRepository.Confirm(id, AdminAuth.Current(HttpContext).Id, received, allowCredit); }
            catch (BookOrderException ex) { return SentError(sub, back, ex.Message); }
            return SentDone(sub, back, "sentconfirmed");
        }

        /// <summary>
        /// After Confirm / Not accepted: back to the order for an order payment; for a balance payment, back to the
        /// customer's account, which shows where the money went.
        /// </summary>
        private ActionResult SentDone(BookPaymentSubmission sub, string back, string notice)
        {
            if (back == "home") return Redirect("/admin/books?notice=" + notice + "#sent");
            if (sub.OrderNo.HasValue)
                return Redirect("/admin/books/orders/" + sub.OrderNo.Value.ToString(CultureInfo.InvariantCulture) + "?notice=" + notice + "#sent");
            return Redirect("/admin/books/customer-accounts/" + sub.CustomerId.ToString("D") + "?notice=" + notice + "#sent");
        }

        private ActionResult SentError(BookPaymentSubmission sub, string back, string error)
        {
            // Session state is off on this site, so TempData does not survive a redirect: the message goes in the address
            // (it is only ever shown HTML-encoded, on admin pages).
            var q = "?error=" + Uri.EscapeDataString(error.Length > 400 ? error.Substring(0, 400) : error) + "#sent";
            if (back == "home") return Redirect("/admin/books" + q);
            if (sub.OrderNo.HasValue) return OrderView(BookRepository.Order(sub.OrderNo.Value), null, null, error);
            return Redirect("/admin/books/customer-accounts/" + sub.CustomerId.ToString("D") + q);
        }

        /// <summary>POST /admin/books/sent-payments/{id}/reject: the payment is not accepted; the customer sees the reason.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.payments")]
        public ActionResult RejectSent(Guid id, string back)
        {
            var sub = BookPaymentSubmissionRepository.Get(id);
            if (sub == null) return HttpNotFound();
            var reason = Trim(Request.Unvalidated.Form["Reason"]);
            if (reason != null && reason.Length > 300) reason = reason.Substring(0, 300);
            try { BookPaymentSubmissionRepository.Reject(id, reason, AdminAuth.Current(HttpContext).Id); }
            catch (BookOrderException ex) { return SentError(sub, back, ex.Message); }
            return SentDone(sub, back, "sentrejected");
        }

        /// <summary>GET /admin/books/sent-payments/{id}/receipt: the receipt the customer attached (admin only, sandboxed).</summary>
        [HttpGet]
        public ActionResult SentReceipt(Guid id)
        {
            var sub = BookPaymentSubmissionRepository.Get(id);
            if (sub == null) return HttpNotFound();
            var path = ReceiptStore.PhysicalPath(sub.ReceiptPath);
            if (!System.IO.File.Exists(path)) return HttpNotFound();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Cache-Control"] = "private, no-store";
            Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; img-src 'self'; style-src 'unsafe-inline'";
            return File(System.IO.File.ReadAllBytes(path), sub.ReceiptMime ?? "application/octet-stream");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.orders")]
        public ActionResult OrderStatus(long orderNo, string status, string back)
        {
            var o = BookRepository.Order(orderNo);
            if (o == null) return HttpNotFound();
            var error = BookOrderRepository.SetStatus(orderNo, status, AdminAuth.Current(HttpContext).Id);
            // From the Today page, go back there (fixed address only).
            if (back == "home")
                return Redirect("/admin/books?" + (error != null ? "error=" + Uri.EscapeDataString(o.OrderLabel + ": " + error) : "notice=status&no=" + orderNo.ToString(CultureInfo.InvariantCulture)));
            if (error != null) return OrderView(o, null, null, error);
            return Redirect("/admin/books/orders/" + orderNo.ToString(CultureInfo.InvariantCulture) + "?notice=status");
        }

        /// <summary>
        /// POST /admin/books/orders/{no}/remove: a Super Admin removes a cancelled order that no money, stock or return
        /// touched. It is archived (kept in the database) and hidden everywhere; see BookOrderRepository.RemoveCancelled.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.orders")]
        public ActionResult RemoveOrder(long orderNo)
        {
            var actor = AdminAuth.Current(HttpContext);
            if (!actor.IsSuperAdmin) return new HttpStatusCodeResult(403);
            var o = BookRepository.Order(orderNo);
            if (o == null) return HttpNotFound();
            try { BookOrderRepository.RemoveCancelled(orderNo, actor.Id); }
            catch (BookOrderException ex) { return OrderView(o, null, null, ex.Message); }
            return Redirect("/admin/books/orders?notice=removed");
        }

        // ---- Payments -----------------------------------------------------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.payments")]
        public ActionResult Pay(long orderNo)
        {
            var o = BookRepository.Order(orderNo);
            if (o == null) return HttpNotFound();
            var form = Request.Unvalidated.Form;
            var p = new BookPaymentForm
            {
                Method = form["Method"] == "bank" ? "bank" : "cash",
                Note = Trim(form["Note"])
            };
            var errors = new List<string>();
            decimal amount, discount;
            if (!TryMoney(form["Amount"], out amount)) errors.Add("Please enter the amount received in rupees (0 if only a discount is given).");
            if (!TryMoney(form["Discount"], out discount)) errors.Add("Please enter the discount in rupees, or leave it empty.");
            p.Amount = amount;
            p.Discount = discount;
            Guid account;
            p.AccountId = Guid.TryParse(form["AccountId"], out account) ? account : (Guid?)null;
            var on = ParseDate(form["ReceivedOn"]);
            if (on == null) errors.Add("Please enter the date the payment was received.");
            else if (on.Value > DateTime.UtcNow.Add(PakistanTime).Date) errors.Add("The payment date cannot be in the future.");
            p.ReceivedOn = on ?? DateTime.UtcNow.Add(PakistanTime).Date;
            if (p.Note != null && p.Note.Length > 500) errors.Add("The note can be at most 500 characters.");
            if (p.Amount > 0 && p.AccountId == null) errors.Add("Please choose the account the money went into.");
            if (errors.Count > 0) return OrderView(o, p, null, string.Join(" ", errors));

            string receiptPath = null, receiptMime = null;
            var file = Request.Files["Receipt"];
            if (file != null && file.ContentLength > 0)
            {
                try { receiptPath = ReceiptStore.Save(file.InputStream, file.ContentLength, out receiptMime); }
                catch (ImageRejectedException ex) { return OrderView(o, p, null, ex.Message); }
            }
            try
            {
                BookOrderRepository.RecordPayment(orderNo, p, receiptPath, receiptMime, AdminAuth.Current(HttpContext).Id);
            }
            catch (BookOrderException ex)
            {
                if (receiptPath != null) ReceiptStore.Delete(receiptPath);
                return OrderView(BookRepository.Order(orderNo), p, null, ex.Message);
            }
            catch
            {
                if (receiptPath != null) ReceiptStore.Delete(receiptPath);
                throw;
            }
            return Redirect("/admin/books/orders/" + orderNo.ToString(CultureInfo.InvariantCulture) + "?notice=paid#payments");
        }

        /// <summary>GET /admin/books/payments/{id}/receipt: the stored receipt, for admin users only.</summary>
        [HttpGet]
        [AdminAuthorize("books.payments")]
        public ActionResult Receipt(Guid id)
        {
            var p = BookOrderRepository.Payment(id);
            if (p == null || string.IsNullOrEmpty(p.ReceiptPath)) return HttpNotFound();
            var path = ReceiptStore.PhysicalPath(p.ReceiptPath);
            if (!System.IO.File.Exists(path)) return HttpNotFound();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Cache-Control"] = "private, no-store";
            // A PDF opens in the browser, but in a sandbox: it cannot run scripts on the admin panel's address.
            Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; img-src 'self'; style-src 'unsafe-inline'";
            return File(System.IO.File.ReadAllBytes(path), p.ReceiptMime ?? "application/octet-stream");
        }

        // ---- Invoice ------------------------------------------------------------------------

        [HttpGet]
        public ActionResult Invoice(long orderNo)
        {
            var o = BookRepository.Order(orderNo);
            if (o == null) return HttpNotFound();
            return View("~/Views/Admin/Books/Invoice.cshtml", o);
        }

        [HttpGet]
        public ActionResult InvoicePdf(long orderNo)
        {
            var o = BookRepository.Order(orderNo);
            if (o == null) return HttpNotFound();
            Response.Headers["Cache-Control"] = "private, no-store";
            return File(BookInvoice.Pdf(o), "application/pdf", "invoice-" + o.OrderLabel + ".pdf");
        }

        // ---- Office orders ------------------------------------------------------------------

        /// <summary>GET /admin/books/orders/new: choose the customer (by mobile, name or campus), then the books.</summary>
        [HttpGet]
        [AdminAuthorize("books.orders")]
        public ActionResult NewOrder(Guid? customer)
        {
            var q = Request.Unvalidated.QueryString["q"];
            if (customer.HasValue)
            {
                var c = BookRepository.Customer(customer.Value);
                if (c == null) return HttpNotFound();
                return OfficeFormView(c, null, new Dictionary<Guid, int>(), "pickup", null);
            }
            q = Trim(q);
            ViewBag.Query = q;
            ViewBag.Customers = q == null ? new List<BookCustomer>() : BookRepository.Customers(Mobile.Normalize(q) ?? q).Take(30).ToList();
            return View("~/Views/Admin/Books/ChooseCustomer.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.orders")]
        public ActionResult NewOrderPost(Guid customer, string fulfilment)
        {
            var c = BookRepository.Customer(customer);
            if (c == null) return HttpNotFound();
            var errors = new List<string>();
            var qty = Quantities(errors);
            fulfilment = fulfilment == "delivery" ? "delivery" : "pickup";
            if (errors.Count > 0) return OfficeFormView(c, null, qty, fulfilment, errors);
            try
            {
                var no = BookRepository.PlaceOrder(c.Id, qty, fulfilment, false, AdminAuth.Current(HttpContext).Id);
                return Redirect("/admin/books/orders/" + no.ToString(CultureInfo.InvariantCulture) + "?notice=placed");
            }
            catch (BookOrderException ex) { return OfficeFormView(c, null, qty, fulfilment, new[] { ex.Message }); }
        }

        [HttpGet]
        [AdminAuthorize("books.orders")]
        public ActionResult EditOrder(long orderNo)
        {
            var o = BookRepository.Order(orderNo);
            if (o == null) return HttpNotFound();
            if (!o.OfficeCanEdit) return Redirect("/admin/books/orders/" + orderNo.ToString(CultureInfo.InvariantCulture));
            return OfficeFormView(BookRepository.Customer(o.CustomerId), o, o.Lines.ToDictionary(l => l.ItemId, l => l.Quantity), o.Fulfilment, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.orders")]
        [ActionName("EditOrder")]
        public ActionResult EditOrderPost(long orderNo, string fulfilment)
        {
            var o = BookRepository.Order(orderNo);
            if (o == null) return HttpNotFound();
            var errors = new List<string>();
            var qty = Quantities(errors);
            fulfilment = fulfilment == "delivery" ? "delivery" : "pickup";
            var c = BookRepository.Customer(o.CustomerId);
            if (errors.Count > 0) return OfficeFormView(c, o, qty, fulfilment, errors);
            try
            {
                BookRepository.EditOrder(orderNo, null, qty, fulfilment, AdminAuth.Current(HttpContext).Id);
                return Redirect("/admin/books/orders/" + orderNo.ToString(CultureInfo.InvariantCulture) + "?notice=edited");
            }
            catch (BookOrderException ex) { return OfficeFormView(c, o, qty, fulfilment, new[] { ex.Message }); }
        }

        private ActionResult OfficeFormView(BookCustomer c, BookOrder editing, IDictionary<Guid, int> qty, string fulfilment, IList<string> errors)
        {
            var keep = editing == null ? new Dictionary<Guid, decimal>() : editing.Lines.ToDictionary(l => l.ItemId, l => l.UnitPrice);
            var items = BookRepository.Items(true)
                .Where(i => keep.ContainsKey(i.Id) || (i.IsActive && i.Stock > 0))
                .Select(i => { if (keep.ContainsKey(i.Id)) i.SalePrice = keep[i.Id]; return i; }).ToList();
            return View("~/Views/Admin/Books/OrderForm.cshtml", new BookOrderFormViewModel
            {
                Customer = c, Items = items, Quantities = qty, Fulfilment = fulfilment, Editing = editing, Errors = errors
            });
        }

        private Dictionary<Guid, int> Quantities(List<string> errors)
        {
            var result = new Dictionary<Guid, int>();
            foreach (var key in Request.Form.AllKeys.Where(k => k != null && k.StartsWith("q_", StringComparison.Ordinal)))
            {
                Guid id;
                if (!Guid.TryParse(key.Substring(2), out id)) continue;
                var raw = (Request.Form[key] ?? "").Trim();
                int n;
                if (raw.Length == 0) n = 0;
                else if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out n) || n > BookRepository.MaxQuantityPerBook)
                {
                    if (errors.Count == 0) errors.Add("Please enter whole numbers only in the quantity boxes.");
                    n = 0;
                }
                result[id] = n;
            }
            return result;
        }

        // ---- Accounts -----------------------------------------------------------------------

        [HttpGet]
        [AdminAuthorize("books.payments")]
        public ActionResult Accounts(string notice)
        {
            ViewBag.Notice = notice == "saved" ? "Saved." : null;
            return View("~/Views/Admin/Books/Accounts.cshtml", BookOrderRepository.Accounts(false));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.payments")]
        public ActionResult SaveAccount(Guid? id)
        {
            var form = Request.Unvalidated.Form;
            var a = new BookAccount
            {
                Id = id ?? Guid.Empty, Name = Trim(form["Name"]), Kind = form["Kind"] == "bank" ? "bank" : "cash",
                Details = Trim(form["Details"]), IsActive = (form["IsActive"] ?? "").Split(',').Contains("true"),
                AccountTitle = Trim(form["AccountTitle"]),
                AccountNumber = Trim(form["AccountNumber"]),
                Iban = (Trim(form["Iban"]) ?? "").Replace(" ", "").ToUpperInvariant(),
                ShowToCustomers = (form["ShowToCustomers"] ?? "").Split(',').Contains("true")
            };
            if (a.Iban.Length == 0) a.Iban = null;
            string error = null;
            if (a.Name == null || a.Name.Length < 2 || a.Name.Length > 120 || (a.Details != null && a.Details.Length > 300))
                error = "Please enter an account name (2 to 120 characters); the note can be at most 300 characters.";
            else if (a.AccountTitle != null && a.AccountTitle.Length > 150) error = "The account title can be at most 150 characters.";
            else if (a.AccountNumber != null && !System.Text.RegularExpressions.Regex.IsMatch(a.AccountNumber, "^[0-9 -]{4,40}$")) error = "The account number may have only digits, spaces and dashes.";
            else if (a.Iban != null && !System.Text.RegularExpressions.Regex.IsMatch(a.Iban, "^[A-Z]{2}[0-9]{2}[A-Z0-9]{10,30}$")) error = "Please check the IBAN, for example PK88BKIP0223900012520001.";
            else if (a.ShowToCustomers && (a.Kind != "bank" || a.AccountNumber == null)) error = "Only a bank account with an account number can be shown to customers.";
            if (error != null)
            {
                ViewBag.Error = error;
                return View("~/Views/Admin/Books/Accounts.cshtml", BookOrderRepository.Accounts(false));
            }
            if (id.HasValue && BookOrderRepository.Account(id.Value) == null) return HttpNotFound();
            BookOrderRepository.SaveAccount(a, !id.HasValue, AdminAuth.Current(HttpContext).Id);
            return Redirect("/admin/books/accounts?notice=saved");
        }

        // ---- Helpers ------------------------------------------------------------------------

        private static DateTime? ParseDate(string s)
        {
            DateTime d;
            return DateTime.TryParseExact((s ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d : (DateTime?)null;
        }

        /// <summary>Rupees with up to two decimals; empty means 0; commas are allowed (1,250).</summary>
        private static bool TryMoney(string s, out decimal value)
        {
            value = 0;
            s = (s ?? "").Replace(",", "").Trim();
            if (s.Length == 0) return true;
            return decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= 99999999 && decimal.Round(value, 2) == value;
        }
    }
}
