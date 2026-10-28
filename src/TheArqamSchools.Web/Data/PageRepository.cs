using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Newtonsoft.Json.Linq;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    public static class PageRepository
    {
        private const string Columns = "id, parent_id, title, slug, path, template, summary, status, seo_id, updated_at";

        /// <summary>A published page by its path, e.g. "/about". Archived and draft pages are not public.</summary>
        public static Page PublishedByPath(string path)
        {
            using (var db = Db.Open())
            {
                return db.QueryFirstOrDefault<Page>(
                    "SELECT " + Columns + " FROM dbo.pages WHERE path = @path AND status = N'published' AND deleted_at IS NULL",
                    new { path });
            }
        }

        public static IList<PageSection> VisibleSections(Guid pageId)
        {
            using (var db = Db.Open())
            {
                return db.Query("SELECT id, [type], data, sort_order FROM dbo.page_sections WHERE page_id = @pageId AND is_visible = 1 ORDER BY sort_order", new { pageId })
                    .Select(r => new PageSection
                    {
                        Id = (Guid)r.id,
                        Type = (string)r.type,
                        Data = JObject.Parse((string)r.data),
                        SortOrder = (int)r.sort_order
                    })
                    .ToList();
            }
        }

        /// <summary>Published pages for the sitemap.</summary>
        public static IList<Page> AllPublished()
        {
            using (var db = Db.Open())
            {
                return db.Query<Page>("SELECT " + Columns + " FROM dbo.pages WHERE status = N'published' AND deleted_at IS NULL ORDER BY path").ToList();
            }
        }

        public static SeoMetadata Seo(Guid? id)
        {
            if (id == null) return null;
            using (var db = Db.Open())
            {
                return db.QueryFirstOrDefault<SeoMetadata>(
                    "SELECT id, meta_title, meta_description, og_title, og_description, canonical_url, robots_noindex, robots_nofollow FROM dbo.seo_metadata WHERE id = @id",
                    new { id });
            }
        }

        public static SeoMetadata SeoForRoute(string routePath)
        {
            using (var db = Db.Open())
            {
                return db.QueryFirstOrDefault<SeoMetadata>(
                    "SELECT id, meta_title, meta_description, og_title, og_description, canonical_url, robots_noindex, robots_nofollow FROM dbo.seo_metadata WHERE route_path = @routePath",
                    new { routePath });
            }
        }
    }
}
