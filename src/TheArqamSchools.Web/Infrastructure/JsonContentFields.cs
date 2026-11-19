using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// Turns a page section's JSON into plain form fields and back, without changing
    /// its structure: editors change words, links, switches and photos, never the shape the
    /// section template expects. Technical values (icons, ids, layout options) are not shown
    /// and are kept as they are. A signature of the field paths and values travels with the
    /// form, so a save is refused if the section changed meanwhile.
    /// Lists of items (stages, leaders, cards, slides ...) can be added to, ordered and
    /// shortened with <see cref="AddItem"/>, <see cref="RemoveItem"/> and <see cref="MoveItem"/>.
    /// </summary>
    public static class JsonContentFields
    {
        public enum FieldKind { Text, LongText, Link, Number, Switch, Image }

        public class Field
        {
            public string Path;
            public string Label;
            public FieldKind Kind;
            public string Value;
            public bool WholeNumber;
            /// <summary>True for text that may use **bold** and *italic* (the website shows them).</summary>
            public bool Formatted;
            /// <summary>For images: the current picture's address and description (alt text).</summary>
            public string ImageUrl;
            public string Alt;
            /// <summary>The list item this field belongs to, for example "items[2]" (null for fields outside a list).</summary>
            public string ItemPath;
            public string ItemLabel;
            /// <summary>The list the item belongs to, for example "items", and the item's position and the list length.</summary>
            public string ListPath;
            public int ItemIndex;
            public int ListCount;
            public string ItemNoun;
        }

        private static readonly HashSet<string> HiddenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "type", "icon", "variant", "layout", "page_intro", "style", "theme", "align", "alignment", "tone", "anchor",
            "bucket", "storage_path", "path", "media", "image_url", "src", "background", "color", "colour", "width", "height",
            "image_side", "compact", "columns", "stage"
        };

        /// <summary>Keys that hold a picture ({bucket, path, alt, width, height}, or null when there is none yet).</summary>
        public static readonly HashSet<string> ImageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image", "seal", "badge", "photo", "mobile_image", "whole_child_image"
        };

        private static readonly HashSet<string> LinkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "href", "url", "link"
        };

        /// <summary>Text keys that may carry **bold** and *italic*; the section templates show these with SimpleFormat.</summary>
        public static readonly HashSet<string> FormattedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "body", "intro", "summary", "description", "text", "answer", "message", "lead", "quote",
            "hero_intro", "whole_child_lead", "finder_lead", "admissions_body"
        };

        private static readonly Dictionary<string, string> KeyLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "heading", "Heading" }, { "title", "Title" }, { "eyebrow", "Small heading above the title" }, { "intro", "Introduction" },
            { "body", "Text" }, { "summary", "Summary" }, { "description", "Description" }, { "text", "Text" }, { "ages", "Ages" },
            { "points", "Point" }, { "classes", "Classes" }, { "name", "Name" }, { "role", "Role" }, { "message", "Message" },
            { "question", "Question" }, { "answer", "Answer" }, { "label", "Text" }, { "href", "Link address" }, { "link", "Link" },
            { "primary_cta", "Main button" }, { "secondary_cta", "Second button" }, { "cta", "Button" }, { "image", "Photo" },
            { "mobile_image", "Photo for phones" }, { "seal", "Seal picture" }, { "badge", "Badge picture" }, { "year", "Year" },
            { "vision", "Vision" }, { "mission", "Mission" }, { "quote", "Quote" }, { "success", "Message after sending" },
            { "value", "Number" }, { "lead", "Text" },
            { "hero_badge", "Top photo – Badge" }, { "hero_line_1", "Top photo – Heading, line 1" }, { "hero_line_2", "Top photo – Heading, line 2" },
            { "hero_intro", "Top photo – Text" }, { "hero_primary_cta", "Top photo – Main button" }, { "hero_secondary_cta", "Top photo – Second button" },
            { "explore_heading", "Explore – Heading" }, { "whole_child_heading", "Educating the whole child – Heading" },
            { "whole_child_lead", "Educating the whole child – Text" }, { "whole_child_link", "Educating the whole child – Link" },
            { "whole_child_image", "Educating the whole child – Photo" }, { "news_heading", "News – Heading" },
            { "finder_heading", "Find a campus – Heading" }, { "finder_lead", "Find a campus – Text" },
            { "admissions_heading", "Admissions banner – Heading" }, { "admissions_body", "Admissions banner – Text" },
            { "admissions_cta", "Admissions banner – Button" }
        };

        /// <summary>What one item of a list is called, by list name (and section type for "items").</summary>
        private static string Noun(string listName, string sectionType)
        {
            switch ((listName ?? "").ToLowerInvariant())
            {
                case "steps": return "Step";
                case "slides": return "Slide";
                case "figures": return "Figure";
                case "tiles": return "Explore tile";
                case "whole_child_points": return "Whole child point";
                case "points": return "Point";
                case "body": return "Paragraph";
                case "items":
                    switch (sectionType)
                    {
                        case "academic_levels": return "Stage";
                        case "leadership": return "Person";
                        case "values": return "Value";
                        case "card_grid": return "Card";
                        case "faq": return "Question";
                        case "timeline": return "Milestone";
                        default: return "Item";
                    }
                default: return "Item";
            }
        }

        public static IList<Field> Flatten(JObject root, string sectionType = null)
        {
            var fields = new List<Field>();
            Walk(root, fields, sectionType ?? "");
            foreach (var f in fields)
            {
                var item = TopItem(f.Path);
                if (item == null) continue;
                var list = item.Substring(0, item.IndexOf('['));
                var arr = root[list] as JArray;
                f.ItemPath = item;
                f.ListPath = list;
                f.ItemIndex = int.Parse(item.Substring(item.IndexOf('[') + 1).TrimEnd(']'), CultureInfo.InvariantCulture);
                f.ListCount = arr == null ? 0 : arr.Count;
                f.ItemNoun = Noun(list, sectionType);
                f.ItemLabel = f.ItemNoun + " " + (f.ItemIndex + 1).ToString(CultureInfo.InvariantCulture);
            }
            return fields;
        }

        /// <summary>"items[2]" for "items[2].points[0]", when the top-level value is a list of objects; otherwise null.</summary>
        private static string TopItem(string path)
        {
            var b = path.IndexOf('[');
            if (b <= 0 || path.Substring(0, b).Contains(".")) return null;
            var e = path.IndexOf(']', b);
            if (e < 0 || e + 1 >= path.Length || path[e + 1] != '.') return null;
            return path.Substring(0, e + 1);
        }

        /// <summary>
        /// Hash of every editable field's path and current value, in order. If anyone saves the
        /// section (or it changes shape) while a form is open, that form's save is refused
        /// instead of silently overwriting their words.
        /// </summary>
        public static string Signature(IList<Field> fields)
        {
            var text = string.Join("\n", fields.Select(f => f.Path + "\u0001" + (f.Value ?? "") + "\u0001" + (f.Alt ?? "")));
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        /// <summary>
        /// Writes posted values into <paramref name="root"/>. Returns error messages (empty when
        /// all values are acceptable); nothing is written when there are errors. Image fields take
        /// only their description here (posted as "a{i}"); new pictures are set with <see cref="SetImage"/>.
        /// </summary>
        public static IList<string> Apply(JObject root, IList<Field> fields, Func<int, string> posted, Func<int, string> postedAlt = null)
        {
            var errors = new List<string>();
            var values = new List<JToken>();
            for (var i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                var raw = posted(i);
                switch (f.Kind)
                {
                    case FieldKind.Image:
                        var alt = postedAlt == null ? null : (postedAlt(i) ?? "").Replace("\r\n", " ").Replace("\n", " ").Trim();
                        if (alt != null && alt.Length > 250) errors.Add(f.Label + ": the photo description can be at most 250 characters.");
                        values.Add(alt == null ? null : new JValue(alt));
                        break;
                    case FieldKind.Switch:
                        values.Add(new JValue(raw != null && raw.Split(',').Contains("true")));
                        break;
                    case FieldKind.Number:
                        decimal number;
                        if (string.IsNullOrWhiteSpace(raw) || !decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out number))
                        {
                            errors.Add(f.Label + ": please enter a number.");
                            values.Add(null);
                        }
                        else if (f.WholeNumber && (number != decimal.Truncate(number) || Math.Abs(number) > long.MaxValue / 2))
                        {
                            errors.Add(f.Label + ": please enter a whole number.");
                            values.Add(null);
                        }
                        else values.Add(f.WholeNumber ? new JValue((long)number) : new JValue(number));
                        break;
                    default:
                        var text = (raw ?? "").Replace("\r\n", "\n").Trim();
                        if (text.Length > 10000) errors.Add(f.Label + ": the text is too long.");
                        if (f.Kind == FieldKind.Link && text.Length > 0 && !IsSafeLink(text))
                            errors.Add(f.Label + ": a link must start with / (a page on this website), #, https://, mailto: or tel:.");
                        if (f.Kind != FieldKind.LongText && text.Contains("\n")) text = text.Replace("\n", " ");
                        values.Add(new JValue(text));
                        break;
                }
            }
            if (errors.Count > 0) return errors;
            for (var i = 0; i < fields.Count; i++)
            {
                var token = root.SelectToken(fields[i].Path);
                if (fields[i].Kind == FieldKind.Image)
                {
                    var obj = token as JObject;
                    if (obj != null && values[i] != null) obj["alt"] = values[i];
                    continue;
                }
                if (token is JValue && values[i] != null) ((JValue)token).Value = ((JValue)values[i]).Value;
            }
            return errors;
        }

        /// <summary>Puts a new picture (or none) at an image field's place, keeping its description when no new one is given.</summary>
        public static void SetImage(JObject root, Field field, StoredImage img, string alt)
        {
            var token = root.SelectToken(field.Path);
            if (token == null) return;
            var old = token as JObject;
            if (img == null)
            {
                token.Replace(JValue.CreateNull());
                return;
            }
            var description = !string.IsNullOrWhiteSpace(alt) ? alt : old != null ? (string)old["alt"] ?? "" : "";
            token.Replace(new JObject
            {
                { "bucket", "public-media" }, { "path", img.StoragePath }, { "alt", description },
                { "width", img.Width }, { "height", img.Height }
            });
        }

        // ---- Lists ----------------------------------------------------------------------

        /// <summary>
        /// Adds an item at the end of a top-level list, shaped like the last item with its words
        /// emptied, its picture removed and its technical values (icons) kept.
        /// </summary>
        public static bool AddItem(JObject root, string listPath, int max = 60)
        {
            var arr = root[listPath] as JArray;
            if (arr == null || arr.Count == 0 || arr.Count >= max || arr.Last.Type != JTokenType.Object) return false;
            var copy = (JObject)arr.Last.DeepClone();
            Blank(copy);
            arr.Add(copy);
            return true;
        }

        public static bool RemoveItem(JObject root, string listPath, int index)
        {
            var arr = root[listPath] as JArray;
            if (arr == null || arr.Count <= 1 || index < 0 || index >= arr.Count) return false;
            arr.RemoveAt(index);
            return true;
        }

        public static bool MoveItem(JObject root, string listPath, int index, int direction)
        {
            var arr = root[listPath] as JArray;
            var j = index + (direction < 0 ? -1 : 1);
            if (arr == null || index < 0 || index >= arr.Count || j < 0 || j >= arr.Count) return false;
            var item = arr[index];
            arr.RemoveAt(index);
            arr.Insert(j, item);
            return true;
        }

        private static void Blank(JToken token)
        {
            var obj = token as JObject;
            if (obj != null)
            {
                foreach (var p in obj.Properties().ToList())
                {
                    if (ImageKeys.Contains(p.Name)) { p.Value = JValue.CreateNull(); continue; }
                    if (p.Name.Equals("stage", StringComparison.OrdinalIgnoreCase)) { p.Value = ""; continue; }
                    if (HiddenKeys.Contains(p.Name) || p.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)) continue;
                    Blank(p.Value);
                }
                return;
            }
            var arr = token as JArray;
            if (arr != null) { foreach (var x in arr) Blank(x); return; }
            var v = token as JValue;
            if (v != null && v.Type == JTokenType.String) v.Value = "";
        }

        // ---- Reading the JSON -----------------------------------------------------------

        private static void Walk(JToken token, List<Field> fields, string sectionType)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    foreach (var p in ((JObject)token).Properties())
                    {
                        if (ImageKeys.Contains(p.Name) && (p.Value.Type == JTokenType.Null || p.Value.Type == JTokenType.Object))
                        {
                            var o = p.Value as JObject;
                            fields.Add(new Field
                            {
                                Path = p.Value.Path, Label = Label(p.Value.Path, sectionType), Kind = FieldKind.Image,
                                Value = o == null ? "" : (string)o["path"] ?? "",
                                ImageUrl = o == null ? null : Helpers.Media.Url(o),
                                Alt = o == null ? "" : (string)o["alt"] ?? ""
                            });
                            continue;
                        }
                        if (HiddenKeys.Contains(p.Name) || p.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)) continue;
                        Walk(p.Value, fields, sectionType);
                    }
                    break;
                case JTokenType.Array:
                    foreach (var item in (JArray)token) Walk(item, fields, sectionType);
                    break;
                case JTokenType.String:
                    var key = KeyOf(token);
                    var s = (string)token;
                    // Strings in a list (paragraphs of "body", "points") take the list's name.
                    var listKey = key == null && token.Parent is JArray && token.Parent.Parent is JProperty ? ((JProperty)token.Parent.Parent).Name : null;
                    var formatted = (key != null && FormattedKeys.Contains(key)) || (listKey != null && (FormattedKeys.Contains(listKey) || listKey == "points"));
                    var kind = LinkKeys.Contains(key ?? "") ? FieldKind.Link
                        : (s.Length > 90 || s.Contains("\n") || (key ?? "").IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0
                           || (key ?? "").IndexOf("description", StringComparison.OrdinalIgnoreCase) >= 0
                           || (key ?? "").IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0
                           || (key ?? "").IndexOf("answer", StringComparison.OrdinalIgnoreCase) >= 0
                           || (key ?? "").IndexOf("message", StringComparison.OrdinalIgnoreCase) >= 0
                           || (key ?? "").IndexOf("summary", StringComparison.OrdinalIgnoreCase) >= 0
                           || (key ?? "").IndexOf("intro", StringComparison.OrdinalIgnoreCase) >= 0) ? FieldKind.LongText
                        : FieldKind.Text;
                    fields.Add(new Field { Path = token.Path, Label = Label(token.Path, sectionType), Kind = kind, Value = s, Formatted = formatted && kind != FieldKind.Link });
                    break;
                case JTokenType.Integer:
                case JTokenType.Float:
                    fields.Add(new Field
                    {
                        Path = token.Path, Label = Label(token.Path, sectionType), Kind = FieldKind.Number, WholeNumber = token.Type == JTokenType.Integer,
                        Value = ((IFormattable)((JValue)token).Value).ToString(null, CultureInfo.InvariantCulture)
                    });
                    break;
                case JTokenType.Boolean:
                    fields.Add(new Field { Path = token.Path, Label = Label(token.Path, sectionType), Kind = FieldKind.Switch, Value = (bool)token ? "true" : "false" });
                    break;
            }
        }

        /// <summary>
        /// Plain names for the admin form: "items[0].points[2]" in academic levels becomes
        /// "Stage 1 – Point 3"; "primary_cta.label" becomes "Main button – Text".
        /// </summary>
        public static string Label(string path, string sectionType)
        {
            var parts = new List<string>();
            foreach (var segment in path.Split('.'))
            {
                var name = segment.Trim('\'', '[', ']');
                string index = null;
                var b = segment.IndexOf('[');
                if (b >= 0)
                {
                    name = segment.Substring(0, b);
                    int n;
                    if (int.TryParse(segment.Substring(b + 1).TrimEnd(']'), out n)) index = (n + 1).ToString(CultureInfo.InvariantCulture);
                }
                string friendly;
                if (index != null) friendly = Noun(name, sectionType) + " " + index;
                else if (!KeyLabels.TryGetValue(name, out friendly))
                {
                    friendly = name.Replace("_", " ").Trim();
                    if (friendly.Length > 0) friendly = char.ToUpperInvariant(friendly[0]) + friendly.Substring(1);
                }
                if (friendly.Length > 0) parts.Add(friendly);
            }
            return string.Join(" – ", parts);
        }

        private static string KeyOf(JToken token)
        {
            var prop = token.Parent as JProperty;
            return prop == null ? null : prop.Name;
        }

        public static bool IsSafeLink(string href)
        {
            if (href.Any(char.IsWhiteSpace) || href.Length > 500) return false;
            return (href.StartsWith("/") && !href.StartsWith("//") && !href.StartsWith("/\\")) || href.StartsWith("#")
                || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
