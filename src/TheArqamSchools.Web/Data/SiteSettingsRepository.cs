using System.Collections.Generic;
using System.Linq;
using Dapper;
using Newtonsoft.Json.Linq;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Public site settings (dbo.site_settings, is_public = 1). Values are stored
    /// as JSON, exactly as in the previous version of the website.
    /// </summary>
    public static class SiteSettingsRepository
    {
        public static IDictionary<string, JToken> Public()
        {
            return Cache.Get("settings", () =>
            {
                using (var db = Db.Open())
                {
                    return (IDictionary<string, JToken>)db.Query<(string Key, string Value)>(
                            "SELECT [key], [value] FROM dbo.site_settings WHERE is_public = 1")
                        .ToDictionary(r => r.Key, r => JToken.Parse(r.Value));
                }
            });
        }

        public static string Text(IDictionary<string, JToken> settings, string key, string fallback = "")
        {
            JToken v;
            return settings.TryGetValue(key, out v) && v.Type == JTokenType.String ? (string)v : fallback;
        }

        public static JToken Json(IDictionary<string, JToken> settings, string key)
        {
            JToken v;
            return settings.TryGetValue(key, out v) ? v : null;
        }
    }
}
