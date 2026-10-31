using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// Error pages. Every unhandled error is answered in place by <see cref="Render"/>
    /// (called from Application_Error in Global.asax), with the right status code and
    /// no redirect: 404 shows the "Page not found" page, every other code shows the
    /// stand-alone error view, which never touches the database. /error (Index) still
    /// answers 500 for anyone who has it bookmarked or linked.
    /// No exception details are ever shown to visitors, and error responses are never cached.
    /// </summary>
    public class ErrorController : Controller
    {
        public ActionResult Index()
        {
            return ErrorPage(500);
        }

        // Show and NotFound are not reachable by URL: the "error" route only maps Index,
        // and there is no {controller}/{action} route. They run only through Render.
        public ActionResult Show(int status)
        {
            return ErrorPage(status >= 400 && status <= 599 ? status : 500);
        }

        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            return View("NotFound");
        }

        private ActionResult ErrorPage(int status)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            // A temporary failure must not be kept by browsers or proxies.
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            if (status == 503) Response.AppendHeader("Retry-After", "60");
            return View("Error");
        }

        /// <summary>
        /// The status code to answer for an unhandled exception, looking through wrapped
        /// exceptions (view errors arrive wrapped): 400 for request-validation and
        /// anti-forgery failures, 503 when the database is unavailable, otherwise the
        /// innermost HttpException code (for example 404), otherwise 500.
        /// </summary>
        public static int StatusFor(Exception ex)
        {
            var code = 500;
            for (var cause = ex; cause != null; cause = cause.InnerException)
            {
                if (cause is HttpRequestValidationException || cause is HttpAntiForgeryException) return 400;
                if (Data.Db.IsUnavailable(cause)) return 503;
                var http = cause as HttpException;
                if (http != null) code = http.GetHttpCode();
            }
            return code >= 400 && code <= 599 ? code : 500;
        }

        /// <summary>
        /// Writes the error page for <paramref name="status"/> into the current response.
        /// If the 404 page cannot be shown (it uses the site layout), the stand-alone
        /// error view is used with the same status.
        /// </summary>
        public static void Render(HttpContext context, int status)
        {
            try
            {
                Execute(context, status == 404 ? "NotFound" : "Show", status);
            }
            catch (Exception) when (status == 404)
            {
                Execute(context, "Show", status);
            }
        }

        private static void Execute(HttpContext context, string action, int status)
        {
            context.Response.Clear();
            context.Response.TrySkipIisCustomErrors = true;
            var routeData = new RouteData();
            routeData.Values["controller"] = "Error";
            routeData.Values["action"] = action;
            routeData.Values["status"] = status;
            IController controller = new ErrorController();
            controller.Execute(new RequestContext(new HttpContextWrapper(context), routeData));
        }
    }
}
