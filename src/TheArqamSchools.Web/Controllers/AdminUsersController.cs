using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/users: admin accounts and their roles (permission "users.manage").
    /// Safeguards: only a Super Admin can give or remove the Super Admin role; you
    /// cannot deactivate yourself or remove your own Super Admin role; there is always
    /// at least one active Super Admin. Temporary passwords are shown once and never stored in clear.
    /// </summary>
    [AdminAuthorize("users.manage")]
    public class AdminUsersController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View("~/Views/Admin/Users/Index.cshtml", UserAdminRepository.List());
        }

        [HttpGet]
        public ActionResult New()
        {
            return EditView(new UserEditForm { IsActive = true, RoleIds = new List<Guid>() }, true, null, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("New")]
        public ActionResult NewPost(UserEditForm form)
        {
            if (form == null) form = new UserEditForm();
            Clean(form);
            form.IsActive = true;
            var actor = AdminAuth.Current(HttpContext);
            CheckRoles(form, null, actor);
            if (ModelState.IsValid && UserAdminRepository.EmailTaken(form.Email, null))
                ModelState.AddModelError("Email", "A user with this email address already exists.");
            if (!ModelState.IsValid) return EditView(form, true, null, null);

            var password = UserAdminRepository.NewTemporaryPassword();
            var id = UserAdminRepository.Create(form, PasswordHasher.Hash(password), actor.Id);
            var saved = UserAdminRepository.Get(id);
            ModelState.Clear();
            return EditView(saved, false, "The user was created. Give them the temporary password below. "
                + "At first sign-in they will set up two-step verification; ask them to change the password afterwards.", password);
        }

        [HttpGet]
        public ActionResult Edit(Guid id, string notice)
        {
            var form = UserAdminRepository.Get(id);
            if (form == null) return HttpNotFound();
            return EditView(form, false, notice == "saved" ? "Changes saved." : notice == "2fa" ? "Two-step verification was reset. The user will set it up again at next sign-in." : null, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Edit")]
        public ActionResult EditPost(Guid id, UserEditForm form)
        {
            var existing = UserAdminRepository.Get(id);
            if (existing == null) return HttpNotFound();
            if (form == null) form = new UserEditForm();
            form.Id = id;
            Clean(form);
            form.TwoFactorEnabled = existing.TwoFactorEnabled;
            form.LastSignInAt = existing.LastSignInAt;
            var actor = AdminAuth.Current(HttpContext);

            CheckRoles(form, existing, actor);
            if (id == actor.Id && !form.IsActive)
                ModelState.AddModelError("IsActive", "You cannot deactivate your own account.");
            if (ModelState.IsValid && UserAdminRepository.EmailTaken(form.Email, id))
                ModelState.AddModelError("Email", "A user with this email address already exists.");
            if (!ModelState.IsValid) return EditView(form, false, null, null);

            UserAdminRepository.Update(form, actor.Id);
            return Redirect("/admin/users/" + id.ToString("D") + "?notice=saved");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(Guid id)
        {
            var user = UserAdminRepository.Get(id);
            if (user == null) return HttpNotFound();
            var actor = AdminAuth.Current(HttpContext);
            if (!CanManage(user, actor)) return Forbidden();
            if (id == actor.Id) return Redirect("/admin/account/password");

            var password = UserAdminRepository.NewTemporaryPassword();
            UserAdminRepository.ResetPassword(id, PasswordHasher.Hash(password), actor.Id);
            return EditView(UserAdminRepository.Get(id), false,
                "The password was reset and the user was signed out everywhere. Give them the temporary password below.", password);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetTwoFactor(Guid id)
        {
            var user = UserAdminRepository.Get(id);
            if (user == null) return HttpNotFound();
            var actor = AdminAuth.Current(HttpContext);
            if (!CanManage(user, actor)) return Forbidden();
            UserAdminRepository.ResetTwoFactor(id, actor.Id);
            if (id == actor.Id)
            {
                AdminAuth.SignOut(HttpContext);
                return Redirect("/admin/login");
            }
            return Redirect("/admin/users/" + id.ToString("D") + "?notice=2fa");
        }

        // ---- Helpers --------------------------------------------------------------------

        private static void Clean(UserEditForm form)
        {
            form.FullName = (form.FullName ?? "").Trim();
            form.Email = (form.Email ?? "").Trim();
            form.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
            form.JobTitle = string.IsNullOrWhiteSpace(form.JobTitle) ? null : form.JobTitle.Trim();
            form.RoleIds = (form.RoleIds ?? new List<Guid>()).Distinct().ToList();
        }

        /// <summary>Super Admin rules for role changes; adds model errors when a rule is broken.</summary>
        private void CheckRoles(UserEditForm form, UserEditForm existing, AdminPrincipal actor)
        {
            var roles = UserAdminRepository.Roles();
            var known = new HashSet<Guid>(roles.Select(r => r.Id));
            if (form.RoleIds.Any(r => !known.Contains(r)))
                ModelState.AddModelError("RoleIds", "Please choose roles from the list.");
            if (form.RoleIds.Count == 0)
                ModelState.AddModelError("RoleIds", "Please choose at least one role.");

            var superId = roles.Where(r => r.Key == AdminUserRepository.SuperAdminRole).Select(r => (Guid?)r.Id).FirstOrDefault();
            var wantsSuper = superId.HasValue && form.RoleIds.Contains(superId.Value);
            var hadSuper = existing != null && superId.HasValue && existing.RoleIds.Contains(superId.Value);

            if (!actor.IsSuperAdmin && (wantsSuper != hadSuper || hadSuper))
                ModelState.AddModelError("RoleIds", "Only a Super Admin can manage Super Admin accounts.");
            if (existing != null && existing.Id == actor.Id && hadSuper && !wantsSuper)
                ModelState.AddModelError("RoleIds", "You cannot remove your own Super Admin role.");
            if (existing != null && hadSuper && (!wantsSuper || !form.IsActive)
                && UserAdminRepository.OtherActiveSuperAdmins(existing.Id) == 0)
                ModelState.AddModelError("RoleIds", "There must always be at least one active Super Admin.");
        }

        /// <summary>Only a Super Admin can reset another Super Admin.</summary>
        private static bool CanManage(UserEditForm user, AdminPrincipal actor)
        {
            if (actor.IsSuperAdmin) return true;
            var superId = UserAdminRepository.Roles().Where(r => r.Key == AdminUserRepository.SuperAdminRole).Select(r => r.Id).FirstOrDefault();
            return !user.RoleIds.Contains(superId);
        }

        private ActionResult Forbidden()
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Admin/Forbidden.cshtml");
        }

        private ActionResult EditView(UserEditForm form, bool isNew, string notice, string temporaryPassword)
        {
            var actor = AdminAuth.Current(HttpContext);
            return View("~/Views/Admin/Users/Edit.cshtml", new UserEditViewModel
            {
                Form = form,
                Roles = UserAdminRepository.Roles(),
                IsNew = isNew,
                IsSelf = !isNew && form.Id == actor.Id,
                ActorIsSuperAdmin = actor.IsSuperAdmin,
                Notice = notice,
                TemporaryPassword = temporaryPassword
            });
        }
    }
}
