using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// CMS pages (dbo.pages, dbo.page_sections, dbo.seo_metadata) for the admin panel.
    /// Section text is edited through JsonContentFields. Users with pages.publish save
    /// straight to the live section (data); users with only pages.update save a draft
    /// (draft_data) that a publisher then publishes or discards. A publisher's own save
    /// leaves a waiting draft untouched, so nobody's draft is lost silently. The public pages read
    /// the database directly, so published changes show at once.
    /// </summary>
    public static class PageAdminRepository
    {
        public static readonly string[] Statuses = { "draft", "in_review", "published", "archived" };

        public static IList<PageListRow> List()
        {
            using (var db = Db.Open())
                return db.Query<PageListRow>(
                    @"SELECT p.id, p.title, p.path, p.status, p.updated_at,
                             (SELECT COUNT(*) FROM dbo.page_sections s WHERE s.page_id = p.id) AS section_count,
                             (SELECT COUNT(*) FROM dbo.page_sections s WHERE s.page_id = p.id AND s.draft_data IS NOT NULL) AS draft_count
                      FROM dbo.pages p WHERE p.deleted_at IS NULL
                      ORDER BY CASE p.status WHEN N'archived' THEN 1 ELSE 0 END, p.path").ToList();
        }

        public static PageEditForm Get(Guid id)
        {
            using (var db = Db.Open())
            {
                var page = db.QuerySingleOrDefault<PageEditForm>(
                    @"SELECT p.id, p.title, p.path, p.summary, p.status, p.seo_id, s.meta_title, s.meta_description
                      FROM dbo.pages p LEFT JOIN dbo.seo_metadata s ON s.id = p.seo_id
                      WHERE p.id = @id AND p.deleted_at IS NULL", new { id });
                if (page == null) return null;
                page.Sections = db.Query<SectionRow>(
                    @"SELECT id, [type], sort_order, is_visible, CASE WHEN draft_data IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS has_draft
                      FROM dbo.page_sections WHERE page_id = @id ORDER BY sort_order", new { id }).ToList();
                return page;
            }
        }

        public static void SavePage(PageEditForm f, bool canPublish, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var before = db.QuerySingle<PageEditForm>(
                    @"SELECT p.id, p.title, p.summary, p.status, p.seo_id, s.meta_title, s.meta_description
                      FROM dbo.pages p WITH (UPDLOCK) LEFT JOIN dbo.seo_metadata s ON s.id = p.seo_id WHERE p.id = @Id", new { f.Id }, tx);
                if (!canPublish && (f.Status == "published" || f.Status == "archived" || before.Status == "published" || before.Status == "archived"))
                    f.Status = before.Status;
                if (!canPublish && before.Status == "published")
                {
                    // Visitors see these at once, so on a published page only a publisher may change them.
                    f.Title = before.Title; f.Summary = before.Summary; f.MetaTitle = before.MetaTitle; f.MetaDescription = before.MetaDescription;
                }

                db.Execute(
                    @"UPDATE dbo.pages SET title = @Title, summary = @Summary, status = @Status,
                             published_at = CASE WHEN @Status = N'published' AND status <> N'published' THEN SYSDATETIMEOFFSET() ELSE published_at END,
                             updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                      WHERE id = @Id", new { f.Id, f.Title, f.Summary, f.Status, actorId }, tx);

                if (before.SeoId.HasValue)
                    db.Execute(
                        @"UPDATE dbo.seo_metadata SET meta_title = @MetaTitle, meta_description = @MetaDescription,
                                 updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @seoId",
                        new { f.MetaTitle, f.MetaDescription, seoId = before.SeoId.Value, actorId }, tx);
                else if (f.MetaTitle != null || f.MetaDescription != null)
                {
                    var seoId = db.ExecuteScalar<Guid>(
                        @"INSERT INTO dbo.seo_metadata (meta_title, meta_description, created_by, updated_by) OUTPUT inserted.id
                          VALUES (@MetaTitle, @MetaDescription, @actorId, @actorId)", new { f.MetaTitle, f.MetaDescription, actorId }, tx);
                    db.Execute("UPDATE dbo.pages SET seo_id = @seoId WHERE id = @Id", new { seoId, f.Id }, tx);
                }

                AuditLog.Write(db, tx, actorId, "update", "pages", f.Id.ToString(), new
                {
                    before = new { before.Title, before.Summary, before.Status, before.MetaTitle, before.MetaDescription },
                    after = new { f.Title, f.Summary, f.Status, f.MetaTitle, f.MetaDescription }
                });
                tx.Commit();
            }
        }

        public static SectionContent GetSection(Guid pageId, Guid sectionId)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<SectionContent>(
                    @"SELECT s.id, s.page_id, s.[type], s.data, s.draft_data, s.is_visible, p.title AS page_title, p.path AS page_path, p.status AS page_status
                      FROM dbo.page_sections s JOIN dbo.pages p ON p.id = s.page_id
                      WHERE s.id = @sectionId AND s.page_id = @pageId AND p.deleted_at IS NULL", new { pageId, sectionId });
        }

        /// <summary>Saves edited content live (publish) or as a draft. The audit row keeps the old and new JSON.</summary>
        public static void SaveSection(Guid sectionId, string json, bool live, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var before = db.QuerySingle<SectionContent>(
                    "SELECT id, data, draft_data FROM dbo.page_sections WITH (UPDLOCK) WHERE id = @sectionId", new { sectionId }, tx);
                db.Execute(live
                    ? "UPDATE dbo.page_sections SET data = @json, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @sectionId"
                    : "UPDATE dbo.page_sections SET draft_data = @json, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @sectionId",
                    new { sectionId, json, actorId }, tx);
                AuditLog.Write(db, tx, actorId, "update", "page_sections", sectionId.ToString(), new
                {
                    saved_as = live ? "published" : "draft",
                    from = JsonConvert.DeserializeObject(live ? before.Data : (before.DraftData ?? before.Data)),
                    to = JsonConvert.DeserializeObject(json)
                });
                tx.Commit();
            }
        }

        public static void PublishDraft(Guid sectionId, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var before = db.QuerySingle<SectionContent>(
                    "SELECT id, data, draft_data FROM dbo.page_sections WITH (UPDLOCK) WHERE id = @sectionId", new { sectionId }, tx);
                if (before.DraftData != null)
                {
                    db.Execute(
                        @"UPDATE dbo.page_sections SET data = draft_data, draft_data = NULL, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                          WHERE id = @sectionId", new { sectionId, actorId }, tx);
                    AuditLog.Write(db, tx, actorId, "update", "page_sections", sectionId.ToString(), new
                    {
                        @event = "draft_published",
                        from = JsonConvert.DeserializeObject(before.Data),
                        to = JsonConvert.DeserializeObject(before.DraftData)
                    });
                }
                tx.Commit();
            }
        }

        public static void DiscardDraft(Guid sectionId, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var draft = db.ExecuteScalar<string>("SELECT draft_data FROM dbo.page_sections WITH (UPDLOCK) WHERE id = @sectionId", new { sectionId }, tx);
                if (draft != null)
                {
                    db.Execute("UPDATE dbo.page_sections SET draft_data = NULL, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @sectionId",
                        new { sectionId, actorId }, tx);
                    AuditLog.Write(db, tx, actorId, "update", "page_sections", sectionId.ToString(), new
                    {
                        @event = "draft_discarded",
                        draft = JsonConvert.DeserializeObject(draft)
                    });
                }
                tx.Commit();
            }
        }

        public static void SetSectionVisible(Guid sectionId, bool visible, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                db.Execute("UPDATE dbo.page_sections SET is_visible = @visible, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @sectionId",
                    new { sectionId, visible, actorId }, tx);
                AuditLog.Write(db, tx, actorId, "update", "page_sections", sectionId.ToString(), new { is_visible = visible });
                tx.Commit();
            }
        }
    }
}
