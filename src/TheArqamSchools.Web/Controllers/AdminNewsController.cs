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
            if (kind == "news") f.StartsAt = DateTimeOffset.UtcNow.ToOffset(Pakistan);
            // A new event starts without a time (the time is optional); the date defaults to a week from today.
            if (kind == "event") { f.StartsAt = new DateTimeOffset(DateTimeOffset.UtcNow.ToOffset(Pakistan).Date.AddDays(7), Pakistan); f.IsAllDay = true; }
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
        public ActionResult Edit(string kind, Guid id, bool saved = false, int added = 0, string photo = null)
        {
            kind = kind == "event" ? "event" : "news";
            if (!AdminAuth.Current(HttpContext).Can(Perm(kind, "view"))) return Forbidden();
            var f = NewsRepository.Get(kind, id);
            if (f == null) return HttpNotFound();
            // Photo messages come back as fixed codes in the address (there is no session state), never as free text.
            if (added > 0) ViewBag.PhotoNotice = Math.Min(added, PostImageRepository.MaxPhotosPerPost) + (added == 1 ? " photo added." : " photos added.");
            switch (photo)
            {
                case "cover": ViewBag.PhotoNotice = "Main photo changed."; break;
                case "moved": ViewBag.PhotoNotice = "Photo moved."; break;
                case "removed": ViewBag.PhotoNotice = "Photo removed."; break;
                case "rejected": ViewBag.PhotoError = (added > 0 ? "Some photos were not added. " : "") + "Only JPG and PNG photos up to 15 MB can be uploaded."; break;
                case "limit": ViewBag.PhotoError = "The most photos allowed is " + PostImageRepository.MaxPhotosPerPost + "."; break;
                case "none": ViewBag.PhotoError = "Please choose one or more photos."; break;
            }
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
            if (f.Kind == "news")
            {
                // The news date (shown on the website) can be changed; time is not asked for.
                var date = ParseLocal(posted["StartDate"], null);
                if (date == null) ModelState.AddModelError("StartDate", "Please enter the date of the news.");
                else
                {
                    var stored = isNew ? null : NewsRepository.Get(f.Kind, f.Id);
                    var today = DateTimeOffset.UtcNow.ToOffset(Pakistan);
                    var unchanged = stored != null && stored.StartsAt.HasValue && stored.StartsAt.Value.ToOffset(Pakistan).Date == date.Value.Date;
                    if (unchanged && stored.Status != "published" && f.Status == "published")
                        f.StartsAt = today; // a draft published without changing its date gets the publishing day
                    else if (unchanged)
                        f.StartsAt = stored.StartsAt; // same day: keep the exact time it was published
                    else if (date.Value.Date == today.Date)
                        f.StartsAt = today; // today: now, so it comes first among today's posts
                    else
                        f.StartsAt = date;
                }
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

            Guid id;
            try { id = NewsRepository.Save(f, isNew, actor.Can(Perm(f.Kind, "publish")), actor.Id); }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError("", "This " + (f.Kind == "event" ? "event" : "post") + " was published a moment ago. Please ask a Content Manager to change it.");
                return EditView(f, isNew, null);
            }
            Cache.Remove("home:news");
            return Redirect("/admin/news/" + f.Kind + "/" + id.ToString("D") + "?saved=true");
        }

        /// <summary>
        /// POST /admin/news/{kind}/{id}/photos: adds one or more photos. The edit page sends one
        /// photo per request with JavaScript (so 30 or more photos can be added with a progress
        /// count) and asks for JSON; without JavaScript the form posts them all and is redirected.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Photos(string kind, Guid id)
        {
            kind = kind == "event" ? "event" : "news";
            var actor = AdminAuth.Current(HttpContext);
            var wantsJson = (Request.Headers["Accept"] ?? "").Contains("application/json");
            var post = NewsRepository.Get(kind, id);
            if (post == null) return HttpNotFound();
            var refusal = PhotoRefusal(actor, post);
            if (refusal != null) return PhotoResult(wantsJson, kind, id, 403, refusal, 0, null);

            var files = new List<System.Web.HttpPostedFileBase>();
            for (var i = 0; i < Request.Files.Count; i++)
            {
                var file = Request.Files[i];
                if (file != null && file.ContentLength > 0 && Request.Files.AllKeys[i] == "photos") files.Add(file);
            }
            if (files.Count == 0) return PhotoResult(wantsJson, kind, id, 400, "Please choose one or more photos.", 0, "none");

            var added = 0;
            string error = null, code = null;
            var alt = "Photo from " + post.Title;
            if (alt.Length > 250) alt = alt.Substring(0, 250);
            foreach (var file in files)
            {
                StoredImage img;
                try { img = ImageStore.Save(file.InputStream, file.ContentLength, file.FileName, kind == "event" ? "events" : "news"); }
                catch (ImageRejectedException ex) { error = ex.Message; code = "rejected"; continue; }
                bool ok;
                try { ok = PostImageRepository.Add(kind, id, img, alt, actor.Can(Perm(kind, "publish")), actor.Id); }
                catch (InvalidOperationException)
                {
                    ImageStore.Delete(img.StoragePath);
                    return PhotoResult(wantsJson, kind, id, 403, "This " + What(kind) + " was published a moment ago. Please ask a Content Manager to change its photos.", added, null);
                }
                catch { ImageStore.Delete(img.StoragePath); throw; }
                if (!ok)
                {
                    ImageStore.Delete(img.StoragePath);
                    error = "This " + What(kind) + " already has " + PostImageRepository.MaxPhotosPerPost + " photos, the most allowed.";
                    code = "limit";
                    break;
                }
                added++;
            }
            if (added > 0) Cache.Remove("home:news");
            if (added == 0) return PhotoResult(wantsJson, kind, id, 400, error ?? "The photos could not be added.", 0, code);
            return PhotoResult(wantsJson, kind, id, 200, error, added, code);
        }

        /// <summary>POST /admin/news/{kind}/{id}/photos/{photoId}/{op}: main photo, move earlier or later, or remove.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Photo(string kind, Guid id, Guid photoId, string op)
        {
            kind = kind == "event" ? "event" : "news";
            var actor = AdminAuth.Current(HttpContext);
            var post = NewsRepository.Get(kind, id);
            if (post == null) return HttpNotFound();
            var refusal = PhotoRefusal(actor, post);
            if (refusal != null) return PhotoResult(false, kind, id, 403, refusal, 0, null);
            bool ok;
            string done;
            var canPublish = actor.Can(Perm(kind, "publish"));
            switch (op)
            {
                case "cover": ok = PostImageRepository.SetCover(kind, id, photoId, canPublish, actor.Id); done = "cover"; break;
                case "up": ok = PostImageRepository.Move(kind, id, photoId, -1, canPublish, actor.Id); done = "moved"; break;
                case "down": ok = PostImageRepository.Move(kind, id, photoId, 1, canPublish, actor.Id); done = "moved"; break;
                case "remove":
                    var path = PostImageRepository.Remove(kind, id, photoId, canPublish, actor.Id);
                    ok = path != null;
                    // After the database change: take the files out of the public folder (kept in App_Data, not deleted).
                    if (ok) ImageStore.Archive(path);
                    done = "removed";
                    break;
                default: return HttpNotFound();
            }
            if (!ok) return HttpNotFound();
            Cache.Remove("home:news");
            return Redirect("/admin/news/" + kind + "/" + id.ToString("D") + "?photo=" + done + "#photos");
        }

        /// <summary>Same rule as the text: without the publish permission, only drafts can be changed.</summary>
        private string PhotoRefusal(Models.AdminPrincipal actor, NewsEditForm post)
        {
            if (!actor.Can(Perm(post.Kind, "update"))) return "You do not have permission to change this " + What(post.Kind) + ".";
            if (post.Status != "draft" && !actor.Can(Perm(post.Kind, "publish")))
                return "This " + What(post.Kind) + " is " + (post.Status == "published" ? "on the website" : post.Status == "archived" ? "archived" : "waiting for review") + ". Please ask a Content Manager to change its photos.";
            return null;
        }

        private ActionResult PhotoResult(bool json, string kind, Guid id, int status, string message, int added, string code)
        {
            if (json)
            {
                Response.StatusCode = status;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { ok = status == 200, added, message, stop = code == "limit" || status == 403 });
            }
            if (status == 403)
            {
                Response.StatusCode = 403;
                Response.TrySkipIisCustomErrors = true;
                return View("~/Views/Admin/Forbidden.cshtml");
            }
            var query = "?added=" + added + (code != null ? "&photo=" + code : "");
            return Redirect("/admin/news/" + kind + "/" + id.ToString("D") + query + "#photos");
        }

        private static string What(string kind) { return kind == "event" ? "event" : "post"; }

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
                // A news draft without a date shows today, ready to be changed.
                StartDate = f.StartsAt.HasValue ? f.StartsAt.Value.ToOffset(Pakistan).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : f.Kind == "news" ? DateTimeOffset.UtcNow.ToOffset(Pakistan).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                StartTime = f.StartsAt.HasValue && !f.IsAllDay ? f.StartsAt.Value.ToOffset(Pakistan).ToString("HH:mm", CultureInfo.InvariantCulture) : "",
                EndDate = f.EndsAt.HasValue ? f.EndsAt.Value.ToOffset(Pakistan).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                Photos = isNew || f.Id == Guid.Empty ? new List<PostImage>() : PostImageRepository.List(f.Kind, f.Id),
                CanEditPhotos = !isNew && PhotoRefusal(actor, NewsRepository.Get(f.Kind, f.Id) ?? f) == null,
                EndTime = f.EndsAt.HasValue && (!f.IsAllDay || f.EndsAt.Value.ToOffset(Pakistan).TimeOfDay != TimeSpan.Zero) ? f.EndsAt.Value.ToOffset(Pakistan).ToString("HH:mm", CultureInfo.InvariantCulture) : "",
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
