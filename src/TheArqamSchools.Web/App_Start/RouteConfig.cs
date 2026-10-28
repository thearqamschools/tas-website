using System.Web.Mvc;
using System.Web.Routing;

namespace TheArqamSchools.Web
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.LowercaseUrls = true;
            routes.AppendTrailingSlash = false;

            // Technical status page for the developer (database connection, table counts).
            routes.MapRoute("Status", "status", new { controller = "Status", action = "Index" });
            routes.MapRoute("Sitemap", "sitemap.xml", new { controller = "Seo", action = "Sitemap" });
            routes.MapRoute("Robots", "robots.txt", new { controller = "Seo", action = "Robots" });

            routes.MapRoute("Home", "", new { controller = "Home", action = "Index" });

            // Admission inquiry form (the /admissions page itself is a CMS page).
            routes.MapRoute("AdmissionInquiry", "admissions/inquiry", new { controller = "Admissions", action = "Inquiry" },
                new { httpMethod = new HttpMethodConstraint("POST") });

            // Campuses: /campuses is the CMS page with the directory; city and campus pages come from dbo.branches.
            routes.MapRoute("CampusMessage", "campuses/{city}/{slug}/message", new { controller = "Campuses", action = "Message" },
                new { httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("CampusDetail", "campuses/{city}/{slug}", new { controller = "Campuses", action = "Detail" });
            routes.MapRoute("CampusCity", "campuses/{city}", new { controller = "Campuses", action = "City" });

            // Every other address is looked up in dbo.pages; unknown addresses get the 404 page.
            routes.MapRoute("Page", "{*path}", new { controller = "Pages", action = "Show" });
        }
    }
}
