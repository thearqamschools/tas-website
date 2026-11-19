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

            // Books ordering (customers registered by the office order with their mobile number).
            var post = new HttpMethodConstraint("POST");
            var get = new HttpMethodConstraint("GET", "HEAD");
            routes.MapRoute("BooksStart", "books", new { controller = "Books", action = "Index" });
            routes.MapRoute("BooksSignOut", "books/signout", new { controller = "Books", action = "SignOut" }, new { httpMethod = post });
            routes.MapRoute("BooksOrderPost", "books/order", new { controller = "Books", action = "Submit" }, new { httpMethod = post });
            routes.MapRoute("BooksOrder", "books/order", new { controller = "Books", action = "Order" }, new { httpMethod = get });
            routes.MapRoute("BooksOrders", "books/orders", new { controller = "Books", action = "Orders" }, new { httpMethod = get });
            routes.MapRoute("BooksEditPost", "books/orders/{orderNo}/edit", new { controller = "Books", action = "Submit" }, new { orderNo = @"\d{1,12}", httpMethod = post });
            routes.MapRoute("BooksEdit", "books/orders/{orderNo}/edit", new { controller = "Books", action = "Edit" }, new { orderNo = @"\d{1,12}", httpMethod = get });
            routes.MapRoute("BooksDetail", "books/orders/{orderNo}", new { controller = "Books", action = "Detail" }, new { orderNo = @"\d{1,12}", httpMethod = get });

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
            routes.MapRoute("AdminHomePage", "admin/home", new { controller = "AdminHome", action = "Index" });
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
            routes.MapRoute("AdminNewsPhotos", "admin/news/{kind}/{id}/photos", new { controller = "AdminNews", action = "Photos" },
                new { kind = "news|event", id = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminNewsPhoto", "admin/news/{kind}/{id}/photos/{photoId}/{op}", new { controller = "AdminNews", action = "Photo" },
                new { kind = "news|event", id = new GuidRouteConstraint(), photoId = new GuidRouteConstraint(), op = "cover|remove|up|down", httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminBooks", "admin/books", new { controller = "AdminBooks", action = "Index" });
            routes.MapRoute("AdminBookItems", "admin/books/items", new { controller = "AdminBooks", action = "Items" });
            routes.MapRoute("AdminBookItemNew", "admin/books/items/new", new { controller = "AdminBooks", action = "NewItem" });
            routes.MapRoute("AdminBookItemStock", "admin/books/items/{id}/stock", new { controller = "AdminBooks", action = "Stock" },
                new { id = new GuidRouteConstraint(), httpMethod = new HttpMethodConstraint("POST") });
            routes.MapRoute("AdminBookItem", "admin/books/items/{id}", new { controller = "AdminBooks", action = "Item" }, new { id = new GuidRouteConstraint() });
            var bookNo = @"\d{1,12}";
            var postOnly = new HttpMethodConstraint("POST");
            routes.MapRoute("AdminBookOrders", "admin/books/orders", new { controller = "AdminBooks", action = "Orders" });
            routes.MapRoute("AdminBookFind", "admin/books/find", new { controller = "AdminBooks", action = "Find" });
            routes.MapRoute("AdminBookOrderNewPost", "admin/books/orders/new", new { controller = "AdminBooks", action = "NewOrderPost" }, new { httpMethod = postOnly });
            routes.MapRoute("AdminBookOrderNew", "admin/books/orders/new", new { controller = "AdminBooks", action = "NewOrder" });
            routes.MapRoute("AdminBookOrderStatus", "admin/books/orders/{orderNo}/status", new { controller = "AdminBooks", action = "OrderStatus" }, new { orderNo = bookNo, httpMethod = postOnly });
            routes.MapRoute("AdminBookOrderPay", "admin/books/orders/{orderNo}/payments", new { controller = "AdminBooks", action = "Pay" }, new { orderNo = bookNo, httpMethod = postOnly });
            routes.MapRoute("AdminBookInvoicePdf", "admin/books/orders/{orderNo}/invoice.pdf", new { controller = "AdminBooks", action = "InvoicePdf" }, new { orderNo = bookNo });
            routes.MapRoute("AdminBookInvoice", "admin/books/orders/{orderNo}/invoice", new { controller = "AdminBooks", action = "Invoice" }, new { orderNo = bookNo });
            routes.MapRoute("AdminBookOrderEdit", "admin/books/orders/{orderNo}/edit", new { controller = "AdminBooks", action = "EditOrder" }, new { orderNo = bookNo });
            routes.MapRoute("AdminBookOrderReturn", "admin/books/orders/{orderNo}/returns", new { controller = "AdminBooks", action = "Return" }, new { orderNo = bookNo, httpMethod = postOnly });
            routes.MapRoute("AdminBookReturnNote", "admin/books/returns/{returnNo}", new { controller = "AdminBooks", action = "ReturnNote" }, new { returnNo = bookNo });
            routes.MapRoute("AdminBookReports", "admin/books/reports", new { controller = "AdminBooks", action = "Reports" });
            routes.MapRoute("AdminBookOrder", "admin/books/orders/{orderNo}", new { controller = "AdminBooks", action = "Order" }, new { orderNo = bookNo });
            routes.MapRoute("AdminBookReceipt", "admin/books/payments/{id}/receipt", new { controller = "AdminBooks", action = "Receipt" }, new { id = new GuidRouteConstraint() });
            routes.MapRoute("AdminBookAccountsPost", "admin/books/accounts", new { controller = "AdminBooks", action = "SaveAccount" }, new { httpMethod = postOnly });
            routes.MapRoute("AdminBookAccounts", "admin/books/accounts", new { controller = "AdminBooks", action = "Accounts" });
            routes.MapRoute("AdminBookAccount", "admin/books/accounts/{id}", new { controller = "AdminBooks", action = "SaveAccount" }, new { id = new GuidRouteConstraint(), httpMethod = postOnly });
            routes.MapRoute("AdminBookCustomers", "admin/books/customers", new { controller = "AdminBooks", action = "Customers" });
            routes.MapRoute("AdminBookCustomerNew", "admin/books/customers/new", new { controller = "AdminBooks", action = "NewCustomer" });
            routes.MapRoute("AdminBookCustomer", "admin/books/customers/{id}", new { controller = "AdminBooks", action = "Customer" }, new { id = new GuidRouteConstraint() });
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
