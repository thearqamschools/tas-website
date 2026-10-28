using System;
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

        public static T Get<T>(string key, Func<T> load) where T : class
        {
            var hit = MemoryCache.Default.Get(key) as T;
            if (hit != null) return hit;
            var value = load();
            if (value != null)
                MemoryCache.Default.Set(key, value, DateTimeOffset.UtcNow.Add(Short));
            return value;
        }

        public static void Remove(string key) { MemoryCache.Default.Remove(key); }
    }
}
