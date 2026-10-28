using System;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Saves inquiries from the website. Admission inquiries go to
    /// dbo.admission_inquiries and other messages to dbo.contact_messages, where
    /// the admin panel (Admissions and Messages) lists them with status "new".
    /// </summary>
    public static class InquiryRepository
    {
        public static Guid SaveAdmissionInquiry(Guid? branchId, Guid? classLevelId, string name, string phone, string email, string message, string sourcePage)
        {
            using (var db = Db.Open())
            {
                return db.ExecuteScalar<Guid>(
                    @"INSERT INTO dbo.admission_inquiries (branch_id, class_level_id, name, phone, email, message, source_page)
                      OUTPUT inserted.id
                      VALUES (@branchId, @classLevelId, @name, @phone, @email, @message, @sourcePage)",
                    new { branchId, classLevelId, name, phone, email, message, sourcePage });
            }
        }

        public static Guid SaveContactMessage(string type, Guid? branchId, string name, string phone, string email, string message, string sourcePage)
        {
            using (var db = Db.Open())
            {
                return db.ExecuteScalar<Guid>(
                    @"INSERT INTO dbo.contact_messages ([type], branch_id, name, phone, email, message, source_page)
                      OUTPUT inserted.id
                      VALUES (@type, @branchId, @name, @phone, @email, @message, @sourcePage)",
                    new { type, branchId, name, phone, email, message, sourcePage });
            }
        }

        /// <summary>A class level id is only stored if it exists.</summary>
        public static bool ClassLevelExists(Guid id)
        {
            using (var db = Db.Open())
            {
                return db.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.class_levels WHERE id = @id", new { id }) > 0;
            }
        }
    }
}
