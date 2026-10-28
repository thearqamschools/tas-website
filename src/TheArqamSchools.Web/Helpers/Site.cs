using System;
using System.Configuration;
using System.Web;

namespace TheArqamSchools.Web.Helpers
{
    public static class Site
    {
        /// <summary>Public address of the site from Web.config (Site:Url), without a trailing slash.</summary>
        public static string BaseUrl
        {
            get { return (ConfigurationManager.AppSettings["Site:Url"] ?? "https://www.thearqam.com").TrimEnd('/'); }
        }

        public static string Absolute(string path)
        {
            if (string.IsNullOrEmpty(path)) return BaseUrl + "/";
            if (path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return path;
            return BaseUrl + (path.StartsWith("/") ? path : "/" + path);
        }

        /// <summary>Links from the CMS are only used if they are site-relative, https, mailto, tel or an in-page anchor.</summary>
        public static bool IsSafeHref(string href)
        {
            if (string.IsNullOrWhiteSpace(href)) return false;
            return (href.StartsWith("/") && !href.StartsWith("//")) || href.StartsWith("#")
                || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsExternal(string href)
        {
            return href != null && href.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Is this menu link the current section? "/about" is current on "/about"
        /// and below; "/" only on the homepage. Anchors and query strings are ignored.
        /// </summary>
        public static bool IsCurrent(string href, string currentPath)
        {
            if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(currentPath)) return false;
            var p = href.Split('#', '?')[0].TrimEnd('/');
            var c = currentPath.TrimEnd('/');
            if (p.Length == 0) return c.Length == 0;
            return c.Equals(p, StringComparison.OrdinalIgnoreCase) || c.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool RequireHttps
        {
            get { return !string.Equals(ConfigurationManager.AppSettings["Site:RequireHttps"], "false", StringComparison.OrdinalIgnoreCase); }
        }

        public static string Encode(string s) { return HttpUtility.HtmlEncode(s ?? ""); }
    }
}
