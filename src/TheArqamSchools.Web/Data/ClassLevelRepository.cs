using System.Collections.Generic;
using System.Linq;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    public static class ClassLevelRepository
    {
        /// <summary>All class levels in order (Play Group ... Class 10, Hifz-e-Quran).</summary>
        public static IList<ClassLevel> All()
        {
            return Cache.Get("class_levels", () =>
            {
                using (var db = Db.Open())
                {
                    return (IList<ClassLevel>)db.Query<ClassLevel>("SELECT id, name, slug, stage, sort_order FROM dbo.class_levels ORDER BY sort_order, name").ToList();
                }
            });
        }
    }
}
