using System.Web;
using System.Web.Mvc;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// The page that Web.config's customErrors sends visitors to (defaultRedirect="~/Error")
    /// when an error happens outside an MVC action. Without this route the redirect fell
    /// through to the CMS catch-all and showed "Page not found" with a 404, which hid the
    /// real failure. It answers 500 and shows the same stand-alone error view that
    /// HandleErrorAttribute uses. It deliberately does not touch the database, because
    /// the database may be the reason the error happened.
    /// </summary>
    public class ErrorController : Controller
    {
        public ActionResult Index()
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
            // A temporary failure must not be kept by browsers or proxies.
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            return View("Error");
        }
    }
}
