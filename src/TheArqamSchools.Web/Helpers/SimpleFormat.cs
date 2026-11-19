using System.Text.RegularExpressions;
using System.Web;

namespace TheArqamSchools.Web.Helpers
{
    /// <summary>
    /// Bold and italic in news and event text (owner, 11 November 2026). The admin panel's B and I
    /// buttons wrap words in **bold** and *italic*. The text is HTML-encoded first, then only these
    /// two marks become &lt;strong&gt; and &lt;em&gt;, so nothing else typed in the text can become HTML.
    /// </summary>
    public static class SimpleFormat
    {
        private static readonly Regex Bold = new Regex(@"\*\*(?=\S)([^\n]{1,500}?)(?<=\S)\*\*", RegexOptions.Compiled);
        private static readonly Regex Italic = new Regex(@"(?<![\*\w])\*(?=[^\s\*])([^\n]{1,500}?)(?<=[^\s\*])\*(?![\*\w])", RegexOptions.Compiled);

        public static IHtmlString Render(string text)
        {
            if (string.IsNullOrEmpty(text)) return new HtmlString("");
            var html = HttpUtility.HtmlEncode(text);
            html = Bold.Replace(html, "<strong>$1</strong>");
            html = Italic.Replace(html, "<em>$1</em>");
            return new HtmlString(html);
        }

        /// <summary>The text without the marks, for cards, page descriptions and search results.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Italic.Replace(Bold.Replace(text, "$1"), "$1");
        }
    }
}
