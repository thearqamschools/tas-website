using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Helpers;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /books: customers order books with their mobile number (owner's specification). Only numbers
    /// registered by the office can order; there is no password. After the number is entered, a
    /// short-lived encrypted cookie (MachineKey) remembers the customer for 2 hours, so the number is
    /// not sent again on every page. Protections: anti-forgery tokens, the hidden Website honeypot,
    /// rate limits per network, and customers only ever see their own orders (order number, date,
    /// books, total, status and part of the address; never discounts, purchase prices or payments).
    /// </summary>
    public class BooksController : Controller
    {
        private const string CookieName = "tas_books";
        private const string Purpose = "books-customer-v1";
        private static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

        [HttpGet]
        public ActionResult Index()
        {
            if (CurrentCustomer() != null) return Redirect("/books/order");
            return View("~/Views/Books/Index.cshtml", Meta());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Index")]
        public ActionResult Start(string mobile, string website)
        {
            if (!string.IsNullOrEmpty(website)) return Redirect("/books");
            if (!RateLimiter.Allow("books-start", PageBuilder.ClientKey(Request), 10, TimeSpan.FromMinutes(10)))
                return StartError("Too many tries from this network. Please wait a few minutes and try again.", mobile);
            var normal = Mobile.Normalize(mobile);
            if (normal == null) return StartError("Please enter a mobile number like 0300-1234567.", mobile);
            // Also limited per number and for the whole site, so numbers cannot be tried from many networks.
            if (!RateLimiter.Allow("books-number", normal, 8, TimeSpan.FromMinutes(30)) || !RateLimiter.Allow("books-all", "site", 300, TimeSpan.FromMinutes(10)))
                return StartError("Too many tries. Please wait a few minutes and try again, or call the office.", mobile);
            var customer = BookRepository.CustomerByMobile(normal);
            if (customer == null)
            {
                ViewBag.NotRegistered = true;
                return StartError("This number is not registered for book orders. Please contact the office to register.", mobile);
            }
            SetCookie(customer.Id);
            return Redirect("/books/order");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SignOut()
        {
            Response.Cookies.Add(new HttpCookie(CookieName, "") { Expires = DateTime.UtcNow.AddDays(-1), HttpOnly = true, Path = "/books" });
            return Redirect("/books");
        }

        // ---- New order and edit -----------------------------------------------------------

        [HttpGet]
        public ActionResult Order()
        {
            var customer = CurrentCustomer();
            if (customer == null) return Redirect("/books");
            return FormView(customer, null, new Dictionary<Guid, int>(), "pickup", null, false);
        }

        [HttpGet]
        public ActionResult Edit(long orderNo)
        {
            var customer = CurrentCustomer();
            if (customer == null) return Redirect("/books");
            var order = OwnOrder(customer, orderNo);
            if (order == null) return HttpNotFound();
            if (!order.CustomerCanEdit) return Redirect("/books/orders/" + orderNo.ToString(CultureInfo.InvariantCulture));
            return FormView(customer, order, order.Lines.ToDictionary(l => l.ItemId, l => l.Quantity), order.Fulfilment, null, false);
        }

        /// <summary>
        /// POST /books/order and /books/orders/{no}/edit. step = "preview" shows the summary,
        /// "back" returns to the form with the same quantities, "place" saves the order.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(long? orderNo, string step, string fulfilment, string website)
        {
            var customer = CurrentCustomer();
            if (customer == null) return Redirect("/books");
            if (!string.IsNullOrEmpty(website)) return Redirect("/books");
            BookOrder editing = null;
            if (orderNo.HasValue)
            {
                editing = OwnOrder(customer, orderNo.Value);
                if (editing == null) return HttpNotFound();
            }
            fulfilment = fulfilment == "delivery" ? "delivery" : "pickup";
            var errors = new List<string>();
            var quantities = ReadQuantities(errors);
            if (errors.Count > 0) return FormView(customer, editing, quantities, fulfilment, errors, false);
            if (quantities.Values.All(q => q == 0)) return FormView(customer, editing, quantities, fulfilment, new[] { "Please enter a quantity for at least one book." }, false);
            if (fulfilment == "delivery" && string.IsNullOrWhiteSpace(customer.Address))
                return FormView(customer, editing, quantities, fulfilment, new[] { "There is no delivery address for this number. Please choose Pick up, or ask the office to add your address." }, false);

            if (step == "back") return FormView(customer, editing, quantities, fulfilment, null, false);
            if (step != "place") return FormView(customer, editing, quantities, fulfilment, null, true);

            if (!RateLimiter.Allow("books-place", PageBuilder.ClientKey(Request), 20, TimeSpan.FromHours(1)))
                return FormView(customer, editing, quantities, fulfilment, new[] { "Too many orders from this network. Please wait and try again, or call the office." }, true);
            try
            {
                if (editing != null)
                {
                    BookRepository.EditOrder(editing.OrderNo, customer.Id, quantities, fulfilment, null);
                    return Redirect("/books/orders/" + editing.OrderNo.ToString(CultureInfo.InvariantCulture) + "?changed=true");
                }
                var no = BookRepository.PlaceOrder(customer.Id, quantities, fulfilment, true, null);
                return Redirect("/books/orders/" + no.ToString(CultureInfo.InvariantCulture) + "?placed=true");
            }
            catch (BookOrderException ex)
            {
                return FormView(customer, editing, quantities, fulfilment, new[] { ex.Message }, false);
            }
        }

        // ---- History ------------------------------------------------------------------------

        [HttpGet]
        public ActionResult Orders()
        {
            var customer = CurrentCustomer();
            if (customer == null) return Redirect("/books");
            ViewBag.Customer = customer;
            ViewBag.Meta = Meta();
            return View("~/Views/Books/Orders.cshtml", BookRepository.CustomerOrders(customer.Id));
        }

        /// <summary>
        /// POST /books/orders/{no}/pay (multipart): the customer has paid into one of the school's bank accounts and
        /// sends the amount and the receipt. The office checks and confirms it; nothing counts as paid before that.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Pay(long orderNo, string website)
        {
            var customer = CurrentCustomer();
            if (customer == null) return Redirect("/books");
            if (!string.IsNullOrEmpty(website)) return Redirect("/books");
            var order = OwnOrder(customer, orderNo);
            if (order == null) return HttpNotFound();
            if (!RateLimiter.Allow("books-pay", PageBuilder.ClientKey(Request), 10, TimeSpan.FromHours(1)))
                return DetailView(customer, order, false, false, false, "Too many payments sent from this network. Please wait and try again, or call the office.");
            var form = Request.Unvalidated.Form;
            Guid accountId;
            decimal amount;
            var rawAmount = (form["PayAmount"] ?? "").Replace(",", "").Trim();
            if (!Guid.TryParse(form["PayAccount"], out accountId))
                return DetailView(customer, order, false, false, false, "Please choose the bank account you paid into.");
            if (!decimal.TryParse(rawAmount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount) || amount <= 0)
                return DetailView(customer, order, false, false, false, "Please enter the amount you paid, in rupees.");
            var file = Request.Files["PayReceipt"];
            if (file == null || file.ContentLength <= 0)
                return DetailView(customer, order, false, false, false, "Please attach the receipt (a photo or a PDF).");
            string path, mime;
            try { path = ReceiptStore.Save(file.InputStream, file.ContentLength, out mime); }
            catch (ImageRejectedException ex) { return DetailView(customer, order, false, false, false, ex.Message); }
            try { BookPaymentSubmissionRepository.Submit(orderNo, customer.Id, accountId, amount, path, mime); }
            catch (BookOrderException ex)
            {
                ReceiptStore.Delete(path);
                return DetailView(customer, OwnOrder(customer, orderNo), false, false, false, ex.Message);
            }
            catch
            {
                ReceiptStore.Delete(path);   // no orphan receipt when the database is unavailable
                throw;
            }
            return Redirect("/books/orders/" + orderNo.ToString(CultureInfo.InvariantCulture) + "?sent=true#payment");
        }

        /// <summary>POST /books/orders/{no}/cancel: the customer cancels their own order while it is still New.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(long orderNo, string website)
        {
            var customer = CurrentCustomer();
            if (customer == null) return Redirect("/books");
            if (!string.IsNullOrEmpty(website)) return Redirect("/books");
            var order = OwnOrder(customer, orderNo);
            if (order == null) return HttpNotFound();
            if (!RateLimiter.Allow("books-cancel", PageBuilder.ClientKey(Request), 20, TimeSpan.FromHours(1)))
                return DetailView(customer, order, false, false, false, "Too many requests from this network. Please wait and try again, or call the office.");
            try { BookRepository.CancelByCustomer(orderNo, customer.Id); }
            catch (BookOrderException ex) { return DetailView(customer, OwnOrder(customer, orderNo), false, false, false, ex.Message); }
            return Redirect("/books/orders/" + orderNo.ToString(CultureInfo.InvariantCulture) + "?cancelled=true");
        }

        [HttpGet]
        public ActionResult Detail(long orderNo, bool placed = false, bool changed = false, bool cancelled = false, bool sent = false)
        {
            var customer = CurrentCustomer();
            if (customer == null) return Redirect("/books");
            var order = OwnOrder(customer, orderNo);
            if (order == null) return HttpNotFound();
            ViewBag.Sent = sent;
            return DetailView(customer, order, placed, changed, cancelled, null);
        }

        private ActionResult DetailView(BookCustomer customer, BookOrder order, bool placed, bool changed, bool cancelled, string error)
        {
            ViewBag.Customer = customer;
            ViewBag.Placed = placed;
            ViewBag.Changed = changed;
            ViewBag.Cancelled = cancelled;
            ViewBag.Error = error;
            ViewBag.Accounts = BookPaymentSubmissionRepository.CustomerAccounts();
            ViewBag.Submissions = order == null ? new List<BookPaymentSubmission>() : BookPaymentSubmissionRepository.ForOrder(order.Id);
            ViewBag.Meta = Meta();
            return View("~/Views/Books/Detail.cshtml", order);
        }

        // ---- Helpers ------------------------------------------------------------------------

        private ActionResult FormView(BookCustomer customer, BookOrder editing, IDictionary<Guid, int> quantities, string fulfilment, IList<string> errors, bool preview)
        {
            // Books already in the order being edited stay on the form even when their stock has run out or the
            // office has hidden them, and they keep the price stored on the order (the saved total uses it too).
            var keep = editing == null ? new Dictionary<Guid, decimal>() : editing.Lines.ToDictionary(l => l.ItemId, l => l.UnitPrice);
            var items = BookRepository.Items(true)
                .Where(i => keep.ContainsKey(i.Id) || (i.IsActive && i.Stock > 0))
                .Select(i => { if (keep.ContainsKey(i.Id)) i.SalePrice = keep[i.Id]; return i; })
                .ToList();
            ViewBag.Meta = Meta();
            return View("~/Views/Books/Order.cshtml", new BookOrderFormViewModel
            {
                Customer = customer, Items = items, Quantities = quantities ?? new Dictionary<Guid, int>(),
                Fulfilment = fulfilment, Editing = editing, Errors = errors, Preview = preview && (errors == null || errors.Count == 0)
            });
        }

        /// <summary>Reads q_{bookId} fields. Blank means 0.</summary>
        private Dictionary<Guid, int> ReadQuantities(List<string> errors)
        {
            var result = new Dictionary<Guid, int>();
            foreach (var key in Request.Form.AllKeys.Where(k => k != null && k.StartsWith("q_", StringComparison.Ordinal)))
            {
                Guid id;
                if (!Guid.TryParse(key.Substring(2), out id)) continue;
                var raw = (Request.Form[key] ?? "").Trim();
                if (raw.Length == 0) { result[id] = 0; continue; }
                int n;
                if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out n) || n > BookRepository.MaxQuantityPerBook)
                {
                    if (errors.Count == 0) errors.Add("Please enter whole numbers only in the quantity boxes (0 to " + BookRepository.MaxQuantityPerBook + ").");
                    result[id] = 0;
                    continue;
                }
                result[id] = n;
            }
            return result;
        }

        private static BookOrder OwnOrder(BookCustomer customer, long orderNo)
        {
            var order = BookRepository.Order(orderNo);
            return order != null && order.CustomerId == customer.Id ? order : null;
        }

        private ActionResult StartError(string message, string mobile)
        {
            ViewBag.Error = message;
            ViewBag.Mobile = mobile;
            return View("~/Views/Books/Index.cshtml", Meta());
        }

        private PageMeta Meta()
        {
            return new PageMeta { Title = "Books | The Arqam Schools", Description = "Order books from The Arqam Schools.", CanonicalPath = "/books", NoIndex = true };
        }

        private void SetCookie(Guid customerId)
        {
            var payload = customerId.ToString("N") + "|" + DateTime.UtcNow.Add(Lifetime).Ticks.ToString(CultureInfo.InvariantCulture);
            var value = Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(payload), Purpose));
            Response.Cookies.Add(new HttpCookie(CookieName, value)
            {
                HttpOnly = true, Path = "/books", Expires = DateTime.UtcNow.Add(Lifetime),
                Secure = Request.IsSecureConnection || Site.RequireHttps
            });
        }

        /// <summary>The customer from the cookie, while it is valid and the customer is still active; otherwise null.</summary>
        private BookCustomer CurrentCustomer()
        {
            var cookie = Request.Cookies[CookieName];
            if (cookie == null || string.IsNullOrEmpty(cookie.Value)) return null;
            try
            {
                var parts = Encoding.UTF8.GetString(MachineKey.Unprotect(Convert.FromBase64String(cookie.Value), Purpose)).Split('|');
                Guid id;
                long ticks;
                if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out id) || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out ticks)) return null;
                if (new DateTime(ticks, DateTimeKind.Utc) < DateTime.UtcNow) return null;
                var c = BookRepository.Customer(id);
                return c != null && c.IsActive ? c : null;
            }
            catch (FormatException) { return null; }
            catch (System.Security.Cryptography.CryptographicException) { return null; }
            catch (ArgumentException) { return null; }
        }
    }
}
