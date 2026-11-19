using System;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/pages: CMS pages and their sections. pages.view to look, pages.update to
    /// change titles and save section drafts, pages.publish to publish (pages and
    /// section content) and to show or hide sections. Page addresses are not editable.
    /// </summary>
    [AdminAuthorize("pages.view")]
    public class AdminPagesController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View("~/Views/Admin/Pages/Index.cshtml", PageAdminRepository.List());
        }

        [HttpGet]
        public ActionResult Edit(Guid id, string notice)
        {
            var page = PageAdminRepository.Get(id);
            if (page == null) return HttpNotFound();
            return PageView(page, notice == "saved" ? "Page saved." : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("pages.update")]
        [ActionName("Edit")]
        public ActionResult EditPost(Guid id, PageEditForm form)
        {
            var page = PageAdminRepository.Get(id);
            if (page == null) return HttpNotFound();
            if (form == null) form = new PageEditForm();
            form.Id = id;
            form.Path = page.Path;
            form.Sections = page.Sections;
            form.Title = Trim(form.Title);
            form.Summary = Trim(form.Summary);
            form.MetaTitle = Trim(form.MetaTitle);
            form.MetaDescription = Trim(form.MetaDescription);
            if (!PageAdminRepository.Statuses.Contains(form.Status)) form.Status = page.Status;
            if (!ModelState.IsValid) return PageView(form, null);

            var actor = AdminAuth.Current(HttpContext);
            PageAdminRepository.SavePage(form, actor.Can("pages.publish"), actor.Id);
            return Redirect("/admin/pages/" + id.ToString("D") + "?notice=saved");
        }

        [HttpGet]
        public ActionResult Section(Guid pageId, Guid sectionId, string notice)
        {
            var section = PageAdminRepository.GetSection(pageId, sectionId);
            if (section == null) return HttpNotFound();
            var message = notice == "published" ? "Saved and published. The website shows it now."
                : notice == "draft" ? "Saved as a draft. A Content Manager or Super Admin can publish it."
                : notice == "draft-published" ? "The draft is published."
                : notice == "draft-discarded" ? "The draft was discarded."
                : notice == "visibility" ? "Saved."
                : notice == "list" ? "Saved and published. The list was changed."
                : notice == "list-draft" ? "Saved as a draft. The list was changed." : null;
            return SectionView(section, null, message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("pages.update")]
        [ActionName("Section")]
        public ActionResult SectionPost(Guid pageId, Guid sectionId)
        {
            var section = PageAdminRepository.GetSection(pageId, sectionId);
            if (section == null) return HttpNotFound();
            var actor = AdminAuth.Current(HttpContext);
            var live = actor.Can("pages.publish");
            var root = Parse(live ? section.Data : (section.DraftData ?? section.Data));

            // Every value is read unvalidated (content may contain "<"), stored as JSON text
            // and HTML-encoded by the section templates when shown.
            var result = ContentEditor.Process(root, section.Type, Request);
            if (result.Errors.Count > 0) return SectionView(section, result.Errors, null, result.PostedFields);

            try { PageAdminRepository.SaveSection(sectionId, root.ToString(Formatting.None), live, actor.Id); }
            catch { foreach (var f in result.NewFiles) ImageStore.Delete(f); throw; }
            if (result.Anchor != null)
                return Redirect(SectionUrl(pageId, sectionId) + "?notice=" + (live ? "list" : "list-draft") + result.Anchor);
            return Redirect(SectionUrl(pageId, sectionId) + "?notice=" + (live ? "published" : "draft"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("pages.publish")]
        public ActionResult PublishDraft(Guid pageId, Guid sectionId)
        {
            if (PageAdminRepository.GetSection(pageId, sectionId) == null) return HttpNotFound();
            PageAdminRepository.PublishDraft(sectionId, AdminAuth.Current(HttpContext).Id);
            return Redirect(SectionUrl(pageId, sectionId) + "?notice=draft-published");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("pages.update")]
        public ActionResult DiscardDraft(Guid pageId, Guid sectionId)
        {
            if (PageAdminRepository.GetSection(pageId, sectionId) == null) return HttpNotFound();
            PageAdminRepository.DiscardDraft(sectionId, AdminAuth.Current(HttpContext).Id);
            return Redirect(SectionUrl(pageId, sectionId) + "?notice=draft-discarded");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("pages.publish")]
        public ActionResult Visibility(Guid pageId, Guid sectionId, bool visible)
        {
            if (PageAdminRepository.GetSection(pageId, sectionId) == null) return HttpNotFound();
            PageAdminRepository.SetSectionVisible(sectionId, visible, AdminAuth.Current(HttpContext).Id);
            return Redirect(SectionUrl(pageId, sectionId) + "?notice=visibility");
        }

        // ---- Helpers --------------------------------------------------------------------

        private ActionResult PageView(PageEditForm form, string notice)
        {
            var actor = AdminAuth.Current(HttpContext);
            return View("~/Views/Admin/Pages/Edit.cshtml", new PageEditViewModel
            {
                Form = form, Notice = notice, CanEdit = actor.Can("pages.update"), CanPublish = actor.Can("pages.publish")
            });
        }

        private ActionResult SectionView(SectionContent section, System.Collections.Generic.IList<string> errors, string notice,
            System.Collections.Generic.IList<JsonContentFields.Field> postedFields = null)
        {
            var actor = AdminAuth.Current(HttpContext);
            var canPublish = actor.Can("pages.publish");
            // Publishers edit the live content (a pending draft is shown separately); others continue the draft.
            var showingDraft = !canPublish && section.DraftData != null;
            var fields = JsonContentFields.Flatten(Parse(showingDraft ? section.DraftData : section.Data), section.Type);
            var signature = JsonContentFields.Signature(fields);
            return View("~/Views/Admin/Pages/Section.cshtml", new SectionEditViewModel
            {
                Section = section, Fields = postedFields ?? fields, Signature = signature, ShowingDraft = showingDraft,
                CanEdit = actor.Can("pages.update"), CanPublish = canPublish, Notice = notice, Errors = errors
            });
        }

        private static JObject Parse(string json)
        {
            try { return JObject.Parse(string.IsNullOrEmpty(json) ? "{}" : json); }
            catch (JsonReaderException) { return new JObject(); }
        }

        private static string SectionUrl(Guid pageId, Guid sectionId)
        {
            return "/admin/pages/" + pageId.ToString("D") + "/sections/" + sectionId.ToString("D");
        }

        private static string Trim(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
    }
}
