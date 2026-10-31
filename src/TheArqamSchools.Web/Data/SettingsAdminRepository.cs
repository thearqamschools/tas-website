using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Website settings (dbo.site_settings) edited with plain form fields instead of
    /// raw JSON. Only the settings listed in <see cref="Fields"/> can be changed here;
    /// values are stored as JSON exactly in the shape the public pages read.
    /// </summary>
    public static class SettingsAdminRepository
    {
        public enum Kind { Text, Multiline, Phone, Link }

        public class Field
        {
            public string Key;
            public string Label;
            public string Hint;
            public Kind Kind;
            public int Max;
            public string Group;
        }

        public static readonly Field[] Fields =
        {
            new Field { Group = "Website", Key = "site.name", Label = "Website name", Kind = Kind.Text, Max = 80 },
            new Field { Group = "Website", Key = "site.tagline", Label = "Tagline", Kind = Kind.Text, Max = 120 },
            new Field { Group = "Website", Key = "site.description", Label = "Website description", Kind = Kind.Multiline, Max = 300,
                Hint = "Shown by search engines and when a link is shared. Use only approved figures: 350+ campuses, 250,000+ students, 20,000+ Hafiz-e-Quran." },
            new Field { Group = "Contact (header and footer)", Key = "contact.phone", Label = "Phone", Kind = Kind.Phone, Max = 20 },
            new Field { Group = "Contact (header and footer)", Key = "contact.phone_alt", Label = "Second phone", Kind = Kind.Phone, Max = 20 },
            new Field { Group = "Contact (header and footer)", Key = "contact.whatsapp", Label = "WhatsApp", Kind = Kind.Phone, Max = 20 },
            new Field { Group = "Contact (header and footer)", Key = "contact.address", Label = "Address", Kind = Kind.Multiline, Max = 300 },
            new Field { Group = "Header", Key = "header.cta_apply", Label = "Red header button", Kind = Kind.Link, Max = 40 },
            new Field { Group = "Header", Key = "header.cta_branch", Label = "Second header button", Kind = Kind.Link, Max = 40 },
            new Field { Group = "Footer", Key = "footer.mission", Label = "Footer message", Kind = Kind.Multiline, Max = 300 },
            new Field { Group = "Footer", Key = "legal.copyright_holder", Label = "Copyright holder", Kind = Kind.Text, Max = 120 },
            new Field { Group = "Search engines", Key = "seo.org_locality", Label = "Head office city", Kind = Kind.Text, Max = 80 },
            new Field { Group = "Search engines", Key = "seo.org_region", Label = "Head office province", Kind = Kind.Text, Max = 80 }
        };

        /// <summary>Form slots for social links; "facebook2" is a second Facebook page (saved with platform "facebook").</summary>
        public static readonly string[] SocialPlatforms = { "facebook", "facebook2", "instagram", "youtube", "tiktok", "x", "linkedin", "whatsapp" };

        public static string PlatformOf(string slot) { return slot == "facebook2" ? "facebook" : slot; }

        /// <summary>The current values as form text: "text" for text settings; "label" and "href" for links.</summary>
        public static SettingsForm Load()
        {
            var keys = Fields.Select(f => f.Key).Concat(new[] { "social" }).ToArray();
            using (var db = Db.Open())
            {
                var rows = db.Query<KeyValue>("SELECT [key], [value] FROM dbo.site_settings WHERE [key] IN @keys", new { keys })
                    .ToDictionary(r => r.Key, r => r.Value);
                var form = new SettingsForm();
                foreach (var f in Fields)
                {
                    string json;
                    if (!rows.TryGetValue(f.Key, out json)) continue;
                    var token = Parse(json);
                    if (f.Kind == Kind.Link)
                    {
                        var o = token as JObject;
                        form.Values[f.Key + ".label"] = o == null ? null : (string)o["label"];
                        form.Values[f.Key + ".href"] = o == null ? null : (string)o["href"];
                    }
                    else
                    {
                        form.Values[f.Key] = token != null && token.Type == JTokenType.String ? (string)token : null;
                    }
                }
                string social;
                if (rows.TryGetValue("social", out social))
                {
                    var arr = Parse(social) as JArray;
                    if (arr != null)
                        foreach (var item in arr.OfType<JObject>())
                        {
                            var platform = (string)item["platform"];
                            var slot = platform == "facebook" && form.Values.ContainsKey("social.facebook") ? "facebook2" : platform;
                            if (slot != null && SocialPlatforms.Contains(slot) && !form.Values.ContainsKey("social." + slot))
                                form.Values["social." + slot] = (string)item["url"];
                            else
                                form.KeptSocial.Add(item);   // not shown on the form: kept as it is on save
                        }
                }
                return form;
            }
        }

        /// <summary>Saves the settings that changed, each with an audit row, in one transaction.</summary>
        public static int Save(IDictionary<string, string> jsonByKey, Guid actorId)
        {
            var changed = 0;
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                foreach (var pair in jsonByKey)
                {
                    var old = db.ExecuteScalar<string>("SELECT [value] FROM dbo.site_settings WITH (UPDLOCK) WHERE [key] = @key", new { key = pair.Key }, tx);
                    if (old != null && JToken.DeepEquals(Parse(old), Parse(pair.Value))) continue;
                    if (old == null)
                        db.Execute(
                            @"INSERT INTO dbo.site_settings ([key], [value], is_public, created_by, updated_by)
                              VALUES (@key, @value, 1, @actorId, @actorId)", new { key = pair.Key, value = pair.Value, actorId }, tx);
                    else
                        db.Execute(
                            @"UPDATE dbo.site_settings SET [value] = @value, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                              WHERE [key] = @key", new { key = pair.Key, value = pair.Value, actorId }, tx);
                    AuditLog.Write(db, tx, actorId, old == null ? "insert" : "update", "site_settings", pair.Key,
                        new { from = old == null ? null : Parse(old), to = Parse(pair.Value) });
                    changed++;
                }
                tx.Commit();
            }
            Cache.Remove("settings");
            return changed;
        }

        public static JToken Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JToken.Parse(json); }
            catch (JsonReaderException) { return null; }
        }

        private class KeyValue
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }
    }
}
