using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dapper;
using Newtonsoft.Json;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Campuses (dbo.branches) and cities for the admin panel. Unlike the public
    /// CampusRepository this sees unpublished and inactive campuses. Campuses are
    /// archived, never deleted. Every save writes an audit row in the same transaction.
    /// </summary>
    public static class CampusAdminRepository
    {
        public static readonly string[] Statuses = { "active", "coming_soon", "inactive" };
        public static readonly string[] AdmissionStatuses = { "open", "closed", "waiting_list", "opening_soon", "contact_campus" };

        public static IList<CampusListRow> List(string q)
        {
            q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            using (var db = Db.Open())
                return db.Query<CampusListRow>(
                    @"SELECT b.id, b.name, b.code, b.slug, b.area, b.status, b.admission_status, b.is_published, b.archived_at,
                             b.updated_at, c.name AS city_name, c.slug AS city_slug
                      FROM dbo.branches b JOIN dbo.cities c ON c.id = b.city_id
                      WHERE b.deleted_at IS NULL
                        AND (@like IS NULL OR b.name LIKE @like OR b.area LIKE @like OR c.name LIKE @like OR b.code LIKE @like)
                      ORDER BY CASE WHEN b.archived_at IS NULL THEN 0 ELSE 1 END, c.name, b.sort_order, b.name",
                    new { like = q == null ? null : "%" + q.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%" }).ToList();
        }

        public static IList<CityOption> Cities()
        {
            using (var db = Db.Open())
                return db.Query<CityOption>("SELECT id, name, slug FROM dbo.cities ORDER BY name").ToList();
        }

        public static CampusEditForm Get(Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<CampusEditForm>(
                    @"SELECT id, city_id, name, slug, code, area, address_line, phone, phone_alt, whatsapp, email, timings,
                             google_maps_url, latitude, longitude, overview, principal_name, principal_title, principal_message,
                             status, admission_status, admission_note, is_published, sort_order,
                             CASE WHEN archived_at IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS is_archived
                      FROM dbo.branches WHERE id = @id AND deleted_at IS NULL", new { id });
        }

        /// <summary>Error message when the slug or code is already used, else null.</summary>
        public static string Conflict(CampusEditForm form)
        {
            using (var db = Db.Open())
            {
                if (db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM dbo.branches WHERE city_id = @CityId AND slug = @Slug AND deleted_at IS NULL AND id <> @Id",
                    new { form.CityId, form.Slug, form.Id }) > 0)
                    return "Another campus in this city already uses this web address.";
                if (form.Code != null && db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM dbo.branches WHERE code = @Code AND deleted_at IS NULL AND id <> @Id",
                    new { form.Code, form.Id }) > 0)
                    return "Another campus already uses this campus code.";
                return null;
            }
        }

        /// <summary>Returns the id of an existing city with this name, or creates the city.</summary>
        public static Guid FindOrCreateCity(string name, Guid actorId)
        {
            name = name.Trim();
            var slug = Slugify(name);
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var id = db.ExecuteScalar<Guid?>(
                    "SELECT id FROM dbo.cities WITH (UPDLOCK, HOLDLOCK) WHERE name = @name OR slug = @slug", new { name, slug }, tx);
                if (id == null)
                {
                    id = db.ExecuteScalar<Guid>(
                        @"INSERT INTO dbo.cities (name, slug, created_by, updated_by) OUTPUT inserted.id
                          VALUES (@name, @slug, @actorId, @actorId)", new { name, slug, actorId }, tx);
                    AuditLog.Write(db, tx, actorId, "insert", "cities", id.ToString(), new { name, slug });
                }
                tx.Commit();
                return id.Value;
            }
        }

        public static Guid Save(CampusEditForm f, bool isNew, bool canPublish, Guid actorId)
        {
            var areaSlug = string.IsNullOrEmpty(f.Area) ? null : Slugify(f.Area);
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                CampusEditForm before = null;
                if (isNew)
                {
                    f.Id = db.ExecuteScalar<Guid>(
                        @"INSERT INTO dbo.branches (city_id, name, slug, created_by, updated_by, is_published, status, admission_status)
                          OUTPUT inserted.id VALUES (@CityId, @Name, @Slug, @actorId, @actorId, 0, N'coming_soon', N'closed')",
                        new { f.CityId, f.Name, f.Slug, actorId }, tx);
                }
                else
                {
                    before = db.QuerySingle<CampusEditForm>(
                        @"SELECT id, city_id, name, slug, code, area, address_line, phone, phone_alt, whatsapp, email, timings,
                                 google_maps_url, latitude, longitude, overview, principal_name, principal_title, principal_message,
                                 status, admission_status, admission_note, is_published, sort_order
                          FROM dbo.branches WITH (UPDLOCK) WHERE id = @Id", new { f.Id }, tx);
                    if (!canPublish) f.IsPublished = before.IsPublished;
                }

                db.Execute(
                    @"UPDATE dbo.branches SET
                        city_id = @CityId, name = @Name, slug = @Slug, code = @Code, area = @Area, area_slug = @areaSlug,
                        address_line = @AddressLine, phone = @Phone, phone_alt = @PhoneAlt, whatsapp = @Whatsapp, email = @Email,
                        timings = @Timings, google_maps_url = @GoogleMapsUrl, latitude = @Latitude, longitude = @Longitude,
                        overview = @Overview, principal_name = @PrincipalName, principal_title = @PrincipalTitle,
                        principal_message = @PrincipalMessage, status = @Status, admission_status = @AdmissionStatus,
                        is_admission_open = CASE WHEN @AdmissionStatus = N'open' THEN 1 ELSE 0 END,
                        admission_note = @AdmissionNote, sort_order = @SortOrder,
                        is_published = @IsPublished,
                        published_at = CASE WHEN @IsPublished = 1 AND is_published = 0 THEN SYSDATETIMEOFFSET() ELSE published_at END,
                        updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                      WHERE id = @Id",
                    new
                    {
                        f.Id, f.CityId, f.Name, f.Slug, f.Code, f.Area, areaSlug, f.AddressLine, f.Phone, f.PhoneAlt, f.Whatsapp,
                        f.Email, f.Timings, f.GoogleMapsUrl, f.Latitude, f.Longitude, f.Overview, f.PrincipalName, f.PrincipalTitle,
                        f.PrincipalMessage, f.Status, f.AdmissionStatus, f.AdmissionNote, f.SortOrder, f.IsPublished, actorId
                    }, tx);

                AuditLog.Write(db, tx, actorId, isNew ? "insert" : "update", "branches", f.Id.ToString(), Changes(before, f));
                tx.Commit();
                return f.Id;
            }
        }

        /// <summary>Hides a campus from the website (archived_at), or brings it back.</summary>
        public static void SetArchived(Guid id, bool archived, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                db.Execute(
                    @"UPDATE dbo.branches SET archived_at = CASE WHEN @archived = 1 THEN COALESCE(archived_at, SYSDATETIMEOFFSET()) ELSE NULL END,
                             updated_at = SYSDATETIMEOFFSET(), updated_by = @actorId
                      WHERE id = @id AND deleted_at IS NULL", new { id, archived, actorId }, tx);
                AuditLog.Write(db, tx, actorId, "update", "branches", id.ToString(), new { archived });
                tx.Commit();
            }
        }

        /// <summary>"Model Town Campus" becomes "model-town-campus" (letters and digits only, joined by hyphens).</summary>
        public static string Slugify(string text)
        {
            var sb = new StringBuilder();
            var dash = false;
            foreach (var ch in (text ?? "").Trim().ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) { sb.Append(ch); dash = false; }
                else if (!dash && sb.Length > 0) { sb.Append('-'); dash = true; }
            }
            var s = sb.ToString().Trim('-');
            return s.Length > 120 ? s.Substring(0, 120).Trim('-') : s;
        }

        /// <summary>Only the fields that changed, as { field: { from, to } }; everything for a new campus.</summary>
        private static object Changes(CampusEditForm before, CampusEditForm after)
        {
            var result = new Dictionary<string, object>();
            foreach (var p in typeof(CampusEditForm).GetProperties())
            {
                if (p.Name == "Id" || p.Name == "IsArchived") continue;
                var a = p.GetValue(after);
                if (before == null) { if (a != null) result[p.Name] = a; continue; }
                var b = p.GetValue(before);
                if (!Equals(a, b)) result[p.Name] = new { from = b, to = a };
            }
            return result;
        }
    }
}
