using System.Web.Mvc;
using TheArqamSchools.Web.Infrastructure;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>Shows any published CMS page by its address (/about, /academics, /admissions, /campuses, /faq ...).</summary>
    public class PagesController : Controller
    {
        // Query strings (campus search) are cleaned and encoded by the page itself; see PageBuilder.
        [ValidateInput(false)]
        public ActionResult Show()
        {
            // Read from the route, not from model binding, so no query-string value is bound or validated here.
            var path = RouteData.Values["path"] as string;
            var model = PageBuilder.Build("/" + (path ?? "").Trim('/').ToLowerInvariant(), Request);
            if (model == null) return NotFoundPage();
            return View("Show", model);
        }

        internal ActionResult NotFoundPage()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/NotFound.cshtml");
        }
    }
}
