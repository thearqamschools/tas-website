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
