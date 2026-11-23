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
        /// The part of a photo chosen with "Adjust" in the admin panel (owner's notes 48 and 52), like Facebook's
        /// "Reposition": fx / fy is the point of the photo (0-100 % of its width / height) that sits in the middle of the
        /// frame, and zoom (1-3) enlarges it beyond "just covers the frame". The file itself is never changed.
        /// Returns CSS custom properties for the stylesheet, which places the photo with the same formula as the admin
        /// preview: "--cx-d:0.4;--cy-d:0.2;--z-d:1.3;--ar-d:2.438;" (empty when nothing was chosen, or the photo's size
        /// is not known, so the website keeps its usual position). <paramref name="suffix"/> is d (computer) or m (phone).
        /// </summary>
        public static string FocusVars(JToken image, string suffix)
        {
            if (image == null || image.Type != JTokenType.Object) return "";
            double fx, fy, z, w, h;
            if (!TryNumber(image["fx"], 0, 100, out fx) || !TryNumber(image["fy"], 0, 100, out fy)) return "";
            if (!TryNumber(image["width"], 1, 100000, out w) || !TryNumber(image["height"], 1, 100000, out h)) return "";
            if (!TryNumber(image["zoom"], 1, 3, out z)) z = 1;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return "--cx-" + suffix + ":" + (fx / 100).ToString("0.####", inv) + ";--cy-" + suffix + ":" + (fy / 100).ToString("0.####", inv)
                 + ";--z-" + suffix + ":" + z.ToString("0.##", inv) + ";--ar-" + suffix + ":" + (w / h).ToString("0.####", inv) + ";";
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
