using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Managing admin users from /admin/users: create, edit, roles, deactivate, and
    /// reset password or two-step verification. Users are never deleted, only
    /// deactivated. Every change writes an audit row in the same transaction.
    /// </summary>
    public static class UserAdminRepository
    {
        public static IList<UserListRow> List()
        {
            using (var db = Db.Open())
            {
                var users = db.Query<UserListRow>(
                    @"SELECT id, email, full_name, is_active, two_factor_enabled, last_sign_in_at, lockout_end
                      FROM dbo.users WHERE deleted_at IS NULL ORDER BY is_active DESC, full_name, email").ToList();
                var roles = db.Query<UserRoleRow>(
                    @"SELECT ur.user_id, r.[key], r.name FROM dbo.user_roles ur JOIN dbo.roles r ON r.id = ur.role_id
                      WHERE ur.region_id IS NULL AND ur.branch_id IS NULL").ToList();
                foreach (var u in users)
                    u.RoleNames = roles.Where(r => r.UserId == u.Id).Select(r => r.Name).OrderBy(n => n).ToList();
                return users;
            }
        }

        public static IList<RoleOption> Roles()
        {
            using (var db = Db.Open())
                return db.Query<RoleOption>("SELECT id, [key], name, description FROM dbo.roles ORDER BY name").ToList();
        }

        public static UserEditForm Get(Guid id)
        {
            using (var db = Db.Open())
            {
                var form = db.QuerySingleOrDefault<UserEditForm>(
                    @"SELECT id, email, full_name, phone, job_title, is_active, two_factor_enabled, last_sign_in_at
                      FROM dbo.users WHERE id = @id AND deleted_at IS NULL", new { id });
                if (form == null) return null;
                form.RoleIds = db.Query<Guid>(
                    "SELECT role_id FROM dbo.user_roles WHERE user_id = @id AND region_id IS NULL AND branch_id IS NULL", new { id }).ToList();
                return form;
            }
        }

        public static bool EmailTaken(string email, Guid? exceptId)
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM dbo.users WHERE email = @email AND (@exceptId IS NULL OR id <> @exceptId)",
                    new { email, exceptId }) > 0;
        }

        /// <summary>Creates a user with a temporary password; two-step verification is set up at first sign-in.</summary>
        public static Guid Create(UserEditForm form, string passwordHash, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var id = Guid.NewGuid();
                db.Execute(
                    @"INSERT INTO dbo.users (id, email, full_name, phone, job_title, is_active, password_hash, security_stamp, email_confirmed, two_factor_enabled)
                      VALUES (@id, @Email, @FullName, @Phone, @JobTitle, 1, @passwordHash, @stamp, 1, 0)",
                    new { id, form.Email, form.FullName, form.Phone, form.JobTitle, passwordHash, stamp = AdminUserRepository.NewStamp() }, tx);
                SetRoles(db, tx, id, form.RoleIds, actorId);
                AuditLog.Write(db, tx, actorId, "insert", "users", id.ToString(),
                    new { email = form.Email, full_name = form.FullName, roles = RoleKeys(db, tx, form.RoleIds) });
                tx.Commit();
                return id;
            }
        }

        public static void Update(UserEditForm form, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                var wasActive = db.ExecuteScalar<bool>("SELECT is_active FROM dbo.users WITH (UPDLOCK) WHERE id = @Id", new { form.Id }, tx);
                db.Execute(
                    @"UPDATE dbo.users SET email = @Email, full_name = @FullName, phone = @Phone, job_title = @JobTitle,
                             is_active = @IsActive, updated_at = SYSDATETIMEOFFSET(),
                             security_stamp = CASE WHEN @IsActive = 0 THEN @stamp ELSE security_stamp END
                      WHERE id = @Id AND deleted_at IS NULL",
                    new { form.Id, form.Email, form.FullName, form.Phone, form.JobTitle, form.IsActive, stamp = AdminUserRepository.NewStamp() }, tx);
                SetRoles(db, tx, form.Id, form.RoleIds, actorId);
                AuditLog.Write(db, tx, actorId, "update", "users", form.Id.ToString(),
                    new { email = form.Email, full_name = form.FullName, is_active = form.IsActive, was_active = wasActive, roles = RoleKeys(db, tx, form.RoleIds) });
                tx.Commit();
            }
        }

        /// <summary>New temporary password; ends the user's sessions.</summary>
        public static void ResetPassword(Guid userId, string passwordHash, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                db.Execute(
                    @"UPDATE dbo.users SET password_hash = @passwordHash, security_stamp = @stamp, access_failed_count = 0,
                             lockout_end = NULL, updated_at = SYSDATETIMEOFFSET()
                      WHERE id = @userId AND deleted_at IS NULL",
                    new { userId, passwordHash, actorId, stamp = AdminUserRepository.NewStamp() }, tx);
                AuditLog.Write(db, tx, actorId, "update", "users", userId.ToString(), new { @event = "password_reset_by_admin" });
                tx.Commit();
            }
        }

        /// <summary>Removes the authenticator so the user sets it up again at next sign-in (lost phone); ends their sessions.</summary>
        public static void ResetTwoFactor(Guid userId, Guid actorId)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction())
            {
                db.Execute(
                    @"UPDATE dbo.users SET two_factor_enabled = 0, authenticator_key = NULL, security_stamp = @stamp,
                             updated_at = SYSDATETIMEOFFSET()
                      WHERE id = @userId AND deleted_at IS NULL",
                    new { userId, actorId, stamp = AdminUserRepository.NewStamp() }, tx);
                AuditLog.Write(db, tx, actorId, "update", "users", userId.ToString(), new { @event = "two_factor_reset_by_admin" });
                tx.Commit();
            }
        }

        /// <summary>Active users with the Super Admin role other than <paramref name="exceptUserId"/>.</summary>
        public static int OtherActiveSuperAdmins(Guid exceptUserId)
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>(
                    @"SELECT COUNT(DISTINCT u.id) FROM dbo.users u
                      JOIN dbo.user_roles ur ON ur.user_id = u.id JOIN dbo.roles r ON r.id = ur.role_id
                      WHERE r.[key] = @role AND u.is_active = 1 AND u.deleted_at IS NULL AND u.id <> @exceptUserId",
                    new { role = AdminUserRepository.SuperAdminRole, exceptUserId });
        }

        /// <summary>16 random letters and digits that are easy to read aloud, in groups of four (for example "Kq7m-...").</summary>
        public static string NewTemporaryPassword()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var sb = new StringBuilder();
            for (var i = 0; i < bytes.Length; i++)
            {
                if (i > 0 && i % 4 == 0) sb.Append('-');
                sb.Append(alphabet[bytes[i] % alphabet.Length]);
            }
            return sb.ToString();
        }

        /// <summary>Replaces the user's network-wide roles. Region- or campus-limited assignments are left alone.</summary>
        private static void SetRoles(IDbConnection db, IDbTransaction tx, Guid userId, IList<Guid> roleIds, Guid actorId)
        {
            var wanted = (roleIds ?? new List<Guid>()).Distinct().ToList();
            db.Execute(
                @"DELETE FROM dbo.user_roles WHERE user_id = @userId AND region_id IS NULL AND branch_id IS NULL
                  AND role_id NOT IN (SELECT value FROM OPENJSON(@wanted) WITH (value UNIQUEIDENTIFIER '$'))",
                new { userId, wanted = Newtonsoft.Json.JsonConvert.SerializeObject(wanted) }, tx);
            foreach (var roleId in wanted)
                db.Execute(
                    @"IF NOT EXISTS (SELECT 1 FROM dbo.user_roles WHERE user_id = @userId AND role_id = @roleId AND region_id IS NULL AND branch_id IS NULL)
                        INSERT INTO dbo.user_roles (user_id, role_id, created_by)
                        SELECT @userId, id, @actorId FROM dbo.roles WHERE id = @roleId",
                    new { userId, roleId, actorId }, tx);
        }

        private static IList<string> RoleKeys(IDbConnection db, IDbTransaction tx, IList<Guid> roleIds)
        {
            if (roleIds == null || roleIds.Count == 0) return new List<string>();
            return db.Query<string>("SELECT [key] FROM dbo.roles WHERE id IN @roleIds ORDER BY [key]", new { roleIds }, tx).ToList();
        }
    }
}
