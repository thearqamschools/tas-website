using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Admin users, their roles and permissions, and sign-in state (failed attempts,
    /// lockout, authenticator key). PostgreSQL row-level security used to guard these
    /// tables; on SQL Server the checks are done here and in AdminAuthorizeAttribute.
    /// </summary>
    public static class AdminUserRepository
    {
        public const string SuperAdminRole = "super_admin";
        public const int MaxFailedAttempts = 5;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        private const string UserColumns =
            @"id, email, full_name, is_active, deleted_at, password_hash, security_stamp, two_factor_enabled,
              authenticator_key, lockout_end, access_failed_count, last_sign_in_at";

        public static AdminUserRecord FindByEmail(string email)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<AdminUserRecord>(
                    "SELECT " + UserColumns + " FROM dbo.users WHERE email = @email",
                    new { email = (email ?? "").Trim() });
        }

        public static AdminUserRecord FindById(Guid id)
        {
            using (var db = Db.Open())
                return db.QuerySingleOrDefault<AdminUserRecord>(
                    "SELECT " + UserColumns + " FROM dbo.users WHERE id = @id", new { id });
        }

        /// <summary>
        /// Roles and permissions for network-wide role assignments. Assignments limited to
        /// a region or campus (user_roles.region_id / branch_id) are not counted yet: the
        /// screens that need scoped access will check the scope themselves.
        /// </summary>
        public static AdminPrincipal LoadPrincipal(AdminUserRecord user)
        {
            using (var db = Db.Open())
            {
                var roles = db.Query<RoleRow>(
                    @"SELECT r.[key], r.name FROM dbo.user_roles ur JOIN dbo.roles r ON r.id = ur.role_id
                      WHERE ur.user_id = @id AND ur.region_id IS NULL AND ur.branch_id IS NULL
                      ORDER BY r.name", new { id = user.Id }).ToList();
                var permissions = db.Query<string>(
                    @"SELECT DISTINCT p.[key] FROM dbo.user_roles ur
                      JOIN dbo.role_permissions rp ON rp.role_id = ur.role_id
                      JOIN dbo.permissions p ON p.id = rp.permission_id
                      WHERE ur.user_id = @id AND ur.region_id IS NULL AND ur.branch_id IS NULL", new { id = user.Id });
                return new AdminPrincipal
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    RoleNames = roles.Select(r => r.Name).ToList(),
                    IsSuperAdmin = roles.Any(r => r.Key == SuperAdminRole),
                    Permissions = new HashSet<string>(permissions, StringComparer.Ordinal)
                };
            }
        }

        /// <summary>A wrong password or code: count it, and lock the account for 15 minutes after 5 in a row.</summary>
        public static void RecordFailure(Guid userId)
        {
            using (var db = Db.Open())
            {
                var locked = db.ExecuteScalar<int>(
                    @"UPDATE dbo.users
                      SET access_failed_count = CASE WHEN access_failed_count + 1 >= @max THEN 0 ELSE access_failed_count + 1 END,
                          lockout_end = CASE WHEN access_failed_count + 1 >= @max THEN DATEADD(MINUTE, @minutes, SYSDATETIMEOFFSET()) ELSE lockout_end END
                      OUTPUT CASE WHEN inserted.access_failed_count = 0 THEN 1 ELSE 0 END
                      WHERE id = @userId",
                    new { userId, max = MaxFailedAttempts, minutes = (int)LockoutDuration.TotalMinutes });
                if (locked == 1)
                    AuditLog.Write(db, null, userId, "update", "users", userId.ToString(), new { @event = "locked_out", minutes = (int)LockoutDuration.TotalMinutes });
            }
        }

        public static void ResetFailures(Guid userId)
        {
            using (var db = Db.Open())
                db.Execute("UPDATE dbo.users SET access_failed_count = 0, lockout_end = NULL WHERE id = @userId", new { userId });
        }

        /// <summary>Fills in a missing security stamp (users created outside the admin panel).</summary>
        public static string EnsureSecurityStamp(AdminUserRecord user)
        {
            if (!string.IsNullOrEmpty(user.SecurityStamp)) return user.SecurityStamp;
            var stamp = NewStamp();
            using (var db = Db.Open())
                db.Execute("UPDATE dbo.users SET security_stamp = @stamp WHERE id = @id AND security_stamp IS NULL", new { stamp, id = user.Id });
            user.SecurityStamp = FindById(user.Id).SecurityStamp;
            return user.SecurityStamp;
        }

        public static void RecordSignIn(Guid userId)
        {
            using (var db = Db.Open())
            {
                db.Execute(
                    @"UPDATE dbo.users SET last_sign_in_at = SYSDATETIMEOFFSET(), last_seen_at = SYSDATETIMEOFFSET(),
                             access_failed_count = 0, lockout_end = NULL
                      WHERE id = @userId", new { userId });
                AuditLog.Write(db, null, userId, "update", "users", userId.ToString(), new { @event = "signed_in" });
            }
        }

        /// <summary>Turns on two-step sign-in with a verified authenticator secret. Returns the new security stamp.</summary>
        public static string EnableTwoFactor(Guid userId, string secret)
        {
            var stamp = NewStamp();
            using (var db = Db.Open())
            {
                db.Execute(
                    @"UPDATE dbo.users SET authenticator_key = @secret, two_factor_enabled = 1, security_stamp = @stamp,
                             updated_at = SYSDATETIMEOFFSET()
                      WHERE id = @userId", new { userId, secret, stamp });
                AuditLog.Write(db, null, userId, "update", "users", userId.ToString(), new { @event = "two_factor_enabled" });
            }
            return stamp;
        }

        /// <summary>Saves a new password and a new security stamp, which signs the user out everywhere else.</summary>
        public static string ChangePassword(Guid userId, string passwordHash)
        {
            var stamp = NewStamp();
            using (var db = Db.Open())
            {
                db.Execute(
                    @"UPDATE dbo.users SET password_hash = @passwordHash, security_stamp = @stamp,
                             updated_at = SYSDATETIMEOFFSET()
                      WHERE id = @userId", new { userId, passwordHash, stamp });
                AuditLog.Write(db, null, userId, "update", "users", userId.ToString(), new { @event = "password_changed" });
            }
            return stamp;
        }

        public static bool SuperAdminExists()
        {
            using (var db = Db.Open())
                return db.ExecuteScalar<int>(
                    @"SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.id = ur.role_id
                        JOIN dbo.users u ON u.id = ur.user_id
                        WHERE r.[key] = @role AND u.deleted_at IS NULL) THEN 1 ELSE 0 END",
                    new { role = SuperAdminRole }) == 1;
        }

        /// <summary>
        /// Creates the first Super Admin. Runs in one transaction that re-checks, under
        /// a lock, that no Super Admin exists, so two people cannot both claim it.
        /// Returns false when a Super Admin already exists or the email is taken.
        /// </summary>
        public static bool CreateFirstSuperAdmin(string fullName, string email, string passwordHash)
        {
            using (var db = Db.Open())
            using (var tx = db.BeginTransaction(IsolationLevel.Serializable))
            {
                var exists = db.ExecuteScalar<int>(
                    @"SELECT COUNT(*) FROM dbo.user_roles ur WITH (UPDLOCK, HOLDLOCK)
                      JOIN dbo.roles r ON r.id = ur.role_id
                      JOIN dbo.users u ON u.id = ur.user_id
                      WHERE r.[key] = @role AND u.deleted_at IS NULL", new { role = SuperAdminRole }, tx);
                var emailTaken = db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM dbo.users WITH (UPDLOCK, HOLDLOCK) WHERE email = @email", new { email }, tx);
                var roleId = db.ExecuteScalar<Guid?>("SELECT id FROM dbo.roles WHERE [key] = @role", new { role = SuperAdminRole }, tx);
                if (exists > 0 || emailTaken > 0 || roleId == null)
                {
                    tx.Rollback();
                    return false;
                }

                var userId = Guid.NewGuid();
                db.Execute(
                    @"INSERT INTO dbo.users (id, email, full_name, is_active, password_hash, security_stamp, email_confirmed, two_factor_enabled)
                      VALUES (@userId, @email, @fullName, 1, @passwordHash, @stamp, 1, 0)",
                    new { userId, email, fullName, passwordHash, stamp = NewStamp() }, tx);
                db.Execute(
                    "INSERT INTO dbo.user_roles (user_id, role_id, created_by) VALUES (@userId, @roleId, @userId)",
                    new { userId, roleId }, tx);
                AuditLog.Write(db, tx, userId, "insert", "users", userId.ToString(),
                    new { @event = "first_super_admin_created", email, full_name = fullName });
                tx.Commit();
                return true;
            }
        }

        public static string NewStamp()
        {
            return Guid.NewGuid().ToString("N");
        }

        private class RoleRow
        {
            public string Key { get; set; }
            public string Name { get; set; }
        }
    }

    /// <summary>
    /// Writes dbo.audit_logs rows. PostgreSQL triggers did this before; on SQL Server
    /// every admin change calls it. Passwords, hashes and authenticator keys are never written.
    /// </summary>
    public static class AuditLog
    {
        public static void Write(IDbConnection db, IDbTransaction tx, Guid? actorId, string action, string table, string recordId, object changes)
        {
            db.Execute(
                @"INSERT INTO dbo.audit_logs (actor_id, [action], table_name, record_id, changes)
                  VALUES (@actorId, @action, @table, @recordId, @changes)",
                new { actorId, action, table, recordId, changes = changes == null ? null : JsonConvert.SerializeObject(changes) }, tx);
        }
    }
}
