using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    public static class NavigationRepository
    {
        /// <summary>
        /// The visible items of a menu ("header", "utility", "footer") as a tree.
        /// An item gets a dropdown only if it has visible children; About Us,
        /// Academics, Admissions and Campuses have none, so they are plain links.
        /// </summary>
        public static IList<NavItem> Menu(string key)
        {
            return Cache.Get("nav:" + key, () =>
            {
                using (var db = Db.Open())
                {
                    var rows = db.Query<NavItem>(
                        @"SELECT i.id, i.parent_id, i.label, COALESCE(i.url, p.path) AS url, i.target, i.description, i.sort_order
                          FROM dbo.navigation_items i
                          JOIN dbo.navigation_menus m ON m.id = i.menu_id
                          LEFT JOIN dbo.pages p ON p.id = i.page_id AND p.status = N'published'
                          WHERE m.[key] = @key AND i.is_visible = 1
                          ORDER BY i.sort_order, i.label", new { key }).ToList();
                    var byId = rows.ToDictionary(r => r.Id);
                    var roots = new List<NavItem>();
                    foreach (var r in rows)
                    {
                        if (string.IsNullOrEmpty(r.Url)) continue;
                        NavItem parent;
                        if (r.ParentId.HasValue && byId.TryGetValue(r.ParentId.Value, out parent))
                            parent.Children.Add(r);
                        else if (!r.ParentId.HasValue)
                            roots.Add(r);
                    }
                    return (IList<NavItem>)roots;
                }
            });
        }
    }
}
