using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Xml.Linq;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Helpers;

namespace TheArqamSchools.Web.Controllers
{
    public class SeoController : Controller
    {
        /// <summary>/sitemap.xml: published pages, city pages and campus pages. Archived pages and redirected addresses are left out.</summary>
        public ActionResult Sitemap()
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var urls = new XElement(ns + "urlset");
            urls.Add(UrlNode(ns, "/", null));
            foreach (var p in PageRepository.AllPublished())
            {
                var seo = PageRepository.Seo(p.SeoId);
                if (seo != null && seo.RobotsNoindex) continue;
                urls.Add(UrlNode(ns, p.Path, p.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd")));
            }
            var campuses = CampusRepository.All();
            foreach (var city in campuses.Select(c => c.CitySlug).Distinct())
                urls.Add(UrlNode(ns, "/campuses/" + city, null));
            foreach (var c in campuses)
                urls.Add(UrlNode(ns, c.Url, c.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd")));
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), urls);
            return Content(doc.Declaration + "\n" + doc.ToString(), "application/xml", Encoding.UTF8);
        }

        public ActionResult Robots()
        {
            var body = "User-agent: *\nDisallow: /admin\nDisallow: /status\n\nSitemap: " + Site.Absolute("/sitemap.xml") + "\n";
            return Content(body, "text/plain", Encoding.UTF8);
        }

        private static XElement UrlNode(XNamespace ns, string path, string lastmod)
        {
            var e = new XElement(ns + "url", new XElement(ns + "loc", Site.Absolute(path)));
            if (lastmod != null) e.Add(new XElement(ns + "lastmod", lastmod));
            return e;
        }
    }
}
