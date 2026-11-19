using System.Collections.Generic;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    public static class HeadOfficeRepository
    {
        /// <summary>Published head offices in footer order.</summary>
        public static IList<HeadOffice> Published()
        {
            return Cache.GetOrLastGood("head_offices", () =>
            {
                using (var db = Db.Open())
                {
                    // mobile and facebook_url come from Database/12. Until it has run they read as NULL, so uploading
                    // the site before the script cannot break every page (the footer uses this list).
                    var hasNew = db.ExecuteScalar<int>(
                        "SELECT CASE WHEN COL_LENGTH(N'dbo.head_offices', N'mobile') IS NOT NULL AND COL_LENGTH(N'dbo.head_offices', N'facebook_url') IS NOT NULL THEN 1 ELSE 0 END") == 1;
                    return (IList<HeadOffice>)db.Query<HeadOffice>(
                        "SELECT id, office_number, name, city, address, phone, phone_alt, whatsapp, email, google_maps_url, hours, is_published, "
                        + (hasNew ? "mobile, facebook_url" : "CAST(NULL AS NVARCHAR(20)) AS mobile, CAST(NULL AS NVARCHAR(300)) AS facebook_url")
                        + " FROM dbo.head_offices WHERE is_published = 1 ORDER BY office_number").ToList();
                }
            }, () => new List<HeadOffice>());
        }
    }
}
