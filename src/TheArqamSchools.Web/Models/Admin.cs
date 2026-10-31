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
}
