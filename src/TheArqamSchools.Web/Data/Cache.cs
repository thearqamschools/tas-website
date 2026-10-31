using System;
using System.Diagnostics;
using System.Runtime.Caching;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Short in-memory cache for data that every page reads (menus, settings,
    /// redirects). Changes made in the admin panel appear within a minute.
    /// </summary>
    public static class Cache
    {
        public static readonly TimeSpan Short = TimeSpan.FromSeconds(60);
        private const string LastGoodPrefix = "lastgood:";

        public static T Get<T>(string key, Func<T> load) where T : class
        {
            var hit = MemoryCache.Default.Get(key) as T;
            if (hit != null) return hit;
            var value = load();
            if (value != null)
                MemoryCache.Default.Set(key, value, DateTimeOffset.UtcNow.Add(Short));
            return value;
        }

        /// <summary>
        /// Like <see cref="Get{T}"/>, but if the database is unreachable it returns the
        /// last value that loaded successfully, or <paramref name="fallback"/> when there
        /// is none yet (for example right after the site starts). Used only for data a
        /// page can show slightly out of date or leave out: menus, settings, footer
        /// offices and the homepage campus finder. Other errors are not caught.
        /// </summary>
        public static T GetOrLastGood<T>(string key, Func<T> load, Func<T> fallback) where T : class
        {
            var hit = MemoryCache.Default.Get(key) as T;
            if (hit != null) return hit;
            try
            {
                var value = load();
                if (value != null)
                {
                    MemoryCache.Default.Set(key, value, DateTimeOffset.UtcNow.Add(Short));
                    MemoryCache.Default.Set(LastGoodPrefix + key, value, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                }
                return value;
            }
            catch (Exception ex) when (Db.IsUnavailable(ex))
            {
                Trace.TraceWarning("Database unavailable; serving last good copy of '{0}'. {1}", key, ex.Message);
                return MemoryCache.Default.Get(LastGoodPrefix + key) as T ?? fallback();
            }
        }

        public static void Remove(string key) { MemoryCache.Default.Remove(key); }
    }
}
