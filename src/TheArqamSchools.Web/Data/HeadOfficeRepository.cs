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
            return Cache.Get("head_offices", () =>
            {
                using (var db = Db.Open())
                {
                    return (IList<HeadOffice>)db.Query<HeadOffice>(
                        @"SELECT id, office_number, name, city, address, phone, phone_alt, whatsapp, email, google_maps_url, hours, is_published
                          FROM dbo.head_offices WHERE is_published = 1 ORDER BY office_number").ToList();
                }
            });
        }
    }
}
