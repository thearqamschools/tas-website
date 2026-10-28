using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>Public campus directory (dbo.branches). Only published, active or coming-soon campuses are shown.</summary>
    public static class CampusRepository
    {
        private const string Select = @"
            SELECT b.id, b.name, b.slug, b.area, b.address_line, b.phone, b.phone_alt, b.whatsapp, b.email, b.timings,
                   b.overview, b.principal_name, b.principal_title, b.principal_message, b.status, b.admission_status,
                   b.admission_note, b.google_maps_url, b.latitude, b.longitude, b.social_links, b.seo_id, b.updated_at,
                   c.name AS city_name, c.slug AS city_slug, d.name AS district_name, d.slug AS district_slug,
                   m.bucket AS cover_bucket, m.storage_path AS cover_path, m.alt_text AS cover_alt
            FROM dbo.branches b
            JOIN dbo.cities c ON c.id = b.city_id
            LEFT JOIN dbo.districts d ON d.id = c.district_id
            LEFT JOIN dbo.media m ON m.id = b.cover_media_id
            WHERE b.is_published = 1 AND b.deleted_at IS NULL AND b.archived_at IS NULL AND b.status <> N'inactive'";

        /// <summary>
        /// Search by campus name, area, address, city or district (q), and filter by
        /// city and district slugs. All values are passed as parameters.
        /// </summary>
        public static IList<Campus> Search(string q, string city, string district)
        {
            var sql = Select;
            if (!string.IsNullOrWhiteSpace(q))
                sql += @" AND (b.name LIKE @like OR b.area LIKE @like OR b.address_line LIKE @like OR c.name LIKE @like OR d.name LIKE @like OR b.code = @q)";
            if (!string.IsNullOrWhiteSpace(city)) sql += " AND c.slug = @city";
            if (!string.IsNullOrWhiteSpace(district)) sql += " AND d.slug = @district";
            sql += " ORDER BY c.sort_order, c.name, b.sort_order, b.name";
            var term = (q ?? "").Trim();
            using (var db = Db.Open())
            {
                return db.Query<Campus>(sql, new { q = term, like = "%" + EscapeLike(term) + "%", city, district }).ToList();
            }
        }

        public static Campus Get(string citySlug, string slug)
        {
            using (var db = Db.Open())
            {
                return db.QueryFirstOrDefault<Campus>(Select + " AND c.slug = @citySlug AND b.slug = @slug", new { citySlug, slug });
            }
        }

        public static Campus GetById(Guid id)
        {
            using (var db = Db.Open())
            {
                return db.QueryFirstOrDefault<Campus>(Select + " AND b.id = @id", new { id });
            }
        }

        public static IList<Campus> All() { return Search(null, null, null); }

        public static int CountPublished()
        {
            using (var db = Db.Open())
            {
                return db.ExecuteScalar<int>("SELECT COUNT(*) FROM (" + Select + ") x");
            }
        }

        public static IList<FilterOption> Cities()
        {
            using (var db = Db.Open())
            {
                return db.Query<FilterOption>("SELECT x.city_slug AS slug, x.city_name AS name, COUNT(*) AS count FROM (" + Select + ") x GROUP BY x.city_slug, x.city_name ORDER BY x.city_name").ToList();
            }
        }

        public static IList<FilterOption> Districts()
        {
            using (var db = Db.Open())
            {
                return db.Query<FilterOption>("SELECT x.district_slug AS slug, x.district_name AS name, COUNT(*) AS count FROM (" + Select + ") x WHERE x.district_slug IS NOT NULL GROUP BY x.district_slug, x.district_name ORDER BY x.district_name").ToList();
            }
        }

        public static FilterOption City(string slug)
        {
            using (var db = Db.Open())
            {
                return db.QueryFirstOrDefault<FilterOption>("SELECT slug, name, 0 AS count FROM dbo.cities WHERE slug = @slug", new { slug });
            }
        }

        public static IList<CampusClass> Classes(Guid branchId)
        {
            using (var db = Db.Open())
            {
                return db.Query<CampusClass>(
                    @"SELECT cl.name, cl.stage, bc.is_admission_open, bc.shift, bc.note
                      FROM dbo.branch_classes bc JOIN dbo.class_levels cl ON cl.id = bc.class_level_id
                      WHERE bc.branch_id = @branchId ORDER BY cl.sort_order", new { branchId }).ToList();
            }
        }

        public static IList<string> Facilities(Guid branchId)
        {
            using (var db = Db.Open())
            {
                return db.Query<string>(
                    @"SELECT f.name FROM dbo.branch_facilities bf JOIN dbo.facilities f ON f.id = bf.facility_id
                      WHERE bf.branch_id = @branchId ORDER BY f.name", new { branchId }).ToList();
            }
        }

        private static string EscapeLike(string s)
        {
            return s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }
    }
}
