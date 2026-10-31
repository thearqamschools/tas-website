using System;
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

        protected void Application_Error(object sender, EventArgs e)
        {
            var error = Server.GetLastError();
            var status = 500;
            for (var cause = error; cause != null; cause = cause.InnerException)
            {
                var http = cause as HttpException;
                if (http != null) status = http.GetHttpCode();
                if (cause is HttpRequestValidationException || cause is System.Web.Mvc.HttpAntiForgeryException)
                { status = 400; break; }
                // Includes failures in the shared header/footer, even on a cold cache.
                if (cause is System.Data.SqlClient.SqlException && Request.Path == "/")
                { status = 503; break; }
            }
            if (status < 400 || status > 599) status = 500;
            Server.ClearError();
            Response.Clear();
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            Response.ContentType = "text/html; charset=utf-8";
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            if (status == 503) Response.AddHeader("Retry-After", "60");
            // No Razor, layout, database, exception text or user input in this last-resort page.
            var title = status == 503 ? "Temporarily unavailable" :
                status == 404 ? "Page not found" : status < 500 ? "Request could not be completed" : "Something went wrong";
            Response.Write("<!doctype html><html lang=\"en-PK\"><head><meta charset=\"utf-8\">" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                "<meta name=\"robots\" content=\"noindex\"><title>" + title + " | The Arqam Schools</title>" +
                "<link rel=\"stylesheet\" href=\"/Content/site.css?v=20261107\"></head>" +
                "<body class=\"error-page\"><main id=\"main\" class=\"container\"><h1>" + title + "</h1>" +
                "<p>The Arqam Schools</p><p>Please try again in a few minutes, or call " +
                "<a href=\"tel:+92412660265\">041-2660265</a>.</p><p><a href=\"/\">Go to the homepage</a></p></main></body></html>");
            CompleteRequest();
        }
    }
}
