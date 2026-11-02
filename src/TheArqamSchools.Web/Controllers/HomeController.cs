using System;
using System.Collections.Generic;
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
        /// <summary>Approved limit for campus cards on the homepage.</summary>
        public const int MaxHomepageCampuses = 3;

        public ActionResult Index()
        {
            var settings = SiteSettingsRepository.Public();
            ViewBag.Tagline = SiteSettingsRepository.Text(settings, "site.tagline");
            // Campus finder: only published campuses from dbo.branches, never sample data.
            // At most three campus cards (TOP 3 in SQL); the full list is on /campuses.
            // Cached for a minute because the homepage is the busiest public URL. If the
            // database is briefly unreachable the last good copy is used, or the finder
            // is left out (the view hides it when the lists are empty).
            ViewBag.Cities = Cache.GetOrLastGood("home:cities", () => CampusRepository.Cities(), () => new List<FilterOption>());
            ViewBag.Campuses = Cache.GetOrLastGood("home:campuses", () => CampusRepository.Featured(MaxHomepageCampuses), () => new List<Campus>());
            // Newest three published news posts and events (cleared when one is saved in the admin panel).
            ViewBag.News = Cache.GetOrLastGood("home:news", () => NewsRepository.Published(null, 3), () => new List<NewsItem>());
            SeoMetadata seo = null;
            try { seo = PageRepository.SeoForRoute("/"); }
            catch (Exception ex) when (Db.IsUnavailable(ex)) { /* use the default title and description below */ }
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
