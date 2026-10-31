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
    /// its structure: editors change words, links and switches, never the shape the
    /// section template expects. Technical values (icons, ids, image paths, layout
    /// options) are not shown and are kept as they are. A signature of the field paths
    /// travels with the form, so a save is refused if the section changed shape meanwhile.
    /// </summary>
    public static class JsonContentFields
    {
        public enum FieldKind { Text, LongText, Link, Number, Switch }

        public class Field
        {
            public string Path;
            public string Label;
            public FieldKind Kind;
            public string Value;
        }

        private static readonly HashSet<string> HiddenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "type", "icon", "variant", "layout", "style", "theme", "align", "alignment", "tone", "anchor",
            "bucket", "storage_path", "path", "media", "image", "image_url", "src", "background", "color", "colour", "width", "height"
        };

        private static readonly HashSet<string> LinkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "href", "url", "link"
        };

        public static IList<Field> Flatten(JObject root)
        {
            var fields = new List<Field>();
            Walk(root, fields);
            return fields;
        }

        /// <summary>Hash of every editable field's path and kind, in order.</summary>
        public static string Signature(IList<Field> fields)
        {
            var text = string.Join("\n", fields.Select(f => f.Path + "|" + f.Kind));
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        /// <summary>
        /// Writes posted values into <paramref name="root"/>. Returns error messages (empty when
        /// all values are acceptable); nothing is written when there are errors.
        /// </summary>
        public static IList<string> Apply(JObject root, IList<Field> fields, Func<int, string> posted)
        {
            var errors = new List<string>();
            var values = new List<JToken>();
            for (var i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                var raw = posted(i);
                switch (f.Kind)
                {
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
                        else values.Add(number == decimal.Truncate(number) && Math.Abs(number) < int.MaxValue ? new JValue((long)number) : new JValue(number));
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
                if (token is JValue && values[i] != null) ((JValue)token).Value = ((JValue)values[i]).Value;
            }
            return errors;
        }

        private static void Walk(JToken token, List<Field> fields)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    foreach (var p in ((JObject)token).Properties())
                    {
                        if (HiddenKeys.Contains(p.Name) || p.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)) continue;
                        Walk(p.Value, fields);
                    }
                    break;
                case JTokenType.Array:
                    foreach (var item in (JArray)token) Walk(item, fields);
                    break;
                case JTokenType.String:
                    var key = KeyOf(token);
                    var s = (string)token;
                    var kind = LinkKeys.Contains(key ?? "") ? FieldKind.Link
                        : (s.Length > 90 || s.Contains("\n") || (key ?? "").IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0
                           || (key ?? "").IndexOf("description", StringComparison.OrdinalIgnoreCase) >= 0
                           || (key ?? "").IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0
                           || (key ?? "").IndexOf("answer", StringComparison.OrdinalIgnoreCase) >= 0) ? FieldKind.LongText
                        : FieldKind.Text;
                    fields.Add(new Field { Path = token.Path, Label = Label(token.Path), Kind = kind, Value = s });
                    break;
                case JTokenType.Integer:
                case JTokenType.Float:
                    fields.Add(new Field { Path = token.Path, Label = Label(token.Path), Kind = FieldKind.Number, Value = ((IFormattable)((JValue)token).Value).ToString(null, CultureInfo.InvariantCulture) });
                    break;
                case JTokenType.Boolean:
                    fields.Add(new Field { Path = token.Path, Label = Label(token.Path), Kind = FieldKind.Switch, Value = (bool)token ? "true" : "false" });
                    break;
            }
        }

        private static string KeyOf(JToken token)
        {
            var prop = token.Parent as JProperty;
            return prop == null ? null : prop.Name;
        }

        /// <summary>"items[2].cta.label" becomes "Items 3 › Cta › Label".</summary>
        private static string Label(string path)
        {
            var parts = new List<string>();
            foreach (var segment in path.Split('.'))
            {
                var name = segment;
                string index = null;
                var b = segment.IndexOf('[');
                if (b >= 0)
                {
                    name = segment.Substring(0, b);
                    int n;
                    if (int.TryParse(segment.Substring(b + 1).TrimEnd(']'), out n)) index = (n + 1).ToString(CultureInfo.InvariantCulture);
                }
                name = name.Replace("_", " ").Trim('\'', '[', ']');
                if (name.Length > 0) name = char.ToUpperInvariant(name[0]) + name.Substring(1);
                parts.Add(index == null ? name : (name + " " + index).Trim());
            }
            return string.Join(" › ", parts.Where(p => p.Length > 0));
        }

        public static bool IsSafeLink(string href)
        {
            if (href.Any(char.IsWhiteSpace) || href.Length > 500) return false;
            return (href.StartsWith("/") && !href.StartsWith("//")) || href.StartsWith("#")
                || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
