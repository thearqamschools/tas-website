using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using TheArqamSchools.Web.Infrastructure;

namespace TheArqamSchools.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            MvcHandler.DisableMvcResponseHeader = true;
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            Data.Db.ConfigureDapper();
            System.Web.Helpers.AntiForgeryConfig.SuppressXFrameOptionsHeader = true;
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            LegacyUrls.Handle(HttpContext.Current);
        }

        /// <summary>
        /// Unhandled errors that MVC's HandleErrorAttribute does not catch (request
        /// validation, bad paths, errors before or outside a controller). Instead of
        /// the customErrors redirect to /Error (which turned every error, even a 400,
        /// into a 302 and then a 500), the error page is written in place with the
        /// correct status code. When custom errors are off (customErrors RemoteOnly on
        /// the server itself), nothing is changed, so developers still see details.
        /// The log line has the status, method and path: no query string, form data
        /// or cookies, which may hold personal information.
        /// </summary>
        protected void Application_Error(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            if (context == null || !context.IsCustomErrorEnabled) return;
            var ex = Server.GetLastError();
            if (ex == null) return;
            var status = Controllers.ErrorController.StatusFor(ex);
            // 4xx messages can quote what the visitor sent (for example a rejected form value), so only the type is logged.
            Trace.TraceError("Unhandled error {0} for {1} {2}: {3}", status, context.Request.HttpMethod, LogSafe(context.Request.Path),
                status >= 500 ? ex.ToString() : ex.GetType().FullName);
            context.ClearError();
            try
            {
                Controllers.ErrorController.Render(context, status);
            }
            catch (Exception renderError)
            {
                Trace.TraceError("Error page failed: {0}", renderError);
                try
                {
                    // Last resort: plain text, no details. Fails only if headers were already
                    // sent, in which case the response cannot be replaced at all.
                    context.Response.Clear();
                    context.Response.StatusCode = status;
                    context.Response.TrySkipIisCustomErrors = true;
                    context.Response.ContentType = "text/plain";
                    context.Response.Write("Something went wrong. Please try again in a few minutes, or call us on 041-2660265.");
                }
                catch (HttpException)
                {
                    // Headers already sent: nothing more can be written.
                }
            }
        }

        /// <summary>Path for a log line: control characters (forged new lines) removed, at most 200 characters.</summary>
        private static string LogSafe(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            var chars = path.Length > 200 ? path.Substring(0, 200) : path;
            var sb = new System.Text.StringBuilder(chars.Length);
            foreach (var c in chars) sb.Append(char.IsControl(c) ? '_' : c);
            return sb.ToString();
        }
    }
}
