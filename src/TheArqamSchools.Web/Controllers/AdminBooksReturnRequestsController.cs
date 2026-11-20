using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>/admin/books/return-requests: customers' return requests from the website (approve, reject, receive).</summary>
    public partial class AdminBooksController
    {
        [HttpGet]
        public ActionResult ReturnRequests(string status)
        {
            status = new[] { "requested", "approved", "rejected", "received" }.Contains(status) ? status : null;
            ViewBag.Status = status;
            return View("~/Views/Admin/Books/ReturnRequests.cshtml", BookReturnRequestRepository.List(status));
        }

        [HttpGet]
        public ActionResult ReturnRequest(long returnNo, string notice)
        {
            var r = BookReturnRequestRepository.Get(returnNo);
            if (r == null) return HttpNotFound();
            return ReturnRequestView(r, notice == "approved" ? "Approved. The customer has been told (see Messages) to send the books."
                                       : notice == "rejected" ? "Marked as not approved." : notice == "received" ? "Books received and the return recorded." : null, null);
        }

        private ActionResult ReturnRequestView(BookReturnRequestRepository.Request r, string notice, string error)
        {
            ViewBag.Notice = notice;
            ViewBag.Error = error;
            ViewBag.CanPay = AdminAuth.Current(HttpContext).Can("books.payments");
            ViewBag.Available = BookReturnRepository.ReturnableBooks(r.CustomerId).ToDictionary(b => b.ItemId, b => b.Available);
            return View("~/Views/Admin/Books/ReturnRequest.cshtml", r);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("books.payments")]
        public ActionResult ReturnRequestAction(long returnNo, string act)
        {
            var r = BookReturnRequestRepository.Get(returnNo);
            if (r == null) return HttpNotFound();
            var actor = AdminAuth.Current(HttpContext).Id;
            var form = Request.Unvalidated.Form;
            try
            {
                if (act == "approve") BookReturnRequestRepository.Approve(returnNo, actor);
                else if (act == "reject")
                {
                    var reason = Trim(form["Reason"]);
                    if (reason != null && reason.Length > 300) reason = reason.Substring(0, 300);
                    BookReturnRequestRepository.Reject(returnNo, reason, actor);
                }
                else if (act == "receive")
                {
                    var qty = new Dictionary<Guid, int>();
                    foreach (var key in form.AllKeys.Where(k => k != null && k.StartsWith("r_", StringComparison.Ordinal)))
                    {
                        Guid itemId; int n;
                        var raw = (form[key] ?? "").Trim();
                        if (!Guid.TryParse(key.Substring(2), out itemId) || raw.Length == 0) continue;
                        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out n)) return ReturnRequestView(r, null, "Please enter whole numbers.");
                        var asked = r.Lines.Where(l => l.ItemId == itemId).Sum(l => l.Quantity);
                        if (n > asked) return ReturnRequestView(r, null, "More books than the customer asked to return; please record the extra books as a separate return.");
                        qty[itemId] = n;
                    }
                    BookReturnRequestRepository.Receive(returnNo, qty, ParseDate(form["ReceivedOn"]) ?? DateTime.UtcNow.Add(PakistanTime).Date, actor);
                }
                else return HttpNotFound();
            }
            catch (BookOrderException ex) { return ReturnRequestView(BookReturnRequestRepository.Get(returnNo), null, ex.Message); }
            return Redirect("/admin/books/return-requests/" + returnNo.ToString(CultureInfo.InvariantCulture) + "?notice=" + (act == "approve" ? "approved" : act == "reject" ? "rejected" : "received"));
        }
    }
}
