using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TheArqamSchools.Web.Data;

namespace TheArqamSchools.Web.Helpers
{
    /// <summary>One social media link from the website settings.</summary>
    public class SocialLink
    {
        public string Platform { get; set; }
        public string Url { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
    }

    /// <summary>
    /// Social media links for the header and footer, read from the "social" website setting
    /// (admin panel > Settings). WhatsApp comes first, as the school asked; when the list has
    /// no WhatsApp entry, the official WhatsApp number (contact.whatsapp) is used.
    /// Only https:// links are shown.
    /// </summary>
    public static class Social
    {
        public const string FacebookFallback = "https://www.facebook.com/thearqamfsdOfficial";

        private static readonly string[] Order = { "whatsapp", "facebook", "instagram", "tiktok", "youtube", "linkedin", "x" };

        public static IList<SocialLink> Links(IDictionary<string, JToken> settings)
        {
            var list = (SiteSettingsRepository.Json(settings, "social") as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(o => new SocialLink { Platform = ((string)o["platform"] ?? "").Trim().ToLowerInvariant(), Url = ((string)o["url"] ?? "").Trim() })
                .Where(l => l.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!list.Any(l => l.Platform == "whatsapp"))
            {
                var wa = SiteSettingsRepository.Text(settings, "contact.whatsapp");
                if (!string.IsNullOrWhiteSpace(wa)) list.Add(new SocialLink { Platform = "whatsapp", Url = Contact.WhatsAppUrl(wa) });
            }
            if (!list.Any(l => l.Platform == "facebook")) list.Add(new SocialLink { Platform = "facebook", Url = FacebookFallback });

            foreach (var l in list)
            {
                l.Name = NameOf(l.Platform);
                l.Icon = IconOf(l.Platform);
            }
            return list
                .GroupBy(l => l.Url.TrimEnd('/'), StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .Select((l, i) => new { l, i, rank = Array.IndexOf(Order, l.Platform) })
                .OrderBy(x => x.rank < 0 ? Order.Length : x.rank).ThenBy(x => x.i)
                .Select(x => x.l).ToList();
        }

        private static string NameOf(string platform)
        {
            switch (platform)
            {
                case "whatsapp": return "WhatsApp";
                case "facebook": return "Facebook";
                case "instagram": return "Instagram";
                case "tiktok": return "TikTok";
                case "youtube": return "YouTube";
                case "linkedin": return "LinkedIn";
                case "x": return "X";
                default: return string.IsNullOrEmpty(platform) ? "Link" : char.ToUpperInvariant(platform[0]) + platform.Substring(1);
            }
        }

        private static string IconOf(string platform)
        {
            switch (platform)
            {
                case "whatsapp":
                case "facebook":
                case "instagram":
                case "tiktok":
                case "youtube":
                case "linkedin":
                    return platform;
                default: return "external";
            }
        }
    }
}
