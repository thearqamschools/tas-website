using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Infrastructure;

namespace TheArqamSchools.Web.Data
{
    /// <summary>One photo of a news post or event (dbo.post_images joined to dbo.media).</summary>
    public class PostImage
    {
        public Guid Id { get; set; }
        public Guid MediaId { get; set; }
        public string StoragePath { get; set; }
        public string AltText { get; set; }
        public string Caption { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int SortOrder { get; set; }
        public bool IsCover { get; set; }
        public string Url { get { return "/media/public-media/" + StoragePath; } }
        public string SmallUrl { get { return "/media/public-media/" + ImageStore.SmallPath(StoragePath); } }
    }

    /// <summary>
    /// The photo gallery of a news post or event (Database/17). The main photo shown on cards
    /// is news.cover_media_id / events.cover_media_id; the first uploaded photo becomes the main
    /// photo until another one is chosen. Removing a photo sets deleted_at; nothing is deleted.
    /// Every change writes the audit log in the same transaction.
    /// </summary>
    public static class PostImageRepository
    {
        public const int MaxPhotosPerPost = 200;

        private static string Table(string kind) { return kind == "event" ? "dbo.events" : "dbo.news"; }
        private static string Column(string kind) { return kind == "event" ? "event_id" : "news_id"; }

        public static IList<PostImage> List(string kind, Guid postId)
        {
            using (var db = Db.Open())
                return db.Query<PostImage>(@"
                    SELECT pi.id, pi.media_id, m.storage_path, m.alt_text, pi.caption, m.width, m.height, pi.sort_order,
                           CAST(CASE WHEN p.cover_media_id = pi.media_id THEN 1 ELSE 0 END AS BIT) AS is_cover
                    FROM dbo.post_images pi
                    JOIN dbo.media m ON m.id = pi.media_id AND m.deleted_at IS NULL
                    JOIN " + Table(kind) + @" p ON p.id = pi." + Column(kind) + @"
                    WHERE pi." + Column(kind) + @" = @postId AND pi.deleted_at IS NULL
                    ORDER BY pi.sort_order, pi.created_at", new { postId }).ToList();
        }

        public static int Count(string kind, Guid postId)
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM dbo.post_images WHERE " + Column(kind) + " = @postId AND deleted_at IS NULL", new { postId });
        }

        /// <summary>Records a saved photo. Returns false when the post has reached the photo limit (the caller removes the files).</summary>
        public static bool Add(string kind, Guid postId, StoredImage img, string altText, bool canPublish, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                // The lock on the post keeps sort_order in step when several photos arrive at once.
                var post = db.QuerySingleOrDefault<PostLock>(
                    "SELECT status, cover_media_id FROM " + Table(kind) + " WITH (UPDLOCK, HOLDLOCK) WHERE id = @postId AND deleted_at IS NULL",
                    new { postId }, tx);
                if (post == null) throw new InvalidOperationException("The post was not found.");
                // Checked again under the lock, like NewsRepository.Save: the post may have been published a moment ago.
                if (!canPublish && post.Status != "draft") throw new InvalidOperationException("Only drafts can be changed without the publish permission.");
                var cover = post.CoverMediaId;
                var stats = db.QuerySingle<CountAndMax>(
                    "SELECT COUNT(*) AS n, COALESCE(MAX(sort_order), -1) AS max_order FROM dbo.post_images WHERE " + Column(kind) + " = @postId AND deleted_at IS NULL",
                    new { postId }, tx);
                if (stats.N >= MaxPhotosPerPost) { tx.Rollback(); return false; }

                var mediaId = db.ExecuteScalar<Guid>(@"
                    INSERT INTO dbo.media (bucket, storage_path, original_filename, mime_type, size_bytes, width, height, alt_text, checksum_sha256, created_by, updated_by)
                    OUTPUT inserted.id
                    VALUES (N'public-media', @StoragePath, @OriginalFileName, N'image/jpeg', @SizeBytes, @Width, @Height, @altText, @Sha256, @actorId, @actorId)",
                    new { img.StoragePath, img.OriginalFileName, img.SizeBytes, img.Width, img.Height, altText, img.Sha256, actorId }, tx);
                var imageId = db.ExecuteScalar<Guid>(@"
                    INSERT INTO dbo.post_images (" + Column(kind) + @", media_id, sort_order, created_by, updated_by)
                    OUTPUT inserted.id
                    VALUES (@postId, @mediaId, @order, @actorId, @actorId)",
                    new { postId, mediaId, order = stats.MaxOrder + 1, actorId }, tx);
                if (cover == null)
                    db.Execute("UPDATE " + Table(kind) + " SET cover_media_id = @mediaId, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @postId",
                        new { mediaId, postId, actorId }, tx);
                AuditLog.Write(db, tx, actorId, "insert", "post_images", imageId.ToString(),
                    new { kind, post_id = postId, media_id = mediaId, path = img.StoragePath, file = img.OriginalFileName, became_cover = cover == null });
                tx.Commit();
                return true;
            }
        }

