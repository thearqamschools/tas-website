using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Helpers;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/books: the books system (owner's specification). Part 1: books and stock (books.stock),
    /// customers (books.customers). Everyone with books.view can look. Orders, payments, invoices,
    /// returns and reports follow in the next parts.
    /// </summary>
    [AdminAuthorize("books.view")]
    public partial class AdminBooksController : Controller
    {
        private static readonly TimeSpan Pakistan = TimeSpan.FromHours(5);

        [HttpGet]
        public ActionResult Index()
        {
            var items = BookRepository.Items(true);
            ViewBag.ItemCount = items.Count(i => i.IsActive);
            var stocked = BookRepository.StockedItemIds();
            ViewBag.LowStock = items.Where(i => i.IsActive && stocked.Contains(i.Id) && i.Stock <= i.WarnAt).OrderBy(i => i.Stock).ToList();
            ViewBag.NeverStocked = items.Count(i => i.IsActive && !stocked.Contains(i.Id));
            ViewBag.CustomerCount = BookRepository.CustomerCount();
            return View("~/Views/Admin/Books/Index.cshtml");
        }

        // ---- Books and stock ------------------------------------------------------------

        [HttpGet]
        public ActionResult Items()
        {
            ViewBag.CanEdit = AdminAuth.Current(HttpContext).Can("books.stock");
            ViewBag.Stocked = BookRepository.StockedItemIds();
            return View("~/Views/Admin/Books/Items.cshtml", BookRepository.Items(true));
        }

        [HttpGet]
        [AdminAuthorize("books.stock")]
        public ActionResult NewItem()
        {
            return ItemView(new BookItemForm { IsActive = true }, true, 0, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.stock")]
        [ActionName("NewItem")]
        public ActionResult NewItemPost(BookItemForm form)
        {
            form.Id = Guid.Empty;
            return SaveItem(form, true);
        }

        [HttpGet]
        public ActionResult Item(Guid id, string notice)
        {
            var it = BookRepository.Item(id);
            if (it == null) return HttpNotFound();
            var f = FormFor(it);
            return ItemView(f, false, it.Stock, notice == "saved" ? "Saved." : notice == "stock" ? "Stock updated." : notice == "level" ? "Minimum stock level saved." : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.stock")]
        [ActionName("Item")]
        public ActionResult ItemPost(Guid id, BookItemForm form)
        {
            if (BookRepository.Item(id) == null) return HttpNotFound();
            form.Id = id;
            return SaveItem(form, false);
        }

        private ActionResult SaveItem(BookItemForm f, bool isNew)
        {
            f.ClassName = Trim(f.ClassName);
            f.Name = Trim(f.Name);
            f.Vendor = Trim(f.Vendor);
            f.RetailPrice = LooseNumber(Request.Form["RetailPrice"]);
            f.DiscountPercent = LooseNumber(Request.Form["DiscountPercent"]);
            if (f.RetailPrice > 0) f.RetailPrice = Math.Round(f.RetailPrice.Value, 2, MidpointRounding.AwayFromZero);
            if (f.DiscountPercent > 0) f.DiscountPercent = Math.Round(f.DiscountPercent.Value, 2, MidpointRounding.AwayFromZero);
            var existing = isNew ? null : BookRepository.Item(f.Id);
            bool classError = false, retailError = false;
            if (!string.IsNullOrEmpty(f.ClassName) && !BookFormat.Classes.Contains(f.ClassName)
                && (existing == null || existing.ClassName != f.ClassName))
                classError = true;
            // The sale price is worked out from the retail price and discount when a retail price is given.
            if (f.RetailPrice.HasValue)
            {
                if (!f.DiscountPercent.HasValue) f.DiscountPercent = 0;
                if (f.DiscountPercent >= 0 && f.DiscountPercent <= 100 && f.RetailPrice >= 0)
                    f.SalePrice = BookFormat.SaleFromRetail(f.RetailPrice.Value, f.DiscountPercent.Value);
            }
            else if (f.DiscountPercent.HasValue && f.DiscountPercent != 0)
                retailError = true;
            f.IsActive = (Request.Form["IsActive"] ?? "").Split(',').Contains("true");
            ModelState.Clear();
            TryValidateModel(f);
            if (classError) ModelState.AddModelError("ClassName", "Please choose the class from the list.");
            if (retailError) ModelState.AddModelError("RetailPrice", "Please enter the retail price printed on the book, so the discount can be worked out.");
            if (ModelState.IsValid && BookRepository.ItemNameTaken(f.ClassName, f.Name, f.Id))
                ModelState.AddModelError("Name", "This class already has a book with this name.");
            if (!ModelState.IsValid)
            {
                var stock = isNew ? 0 : BookRepository.Item(f.Id).Stock;
                return ItemView(f, isNew, stock, null);
            }
            var id = BookRepository.SaveItem(f, isNew, AdminAuth.Current(HttpContext).Id);
            return Redirect("/admin/books/items/" + id.ToString("D") + "?notice=saved");
        }

        /// <summary>POST /admin/books/items/{id}/stock: a purchase (books received) or a correction (+ or −).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.stock")]
        public ActionResult Stock(Guid id, string kind, string quantity, string date, string note)
        {
            var it = BookRepository.Item(id);
            if (it == null) return HttpNotFound();
            int qty;
            DateTime on;
            note = Trim(note);
            if (!int.TryParse((quantity ?? "").Trim().Replace('\u2212', '-').Replace('\u2013', '-'), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out qty))
                ModelState.AddModelError("quantity", "Please enter a whole number of books.");
            if (!DateTime.TryParseExact((date ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out on))
                ModelState.AddModelError("date", "Please enter the date.");
            if (note != null && note.Length > 500) ModelState.AddModelError("note", "The note can be at most 500 characters.");
            if (kind == "correction" && string.IsNullOrEmpty(note)) ModelState.AddModelError("note", "Please write why the stock is corrected.");
            if (ModelState.IsValid)
            {
                try
                {
                    BookRepository.ChangeStock(id, kind == "correction" ? "correction" : "purchase", qty, on, note, AdminAuth.Current(HttpContext).Id);
                    return Redirect("/admin/books/items/" + id.ToString("D") + "?notice=stock#stock");
                }
                catch (BookOrderException ex) { ModelState.AddModelError("quantity", ex.Message); }
            }
            var f = FormFor(it);
            return ItemView(f, false, it.Stock, null);
        }

        /// <summary>"1,200", "25%" or "" (null). A value that is not a number gives -1, which fails validation with a clear message.</summary>
        private static decimal? LooseNumber(string raw)
        {
            var v = (raw ?? "").Replace(",", "").Replace("%", "").Trim();
            if (v.Length == 0) return null;
            decimal d;
            return decimal.TryParse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out d) ? d : -1m;
        }

        private static BookItemForm FormFor(BookItem it)
        {
            return new BookItemForm
            {
                Id = it.Id, ClassName = it.ClassName, Name = it.Name, PurchasePrice = it.PurchasePrice, SalePrice = it.SalePrice,
                SortOrder = it.SortOrder, IsActive = it.IsActive, Vendor = it.Vendor, RetailPrice = it.RetailPrice, DiscountPercent = it.DiscountPercent
            };
        }

        /// <summary>POST /admin/books/items/{id}/reorder: the book's own minimum stock level (empty = the default).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.stock")]
        public ActionResult Reorder(Guid id, string level)
        {
            var it = BookRepository.Item(id);
            if (it == null) return HttpNotFound();
            int n;
            level = Trim(level);
            if (level != null && (!int.TryParse(level, NumberStyles.None, CultureInfo.InvariantCulture, out n) || n > 99999))
            {
                ModelState.AddModelError("level", "Please enter a whole number from 0 to 99999, or leave it empty for the default.");
                return ItemView(FormFor(it), false, it.Stock, null);
            }
            BookRepository.SetReorderLevel(id, level == null ? (int?)null : int.Parse(level, CultureInfo.InvariantCulture), AdminAuth.Current(HttpContext).Id);
            return Redirect("/admin/books/items/" + id.ToString("D") + "?notice=level#stock");
        }

        private ActionResult ItemView(BookItemForm f, bool isNew, int stock, string notice)
        {
            var it = isNew ? null : BookRepository.Item(f.Id);
            var classes = BookFormat.Classes.ToList();
            if (!string.IsNullOrEmpty(f.ClassName) && !classes.Contains(f.ClassName) && it != null && it.ClassName == f.ClassName) classes.Add(f.ClassName);
            return View("~/Views/Admin/Books/Item.cshtml", new BookItemEditViewModel
            {
                Form = f, IsNew = isNew, Stock = stock, Notice = notice, Classes = classes, ReorderLevel = it == null ? null : it.ReorderLevel,
                Moves = isNew ? new System.Collections.Generic.List<BookStockMove>() : BookRepository.Moves(f.Id),
                CanEdit = AdminAuth.Current(HttpContext).Can("books.stock")
            });
        }

        // ---- Customers ------------------------------------------------------------------

        [HttpGet]
        public ActionResult Customers()
        {
            var q = Request.Unvalidated.QueryString["q"];
            q = Trim(q);
            if (q != null && q.Length > 100) q = q.Substring(0, 100);
            // A mobile number typed with dashes or +92 is searched in the stored format.
            var search = Mobile.Normalize(q) ?? q;
            ViewBag.Query = q;
            ViewBag.CanEdit = AdminAuth.Current(HttpContext).Can("books.customers");
            return View("~/Views/Admin/Books/Customers.cshtml", BookRepository.Customers(search));
        }

        [HttpGet]
        [AdminAuthorize("books.customers")]
        public ActionResult NewCustomer()
        {
            return CustomerView(new BookCustomerForm { IsActive = true }, true, null, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.customers")]
        [ActionName("NewCustomer")]
        public ActionResult NewCustomerPost()
        {
            return SaveCustomer(new BookCustomerForm { Id = Guid.Empty }, true);
        }

        [HttpGet]
        public ActionResult Customer(Guid id, string notice)
        {
            var c = BookRepository.Customer(id);
            if (c == null) return HttpNotFound();
            var f = new BookCustomerForm { Id = c.Id, Mobile = Mobile.Format(c.Mobile), Name = c.Name, CampusName = c.CampusName, Address = c.Address, IsActive = c.IsActive };
            return CustomerView(f, false, c, notice == "saved" ? "Saved." : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.customers")]
        [ActionName("Customer")]
        public ActionResult CustomerPost(Guid id)
        {
            if (BookRepository.Customer(id) == null) return HttpNotFound();
            return SaveCustomer(new BookCustomerForm { Id = id }, false);
        }

        private ActionResult SaveCustomer(BookCustomerForm f, bool isNew)
        {
            var posted = Request.Unvalidated.Form;
            f.Name = Trim(posted["Name"]);
            f.CampusName = Trim(posted["CampusName"]);
            f.Address = Trim(posted["Address"]);
            f.Mobile = Trim(posted["Mobile"]);
            f.IsActive = (posted["IsActive"] ?? "").Split(',').Contains("true");
            ModelState.Clear();
            TryValidateModel(f);
            var mobile = Mobile.Normalize(f.Mobile);
            if (f.Mobile != null && mobile == null) ModelState.AddModelError("Mobile", "Please enter a Pakistani mobile number like 0300-1234567.");
            if (mobile != null && BookRepository.MobileTaken(mobile, f.Id)) ModelState.AddModelError("Mobile", "Another customer already has this mobile number.");
            if (!ModelState.IsValid) return CustomerView(f, isNew, isNew ? null : BookRepository.Customer(f.Id), null);
            f.Mobile = mobile;
            var id = BookRepository.SaveCustomer(f, isNew, AdminAuth.Current(HttpContext).Id);
            return Redirect("/admin/books/customers/" + id.ToString("D") + "?notice=saved");
        }

        private ActionResult CustomerView(BookCustomerForm f, bool isNew, BookCustomer stored, string notice)
        {
            ViewBag.IsNew = isNew;
            ViewBag.Notice = notice;
            ViewBag.Stored = stored;
            ViewBag.Orders = stored == null ? new System.Collections.Generic.List<BookOrder>() : BookRepository.CustomerOrders(stored.Id);
            ViewBag.CanEdit = AdminAuth.Current(HttpContext).Can("books.customers");
            return View("~/Views/Admin/Books/Customer.cshtml", f);
        }

        private static string Trim(string s) { return string.IsNullOrWhiteSpace(s) ? null : s.Trim(); }
    }
}
