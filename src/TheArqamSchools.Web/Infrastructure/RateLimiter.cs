using System;
using System.Runtime.Caching;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// Limits how often one visitor (IP address) can submit a form: at most
    /// <c>max</c> submissions in <c>window</c>. Kept in memory; resets when the site restarts.
    /// </summary>
    public static class RateLimiter
    {
        private class Counter { public int Count; }
        private static readonly object Lock = new object();

        public static bool Allow(string bucket, string clientKey, int max, TimeSpan window)
        {
            var key = "rl:" + bucket + ":" + (clientKey ?? "unknown");
            lock (Lock)
            {
                var c = MemoryCache.Default.Get(key) as Counter;
                if (c == null)
                {
                    MemoryCache.Default.Set(key, new Counter { Count = 1 }, DateTimeOffset.UtcNow.Add(window));
                    return true;
                }
                if (c.Count >= max) return false;
                c.Count++;
                return true;
            }
        }
    }
}
