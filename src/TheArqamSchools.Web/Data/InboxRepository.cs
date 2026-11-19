using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// The admin inbox: admission inquiries (dbo.admission_inquiries) and messages
    /// (dbo.contact_messages) sent from the website. Table and column names come only
    /// from the fixed <see cref="InboxKind"/> definitions below, never from the request;
    /// everything the user types is passed as a parameter.
    /// </summary>
    public static class InboxRepository
    {
        public const int PageSize = 25;
        public const int ExportLimit = 5000;
        public static readonly string[] Statuses = { "new", "in_progress", "resolved", "spam" };

        private const string InquirySelect =
            @"SELECT i.id, i.name, i.phone, i.email, i.message, i.status, i.source_page, i.created_at, i.updated_at,
                     i.resolved_at AS closed_at, b.name AS campus_name, c.name AS class_name,
                     CAST(NULL AS NVARCHAR(14)) AS [type], CAST(NULL AS NVARCHAR(200)) AS subject
              FROM dbo.admission_inquiries i
              LEFT JOIN dbo.branches b ON b.id = i.branch_id
              LEFT JOIN dbo.class_levels c ON c.id = i.class_level_id
              WHERE i.deleted_at IS NULL";

        private const string MessageSelect =
            @"SELECT i.id, i.name, i.phone, i.email, i.message, i.status, i.source_page, i.created_at, i.updated_at,
                     i.replied_at AS closed_at, b.name AS campus_name, CAST(NULL AS NVARCHAR(80)) AS class_name,
                     i.[type], i.subject
              FROM dbo.contact_messages i
              LEFT JOIN dbo.branches b ON b.id = i.branch_id
              WHERE i.deleted_at IS NULL";

        private const string Filter =
            @" AND (@status IS NULL OR i.status = @status)
               AND (@like IS NULL OR i.name LIKE @like OR i.phone LIKE @like OR i.email LIKE @like)";

        public static InboxListViewModel List(InboxKind kind, string status, string q, int page)
        {
            status = Statuses.Contains(status) ? status : null;
            q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            if (q != null && q.Length > 100) q = q.Substring(0, 100);
            if (page < 1) page = 1;
            var args = new { status, like = Like(q), skip = (page - 1) * PageSize, take = PageSize };

            using (var db = Db.Open())
            {
                var select = SelectFor(kind);
                var total = db.ExecuteScalar<int>("SELECT COUNT(*) FROM (" + select + Filter + ") x", args);
                var items = db.Query<InboxItem>(
                    select + Filter + " ORDER BY i.created_at DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", args).ToList();
                var counts = db.Query<StatusCount>(
                    "SELECT status, COUNT(*) AS [count] FROM " + TableFor(kind) + " WHERE deleted_at IS NULL GROUP BY status")
                    .ToDictionary(c => c.Status, c => c.Count);
                return new InboxListViewModel
                {
                    Kind = kind, Items = items, Total = total, Page = page, PageSize = PageSize,
                    Status = status, Query = q, StatusCounts = counts
                };
            }
        }

        public static InboxItem Get(InboxKind kind, Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<InboxItem>(SelectFor(kind) + " AND i.id = @id", new { id });
        }

        /// <summary>Rows for the CSV download, newest first, with the same filters as the list (at most 5,000).</summary>
        public static IList<InboxItem> Export(InboxKind kind, string status, string q)
        {
            status = Statuses.Contains(status) ? status : null;
            q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            using (var db = Db.Open())
                return db.Query<InboxItem>(
                    "SELECT TOP (@limit) * FROM (" + SelectFor(kind) + Filter + ") x ORDER BY created_at DESC",
                    new { status, like = Like(q), limit = ExportLimit }).ToList();
        }

        /// <summary>
        /// Changes the status and records who did it, in one transaction with the audit
        /// row. "Resolved" also stamps resolved_at (inquiries) or replied_at (messages).
        /// Returns false when the item no longer exists.
        /// </summary>
        public static bool SetStatus(InboxKind kind, Guid id, string status, Guid actorId)
        {
            if (!Statuses.Contains(status)) throw new ArgumentOutOfRangeException("status");
            var closedColumn = kind == InboxKind.Inquiries ? "resolved_at" : "replied_at";
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var old = db.ExecuteScalar<string>(
                    "SELECT status FROM " + TableFor(kind) + " WITH (UPDLOCK) WHERE id = @id AND deleted_at IS NULL", new { id }, tx);
                if (old == null)
                {
                    tx.Rollback();
                    return false;
                }
                if (old != status)
                {
                    db.Execute(
                        "UPDATE " + TableFor(kind) + @" SET status = @status, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId, "
                        + closedColumn + " = CASE WHEN @status = N'resolved' THEN COALESCE(" + closedColumn + ", SYSDATETIMEOFFSET()) ELSE " + closedColumn + @" END
                          WHERE id = @id",
                        new { id, status, actorId }, tx);
                    AuditLog.Write(db, tx, actorId, "update", TableFor(kind).Replace("dbo.", ""), id.ToString(),
                        new { status = new { from = old, to = status } });
                }
                tx.Commit();
                return true;
            }
        }

        public static string Label(string status)
        {
            switch (status)
            {
                case "new": return "New";
                case "in_progress": return "In progress";
                case "resolved": return "Resolved";
                case "spam": return "Spam";
                default: return status;
            }
        }

        private static string SelectFor(InboxKind kind)
        {
            return kind == InboxKind.Inquiries ? InquirySelect : MessageSelect;
        }

        private static string TableFor(InboxKind kind)
        {
            return kind == InboxKind.Inquiries ? "dbo.admission_inquiries" : "dbo.contact_messages";
        }

        private static string Like(string q)
        {
            return q == null ? null : "%" + q.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        }

        private class StatusCount
        {
            public string Status { get; set; }
            public int Count { get; set; }
        }
    }
}
