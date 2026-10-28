using System.Web.Mvc;
using TheArqamSchools.Web.Helpers;

namespace TheArqamSchools.Web
{
    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            // Site:RequireHttps can be set to false only on a developer machine without a certificate.
            if (Site.RequireHttps)
                filters.Add(new RequireHttpsAttribute());
        }
    }
}
