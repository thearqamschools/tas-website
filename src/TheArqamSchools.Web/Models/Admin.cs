using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TheArqamSchools.Web.Infrastructure.Auth;

namespace TheArqamSchools.Web.Models
{
    /// <summary>An admin user as read from dbo.users for sign-in (never shown in full on a page).</summary>
    public class AdminUserRecord
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public string PasswordHash { get; set; }
        public string SecurityStamp { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string AuthenticatorKey { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public int AccessFailedCount { get; set; }
        public DateTimeOffset? LastSignInAt { get; set; }

        public bool CanSignIn { get { return IsActive && DeletedAt == null; } }
    }

    /// <summary>The signed-in admin user for the current request, with their roles and permissions.</summary>
    public class AdminPrincipal
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public IList<string> RoleNames { get; set; }
        public bool IsSuperAdmin { get; set; }
        public ISet<string> Permissions { get; set; }

        /// <summary>Super Admin may do everything; other users need the permission key (for example "inquiries.view").</summary>
        public bool Can(string permission)
        {
            if (IsSuperAdmin) return true;
            return !string.IsNullOrEmpty(permission) && Permissions != null && Permissions.Contains(permission);
        }

        public string DisplayName { get { return string.IsNullOrWhiteSpace(FullName) ? Email : FullName; } }
    }

    public class AdminLoginForm
    {
        [Required(ErrorMessage = "Please enter your email address.")]
        [StringLength(254)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please enter your password.")]
        [StringLength(PasswordHasher.MaxLength)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class AdminCodeForm
    {
        [Required(ErrorMessage = "Please enter the 6-digit code from your authenticator app.")]
        [StringLength(10)]
        public string Code { get; set; }

        public string ReturnUrl { get; set; }
    }

    /// <summary>First-time authenticator setup: the secret travels only inside the protected sign-in cookie.</summary>
    public class AdminEnrollViewModel
    {
        public string Email { get; set; }
        public string SecretGrouped { get; set; }
        public string SetupUri { get; set; }
        public AdminCodeForm Form { get; set; }
    }

    /// <summary>One-time creation of the first Super Admin (only while none exists and a setup key is configured).</summary>
    public class AdminSetupForm
    {
        [Required(ErrorMessage = "Please enter the setup key.")]
        [StringLength(200)]
        public string SetupKey { get; set; }

        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Please enter your full name (2 to 120 characters).")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [StringLength(254)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please choose a password.")]
        [StringLength(PasswordHasher.MaxLength, MinimumLength = PasswordHasher.MinLength, ErrorMessage = "The password must be 12 to 128 characters long.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please type the password again.")]
        [Compare("Password", ErrorMessage = "The two passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class AdminChangePasswordForm
    {
        [Required(ErrorMessage = "Please enter your current password.")]
        [StringLength(PasswordHasher.MaxLength)]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Please choose a new password.")]
        [StringLength(PasswordHasher.MaxLength, MinimumLength = PasswordHasher.MinLength, ErrorMessage = "The new password must be 12 to 128 characters long.")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please type the new password again.")]
        [Compare("NewPassword", ErrorMessage = "The two passwords do not match.")]
        public string ConfirmPassword { get; set; }

        public bool Saved { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public AdminPrincipal User { get; set; }
        public int? NewAdmissionInquiries { get; set; }
        public int? NewContactMessages { get; set; }
    }

    /// <summary>The two inbox lists: admission inquiries and other messages.</summary>
    public enum InboxKind { Inquiries, Messages }

    public class InboxItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public string SourcePage { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        public string CampusName { get; set; }
        public string ClassName { get; set; }
        public string Type { get; set; }
        public string Subject { get; set; }
    }

    public class InboxListViewModel
    {
        public InboxKind Kind { get; set; }
        public IList<InboxItem> Items { get; set; }
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public string Status { get; set; }
        public string Query { get; set; }
        public IDictionary<string, int> StatusCounts { get; set; }
        public bool CanUpdate { get; set; }

        public int PageCount { get { return Total == 0 ? 1 : (Total + PageSize - 1) / PageSize; } }
        public string BasePath { get { return Kind == InboxKind.Inquiries ? "/admin/inquiries" : "/admin/messages"; } }
        public string Title { get { return Kind == InboxKind.Inquiries ? "Admission inquiries" : "Messages"; } }

        public int CountFor(string status)
        {
            int n;
            return StatusCounts != null && StatusCounts.TryGetValue(status, out n) ? n : 0;
        }
    }

    public class InboxDetailViewModel
    {
        public InboxKind Kind { get; set; }
        public InboxItem Item { get; set; }
        public bool CanUpdate { get; set; }
        public bool Saved { get; set; }
        public string BasePath { get { return Kind == InboxKind.Inquiries ? "/admin/inquiries" : "/admin/messages"; } }
        public string ListTitle { get { return Kind == InboxKind.Inquiries ? "Admission inquiries" : "Messages"; } }
    }

    public class UserListRow
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public bool IsActive { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public DateTimeOffset? LastSignInAt { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public IList<string> RoleNames { get; set; }
    }

    public class UserRoleRow
    {
        public Guid UserId { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
    }

    public class RoleOption
    {
        public Guid Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    /// <summary>Create or edit an admin user. Lengths and formats match the dbo.users checks.</summary>
    public class UserEditForm
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Please enter the full name.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Please enter the full name (2 to 120 characters).")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Please enter the email address.")]
        [StringLength(254)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Please enter a valid phone number, for example 0300-1234567, or leave it empty.")]
        public string Phone { get; set; }

        [StringLength(120)]
        public string JobTitle { get; set; }

        public bool IsActive { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public DateTimeOffset? LastSignInAt { get; set; }
        public IList<Guid> RoleIds { get; set; }
    }

    public class UserEditViewModel
    {
        public UserEditForm Form { get; set; }
        public IList<RoleOption> Roles { get; set; }
        public bool IsNew { get; set; }
        public bool IsSelf { get; set; }
        public bool ActorIsSuperAdmin { get; set; }
        public string Notice { get; set; }
        /// <summary>Shown once, right after creating the user or resetting the password.</summary>
        public string TemporaryPassword { get; set; }
    }
}
