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
    /// Admin inbox for what visitors send from the website: /admin/inquiries
    /// (admission inquiries) and /admin/messages (other messages). Viewing and the
    /// CSV download need "inquiries.view"; changing a status needs "inquiries.update".
    /// </summary>
    [AdminAuthorize("inquiries.view")]
    public class AdminInboxController : Controller
    {
        private static readonly TimeZoneInfo Pakistan = FindPakistanTime();

        [HttpGet]
        public ActionResult Index(string kind, string status, int page = 1)
        {
            // The search box is read unvalidated (it may contain "<") and only ever used as a parameter.
            var k = Kind(kind);
            var model = InboxRepository.List(k, status, Request.Unvalidated.QueryString["q"], page);
            model.CanUpdate = AdminAuth.Current(HttpContext).Can("inquiries.update");
            ViewBag.Pakistan = Pakistan;
            return View("~/Views/Admin/Inbox/Index.cshtml", model);
        }

        [HttpGet]
        public ActionResult Detail(string kind, Guid id, bool saved = false)
        {
            var k = Kind(kind);
            var item = InboxRepository.Get(k, id);
            if (item == null) return HttpNotFound();
            ViewBag.Pakistan = Pakistan;
            return View("~/Views/Admin/Inbox/Detail.cshtml", new InboxDetailViewModel
            {
                Kind = k, Item = item, Saved = saved,
                CanUpdate = AdminAuth.Current(HttpContext).Can("inquiries.update")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("inquiries.update")]
        public ActionResult Status(string kind, Guid id, string status)
        {
            var k = Kind(kind);
            if (Array.IndexOf(InboxRepository.Statuses, status) < 0) return new HttpStatusCodeResult(400);
            var user = AdminAuth.Current(HttpContext);
            if (!InboxRepository.SetStatus(k, id, status, user.Id)) return HttpNotFound();
            var basePath = k == InboxKind.Inquiries ? "/admin/inquiries" : "/admin/messages";
            return Redirect(basePath + "/" + id.ToString("D") + "?saved=true");
        }

        /// <summary>
        /// CSV download (opens in Excel) with the same filters as the list. Cells that
        /// start with = + - @ are prefixed with ' so a spreadsheet cannot run them as formulas.
        /// </summary>
        [HttpGet]
        public ActionResult Export(string kind, string status)
        {
            var k = Kind(kind);
            var rows = InboxRepository.Export(k, status, Request.Unvalidated.QueryString["q"]);
            var csv = new StringBuilder();
            var inquiries = k == InboxKind.Inquiries;
            csv.AppendLine(inquiries
                ? "Received (Pakistan time),Status,Name,Phone,Email,Campus,Class,Message,Page"
                : "Received (Pakistan time),Status,Type,Name,Phone,Email,Campus,Subject,Message,Page");
            foreach (var r in rows)
            {
                var received = TimeZoneInfo.ConvertTime(r.CreatedAt, Pakistan).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                csv.AppendLine(inquiries
                    ? Line(received, InboxRepository.Label(r.Status), r.Name, r.Phone, r.Email, r.CampusName, r.ClassName, r.Message, r.SourcePage)
                    : Line(received, InboxRepository.Label(r.Status), r.Type, r.Name, r.Phone, r.Email, r.CampusName, r.Subject, r.Message, r.SourcePage));
            }
            using (var db = Db.Open())
                AuditLog.Write(db, null, AdminAuth.Current(HttpContext).Id, "update", inquiries ? "admission_inquiries" : "contact_messages", null,
                    new { @event = "csv_export", rows = rows.Count, status = Array.IndexOf(InboxRepository.Statuses, status) >= 0 ? status : null });
            var name = (inquiries ? "admission-inquiries-" : "messages-") + DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv";
            // UTF-8 with BOM so Excel shows Urdu names correctly.
            var bytes = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(csv.ToString());
            var all = new byte[bytes.Length + body.Length];
            Buffer.BlockCopy(bytes, 0, all, 0, bytes.Length);
            Buffer.BlockCopy(body, 0, all, bytes.Length, body.Length);
            return File(all, "text/csv; charset=utf-8", name);
        }

        private static InboxKind Kind(string kind)
        {
            return kind == "messages" ? InboxKind.Messages : InboxKind.Inquiries;
        }

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

        private static TimeZoneInfo FindPakistanTime()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "Pakistan", "Pakistan"); }
        }
    }
}
