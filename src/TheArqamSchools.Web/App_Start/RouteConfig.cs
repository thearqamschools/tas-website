using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Mvc.Routing.Constraints;

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

            // Admin panel. Every /admin address is handled here, never by the CMS catch-all.
            routes.MapRoute("AdminHome", "admin", new { controller = "AdminDashboard", action = "Index" });
            routes.MapRoute("AdminLogin", "admin/login", new { controller = "AdminAccount", action = "Login" });
            routes.MapRoute("AdminLoginCode", "admin/login/code", new { controller = "AdminAccount", action = "Code" });
            routes.MapRoute("AdminLoginAuthenticator", "admin/login/authenticator", new { controller = "AdminAccount", action = "Authenticator" });
            routes.MapRoute("AdminLogout", "admin/logout", new { controller = "AdminAccount", action = "Logout" },
                new { httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminPassword", "admin/account/password", new { controller = "AdminAccount", action = "Password" });
            routes.MapRoute("AdminSetup", "admin/setup", new { controller = "AdminAccount", action = "Setup" });
            routes.MapRoute("AdminInboxExport", "admin/{kind}/export.csv", new { controller = "AdminInbox", action = "Export" },
                new { kind = "inquiries|messages" });
            routes.MapRoute("AdminInboxStatus", "admin/{kind}/{id}/status", new { controller = "AdminInbox", action = "Status" },
                new { kind = "inquiries|messages", id = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminInboxDetail", "admin/{kind}/{id}", new { controller = "AdminInbox", action = "Detail" },
                new { kind = "inquiries|messages", id = new GuidRouteConstraint() });
            routes.MapRoute("AdminInbox", "admin/{kind}", new { controller = "AdminInbox", action = "Index" },
                new { kind = "inquiries|messages" });
            routes.MapRoute("AdminNotFound", "admin/{*rest}", new { controller = "AdminDashboard", action = "NotFound" });

            // Error page used by customErrors in Web.config; must come before the catch-all.
            routes.MapRoute("Error", "error", new { controller = "Error", action = "Index" });

            // Every other address is looked up in dbo.pages; unknown addresses get the 404 page.
            routes.MapRoute("Page", "{*path}", new { controller = "Pages", action = "Show" });
        }
    }
}
