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
    }
}
