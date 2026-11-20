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
    /// <summary>/admin/books part 3: returns (books.payments, because money is paid back) and reports (books.reports).</summary>
    public partial class AdminBooksController
    {
        /// <summary>GET /admin/books/returns/{no}: the printable return note.</summary>
        [HttpGet]
        public ActionResult ReturnNote(long returnNo)
        {
            var r = BookReturnRepository.Get(returnNo);
            if (r == null) return HttpNotFound();
            return View("~/Views/Admin/Books/ReturnNote.cshtml", r);
        }

        /// <summary>GET /admin/books/reports?kind=…&amp;from=…&amp;to=…&amp;format=pdf|xlsx</summary>
        [HttpGet]
        [AdminAuthorize("books.reports")]
        public ActionResult Reports(string kind, string from, string to, string format)
        {
            var today = DateTime.UtcNow.Add(PakistanTime).Date;
            var f = ParseDate(from) ?? new DateTime(today.Year, today.Month, 1);
            var t = ParseDate(to) ?? today;
            if (t < f) { var x = f; f = t; t = x; }
            if ((t - f).TotalDays > 3660) f = t.AddDays(-3660);
            ViewBag.Kind = BookReports.Kinds.Any(k => k[0] == kind) ? kind : null;
            ViewBag.From = f;
            ViewBag.To = t;
            if (ViewBag.Kind == null) return View("~/Views/Admin/Books/Reports.cshtml", (ReportTable)null);
            var table = BookReports.Build(kind, f, t);
            var name = "books-" + kind + (BookReports.NeedsDates(kind) ? "-" + f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "-to-" + t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "");
            Response.Headers["Cache-Control"] = "private, no-store";
            if (format == "pdf") return File(table.ToPdf(), "application/pdf", name + ".pdf");
            if (format == "xlsx") return File(table.ToXlsx(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx");
            return View("~/Views/Admin/Books/Reports.cshtml", table);
        }
    }
}
