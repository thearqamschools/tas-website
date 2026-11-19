using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>/admin/settings: website name, contact details, header buttons, footer and social links (settings.view / settings.update).</summary>
    [AdminAuthorize("settings.view")]
    public class AdminSettingsController : Controller
    {
        private static readonly Regex PhonePattern = new Regex(@"^\+?[0-9 ()-]{7,20}$");

        [HttpGet]
        public ActionResult Index(bool saved = false)
        {
            var form = SettingsAdminRepository.Load();
            form.Saved = saved;
            return Show(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("settings.update")]
        [ActionName("Index")]
        public ActionResult Save()
        {
            // Read unvalidated: a value may contain "<"; every value is length-checked here and HTML-encoded when shown.
            var posted = Request.Unvalidated.Form;
            var form = new SettingsForm();
            var json = new Dictionary<string, string>();

            foreach (var f in SettingsAdminRepository.Fields)
            {
                if (f.Kind == SettingsAdminRepository.Kind.Link)
                {
                    var label = Clean(posted[f.Key + ".label"]);
                    var href = Clean(posted[f.Key + ".href"]);
                    form.Values[f.Key + ".label"] = label;
                    form.Values[f.Key + ".href"] = href;
                    if (label == null || href == null)
                    {
                        ModelState.AddModelError(f.Key, f.Label + ": please fill in both the button text and the link.");
                        continue;
                    }
                    if (label.Length > f.Max) ModelState.AddModelError(f.Key, f.Label + ": the button text is too long (at most " + f.Max + " characters).");
                    if (!IsLink(href)) ModelState.AddModelError(f.Key, f.Label + ": the link must start with / (a page on this website) or https://.");
                    json[f.Key] = new JObject { ["href"] = href, ["label"] = label }.ToString(Formatting.None);
                }
                else
                {
                    var value = f.Kind == SettingsAdminRepository.Kind.Multiline ? CleanMultiline(posted[f.Key]) : Clean(posted[f.Key]);
                    form.Values[f.Key] = value;
                    if (value == null) { ModelState.AddModelError(f.Key, f.Label + ": please fill this in."); continue; }
                    if (value.Length > f.Max) ModelState.AddModelError(f.Key, f.Label + ": at most " + f.Max + " characters.");
                    if (f.Kind == SettingsAdminRepository.Kind.Phone && !PhonePattern.IsMatch(value))
                        ModelState.AddModelError(f.Key, f.Label + ": please enter a valid number, for example 041-2660265.");
                    json[f.Key] = JsonConvert.SerializeObject(value);
                }
            }

            var social = new JArray();
            foreach (var platform in SettingsAdminRepository.SocialPlatforms)
            {
                var url = Clean(posted["social." + platform]);
                form.Values["social." + platform] = url;
                if (url == null) continue;
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.Length > 300 || url.Any(char.IsWhiteSpace))
                    ModelState.AddModelError("social." + platform, platform + ": please paste the full address starting with https://.");
                social.Add(new JObject { ["url"] = url, ["platform"] = SettingsAdminRepository.PlatformOf(platform) });
            }
            foreach (var kept in SettingsAdminRepository.Load().KeptSocial) social.Add(kept);
            json["social"] = social.ToString(Formatting.None);

            if (!ModelState.IsValid) return Show(form);
            SettingsAdminRepository.Save(json, AdminAuth.Current(HttpContext).Id);
            return Redirect("/admin/settings?saved=true");
        }

        private ActionResult Show(SettingsForm form)
        {
            form.CanEdit = AdminAuth.Current(HttpContext).Can("settings.update");
            return View("~/Views/Admin/Settings/Index.cshtml", form);
        }

        private static bool IsLink(string href)
        {
            if (href.Length > 300 || href.Any(char.IsWhiteSpace)) return false;
            return (href.StartsWith("/") && !href.StartsWith("//") && !href.StartsWith("/\\")) || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static string Clean(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static string CleanMultiline(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return string.Join("\n", s.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
        }
    }
}
