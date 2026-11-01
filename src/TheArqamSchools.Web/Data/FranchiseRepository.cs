using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Newtonsoft.Json.Linq;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Franchise applications (dbo.franchise_applications) and the franchise form
    /// settings (dbo.franchise_settings, one row). Reference numbers come from the
    /// SQL Server sequence dbo.franchise_reference_seq, as "FR-2026-00001".
    /// </summary>
    public static class FranchiseRepository
    {
        public static readonly string[] Statuses = { "new", "under_review", "contacted", "meeting_scheduled", "approved", "rejected" };

        public class Settings
        {
            public bool AcceptingApplications { get; set; }
            public string StartTimelines { get; set; }
            public string ResponseTime { get; set; }
            public string ClosedMessage { get; set; }

            public IList<string> TimelineList
            {
                get
                {
                    try { return JArray.Parse(string.IsNullOrEmpty(StartTimelines) ? "[]" : StartTimelines).Select(t => (string)t).Where(t => !string.IsNullOrWhiteSpace(t)).ToList(); }
                    catch (Newtonsoft.Json.JsonReaderException) { return new List<string>(); }
                }
            }
        }

        public static Settings LoadSettings()
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<Settings>(
                    "SELECT accepting_applications, start_timelines, response_time, closed_message FROM dbo.franchise_settings WHERE id = 1")
                    ?? new Settings { AcceptingApplications = true, StartTimelines = "[]" };
        }

        /// <summary>Saves an application and returns its id and reference number.</summary>
        public static Tuple<Guid, string> Save(FranchiseForm f, string sourcePage)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var next = db.ExecuteScalar<long>("SELECT NEXT VALUE FOR dbo.franchise_reference_seq", transaction: tx);
                var reference = "FR-" + DateTime.UtcNow.AddHours(5).Year + "-" + next.ToString("D5");
                var id = db.ExecuteScalar<Guid>(
                    @"INSERT INTO dbo.franchise_applications
                        (reference_no, full_name, mobile, whatsapp, email, city, province, proposed_city, proposed_area,
                         building_plan, building_area, experience, start_timeline, message, source_page)
                      OUTPUT inserted.id
                      VALUES (@reference, @FullName, @Mobile, @Whatsapp, @Email, @City, @Province, @ProposedCity, @ProposedArea,
                              @BuildingPlan, @BuildingArea, @Experience, @StartTimeline, @Message, @sourcePage)",
                    new
                    {
                        reference, f.FullName, f.Mobile, f.Whatsapp, f.Email, f.City, f.Province, f.ProposedCity, f.ProposedArea,
                        f.BuildingPlan, f.BuildingArea, f.Experience, f.StartTimeline, f.Message, sourcePage
                    }, tx);
                tx.Commit();
                return Tuple.Create(id, reference);
            }
        }

        // ---- Admin ----------------------------------------------------------------------

        public static IList<FranchiseRow> List(string status, string q)
        {
            status = Statuses.Contains(status) ? status : null;
            q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            if (q != null && q.Length > 100) q = q.Substring(0, 100);
            var like = q == null ? null : "%" + q.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
            using (var db = Db.Open())
                return db.Query<FranchiseRow>(
                    @"SELECT TOP 500 * FROM dbo.franchise_applications
                      WHERE (@status IS NULL OR status = @status)
                        AND (@like IS NULL OR full_name LIKE @like OR mobile LIKE @like OR email LIKE @like
                             OR reference_no LIKE @like OR proposed_city LIKE @like)
                      ORDER BY submitted_at DESC", new { status, like }).ToList();
        }

        public static FranchiseRow Get(Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<FranchiseRow>("SELECT * FROM dbo.franchise_applications WHERE id = @id", new { id });
        }

        public static bool SetStatus(Guid id, string status, Guid actorId)
        {
            if (!Statuses.Contains(status)) throw new ArgumentOutOfRangeException("status");
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var old = db.ExecuteScalar<string>("SELECT status FROM dbo.franchise_applications WITH (UPDLOCK) WHERE id = @id", new { id }, tx);
                if (old == null) { tx.Rollback(); return false; }
                if (old != status)
                {
                    db.Execute(
                        @"UPDATE dbo.franchise_applications SET status = @status, updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                          WHERE id = @id", new { id, status, actorId }, tx);
                    db.Execute(
                        @"INSERT INTO dbo.franchise_status_history (application_id, from_status, to_status, changed_by)
                          VALUES (@id, @old, @status, @actorId)", new { id, old, status, actorId }, tx);
                    AuditLog.Write(db, tx, actorId, "update", "franchise_applications", id.ToString(), new { status = new { from = old, to = status } });
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
                case "under_review": return "Under review";
                case "contacted": return "Contacted";
                case "meeting_scheduled": return "Meeting scheduled";
                case "approved": return "Approved";
                case "rejected": return "Not taken forward";
                default: return status;
            }
        }
    }
}
