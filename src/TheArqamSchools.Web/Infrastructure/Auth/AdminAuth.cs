using System;
using System.Text;
using System.Web;
using System.Web.Security;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Infrastructure.Auth
{
    /// <summary>
    /// Admin sign-in cookies, encrypted and signed with the ASP.NET machine key
    /// (MachineKey.Protect), so they cannot be read or changed by the browser.
    ///
    /// - "tas_admin_signin" (10 minutes) carries a sign-in that has passed the password
    ///   step but not yet the authenticator code, or the new secret during first-time setup.
    /// - "tas_admin" is the signed-in session: user id, security stamp, issue time and
    ///   last activity. It ends after 60 minutes without activity or 12 hours in total.
    ///   Changing the password or authenticator changes the security stamp, which ends
    ///   every other session of that user.
    ///
    /// Both cookies are HttpOnly, Secure (httpCookies requireSSL), SameSite=Strict and
    /// limited to /admin.
    /// </summary>
    public static class AdminAuth
    {
        private const string SessionCookie = "tas_admin";
        private const string SignInCookie = "tas_admin_signin";
        private const string SessionPurpose = "TAS.Admin.Session.v1";
        private const string SignInPurpose = "TAS.Admin.SignIn.v1";
        private const string ItemsKey = "tas.admin.principal";
        private const string CookiePath = "/admin";

        public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(60);
        public static readonly TimeSpan AbsoluteTimeout = TimeSpan.FromHours(12);
        public static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(5);

        public const string StageCode = "code";
        public const string StageEnroll = "enroll";

        /// <summary>A sign-in waiting for its second step.</summary>
        public class PendingSignIn
        {
            public Guid UserId;
            public string Stamp;
            public string Stage;
            public string Secret;   // only for StageEnroll: the new authenticator secret, not yet saved
            public DateTime ExpiresUtc;
        }

        // ---- Session -------------------------------------------------------------------

        public static void SignIn(HttpContextBase context, Guid userId, string stamp)
        {
            var now = DateTime.UtcNow;
            WriteSession(context, userId, stamp, now, now);
            ClearPending(context);
        }

        public static void SignOut(HttpContextBase context)
        {
            Expire(context, SessionCookie);
            ClearPending(context);
            context.Items.Remove(ItemsKey);
        }

        /// <summary>
        /// The signed-in admin for this request, or null. Checks the cookie, the time
        /// limits, and that the user still exists, is active, uses two-step sign-in and
        /// has the same security stamp. Loaded once per request.
        /// </summary>
        public static AdminPrincipal Current(HttpContextBase context)
        {
            if (context.Items.Contains(ItemsKey)) return context.Items[ItemsKey] as AdminPrincipal;
            AdminPrincipal principal = null;
            try { principal = Load(context); }
            finally { context.Items[ItemsKey] = principal; }
            return principal;
        }

        private static AdminPrincipal Load(HttpContextBase context)
        {
            var parts = Unprotect(context.Request.Cookies[SessionCookie], SessionPurpose);
            if (parts == null || parts.Length != 5 || parts[0] != "v1") return null;
            Guid userId;
            long issuedTicks, seenTicks;
            if (!Guid.TryParse(parts[1], out userId) || !long.TryParse(parts[3], out issuedTicks) || !long.TryParse(parts[4], out seenTicks))
                return null;
            var now = DateTime.UtcNow;
            var issued = new DateTime(issuedTicks, DateTimeKind.Utc);
            var seen = new DateTime(seenTicks, DateTimeKind.Utc);
            if (now - issued > AbsoluteTimeout || now - seen > IdleTimeout || seen > now.AddMinutes(5))
            {
                Expire(context, SessionCookie);
                return null;
            }

            var user = AdminUserRepository.FindById(userId);
            if (user == null || !user.CanSignIn || !user.TwoFactorEnabled || string.IsNullOrEmpty(user.SecurityStamp) || user.SecurityStamp != parts[2])
            {
                Expire(context, SessionCookie);
                return null;
            }

            if (now - seen > RefreshAfter)
                WriteSession(context, userId, user.SecurityStamp, issued, now);
            return AdminUserRepository.LoadPrincipal(user);
        }

        private static void WriteSession(HttpContextBase context, Guid userId, string stamp, DateTime issued, DateTime seen)
        {
            var value = string.Join("|", "v1", userId.ToString("N"), stamp, issued.Ticks.ToString(), seen.Ticks.ToString());
            SetCookie(context, SessionCookie, Protect(value, SessionPurpose), null);
        }

        // ---- Pending sign-in (between password and code) -------------------------------

        public static void SetPending(HttpContextBase context, Guid userId, string stamp, string stage, string secret)
        {
            var expires = DateTime.UtcNow.Add(SignInTimeout);
            var value = string.Join("|", "v1", userId.ToString("N"), stamp, stage, secret ?? "", expires.Ticks.ToString());
            SetCookie(context, SignInCookie, Protect(value, SignInPurpose), null);
        }

        public static PendingSignIn GetPending(HttpContextBase context)
        {
            var parts = Unprotect(context.Request.Cookies[SignInCookie], SignInPurpose);
            if (parts == null || parts.Length != 6 || parts[0] != "v1") return null;
            Guid userId;
            long expiresTicks;
            if (!Guid.TryParse(parts[1], out userId) || !long.TryParse(parts[5], out expiresTicks)) return null;
            var expires = new DateTime(expiresTicks, DateTimeKind.Utc);
            if (DateTime.UtcNow > expires) return null;
            if (parts[3] != StageCode && parts[3] != StageEnroll) return null;
            return new PendingSignIn { UserId = userId, Stamp = parts[2], Stage = parts[3], Secret = parts[4], ExpiresUtc = expires };
        }

        public static void ClearPending(HttpContextBase context)
        {
            Expire(context, SignInCookie);
        }

        // ---- Helpers --------------------------------------------------------------------

        /// <summary>Only addresses inside the admin panel are used after sign-in (no open redirects).</summary>
        public static string SafeReturnUrl(string returnUrl)
        {
            if (string.IsNullOrEmpty(returnUrl)) return "/admin";
            if (!returnUrl.StartsWith("/admin", StringComparison.Ordinal)) return "/admin";
            if (returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.Contains("\\") || returnUrl.Contains("://")) return "/admin";
            if (returnUrl.StartsWith("/admin/login", StringComparison.OrdinalIgnoreCase) || returnUrl.StartsWith("/admin/logout", StringComparison.OrdinalIgnoreCase)) return "/admin";
            return returnUrl;
        }

        private static string Protect(string value, string purpose)
        {
            var bytes = MachineKey.Protect(Encoding.UTF8.GetBytes(value), purpose);
            return HttpServerUtility.UrlTokenEncode(bytes);
        }

        private static string[] Unprotect(HttpCookie cookie, string purpose)
        {
            if (cookie == null || string.IsNullOrEmpty(cookie.Value) || cookie.Value.Length > 2000) return null;
            try
            {
                var bytes = HttpServerUtility.UrlTokenDecode(cookie.Value);
                if (bytes == null) return null;
                var plain = MachineKey.Unprotect(bytes, purpose);
                return plain == null ? null : Encoding.UTF8.GetString(plain).Split('|');
            }
            catch (Exception)
            {
                // Tampered, from another machine key, or from an older format: treat as signed out.
                return null;
            }
        }

        private static void SetCookie(HttpContextBase context, string name, string value, DateTime? expires)
        {
            var cookie = new HttpCookie(name, value)
            {
                HttpOnly = true,
                Secure = context.Request.IsSecureConnection || TheArqamSchools.Web.Helpers.Site.RequireHttps,
                Path = CookiePath,
                SameSite = SameSiteMode.Strict
            };
            if (expires.HasValue) cookie.Expires = expires.Value;
            context.Response.Cookies.Set(cookie);
        }

        private static void Expire(HttpContextBase context, string name)
        {
            if (context.Request.Cookies[name] == null) return;
            SetCookie(context, name, "", DateTime.UtcNow.AddYears(-1));
        }
    }
}
