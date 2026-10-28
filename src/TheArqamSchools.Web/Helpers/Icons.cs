using System.Collections.Generic;
using System.Web;

namespace TheArqamSchools.Web.Helpers
{
    /// <summary>
    /// Small line icons (24 x 24, drawn with the current text colour). Section
    /// data refers to them by name, e.g. "book-open". Unknown names draw nothing.
    /// </summary>
    public static class Icons
    {
        private static readonly Dictionary<string, string> Paths = new Dictionary<string, string>
        {
            { "book-open", "<path d='M2 4h6a4 4 0 0 1 4 4v12a3 3 0 0 0-3-3H2z'/><path d='M22 4h-6a4 4 0 0 0-4 4v12a3 3 0 0 1 3-3h7z'/>" },
            { "moon-star", "<path d='M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9z'/><path d='M19 3v4'/><path d='M21 5h-4'/>" },
            { "shield-check", "<path d='M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z'/><path d='m9 12 2 2 4-4'/>" },
            { "users", "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M22 21v-2a4 4 0 0 0-3-3.87'/><path d='M16 3.13a4 4 0 0 1 0 7.75'/>" },
            { "heart-handshake", "<path d='M19 14c1.5-1.5 3-3.2 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.8 0-3 .5-4.5 2-1.5-1.5-2.7-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4 3 5.5l7 7z'/><path d='M12 5 9 8a2 2 0 0 0 3 3l2-2 3 3'/>" },
            { "compass", "<circle cx='12' cy='12' r='10'/><path d='m16 8-2 6-6 2 2-6z'/>" },
            { "sparkles", "<path d='M12 3l1.9 5.1L19 10l-5.1 1.9L12 17l-1.9-5.1L5 10l5.1-1.9z'/><path d='M20 3v4'/><path d='M22 5h-4'/><path d='M4 17v2'/><path d='M5 18H3'/>" },
            { "graduation-cap", "<path d='M22 10 12 5 2 10l10 5z'/><path d='M6 12v5c3 2 9 2 12 0v-5'/><path d='M22 10v6'/>" },
            { "map-pin", "<path d='M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 0 1 16 0z'/><circle cx='12' cy='10' r='3'/>" },
            { "phone", "<path d='M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1 1 .4 1.9.7 2.8a2 2 0 0 1-.5 2.1L8 9.9a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.4c.9.3 1.8.6 2.8.7a2 2 0 0 1 1.7 2z'/>" },
            { "whatsapp", "<path d='M3 21l1.7-5A9 9 0 1 1 8 19.3z'/><path d='M9 9.5c0 3 2.5 5.5 5.5 5.5l1.5-1.5-2-1-1 1a4 4 0 0 1-2.5-2.5l1-1-1-2z'/>" },
            { "mail", "<rect x='2' y='4' width='20' height='16' rx='2'/><path d='m22 7-10 6L2 7'/>" },
            { "clock", "<circle cx='12' cy='12' r='10'/><path d='M12 6v6l4 2'/>" },
            { "search", "<circle cx='11' cy='11' r='7'/><path d='m21 21-4.3-4.3'/>" },
            { "chevron-down", "<path d='m6 9 6 6 6-6'/>" },
            { "chevron-right", "<path d='m9 18 6-6-6-6'/>" },
            { "arrow-right", "<path d='M5 12h14'/><path d='m12 5 7 7-7 7'/>" },
            { "menu", "<path d='M4 6h16'/><path d='M4 12h16'/><path d='M4 18h16'/>" },
            { "x", "<path d='M18 6 6 18'/><path d='m6 6 12 12'/>" },
            { "check", "<path d='M20 6 9 17l-5-5'/>" },
            { "check-circle", "<circle cx='12' cy='12' r='10'/><path d='m8 12 3 3 5-6'/>" },
            { "award", "<circle cx='12' cy='8' r='6'/><path d='M15.5 13 17 22l-5-3-5 3 1.5-9'/>" },
            { "clipboard-check", "<rect x='8' y='2' width='8' height='4' rx='1'/><path d='M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2'/><path d='m9 14 2 2 4-4'/>" },
            { "headset", "<path d='M3 14v-3a9 9 0 0 1 18 0v3'/><path d='M21 15a2 2 0 0 1-2 2h-1v-6h1a2 2 0 0 1 2 2z'/><path d='M3 15a2 2 0 0 0 2 2h1v-6H5a2 2 0 0 0-2 2z'/><path d='M18 17a4 4 0 0 1-4 4h-2'/>" },
            { "star", "<path d='m12 2 3.1 6.3 6.9 1-5 4.9 1.2 6.8L12 17.8 5.8 21l1.2-6.8-5-4.9 6.9-1z'/>" },
            { "wallet", "<path d='M19 7V5a2 2 0 0 0-2-2H5a2 2 0 0 0 0 4h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5'/><path d='M17 14h.01'/>" },
            { "facebook", "<path d='M18 2h-3a5 5 0 0 0-5 5v3H7v4h3v8h4v-8h3l1-4h-4V7a1 1 0 0 1 1-1h3z'/>" },
            { "external", "<path d='M15 3h6v6'/><path d='M10 14 21 3'/><path d='M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6'/>" },
        };

        public static IHtmlString Svg(string name, int size = 24, string cssClass = "icon")
        {
            string body;
            if (string.IsNullOrEmpty(name) || !Paths.TryGetValue(name, out body)) return new HtmlString("");
            return new HtmlString(
                "<svg class=\"" + HttpUtility.HtmlAttributeEncode(cssClass) + "\" width=\"" + size + "\" height=\"" + size +
                "\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\">" +
                body + "</svg>");
        }
    }
}
