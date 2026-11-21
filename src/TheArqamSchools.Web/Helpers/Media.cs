using System;
using Newtonsoft.Json.Linq;

namespace TheArqamSchools.Web.Helpers
{
    public static class Media
    {
        /// <summary>
        /// Public URL of a stored file. Files in the public-media bucket are served
        /// from /media/public-media/...; private buckets are never linked directly.
        /// </summary>
        public static string Url(string bucket, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/")) return path;
            if (!string.IsNullOrEmpty(bucket) && !bucket.StartsWith("public-", StringComparison.Ordinal)) return null;
            return "/media/" + (string.IsNullOrEmpty(bucket) ? "public-media" : bucket) + "/" + path.TrimStart('/');
        }

        /// <summary>URL of an image object in section JSON ({bucket, path, alt, width, height}).</summary>
        public static string Url(JToken image)
        {
            if (image == null || image.Type != JTokenType.Object) return null;
            return Url(image.Value<string>("bucket"), image.Value<string>("path"));
        }

        /// <summary>
        /// Where a photo sits in its frame and how far it is zoomed (owner's request, 21 November 2026): the admin panel's
        /// "Adjust" stores fx / fy (0 to 100, as in CSS object-position) and zoom (1 to 3) in the image object; the file itself
        /// is never changed. Returns CSS custom properties such as "--fx-d:40%;--fy-d:50%;--z-d:1.2;" (empty when not set),
        /// read by the stylesheet. <paramref name="suffix"/> keeps the computer and phone settings apart.
        /// </summary>
        public static string FocusVars(JToken image, string suffix)
        {
            if (image == null || image.Type != JTokenType.Object) return "";
            double fx, fy, z;
            var sb = new System.Text.StringBuilder();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (TryNumber(image["fx"], 0, 100, out fx)) sb.Append("--fx-").Append(suffix).Append(':').Append(fx.ToString("0.#", inv)).Append("%;");
            if (TryNumber(image["fy"], 0, 100, out fy)) sb.Append("--fy-").Append(suffix).Append(':').Append(fy.ToString("0.#", inv)).Append("%;");
            if (TryNumber(image["zoom"], 1, 3, out z)) sb.Append("--z-").Append(suffix).Append(':').Append(z.ToString("0.##", inv)).Append(';');
            return sb.ToString();
        }

        internal static bool TryNumber(JToken t, double min, double max, out double value)
        {
            value = 0;
            if (t == null || (t.Type != JTokenType.Float && t.Type != JTokenType.Integer)) return false;
            value = t.Value<double>();
            return !double.IsNaN(value) && value >= min && value <= max;
        }

        public static string Alt(JToken image)
        {
            return image == null || image.Type != JTokenType.Object ? "" : image.Value<string>("alt") ?? "";
        }

        /// <summary>Images whose alt text starts with "Placeholder" are stand-ins until the school sends a real photo.</summary>
        public static bool IsPlaceholder(string alt)
        {
            return alt != null && alt.StartsWith("Placeholder", StringComparison.OrdinalIgnoreCase);
        }
    }
}
