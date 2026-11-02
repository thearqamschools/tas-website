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

            // Forms on CMS pages (/contact and /franchise/apply are CMS pages).
            routes.MapRoute("ContactSend", "contact/send", new { controller = "Contact", action = "Send" },
                new { httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("FranchiseApply", "franchise/apply/send", new { controller = "Contact", action = "Franchise" },
                new { httpMethod = new HttpMethodConstraint("POST") });

            // Campuses: /campuses is the CMS page with the directory; city and campus pages come from dbo.branches.
            routes.MapRoute("CampusMessage", "campuses/{city}/{slug}/message", new { controller = "Campuses", action = "Message" },
                new { httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("CampusDetail", "campuses/{city}/{slug}", new { controller = "Campuses", action = "Detail" });
            routes.MapRoute("CampusCity", "campuses/{city}", new { controller = "Campuses", action = "City" });

            // News & Events (dbo.news and dbo.events), published in the admin panel.
            routes.MapRoute("NewsEvents", "news-events", new { controller = "NewsEvents", action = "Index" });
            routes.MapRoute("NewsEventsDetail", "news-events/{slug}", new { controller = "NewsEvents", action = "Detail" });

            // Admin panel. Every /admin address is handled here, never by the CMS catch-all.
            routes.MapRoute("AdminHome", "admin", new { controller = "AdminDashboard", action = "Index" });
            routes.MapRoute("AdminLogin", "admin/login", new { controller = "AdminAccount", action = "Login" });
            routes.MapRoute("AdminLoginCode", "admin/login/code", new { controller = "AdminAccount", action = "Code" });
            routes.MapRoute("AdminLoginAuthenticator", "admin/login/authenticator", new { controller = "AdminAccount", action = "Authenticator" });
            routes.MapRoute("AdminLogout", "admin/logout", new { controller = "AdminAccount", action = "Logout" },
                new { httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminPassword", "admin/account/password", new { controller = "AdminAccount", action = "Password" });
            routes.MapRoute("AdminSetup", "admin/setup", new { controller = "AdminAccount", action = "Setup" });
            routes.MapRoute("AdminUsers", "admin/users", new { controller = "AdminUsers", action = "Index" });
            routes.MapRoute("AdminUserNew", "admin/users/new", new { controller = "AdminUsers", action = "New" });
            routes.MapRoute("AdminUserResetPassword", "admin/users/{id}/reset-password", new { controller = "AdminUsers", action = "ResetPassword" },
                new { id = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminUserResetTwoStep", "admin/users/{id}/reset-two-step", new { controller = "AdminUsers", action = "ResetTwoFactor" },
                new { id = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminUserEdit", "admin/users/{id}", new { controller = "AdminUsers", action = "Edit" },
                new { id = new GuidRouteConstraint() });
            routes.MapRoute("AdminCampuses", "admin/campuses", new { controller = "AdminCampuses", action = "Index" });
            routes.MapRoute("AdminCampusNew", "admin/campuses/new", new { controller = "AdminCampuses", action = "New" });
            routes.MapRoute("AdminCampusArchive", "admin/campuses/{id}/archive", new { controller = "AdminCampuses", action = "Archive" },
                new { id = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminCampusEdit", "admin/campuses/{id}", new { controller = "AdminCampuses", action = "Edit" },
                new { id = new GuidRouteConstraint() });
            routes.MapRoute("AdminSettings", "admin/settings", new { controller = "AdminSettings", action = "Index" });
            routes.MapRoute("AdminMenus", "admin/menus", new { controller = "AdminMenus", action = "Index" });
            routes.MapRoute("AdminMenuItemNew", "admin/menus/{menuId}/new", new { controller = "AdminMenus", action = "New" },
                new { menuId = new GuidRouteConstraint() });
            routes.MapRoute("AdminMenuItemEdit", "admin/menus/items/{id}", new { controller = "AdminMenus", action = "Edit" },
                new { id = new GuidRouteConstraint() });
            routes.MapRoute("AdminPages", "admin/pages", new { controller = "AdminPages", action = "Index" });
            routes.MapRoute("AdminPageSectionPublish", "admin/pages/{pageId}/sections/{sectionId}/publish", new { controller = "AdminPages", action = "PublishDraft" },
                new { pageId = new GuidRouteConstraint(), sectionId = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminPageSectionDiscard", "admin/pages/{pageId}/sections/{sectionId}/discard", new { controller = "AdminPages", action = "DiscardDraft" },
                new { pageId = new GuidRouteConstraint(), sectionId = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminPageSectionVisibility", "admin/pages/{pageId}/sections/{sectionId}/visibility", new { controller = "AdminPages", action = "Visibility" },
                new { pageId = new GuidRouteConstraint(), sectionId = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminPageSection", "admin/pages/{pageId}/sections/{sectionId}", new { controller = "AdminPages", action = "Section" },
                new { pageId = new GuidRouteConstraint(), sectionId = new GuidRouteConstraint() });
            routes.MapRoute("AdminPageEdit", "admin/pages/{id}", new { controller = "AdminPages", action = "Edit" },
                new { id = new GuidRouteConstraint() });
            routes.MapRoute("AdminNews", "admin/news", new { controller = "AdminNews", action = "Index" });
            routes.MapRoute("AdminNewsNew", "admin/news/new/{kind}", new { controller = "AdminNews", action = "New" }, new { kind = "news|event" });
            routes.MapRoute("AdminNewsEdit", "admin/news/{kind}/{id}", new { controller = "AdminNews", action = "Edit" },
                new { kind = "news|event", id = new GuidRouteConstraint() });
            routes.MapRoute("AdminFranchise", "admin/franchise", new { controller = "AdminFranchise", action = "Index" });
            routes.MapRoute("AdminFranchiseExport", "admin/franchise/export.csv", new { controller = "AdminFranchise", action = "Export" });
            routes.MapRoute("AdminFranchiseStatus", "admin/franchise/{id}/status", new { controller = "AdminFranchise", action = "Status" },
                new { id = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminFranchiseDetail", "admin/franchise/{id}", new { controller = "AdminFranchise", action = "Detail" },
                new { id = new GuidRouteConstraint() });
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
