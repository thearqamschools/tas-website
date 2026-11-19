using System;
using System.Web;
using System.Web.Mvc;

namespace TheArqamSchools.Web.Infrastructure.Auth
{
    /// <summary>
    /// Guards admin actions. Without a valid admin session the visitor is sent to
    /// /admin/login (and back afterwards). With a session but without the named
    /// permission, a 403 "no access" page is shown. Every admin response is marked
    /// no-store and noindex so pages with personal data are not cached or indexed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AdminAuthorizeAttribute : FilterAttribute, IAuthorizationFilter
    {
        public AdminAuthorizeAttribute() { }

        public AdminAuthorizeAttribute(string permission)
        {
            Permission = permission;
        }

        /// <summary>Permission key such as "inquiries.view"; null means any signed-in admin.</summary>
        public string Permission { get; private set; }

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var http = filterContext.HttpContext;
            AdminResponse.NoStore(http);

            var user = AdminAuth.Current(http);
            if (user == null)
            {
                var back = http.Request.HttpMethod == "GET" ? http.Request.Url.PathAndQuery : null;
                var target = "/admin/login";
                var safe = AdminAuth.SafeReturnUrl(back);
                if (back != null && safe != "/admin") target += "?returnUrl=" + HttpUtility.UrlEncode(safe);
                filterContext.Result = new RedirectResult(target);
                return;
            }

            filterContext.Controller.ViewBag.AdminUser = user;
            if (Permission != null && !user.Can(Permission))
            {
                http.Response.TrySkipIisCustomErrors = true;
                filterContext.Result = new ViewResult
                {
                    ViewName = "~/Views/Admin/Forbidden.cshtml",
                    ViewData = filterContext.Controller.ViewData
                };
                http.Response.StatusCode = 403;
            }
        }
    }

    public static class AdminResponse
    {
        /// <summary>Admin pages: never cached, never indexed, never framed.</summary>
        public static void NoStore(HttpContextBase http)
        {
            http.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            http.Response.Cache.SetNoStore();
            http.Response.AppendHeader("X-Robots-Tag", "noindex, nofollow");
        }
    }
}
