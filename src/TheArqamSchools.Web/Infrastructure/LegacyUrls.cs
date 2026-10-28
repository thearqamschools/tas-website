using System;
using System.Web;
using TheArqamSchools.Web.Data;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// Runs before routing. Sends old addresses to their new pages with a
    /// permanent (301) redirect, so bookmarks and search results keep working:
    ///  - rules from dbo.redirects (editable in the admin panel);
    ///  - any /branches/... address to the same address under /campuses/...;
    ///  - a trailing slash is removed (/about/ -> /about).
    /// Form posts, files and the admin area are never redirected.
    /// </summary>
    public static class LegacyUrls
    {
        public static void Handle(HttpContext context)
        {
            var req = context.Request;
            if (req.HttpMethod != "GET" && req.HttpMethod != "HEAD") return;
            var path = req.Path;
            if (path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/content/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/scripts/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
                || path.IndexOf('.') >= 0)
                return;

            var key = path.Length > 1 ? path.TrimEnd('/').ToLowerInvariant() : "/";
            string target = null;

            RedirectRule rule = null;
            try { rule = RedirectRepository.Find(key); }
            catch (Exception) { /* database unavailable: fall through to the built-in rules */ }

            if (rule != null)
            {
                target = rule.ToPath;
                RedirectRepository.RecordHit(rule.Id);
            }
            else if (key == "/branches" || key.StartsWith("/branches/", StringComparison.Ordinal))
                target = "/campuses" + key.Substring("/branches".Length);
            else if (path.Length > 1 && path.EndsWith("/"))
                target = key;

            if (target == null) return;
            if (!string.IsNullOrEmpty(req.Url.Query) && target.IndexOf('?') < 0)
            {
                var hash = target.IndexOf('#');
                target = hash < 0 ? target + req.Url.Query : target.Substring(0, hash) + req.Url.Query + target.Substring(hash);
            }
            context.Response.Cache.SetCacheability(HttpCacheability.Public);
            context.Response.Cache.SetMaxAge(TimeSpan.FromHours(1));
            if (rule != null && rule.StatusCode != 301 && rule.StatusCode != 308)
                context.Response.Redirect(target, false);
            else
                context.Response.RedirectPermanent(target, false);
            context.ApplicationInstance.CompleteRequest();
        }
    }
}
