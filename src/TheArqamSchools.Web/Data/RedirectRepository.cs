using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace TheArqamSchools.Web.Data
{
    public class RedirectRule
    {
        public Guid Id { get; set; }
        public string FromPath { get; set; }
        public string ToPath { get; set; }
        public short StatusCode { get; set; }
    }

    /// <summary>Redirects managed in the admin panel (dbo.redirects).</summary>
    public static class RedirectRepository
    {
        public static RedirectRule Find(string path)
        {
            var all = Cache.Get("redirects", () =>
            {
                using (var db = Db.Open())
                {
                    return db.Query<RedirectRule>("SELECT id, from_path, to_path, status_code FROM dbo.redirects")
                        .GroupBy(r => r.FromPath.TrimEnd('/').ToLowerInvariant())
                        .ToDictionary(g => g.Key.Length == 0 ? "/" : g.Key, g => g.First());
                }
            });
            RedirectRule rule;
            return all.TryGetValue(path, out rule) ? rule : null;
        }

        public static void RecordHit(Guid id)
        {
            try
            {
                using (var db = Db.Open())
                {
                    db.Execute("UPDATE dbo.redirects SET hit_count = hit_count + 1, last_hit_at = SYSDATETIMEOFFSET() WHERE id = @id", new { id });
                }
            }
            catch (Exception)
            {
                // Counting a hit must never stop the redirect.
            }
        }
    }
}
