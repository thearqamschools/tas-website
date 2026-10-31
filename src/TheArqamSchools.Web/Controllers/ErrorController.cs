using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// Error pages. Unhandled errors outside MVC actions are answered in place by
    /// <see cref="Render"/> (called from Application_Error in Global.asax), with the
    /// right status code and no redirect: 404 shows the "Page not found" page, every
    /// other code shows the stand-alone error view, which never touches the database.
    /// The /error route (Index) stays as the target of Web.config customErrors, which
    /// is now only a safety net if Application_Error itself fails.
    /// No exception details are ever shown to visitors.
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
            return View("NotFound");
        }

        private ActionResult ErrorPage(int status)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            // A temporary failure must not be kept by browsers or proxies.
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            return View("Error");
        }

        /// <summary>
        /// The status code to answer for an unhandled exception: the code an
        /// HttpException carries (for example 400 for a rejected request path, 404),
        /// 400 for request-validation failures, otherwise 500.
        /// </summary>
        public static int StatusFor(Exception ex)
        {
            if (ex is HttpRequestValidationException) return 400;
            var http = ex as HttpException;
            var code = http != null ? http.GetHttpCode() : 500;
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
