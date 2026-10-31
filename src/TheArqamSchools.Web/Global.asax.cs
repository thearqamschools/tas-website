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
        /// Every unhandled error (there is no global HandleErrorAttribute) is answered
        /// here, in place and without a redirect, with the right status code:
        /// request validation and anti-forgery failures 400, HttpException codes kept
        /// (404 shows the "Page not found" page), database unavailable 503 with
        /// Retry-After, everything else 500. No exception details are shown. The log
        /// line has the status, method and a sanitised path (no query string, form data
        /// or cookies); the full exception is logged only for 5xx.
        /// </summary>
        protected void Application_Error(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            if (context == null) return;
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
                    // Last resort: a fixed page with no Razor, layout, database, exception text or user input.
                    var response = context.Response;
                    response.Clear();
                    response.StatusCode = status;
                    response.TrySkipIisCustomErrors = true;
                    response.ContentType = "text/html; charset=utf-8";
                    response.Cache.SetCacheability(HttpCacheability.NoCache);
                    response.Cache.SetNoStore();
                    if (status == 503) response.AppendHeader("Retry-After", "60");
                    response.Write("<!doctype html><html lang=\"en-PK\"><head><meta charset=\"utf-8\">" +
                        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                        "<meta name=\"robots\" content=\"noindex\"><title>Something went wrong | The Arqam Schools</title></head>" +
                        "<body><main><h1>Something went wrong</h1><p>Please try again in a few minutes, or call us on " +
                        "<a href=\"tel:0412660265\">041-2660265</a>.</p><p><a href=\"/\">Go to the homepage</a></p></main></body></html>");
                }
                catch (HttpException)
                {
                    // Headers already sent: the response cannot be replaced.
                }
            }
            context.ApplicationInstance.CompleteRequest();
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
