using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/news: news posts (news.view / create / update / publish) and events
    /// (events.view / create / update / publish) for the public News &amp; Events page.
    /// Without the publish permission a post is saved as a draft. Nothing is deleted;
    /// set the status to Archived to hide a post.
    /// </summary>
    [AdminAuthorize]
    public class AdminNewsController : Controller
    {
        private static readonly TimeSpan Pakistan = TimeSpan.FromHours(5);

        [HttpGet]
        public ActionResult Index()
        {
            var user = AdminAuth.Current(HttpContext);
            if (!user.Can("news.view") && !user.Can("events.view")) return Forbidden();
            ViewBag.CanNews = user.Can("news.create");
            ViewBag.CanEvents = user.Can("events.create");
            return View("~/Views/Admin/News/Index.cshtml", NewsRepository.AdminList()
                .Where(i => i.IsEvent ? user.Can("events.view") : user.Can("news.view")).ToList());
        }

        [HttpGet]
        public ActionResult New(string kind)
        {
            kind = kind == "event" ? "event" : "news";
            if (!AdminAuth.Current(HttpContext).Can(Perm(kind, "create"))) return Forbidden();
            var f = new NewsEditForm { Kind = kind, Status = "draft", Category = "announcement" };
            if (kind == "event") f.StartsAt = new DateTimeOffset(DateTimeOffset.UtcNow.ToOffset(Pakistan).Date.AddDays(7).AddHours(9), Pakistan);
            return EditView(f, true, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("New")]
        public ActionResult NewPost(string kind)
        {
            kind = kind == "event" ? "event" : "news";
            if (!AdminAuth.Current(HttpContext).Can(Perm(kind, "create"))) return Forbidden();
            return Save(new NewsEditForm { Kind = kind, Id = Guid.Empty }, true);
        }

        [HttpGet]
        public ActionResult Edit(string kind, Guid id, bool saved = false)
        {
            kind = kind == "event" ? "event" : "news";
            if (!AdminAuth.Current(HttpContext).Can(Perm(kind, "view"))) return Forbidden();
            var f = NewsRepository.Get(kind, id);
            if (f == null) return HttpNotFound();
            return EditView(f, false, saved ? "Saved." : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Edit")]
        public ActionResult EditPost(string kind, Guid id)
        {
            kind = kind == "event" ? "event" : "news";
            if (!AdminAuth.Current(HttpContext).Can(Perm(kind, "update"))) return Forbidden();
            if (NewsRepository.Get(kind, id) == null) return HttpNotFound();
            return Save(new NewsEditForm { Kind = kind, Id = id }, false);
        }

        private ActionResult Save(NewsEditForm f, bool isNew)
        {
            var actor = AdminAuth.Current(HttpContext);
            // Text fields may contain "<" (for example "Grade <5"); they are stored as text and HTML-encoded when shown.
            var posted = Request.Unvalidated.Form;
            f.Title = Trim(posted["Title"]); f.Summary = Trim(posted["Summary"]); f.Text = (posted["Text"] ?? "").Replace("\r\n", "\n").Trim();
            f.Venue = Trim(posted["Venue"]); f.Slug = Trim(posted["Slug"]);
            f.Status = posted["Status"]; f.Category = posted["Category"];
            f.IsFeatured = (posted["IsFeatured"] ?? "").Split(',').Contains("true");
            f.IsAllDay = (posted["IsAllDay"] ?? "").Split(',').Contains("true");
            Guid branch;
            f.BranchId = Guid.TryParse(posted["BranchId"], out branch) ? branch : (Guid?)null;
            if (f.Slug != null) f.Slug = f.Slug.ToLowerInvariant();
            if (string.IsNullOrEmpty(f.Slug) && f.Title != null) f.Slug = CampusAdminRepository.Slugify(f.Title);
            ModelState.Clear();
            TryValidateModel(f);
            if (string.IsNullOrEmpty(f.Slug)) ModelState.AddModelError("Slug", "Please type a web address in English letters, for example sports-day-2026.");
            if (!NewsRepository.Statuses.Contains(f.Status)) f.Status = "draft";
            if (f.Kind == "news" && !NewsEditForm.Categories.Contains(f.Category)) ModelState.AddModelError("Category", "Please choose a category.");

            if (f.Kind == "event")
            {
                if (!TimeOk(posted["StartTime"])) ModelState.AddModelError("StartTime", "Please enter the start time as hours and minutes, for example 09:30.");
                if (!TimeOk(posted["EndTime"])) ModelState.AddModelError("EndTime", "Please enter the end time as hours and minutes, for example 13:00.");
                f.IsAllDay = string.IsNullOrWhiteSpace(posted["StartTime"]);
                f.StartsAt = ParseLocal(posted["StartDate"], posted["StartTime"]);
                f.EndsAt = string.IsNullOrWhiteSpace(posted["EndDate"]) ? (DateTimeOffset?)null : ParseLocal(posted["EndDate"], posted["EndTime"]);
                if (f.StartsAt == null) ModelState.AddModelError("StartDate", "Please enter the date the event starts.");
                if (!string.IsNullOrWhiteSpace(posted["EndDate"]) && f.EndsAt == null) ModelState.AddModelError("EndDate", "Please enter a valid end date.");
                if (f.StartsAt != null && f.EndsAt != null && f.EndsAt < f.StartsAt) ModelState.AddModelError("EndDate", "The event cannot end before it starts.");
            }
            if (!isNew && !actor.Can(Perm(f.Kind, "publish")))
            {
                // Like pages: without the publish permission, only drafts can be changed.
                var stored = NewsRepository.Get(f.Kind, f.Id);
                if (stored != null && stored.Status != "draft")
                    ModelState.AddModelError("", "This " + (f.Kind == "event" ? "event" : "post") + " is " + (stored.Status == "published" ? "on the website" : "archived") + ". Please ask a Content Manager to change it.");
            }
            if (f.BranchId.HasValue && !CampusAdminRepository.List(null).Any(c => c.Id == f.BranchId.Value)) f.BranchId = null;
            if (ModelState.IsValid && NewsRepository.SlugTaken(f.Slug, f.Id))
                ModelState.AddModelError("Slug", "Another post already uses this web address.");
            if (!ModelState.IsValid) return EditView(f, isNew, null);

            var id = NewsRepository.Save(f, isNew, actor.Can(Perm(f.Kind, "publish")), actor.Id);
            Cache.Remove("home:news");
            return Redirect("/admin/news/" + f.Kind + "/" + id.ToString("D") + "?saved=true");
        }

        private static bool TimeOk(string time)
        {
            TimeSpan t;
            return string.IsNullOrWhiteSpace(time) || TimeSpan.TryParseExact(time.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out t);
        }

        private static DateTimeOffset? ParseLocal(string date, string time)
        {
            DateTime d;
            if (!DateTime.TryParseExact((date ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return null;
            TimeSpan t;
            if (!string.IsNullOrWhiteSpace(time) && TimeSpan.TryParseExact(time.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out t)) d = d.Add(t);
            return new DateTimeOffset(d, Pakistan);
        }

        private ActionResult EditView(NewsEditForm f, bool isNew, string notice)
        {
            var actor = AdminAuth.Current(HttpContext);
            return View("~/Views/Admin/News/Edit.cshtml", new NewsEditViewModel
            {
                Form = f, IsNew = isNew, Notice = notice,
                Campuses = CampusAdminRepository.List(null).Where(c => c.ArchivedAt == null).ToList(),
                CanEdit = actor.Can(Perm(f.Kind, isNew ? "create" : "update")),
                CanPublish = actor.Can(Perm(f.Kind, "publish")),
                StartDate = f.StartsAt.HasValue ? f.StartsAt.Value.ToOffset(Pakistan).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                StartTime = f.StartsAt.HasValue && !f.IsAllDay ? f.StartsAt.Value.ToOffset(Pakistan).ToString("HH:mm", CultureInfo.InvariantCulture) : "",
                EndDate = f.EndsAt.HasValue ? f.EndsAt.Value.ToOffset(Pakistan).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                EndTime = f.EndsAt.HasValue && !f.IsAllDay ? f.EndsAt.Value.ToOffset(Pakistan).ToString("HH:mm", CultureInfo.InvariantCulture) : "",
            });
        }

        private static string Perm(string kind, string action) { return (kind == "event" ? "events." : "news.") + action; }
        private static string Trim(string s) { return string.IsNullOrWhiteSpace(s) ? null : s.Trim(); }

        private ActionResult Forbidden()
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Admin/Forbidden.cshtml");
        }
    }
}
