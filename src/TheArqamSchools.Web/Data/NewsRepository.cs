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
    /// News (dbo.news) and events (dbo.events) for the public "News &amp; Events" page and
    /// the admin panel. News posts carry a category (Achievement, Campus, Announcement);
    /// events are their own kind ("Event") with a date. Text is stored in the body JSON as
    /// { "text": "..." } and shown as plain paragraphs (never as HTML).
    /// </summary>
    public static class NewsRepository
    {
        public static readonly string[] Statuses = { "draft", "published", "archived" };

        /// <summary>Filter keys shown as buttons, in order. "event" means the events table.</summary>
        public static readonly string[] FilterKeys = { "achievement", "event", "campus", "announcement" };

        public static string FilterLabel(string key)
        {
            switch (key)
            {
                case "achievement": return "Achievement";
                case "event": return "Event";
                case "campus": return "Campus";
                case "announcement": return "Announcement";
                default: return "All";
            }
        }

        private const string PublicFeed = @"
            SELECT N'news' AS kind, n.id, n.slug, n.title, n.excerpt AS summary, n.body,
                   COALESCE(c.slug, N'announcement') AS category, n.published_at AS date, CAST(NULL AS DATETIMEOFFSET(3)) AS ends_at,
                   CAST(NULL AS NVARCHAR(200)) AS venue, b.name AS campus_name, n.is_featured
            FROM dbo.news n
            LEFT JOIN dbo.categories c ON c.id = n.category_id
            LEFT JOIN dbo.branches b ON b.id = n.branch_id
            WHERE n.status = N'published' AND n.deleted_at IS NULL AND n.published_at IS NOT NULL
            UNION ALL
            SELECT N'event', e.id, e.slug, e.title, e.summary, e.body,
                   N'event', e.starts_at, e.ends_at, e.venue, b.name, CAST(0 AS BIT)
            FROM dbo.events e
            LEFT JOIN dbo.branches b ON b.id = e.branch_id
            WHERE e.status = N'published' AND e.deleted_at IS NULL";

        /// <summary>Published items, newest first, optionally only one filter key.</summary>
        public static IList<NewsItem> Published(string filter, int max)
        {
            filter = FilterKeys.Contains(filter) ? filter : null;
            using (var db = Db.Open())
                return db.Query<NewsItem>(
                    "SELECT TOP (@max) * FROM (" + PublicFeed + ") x WHERE (@filter IS NULL OR category = @filter) ORDER BY date DESC",
                    new { filter, max }).ToList();
        }

        public static NewsItem PublishedBySlug(string slug)
        {
            using (var db = Db.Open())
                return db.QueryFirstOrDefault<NewsItem>("SELECT * FROM (" + PublicFeed + ") x WHERE slug = @slug", new { slug });
        }

        // ---- Admin ----------------------------------------------------------------------

        public static IList<NewsItem> AdminList()
        {
            using (var db = Db.Open())
                return db.Query<NewsItem>(@"
                    SELECT N'news' AS kind, n.id, n.slug, n.title, COALESCE(c.slug, N'announcement') AS category,
                           COALESCE(n.published_at, n.created_at) AS date, n.status, n.is_featured
                    FROM dbo.news n LEFT JOIN dbo.categories c ON c.id = n.category_id WHERE n.deleted_at IS NULL
                    UNION ALL
                    SELECT N'event', e.id, e.slug, e.title, N'event', e.starts_at, e.status, CAST(0 AS BIT)
                    FROM dbo.events e WHERE e.deleted_at IS NULL
                    ORDER BY date DESC").ToList();
        }

        public static NewsEditForm Get(string kind, Guid id)
        {
            using (var db = Db.Open())
            {
                NewsEditForm f;
                if (kind == "event")
                    f = db.QuerySingleOrDefault<NewsEditForm>(@"
                        SELECT id, title, slug, summary, body, branch_id, starts_at, ends_at, is_all_day, venue, status
                        FROM dbo.events WHERE id = @id AND deleted_at IS NULL", new { id });
                else
                    f = db.QuerySingleOrDefault<NewsEditForm>(@"
                        SELECT n.id, n.title, n.slug, n.excerpt AS summary, n.body, n.branch_id, n.is_featured, n.status,
                               COALESCE(c.slug, N'announcement') AS category
                        FROM dbo.news n LEFT JOIN dbo.categories c ON c.id = n.category_id
                        WHERE n.id = @id AND n.deleted_at IS NULL", new { id });
                if (f == null) return null;
                f.Kind = kind == "event" ? "event" : "news";
                f.Text = TextOf(f.Body);
                return f;
            }
        }

        /// <summary>True when another news post or event already uses the slug (slugs are shared by one address space).</summary>
        public static bool SlugTaken(string slug, Guid exceptId)
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>(
                    @"SELECT (SELECT COUNT(*) FROM dbo.news WHERE slug = @slug AND deleted_at IS NULL AND id <> @exceptId)
                           + (SELECT COUNT(*) FROM dbo.events WHERE slug = @slug AND deleted_at IS NULL AND id <> @exceptId)",
                    new { slug, exceptId }) > 0;
        }

        public static Guid Save(NewsEditForm f, bool isNew, bool canPublish, Guid actorId)
        {
            var body = JsonConvert.SerializeObject(new { text = f.Text ?? "" });
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                string oldStatus = null;
                if (!isNew)
                    oldStatus = db.ExecuteScalar<string>(
                        "SELECT status FROM " + (f.Kind == "event" ? "dbo.events" : "dbo.news") + " WITH (UPDLOCK) WHERE id = @Id", new { f.Id }, tx);
                if (!canPublish && (f.Status == "published" || oldStatus == "published")) f.Status = oldStatus ?? "draft";

                if (f.Kind == "event")
                {
                    if (isNew)
                        f.Id = db.ExecuteScalar<Guid>(@"
                            INSERT INTO dbo.events (title, slug, summary, body, branch_id, starts_at, ends_at, is_all_day, timezone, venue, status,
                                                    published_at, created_by, updated_by)
                            OUTPUT inserted.id
                            VALUES (@Title, @Slug, @Summary, @body, @BranchId, @StartsAt, @EndsAt, @IsAllDay, N'Asia/Karachi', @Venue, @Status,
                                    CASE WHEN @Status = N'published' THEN SYSDATETIMEOFFSET() END, @actorId, @actorId)",
                            new { f.Title, f.Slug, f.Summary, body, f.BranchId, f.StartsAt, f.EndsAt, f.IsAllDay, f.Venue, f.Status, actorId }, tx);
                    else
                        db.Execute(@"
                            UPDATE dbo.events SET title = @Title, slug = @Slug, summary = @Summary, body = @body, branch_id = @BranchId,
                                   starts_at = @StartsAt, ends_at = @EndsAt, is_all_day = @IsAllDay, venue = @Venue, status = @Status,
                                   published_at = CASE WHEN @Status = N'published' AND published_at IS NULL THEN SYSDATETIMEOFFSET() ELSE published_at END,
                                   updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                            WHERE id = @Id",
                            new { f.Id, f.Title, f.Slug, f.Summary, body, f.BranchId, f.StartsAt, f.EndsAt, f.IsAllDay, f.Venue, f.Status, actorId }, tx);
                }
                else
                {
                    var categoryId = db.ExecuteScalar<Guid?>(
                        "SELECT id FROM dbo.categories WHERE [type] = N'news' AND slug = @Category", new { f.Category }, tx);
                    if (isNew)
                        f.Id = db.ExecuteScalar<Guid>(@"
                            INSERT INTO dbo.news (title, slug, excerpt, body, branch_id, category_id, is_featured, status, published_at, author_id, created_by, updated_by)
                            OUTPUT inserted.id
                            VALUES (@Title, @Slug, @Summary, @body, @BranchId, @categoryId, @IsFeatured, @Status,
                                    CASE WHEN @Status = N'published' THEN SYSDATETIMEOFFSET() END, @actorId, @actorId, @actorId)",
                            new { f.Title, f.Slug, f.Summary, body, f.BranchId, categoryId, f.IsFeatured, f.Status, actorId }, tx);
                    else
                        db.Execute(@"
                            UPDATE dbo.news SET title = @Title, slug = @Slug, excerpt = @Summary, body = @body, branch_id = @BranchId,
                                   category_id = @categoryId, is_featured = @IsFeatured, status = @Status,
                                   published_at = CASE WHEN @Status = N'published' AND published_at IS NULL THEN SYSDATETIMEOFFSET() ELSE published_at END,
                                   updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                            WHERE id = @Id",
                            new { f.Id, f.Title, f.Slug, f.Summary, body, f.BranchId, categoryId, f.IsFeatured, f.Status, actorId }, tx);
                }
                AuditLog.Write(db, tx, actorId, isNew ? "insert" : "update", f.Kind == "event" ? "events" : "news", f.Id.ToString(),
                    new { f.Title, f.Slug, f.Status, from_status = oldStatus, f.Category, f.StartsAt });
                tx.Commit();
                return f.Id;
            }
        }

        /// <summary>The plain text kept in the body JSON ({ "text": ... }), or "" for anything else.</summary>
        public static string TextOf(string bodyJson)
        {
            if (string.IsNullOrWhiteSpace(bodyJson)) return "";
            try
            {
                var o = JObject.Parse(bodyJson);
                var t = o["text"];
                return t != null && t.Type == JTokenType.String ? (string)t : "";
            }
            catch (JsonReaderException) { return ""; }
        }
    }
}
