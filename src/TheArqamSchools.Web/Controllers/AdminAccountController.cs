using System;
using System.Configuration;
using System.Text;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// Admin sign-in: password, then the 6-digit authenticator code (required for
    /// every user; set up on first sign-in), sign-out, password change, and the
    /// one-time creation of the first Super Admin.
    /// </summary>
    public class AdminAccountController : Controller
    {
        private const string Issuer = "The Arqam Schools";
        private const string GenericFailure = "The email or password is incorrect, or the account is locked for a short time. Please try again.";

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            AdminResponse.NoStore(filterContext.HttpContext);
            base.OnActionExecuting(filterContext);
        }

        // ---- Step 1: email and password -------------------------------------------------

        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (AdminAuth.Current(HttpContext) != null) return Redirect(AdminAuth.SafeReturnUrl(returnUrl));
            return View("~/Views/Admin/Account/Login.cshtml", new AdminLoginForm { ReturnUrl = AdminAuth.SafeReturnUrl(returnUrl) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(AdminLoginForm form)
        {
            if (form == null) form = new AdminLoginForm();
            form.ReturnUrl = AdminAuth.SafeReturnUrl(form.ReturnUrl);
            if (!ModelState.IsValid) return LoginView(form, null);

            if (!RateLimiter.Allow("admin-login", PageBuilder.ClientKey(Request), 10, TimeSpan.FromMinutes(10)))
                return LoginView(form, "Too many attempts from this connection. Please wait 10 minutes and try again.");

            var user = AdminUserRepository.FindByEmail(form.Email);
            if (user == null)
            {
                PasswordHasher.VerifyDummy(form.Password);
                return LoginView(form, GenericFailure);
            }
            if (!user.CanSignIn || (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow))
            {
                PasswordHasher.VerifyDummy(form.Password);
                return LoginView(form, GenericFailure);
            }
            if (!PasswordHasher.Verify(form.Password, user.PasswordHash))
            {
                AdminUserRepository.RecordFailure(user.Id);
                return LoginView(form, GenericFailure);
            }

            AdminUserRepository.ResetFailures(user.Id);
            var stamp = AdminUserRepository.EnsureSecurityStamp(user);
            if (user.TwoFactorEnabled && !string.IsNullOrEmpty(user.AuthenticatorKey))
            {
                AdminAuth.SetPending(HttpContext, user.Id, stamp, AdminAuth.StageCode, null);
                return Redirect("/admin/login/code" + ReturnQuery(form.ReturnUrl));
            }
            AdminAuth.SetPending(HttpContext, user.Id, stamp, AdminAuth.StageEnroll, Totp.NewSecret());
            return Redirect("/admin/login/authenticator" + ReturnQuery(form.ReturnUrl));
        }

        // ---- Step 2: authenticator code -------------------------------------------------

        [HttpGet]
        public ActionResult Code(string returnUrl)
        {
            var pending = AdminAuth.GetPending(HttpContext);
            if (pending == null || pending.Stage != AdminAuth.StageCode) return Redirect("/admin/login");
            return View("~/Views/Admin/Account/Code.cshtml", new AdminCodeForm { ReturnUrl = AdminAuth.SafeReturnUrl(returnUrl) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Code(AdminCodeForm form)
        {
            if (form == null) form = new AdminCodeForm();
            form.ReturnUrl = AdminAuth.SafeReturnUrl(form.ReturnUrl);
            var pending = AdminAuth.GetPending(HttpContext);
            if (pending == null || pending.Stage != AdminAuth.StageCode) return Redirect("/admin/login");
            if (!ModelState.IsValid) return View("~/Views/Admin/Account/Code.cshtml", form);

            if (!RateLimiter.Allow("admin-code", PageBuilder.ClientKey(Request), 10, TimeSpan.FromMinutes(10)))
                return CodeError(form, "Too many attempts from this connection. Please wait 10 minutes and try again.");

            var user = AdminUserRepository.FindById(pending.UserId);
            if (user == null || !user.CanSignIn || user.SecurityStamp != pending.Stamp || !user.TwoFactorEnabled
                || (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow))
            {
                AdminAuth.ClearPending(HttpContext);
                return Redirect("/admin/login");
            }
            if (!Totp.Verify(user.AuthenticatorKey, form.Code, user.Id.ToString("N")))
            {
                AdminUserRepository.RecordFailure(user.Id);
                return CodeError(form, "That code is not correct or has expired. Please enter the current code from your app.");
            }

            AdminUserRepository.RecordSignIn(user.Id);
            AdminAuth.SignIn(HttpContext, user.Id, user.SecurityStamp);
            return Redirect(form.ReturnUrl);
        }

        // ---- First sign-in: set up the authenticator app -------------------------------

        [HttpGet]
        public ActionResult Authenticator(string returnUrl)
        {
            var pending = AdminAuth.GetPending(HttpContext);
            if (pending == null || pending.Stage != AdminAuth.StageEnroll) return Redirect("/admin/login");
            var user = AdminUserRepository.FindById(pending.UserId);
            if (user == null || !user.CanSignIn) return Redirect("/admin/login");
            return EnrollView(user, pending, new AdminCodeForm { ReturnUrl = AdminAuth.SafeReturnUrl(returnUrl) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Authenticator")]
        public ActionResult AuthenticatorPost(AdminCodeForm form)
        {
            if (form == null) form = new AdminCodeForm();
            form.ReturnUrl = AdminAuth.SafeReturnUrl(form.ReturnUrl);
            var pending = AdminAuth.GetPending(HttpContext);
            if (pending == null || pending.Stage != AdminAuth.StageEnroll || string.IsNullOrEmpty(pending.Secret)) return Redirect("/admin/login");
            var user = AdminUserRepository.FindById(pending.UserId);
            if (user == null || !user.CanSignIn || user.SecurityStamp != pending.Stamp)
            {
                AdminAuth.ClearPending(HttpContext);
                return Redirect("/admin/login");
            }
            if (!ModelState.IsValid) return EnrollView(user, pending, form);

            if (!RateLimiter.Allow("admin-code", PageBuilder.ClientKey(Request), 10, TimeSpan.FromMinutes(10)))
            {
                ModelState.AddModelError("", "Too many attempts from this connection. Please wait 10 minutes and try again.");
                return EnrollView(user, pending, form);
            }
            if (!Totp.Verify(pending.Secret, form.Code, user.Id.ToString("N")))
            {
                ModelState.AddModelError("Code", "That code is not correct. Check that the app shows \"The Arqam Schools\" and enter its current 6-digit code.");
                return EnrollView(user, pending, form);
            }

            var stamp = AdminUserRepository.EnableTwoFactor(user.Id, pending.Secret);
            AdminUserRepository.RecordSignIn(user.Id);
            AdminAuth.SignIn(HttpContext, user.Id, stamp);
            return Redirect(form.ReturnUrl);
        }

        // ---- Sign out -------------------------------------------------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            AdminAuth.SignOut(HttpContext);
            return Redirect("/admin/login?signedOut=1");
        }

        // ---- Change password ------------------------------------------------------------

        [HttpGet]
        [AdminAuthorize]
        public ActionResult Password(bool saved = false)
        {
            return View("~/Views/Admin/Account/Password.cshtml", new AdminChangePasswordForm { Saved = saved });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        [ActionName("Password")]
        public ActionResult PasswordPost(AdminChangePasswordForm form)
        {
            if (form == null) form = new AdminChangePasswordForm();
            var current = AdminAuth.Current(HttpContext);
            if (!ModelState.IsValid) return PasswordView(form);

            if (!RateLimiter.Allow("admin-password", current.Id.ToString("N"), 5, TimeSpan.FromMinutes(10)))
            {
                ModelState.AddModelError("", "Too many attempts. Please wait 10 minutes and try again.");
                return PasswordView(form);
            }
            var user = AdminUserRepository.FindById(current.Id);
            if (user == null || !PasswordHasher.Verify(form.CurrentPassword, user.PasswordHash))
            {
                ModelState.AddModelError("CurrentPassword", "Your current password is not correct.");
                return PasswordView(form);
            }
            if (form.NewPassword == form.CurrentPassword)
            {
                ModelState.AddModelError("NewPassword", "Please choose a password different from your current one.");
                return PasswordView(form);
            }

            var stamp = AdminUserRepository.ChangePassword(user.Id, PasswordHasher.Hash(form.NewPassword));
            AdminAuth.SignIn(HttpContext, user.Id, stamp);
            return Redirect("/admin/account/password?saved=true");
        }

        // ---- One-time setup of the first Super Admin -----------------------------------

        [HttpGet]
        public ActionResult Setup()
        {
            if (!SetupAvailable()) return HttpNotFound();
            return View("~/Views/Admin/Account/Setup.cshtml", new AdminSetupForm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Setup")]
        public ActionResult SetupPost(AdminSetupForm form)
        {
            if (!SetupAvailable()) return HttpNotFound();
            if (form == null) form = new AdminSetupForm();

            if (!RateLimiter.Allow("admin-setup", PageBuilder.ClientKey(Request), 5, TimeSpan.FromMinutes(30)))
            {
                ModelState.AddModelError("", "Too many attempts. Please wait 30 minutes and try again.");
                return SetupView(form);
            }
            if (!SetupKeyMatches(form.SetupKey))
                ModelState.AddModelError("SetupKey", "The setup key is not correct.");
            if (!ModelState.IsValid) return SetupView(form);

            var email = form.Email.Trim();
            if (!AdminUserRepository.CreateFirstSuperAdmin(form.FullName.Trim(), email, PasswordHasher.Hash(form.Password)))
            {
                ModelState.AddModelError("", "The first Super Admin already exists, or this email is already used. Please sign in instead.");
                return SetupView(form);
            }
            return Redirect("/admin/login?created=1");
        }

        // ---- Helpers --------------------------------------------------------------------

        /// <summary>
        /// Setup works only while no Super Admin exists AND the server has a setup key
        /// (appSetting Admin:SetupKey, at least 24 characters). Without the key nobody
        /// can claim the site, even before the first account exists.
        /// </summary>
        private static bool SetupAvailable()
        {
            var key = ConfigurationManager.AppSettings["Admin:SetupKey"];
            return !string.IsNullOrWhiteSpace(key) && key.Trim().Length >= 24 && !AdminUserRepository.SuperAdminExists();
        }

        private static bool SetupKeyMatches(string typed)
        {
            var key = (ConfigurationManager.AppSettings["Admin:SetupKey"] ?? "").Trim();
            if (string.IsNullOrEmpty(typed) || key.Length == 0) return false;
            return PasswordHasher.FixedTimeEquals(Encoding.UTF8.GetBytes(typed.Trim()), Encoding.UTF8.GetBytes(key));
        }

        private ActionResult LoginView(AdminLoginForm form, string error)
        {
            if (error != null) ModelState.AddModelError("", error);
            form.Password = null;
            return View("~/Views/Admin/Account/Login.cshtml", form);
        }

        private ActionResult CodeError(AdminCodeForm form, string error)
        {
            ModelState.AddModelError("Code", error);
            form.Code = null;
            return View("~/Views/Admin/Account/Code.cshtml", form);
        }

        private ActionResult EnrollView(AdminUserRecord user, AdminAuth.PendingSignIn pending, AdminCodeForm form)
        {
            form.Code = null;
            return View("~/Views/Admin/Account/Authenticator.cshtml", new AdminEnrollViewModel
            {
                Email = user.Email,
                SecretGrouped = Totp.Grouped(pending.Secret),
                SetupUri = Totp.SetupUri(Issuer, user.Email, pending.Secret),
                Form = form
            });
        }

        private ActionResult PasswordView(AdminChangePasswordForm form)
        {
            form.CurrentPassword = form.NewPassword = form.ConfirmPassword = null;
            return View("~/Views/Admin/Account/Password.cshtml", form);
        }

        private ActionResult SetupView(AdminSetupForm form)
        {
            form.SetupKey = form.Password = form.ConfirmPassword = null;
            return View("~/Views/Admin/Account/Setup.cshtml", form);
        }

        private static string ReturnQuery(string returnUrl)
        {
            return returnUrl == "/admin" ? "" : "?returnUrl=" + System.Web.HttpUtility.UrlEncode(returnUrl);
        }
    }
}
