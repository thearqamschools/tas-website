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
            // The campus finder is only a search box and "Show all campuses" (owner's request, 21 November 2026),
            // so the homepage no longer loads cities or campus cards.
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
