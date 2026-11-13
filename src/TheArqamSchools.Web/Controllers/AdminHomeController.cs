using System.Collections.Generic;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Helpers;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/home: the homepage's words and pictures (owner, 13 November 2026), kept in the site
    /// setting "home.content". pages.view to look; pages.publish to save, because the homepage
    /// changes on the website at once (there is no draft for it).
    /// </summary>
    [AdminAuthorize("pages.view")]
    public class AdminHomeController : Controller
    {
        [HttpGet]
        public ActionResult Index(string notice)
        {
            return EditView(Load(), null, notice == "saved" ? "Saved. The homepage shows it now." : notice == "list" ? "Saved. The list was changed." : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("pages.publish")]
        [ActionName("Index")]
        public ActionResult IndexPost()
        {
            var actor = AdminAuth.Current(HttpContext);
            var root = Load();
            var result = ContentEditor.Process(root, "home", Request);
            if (result.Errors.Count > 0) return EditView(root, result, null);
            try { SettingsAdminRepository.Save(new Dictionary<string, string> { { HomeContent.SettingKey, root.ToString(Formatting.None) } }, actor.Id); }
            catch { foreach (var f in result.NewFiles) ImageStore.Delete(f); throw; }
            return Redirect("/admin/home?notice=" + (result.Anchor != null ? "list" + result.Anchor : "saved"));
        }

        /// <summary>The stored content merged over the built-in copy, so every field is there to edit.</summary>
        private static JObject Load()
        {
            var raw = SettingsAdminRepository.RawValue(HomeContent.SettingKey);
            JToken stored = null;
            try { stored = string.IsNullOrEmpty(raw) ? null : JToken.Parse(raw); }
            catch (JsonReaderException) { }
            return HomeContent.Get(new Dictionary<string, JToken> { { HomeContent.SettingKey, stored } });
        }

        private ActionResult EditView(JObject root, ContentEditor.Result result, string notice)
        {
            var actor = AdminAuth.Current(HttpContext);
            var fresh = JsonContentFields.Flatten(Load(), "home");
            return View("~/Views/Admin/Home/Index.cshtml", new SectionEditViewModel
            {
                Fields = result != null && result.PostedFields != null ? result.PostedFields : fresh,
                Signature = JsonContentFields.Signature(fresh),
                CanEdit = actor.Can("pages.publish"), CanPublish = actor.Can("pages.publish"),
                Notice = notice, Errors = result == null ? null : result.Errors
            });
        }
    }
}
