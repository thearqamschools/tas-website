using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// Holding homepage in the new design. The full homepage (slider, sections from
    /// dbo.homepage_content) is built in the rest of phase 2.
    /// </summary>
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var settings = SiteSettingsRepository.Public();
            ViewBag.Tagline = SiteSettingsRepository.Text(settings, "site.tagline");
            var seo = PageRepository.SeoForRoute("/");
            ViewBag.Meta = new PageMeta
            {
                Title = seo != null && !string.IsNullOrWhiteSpace(seo.MetaTitle) ? seo.MetaTitle : "The Arqam Schools",
                Description = seo != null ? seo.MetaDescription : SiteSettingsRepository.Text(settings, "site.description"),
                CanonicalPath = "/",
            };
            return View();
        }
    }
}
