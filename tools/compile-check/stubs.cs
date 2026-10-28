// Minimal stand-ins for ASP.NET MVC 5 and Dapper, used ONLY to compile-check
// the C# code on Linux (the real packages come from NuGet in Visual Studio).
using System;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.Web.Routing;

namespace System.Web.Mvc
{
    public abstract class ActionResult { }
    public class ViewResult : ActionResult { }
    public class HttpStatusCodeResult : ActionResult { public HttpStatusCodeResult(int code) { } public HttpStatusCodeResult(int code, string d) { } }
    public class HttpNotFoundResult : HttpStatusCodeResult { public HttpNotFoundResult() : base(404) { } }
    public class RedirectResult : ActionResult { public RedirectResult(string url) { } public RedirectResult(string url, bool permanent) { } }
    public class RedirectToRouteResult : ActionResult { }
    public class ContentResult : ActionResult { public string Content; public string ContentType; public System.Text.Encoding ContentEncoding; }
    public class FileContentResult : ActionResult { public FileContentResult(byte[] b, string ct) { } public string FileDownloadName { get; set; } }
    public class FilePathResult : ActionResult { public FilePathResult(string p, string ct) { } public string FileDownloadName { get; set; } }
    public class JsonResult : ActionResult { public object Data { get; set; } public JsonRequestBehavior JsonRequestBehavior { get; set; } }
    public enum JsonRequestBehavior { AllowGet, DenyGet }
    public class ModelStateDictionary : Dictionary<string, object> { public bool IsValid => true; public void AddModelError(string k, string m) { } }
    public class TempDataDictionary : Dictionary<string, object> { }
    public class ViewDataDictionary : Dictionary<string, object> { }
    public abstract class ControllerBase { public dynamic ViewBag { get; } public TempDataDictionary TempData { get; set; } public ViewDataDictionary ViewData { get; set; } }
    public abstract class Controller : ControllerBase
    {
        public HttpRequestBase Request { get; }
        public HttpResponseBase Response { get; }
        public HttpContextBase HttpContext { get; }
        public System.Security.Principal.IPrincipal User { get; }
        public UrlHelper Url { get; }
        public ModelStateDictionary ModelState { get; }
        protected ViewResult View() => null;
        protected ViewResult View(object model) => null;
        protected ViewResult View(string viewName) => null;
        protected ViewResult View(string viewName, object model) => null;
        protected HttpNotFoundResult HttpNotFound() => null;
        protected HttpNotFoundResult HttpNotFound(string d) => null;
        protected RedirectResult Redirect(string url) => null;
        protected RedirectResult RedirectPermanent(string url) => null;
        protected RedirectToRouteResult RedirectToAction(string action) => null;
        protected RedirectToRouteResult RedirectToAction(string action, object routeValues) => null;
        protected RedirectToRouteResult RedirectToAction(string action, string controller, object routeValues) => null;
        protected ContentResult Content(string c) => null;
        protected ContentResult Content(string c, string ct) => null;
        protected ContentResult Content(string c, string ct, System.Text.Encoding e) => null;
        protected FileContentResult File(byte[] b, string ct) => null;
        protected FileContentResult File(byte[] b, string ct, string name) => null;
        protected FilePathResult File(string p, string ct) => null;
        protected JsonResult Json(object d) => null;
        protected JsonResult Json(object d, JsonRequestBehavior b) => null;
        protected virtual void OnActionExecuting(ActionExecutingContext c) { }
        protected virtual void OnException(ExceptionContext c) { }
        protected bool TryUpdateModel<T>(T m) where T : class => true;
    }
    public class UrlHelper { public string Action(string a) => ""; public string Action(string a, string c) => ""; public string Action(string a, string c, object r) => ""; public string Content(string p) => ""; public bool IsLocalUrl(string u) => true; }
    public class ActionExecutingContext { public ActionResult Result { get; set; } public HttpContextBase HttpContext { get; } public ActionDescriptor ActionDescriptor { get; } public RouteData RouteData { get; } public IDictionary<string, object> ActionParameters { get; } }
    public class ActionExecutedContext { public ActionResult Result { get; set; } public HttpContextBase HttpContext { get; } }
    public class ResultExecutingContext { public ActionResult Result { get; set; } public HttpContextBase HttpContext { get; } }
    public class ResultExecutedContext { public HttpContextBase HttpContext { get; } }
    public class AuthorizationContext { public ActionResult Result { get; set; } public HttpContextBase HttpContext { get; } public ActionDescriptor ActionDescriptor { get; } public RouteData RouteData { get; } }
    public class ExceptionContext { public Exception Exception { get; } public bool ExceptionHandled { get; set; } public ActionResult Result { get; set; } public HttpContextBase HttpContext { get; } }
    public abstract class ActionDescriptor { public virtual string ActionName { get; } public virtual ControllerDescriptor ControllerDescriptor { get; } public virtual bool IsDefined(Type t, bool inherit) => false; }
    public abstract class ControllerDescriptor { public virtual string ControllerName { get; } public virtual bool IsDefined(Type t, bool inherit) => false; }
    public interface IActionFilter { void OnActionExecuting(ActionExecutingContext c); void OnActionExecuted(ActionExecutedContext c); }
    public interface IAuthorizationFilter { void OnAuthorization(AuthorizationContext c); }
    public interface IResultFilter { void OnResultExecuting(ResultExecutingContext c); void OnResultExecuted(ResultExecutedContext c); }
    public abstract class FilterAttribute : Attribute { public int Order { get; set; } }
    public abstract class ActionFilterAttribute : FilterAttribute, IActionFilter, IResultFilter
    {
        public virtual void OnActionExecuting(ActionExecutingContext c) { }
        public virtual void OnActionExecuted(ActionExecutedContext c) { }
        public virtual void OnResultExecuting(ResultExecutingContext c) { }
        public virtual void OnResultExecuted(ResultExecutedContext c) { }
    }
    public class HandleErrorAttribute : FilterAttribute { }
    public class RequireHttpsAttribute : FilterAttribute, IAuthorizationFilter { public virtual void OnAuthorization(AuthorizationContext c) { } protected virtual void HandleNonHttpsRequest(AuthorizationContext c) { } }
    public class ValidateAntiForgeryTokenAttribute : FilterAttribute { }
    public class HttpPostAttribute : Attribute { }
    public class HttpGetAttribute : Attribute { }
    public class AllowAnonymousAttribute : Attribute { }
    public class ChildActionOnlyAttribute : Attribute { }
    public class OutputCacheAttribute : Attribute { public int Duration { get; set; } public string VaryByParam { get; set; } }
    public class ValidateInputAttribute : Attribute { public ValidateInputAttribute(bool b) { } }
    public class GlobalFilterCollection { public void Add(object f) { } }
    public static class GlobalFilters { public static GlobalFilterCollection Filters { get; } }
    public static class AreaRegistration { public static void RegisterAllAreas() { } }
    public class MvcHandler { public static bool DisableMvcResponseHeader { get; set; } }
    public sealed class UrlParameter { public static readonly UrlParameter Optional = new UrlParameter(); }
    public static class RouteCollectionExtensions
    {
        public static Route MapRoute(this RouteCollection r, string name, string url) => null;
        public static Route MapRoute(this RouteCollection r, string name, string url, object defaults) => null;
        public static Route MapRoute(this RouteCollection r, string name, string url, object defaults, object constraints) => null;
        public static void IgnoreRoute(this RouteCollection r, string url) { }
    }
    public class MvcHtmlString : IHtmlString { public MvcHtmlString(string s) { } public string ToHtmlString() => ""; public static MvcHtmlString Create(string s) => null; }
}

namespace Dapper
{
    public static class SqlMapper
    {
        public static IEnumerable<T> Query<T>(this IDbConnection c, string sql, object param = null, IDbTransaction transaction = null) => null;
        public static T QueryFirstOrDefault<T>(this IDbConnection c, string sql, object param = null, IDbTransaction transaction = null) => default(T);
        public static T QuerySingleOrDefault<T>(this IDbConnection c, string sql, object param = null, IDbTransaction transaction = null) => default(T);
        public static T ExecuteScalar<T>(this IDbConnection c, string sql, object param = null, IDbTransaction transaction = null) => default(T);
        public static int Execute(this IDbConnection c, string sql, object param = null, IDbTransaction transaction = null) => 0;
        public static GridReader QueryMultiple(this IDbConnection c, string sql, object param = null, IDbTransaction transaction = null) => null;
        public class GridReader : IDisposable { public IEnumerable<T> Read<T>() => null; public T ReadFirstOrDefault<T>() => default(T); public void Dispose() { } }
    }
    public class DefaultTypeMap { public static bool MatchNamesWithUnderscores { get; set; } }
    public class DynamicParameters { public void Add(string n, object v) { } }
}
