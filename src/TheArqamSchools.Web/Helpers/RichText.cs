using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

namespace TheArqamSchools.Web.Helpers
{
    /// <summary>
    /// Turns the editor's document JSON (paragraphs, headings, lists, bold,
    /// italic, links) into HTML. All text is HTML-encoded; only https, mailto,
    /// tel and site-relative links are kept.
    /// </summary>
    public static class RichText
    {
        public static IHtmlString Render(JToken doc)
        {
            var sb = new StringBuilder();
            if (doc != null && doc.Type == JTokenType.String) sb.Append("<p>").Append(HttpUtility.HtmlEncode((string)doc)).Append("</p>");
            else Node(doc, sb);
            return new HtmlString(sb.ToString());
        }

        private static void Children(JToken node, StringBuilder sb)
        {
            var content = node["content"] as JArray;
            if (content == null) return;
            foreach (var c in content) Node(c, sb);
        }

        private static void Node(JToken node, StringBuilder sb)
        {
            if (node == null || node.Type != JTokenType.Object) return;
            switch (node.Value<string>("type"))
            {
                case "doc": Children(node, sb); break;
                case "paragraph": sb.Append("<p>"); Children(node, sb); sb.Append("</p>"); break;
                case "heading":
                    var level = node["attrs"] != null ? node["attrs"].Value<int?>("level") ?? 2 : 2;
                    if (level < 2) level = 2;
                    if (level > 4) level = 4;
                    sb.Append("<h").Append(level).Append('>'); Children(node, sb); sb.Append("</h").Append(level).Append('>');
                    break;
                case "bulletList": sb.Append("<ul>"); Children(node, sb); sb.Append("</ul>"); break;
                case "orderedList": sb.Append("<ol>"); Children(node, sb); sb.Append("</ol>"); break;
                case "listItem": sb.Append("<li>"); Children(node, sb); sb.Append("</li>"); break;
                case "blockquote": sb.Append("<blockquote>"); Children(node, sb); sb.Append("</blockquote>"); break;
                case "hardBreak": sb.Append("<br>"); break;
                case "text": Text(node, sb); break;
            }
        }

        private static void Text(JToken node, StringBuilder sb)
        {
            var text = HttpUtility.HtmlEncode(node.Value<string>("text") ?? "");
            var marks = node["marks"] as JArray;
            if (marks != null)
            {
                foreach (var m in marks)
                {
                    switch (m.Value<string>("type"))
                    {
                        case "bold": text = "<strong>" + text + "</strong>"; break;
                        case "italic": text = "<em>" + text + "</em>"; break;
                        case "link":
                            var href = m["attrs"] != null ? m["attrs"].Value<string>("href") : null;
                            if (Site.IsSafeHref(href))
                                text = "<a href=\"" + HttpUtility.HtmlAttributeEncode(href) + "\">" + text + "</a>";
                            break;
                    }
                }
            }
            sb.Append(text);
        }
    }
}
