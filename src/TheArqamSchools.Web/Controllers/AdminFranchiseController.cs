using System;
using System.Globalization;
using System.Text;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/franchise: franchise applications. franchise.view to read, franchise.update
    /// to change the status (kept in franchise_status_history and the audit log),
    /// franchise.export for the CSV download (audited).
    /// </summary>
    [AdminAuthorize("franchise.view")]
    public class AdminFranchiseController : Controller
    {
        private static readonly TimeSpan PakistanOffset = TimeSpan.FromHours(5);

        [HttpGet]
        public ActionResult Index(string status)
        {
            var q = Request.Unvalidated.QueryString["q"];
            ViewBag.Status = Array.IndexOf(FranchiseRepository.Statuses, status) >= 0 ? status : null;
            ViewBag.Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            ViewBag.CanExport = AdminAuth.Current(HttpContext).Can("franchise.export");
            return View("~/Views/Admin/Franchise/Index.cshtml", FranchiseRepository.List(status, q));
        }

        [HttpGet]
        public ActionResult Detail(Guid id, bool saved = false)
        {
            var row = FranchiseRepository.Get(id);
            if (row == null) return HttpNotFound();
            ViewBag.Saved = saved;
            ViewBag.CanUpdate = AdminAuth.Current(HttpContext).Can("franchise.update");
            return View("~/Views/Admin/Franchise/Detail.cshtml", row);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("franchise.update")]
        public ActionResult Status(Guid id, string status)
        {
            if (Array.IndexOf(FranchiseRepository.Statuses, status) < 0) return new HttpStatusCodeResult(400);
            if (!FranchiseRepository.SetStatus(id, status, AdminAuth.Current(HttpContext).Id)) return HttpNotFound();
            return Redirect("/admin/franchise/" + id.ToString("D") + "?saved=true");
        }

        [HttpGet]
        [AdminAuthorize("franchise.export")]
        public ActionResult Export(string status)
        {
            var rows = FranchiseRepository.List(status, Request.Unvalidated.QueryString["q"]);
            var csv = new StringBuilder();
            csv.AppendLine("Reference,Received (Pakistan time),Status,Name,Mobile,WhatsApp,Email,City,Province,Proposed city,Proposed area,Building,Building size,Start,Experience,Message");
            foreach (var r in rows)
                csv.AppendLine(Line(r.ReferenceNo, r.SubmittedAt.ToOffset(PakistanOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    FranchiseRepository.Label(r.Status), r.FullName, r.Mobile, r.Whatsapp, r.Email, r.City, r.Province, r.ProposedCity,
                    r.ProposedArea, r.BuildingPlan, r.BuildingArea, r.StartTimeline, r.Experience, r.Message));
            using (var db = Db.Open())
                AuditLog.Write(db, null, AdminAuth.Current(HttpContext).Id, "update", "franchise_applications", null,
                    new { @event = "csv_export", rows = rows.Count });
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(csv.ToString());
            var all = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, all, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, all, preamble.Length, body.Length);
            return File(all, "text/csv; charset=utf-8", "franchise-applications-" + DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv");
        }

        /// <summary>One CSV line; cells that start like a formula get a leading ' so Excel shows them as text.</summary>
        private static string Line(params string[] cells)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var v = cells[i] ?? "";
                if (v.Length > 0 && "=+-@\t\r".IndexOf(v[0]) >= 0) v = "'" + v;
                sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
            }
            return sb.ToString();
        }
    }
}
