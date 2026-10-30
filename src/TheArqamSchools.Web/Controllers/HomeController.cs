using System.Linq;
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
            // Campus finder: only published campuses from dbo.branches, never sample data.
            // Cached for a minute because the homepage is the busiest public URL.
            ViewBag.Cities = Cache.Get("home:cities", () => CampusRepository.Cities());
            ViewBag.Campuses = Cache.Get("home:campuses", () => CampusRepository.All().Take(3).ToList());
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