        /// <summary>Makes one photo the main photo of the post. Returns false when the photo is not part of the post.</summary>
        public static bool SetCover(string kind, Guid postId, Guid imageId, bool canPublish, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var mediaId = LockPost(db, tx, kind, postId, canPublish) ? MediaOf(db, tx, kind, postId, imageId) : null;
                if (mediaId == null) { tx.Rollback(); return false; }
                db.Execute("UPDATE " + Table(kind) + " SET cover_media_id = @mediaId, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @postId",
                    new { mediaId, postId, actorId }, tx);
                // The main photo also becomes the first photo of the gallery, so the admin grid and the website show the same order.
                var ids = db.Query<Guid>(
                    "SELECT id FROM dbo.post_images WHERE " + Column(kind) + " = @postId AND deleted_at IS NULL ORDER BY CASE WHEN id = @imageId THEN 0 ELSE 1 END, sort_order, created_at",
                    new { postId, imageId }, tx).ToList();
                for (var k = 0; k < ids.Count; k++)
                    db.Execute("UPDATE dbo.post_images SET sort_order = @k, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @id AND sort_order <> @k",
                        new { k, id = ids[k], actorId }, tx);
                AuditLog.Write(db, tx, actorId, "update", Table(kind).Substring(4), postId.ToString(), new { cover_media_id = mediaId, moved_first = imageId });
                tx.Commit();
                return true;
            }
        }

        /// <summary>Moves a photo one place earlier (-1) or later (+1) in the gallery.</summary>
        public static bool Move(string kind, Guid postId, Guid imageId, int direction, bool canPublish, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                if (!LockPost(db, tx, kind, postId, canPublish)) { tx.Rollback(); return false; }
                var ids = db.Query<Guid>(
                    "SELECT id FROM dbo.post_images WHERE " + Column(kind) + " = @postId AND deleted_at IS NULL ORDER BY sort_order, created_at",
                    new { postId }, tx).ToList();
                var i = ids.IndexOf(imageId);
                var j = i + (direction < 0 ? -1 : 1);
                if (i < 0 || j < 0 || j >= ids.Count) { tx.Rollback(); return false; }
                ids[i] = ids[j];
                ids[j] = imageId;
                // Renumber 0, 1, 2 ... so the order stays clean.
                for (var k = 0; k < ids.Count; k++)
                    db.Execute("UPDATE dbo.post_images SET sort_order = @k, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @id",
                        new { k, id = ids[k], actorId }, tx);
                AuditLog.Write(db, tx, actorId, "update", "post_images", imageId.ToString(), new { kind, post_id = postId, moved = direction < 0 ? "earlier" : "later" });
                tx.Commit();
                return true;
            }
        }

        /// <summary>
        /// Takes a photo off the post (soft delete of the post_images and media rows; the files stay).
        /// When it was the main photo, the next photo becomes the main photo.
        /// </summary>
        public static string Remove(string kind, Guid postId, Guid imageId, bool canPublish, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var mediaId = LockPost(db, tx, kind, postId, canPublish) ? MediaOf(db, tx, kind, postId, imageId) : null;
                if (mediaId == null) { tx.Rollback(); return null; }
                var path = db.ExecuteScalar<string>("SELECT storage_path FROM dbo.media WHERE id = @mediaId", new { mediaId }, tx);
                db.Execute("UPDATE dbo.post_images SET deleted_at = SYSDATETIMEOFFSET(), updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @imageId",
                    new { imageId, actorId }, tx);
                db.Execute("UPDATE dbo.media SET deleted_at = SYSDATETIMEOFFSET(), updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @mediaId",
                    new { mediaId, actorId }, tx);
                var next = db.QueryFirstOrDefault<Guid?>(
                    "SELECT TOP (1) media_id FROM dbo.post_images WHERE " + Column(kind) + " = @postId AND deleted_at IS NULL ORDER BY sort_order, created_at",
                    new { postId }, tx);
                db.Execute("UPDATE " + Table(kind) + " SET cover_media_id = @next, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId WHERE id = @postId AND cover_media_id = @mediaId",
                    new { next, postId, mediaId, actorId }, tx);
                AuditLog.Write(db, tx, actorId, "update", "post_images", imageId.ToString(), new { kind, post_id = postId, media_id = mediaId, removed = true, files_moved_to = "App_Data/removed-media/public-media/" + path });
                tx.Commit();
                return path;
            }
        }

        /// <summary>Locks the post row; without the publish permission only a draft may be changed. False when not allowed or not found.</summary>
        private static bool LockPost(System.Data.IDbConnection db, System.Data.IDbTransaction tx, string kind, Guid postId, bool canPublish)
        {
            var status = db.QuerySingleOrDefault<string>(
                "SELECT status FROM " + Table(kind) + " WITH (UPDLOCK, HOLDLOCK) WHERE id = @postId AND deleted_at IS NULL", new { postId }, tx);
            return status != null && (canPublish || status == "draft");
        }

        private static Guid? MediaOf(System.Data.IDbConnection db, System.Data.IDbTransaction tx, string kind, Guid postId, Guid imageId)
        {
            return db.QuerySingleOrDefault<Guid?>(
                "SELECT media_id FROM dbo.post_images WHERE id = @imageId AND " + Column(kind) + " = @postId AND deleted_at IS NULL",
                new { imageId, postId }, tx);
        }

        private class PostLock
        {
            public string Status { get; set; }
            public Guid? CoverMediaId { get; set; }
        }

        private class CountAndMax
        {
            public int N { get; set; }
            public int MaxOrder { get; set; }
        }
    }
}
