using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Website menus (dbo.navigation_menus / navigation_items) for the admin panel.
    /// Items are hidden, never deleted. Each item links either to a CMS page (page_id)
    /// or to an address (url), never both, as the database requires.
    /// </summary>
    public static class MenuAdminRepository
    {
        public static IList<MenuAdminView> All()
        {
            using (var db = Db.Open())
            {
                var menus = db.Query<MenuAdminView>("SELECT id, [key], name FROM dbo.navigation_menus ORDER BY name").ToList();
                var items = db.Query<MenuItemRow>(
                    @"SELECT i.id, i.menu_id, i.parent_id, i.label, i.url, i.page_id, p.path AS page_path, p.status AS page_status,
                             i.target, i.sort_order, i.is_visible
                      FROM dbo.navigation_items i LEFT JOIN dbo.pages p ON p.id = i.page_id
                      ORDER BY i.sort_order, i.label").ToList();
                foreach (var m in menus)
                {
                    var mine = items.Where(i => i.MenuId == m.Id).ToList();
                    m.Items = new List<MenuItemRow>();
                    foreach (var root in mine.Where(i => i.ParentId == null || !mine.Any(x => x.Id == i.ParentId)))
                    {
                        m.Items.Add(root);
                        foreach (var child in mine.Where(i => i.ParentId == root.Id))
                        {
                            child.Depth = 1;
                            m.Items.Add(child);
                        }
                    }
                }
                return menus;
            }
        }

        public static MenuAdminView Menu(Guid id)
        {
            return All().FirstOrDefault(m => m.Id == id);
        }

        public static MenuItemForm GetItem(Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<MenuItemForm>(
                    @"SELECT id, menu_id, parent_id, label, url, page_id, target, description, sort_order, is_visible
                      FROM dbo.navigation_items WHERE id = @id", new { id });
        }

        public static IList<PageOption> Pages()
        {
            using (var db = Db.Open())
                return db.Query<PageOption>(
                    "SELECT id, title, path, status FROM dbo.pages WHERE deleted_at IS NULL AND status <> N'archived' ORDER BY path").ToList();
        }

        public static Guid Save(MenuItemForm f, bool isNew, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                MenuItemForm before = null;
                if (isNew)
                {
                    f.Id = db.ExecuteScalar<Guid>(
                        @"INSERT INTO dbo.navigation_items (menu_id, parent_id, label, url, page_id, target, description, sort_order, is_visible, created_by, updated_by)
                          OUTPUT inserted.id
                          VALUES (@MenuId, @ParentId, @Label, @Url, @PageId, @Target, @Description, @SortOrder, @IsVisible, @actorId, @actorId)",
                        new { f.MenuId, f.ParentId, f.Label, f.Url, f.PageId, f.Target, f.Description, f.SortOrder, f.IsVisible, actorId }, tx);
                }
                else
                {
                    before = db.QuerySingle<MenuItemForm>(
                        @"SELECT id, menu_id, parent_id, label, url, page_id, target, description, sort_order, is_visible
                          FROM dbo.navigation_items WITH (UPDLOCK) WHERE id = @Id", new { f.Id }, tx);
                    db.Execute(
                        @"UPDATE dbo.navigation_items SET parent_id = @ParentId, label = @Label, url = @Url, page_id = @PageId,
                                 target = @Target, description = @Description, sort_order = @SortOrder, is_visible = @IsVisible,
                                 updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                          WHERE id = @Id",
                        new { f.Id, f.ParentId, f.Label, f.Url, f.PageId, f.Target, f.Description, f.SortOrder, f.IsVisible, actorId }, tx);
                }
                var key = db.ExecuteScalar<string>("SELECT [key] FROM dbo.navigation_menus WHERE id = @MenuId", new { f.MenuId }, tx);
                AuditLog.Write(db, tx, actorId, isNew ? "insert" : "update", "navigation_items", f.Id.ToString(), new
                {
                    menu = key,
                    before = before == null ? null : new { before.Label, before.Url, before.PageId, before.ParentId, before.Target, before.SortOrder, before.IsVisible },
                    after = new { f.Label, f.Url, f.PageId, f.ParentId, f.Target, f.SortOrder, f.IsVisible }
                });
                tx.Commit();
                if (key != null) Cache.Remove("nav:" + key);
                return f.Id;
            }
        }
    }
}
