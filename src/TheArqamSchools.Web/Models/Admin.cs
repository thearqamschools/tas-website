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

    public class CampusListRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Slug { get; set; }
        public string Area { get; set; }
        public string Status { get; set; }
        public string AdmissionStatus { get; set; }
        public bool IsPublished { get; set; }
        public DateTimeOffset? ArchivedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string CityName { get; set; }
        public string CitySlug { get; set; }
    }

    public class CityOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
    }

    /// <summary>Campus (dbo.branches) edit form. Lengths and formats match the database checks.</summary>
    public class CampusEditForm
    {
        public Guid Id { get; set; }
        public Guid CityId { get; set; }

        /// <summary>Only on the form: a city not yet in the list, created on save.</summary>
        [StringLength(100)]
        public string NewCityName { get; set; }

        [Required(ErrorMessage = "Please enter the campus name.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Please enter the campus name (2 to 150 characters).")]
        public string Name { get; set; }

        [StringLength(120)]
        [RegularExpression(@"^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, digits and hyphens only, for example model-town.")]
        public string Slug { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^[A-Z0-9-]{2,20}$", ErrorMessage = "Use 2 to 20 capital letters, digits or hyphens, for example FSD-001.")]
        public string Code { get; set; }

        [StringLength(120)] public string Area { get; set; }
        [StringLength(300)] public string AddressLine { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Please enter a valid phone number, for example 041-2660265.")]
        public string Phone { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Please enter a valid phone number.")]
        public string PhoneAlt { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Please enter a valid WhatsApp number, for example 0339-2406479.")]
        public string Whatsapp { get; set; }

        [StringLength(254)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [StringLength(200)] public string Timings { get; set; }

        [StringLength(500)]
        [RegularExpression(@"^https://(www\.)?(google\.[a-z.]+/maps|maps\.google\.[a-z.]+|maps\.app\.goo\.gl/|goo\.gl/maps/).*$",
            ErrorMessage = "Please paste a Google Maps link (it starts with https://maps.app.goo.gl/ or https://www.google.com/maps).")]
        public string GoogleMapsUrl { get; set; }

        [Range(typeof(decimal), "-90", "90", ErrorMessage = "Latitude must be between -90 and 90.")] public decimal? Latitude { get; set; }
        [Range(typeof(decimal), "-180", "180", ErrorMessage = "Longitude must be between -180 and 180.")] public decimal? Longitude { get; set; }

        [StringLength(5000)] public string Overview { get; set; }
        [StringLength(120)] public string PrincipalName { get; set; }
        [StringLength(120)] public string PrincipalTitle { get; set; }
        [StringLength(3000)] public string PrincipalMessage { get; set; }

        public string Status { get; set; }
        public string AdmissionStatus { get; set; }
        [StringLength(200)] public string AdmissionNote { get; set; }
        public bool IsPublished { get; set; }
        [Range(0, 100000)] public int SortOrder { get; set; }
        public bool IsArchived { get; set; }
    }

    public class CampusEditViewModel
    {
        public CampusEditForm Form { get; set; }
        public IList<CityOption> Cities { get; set; }
        public bool IsNew { get; set; }
        public bool CanEdit { get; set; }
        public bool CanPublish { get; set; }
        public bool CanArchive { get; set; }
        public string Notice { get; set; }
        public string PublicUrl { get; set; }
    }

    /// <summary>Settings form values keyed by setting key (links use "key.label" and "key.href"; social links "social.platform").</summary>
    public class SettingsForm
    {
        public SettingsForm() { Values = new Dictionary<string, string>(); KeptSocial = new List<Newtonsoft.Json.Linq.JObject>(); }
        public IDictionary<string, string> Values { get; private set; }
        /// <summary>Social links in the database that the form has no slot for; saved back unchanged.</summary>
        public IList<Newtonsoft.Json.Linq.JObject> KeptSocial { get; private set; }
        public bool Saved { get; set; }
        public bool CanEdit { get; set; }

        public string Get(string key)
        {
            string v;
            return Values.TryGetValue(key, out v) ? v : null;
        }
    }

    public class MenuAdminView
    {
        public Guid Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public IList<MenuItemRow> Items { get; set; }
    }

    public class MenuItemRow
    {
        public Guid Id { get; set; }
        public Guid MenuId { get; set; }
        public Guid? ParentId { get; set; }
        public string Label { get; set; }
        public string Url { get; set; }
        public Guid? PageId { get; set; }
        public string PagePath { get; set; }
        public string PageStatus { get; set; }
        public string Target { get; set; }
        public int SortOrder { get; set; }
        public bool IsVisible { get; set; }
        public int Depth { get; set; }
        public string Link { get { return Url ?? PagePath; } }
    }

    public class PageOption
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string Status { get; set; }
    }

    /// <summary>A menu item. Exactly one of PageId and Url is saved (the form's LinkType decides which).</summary>
    public class MenuItemForm
    {
        public Guid Id { get; set; }
        public Guid MenuId { get; set; }
        public Guid? ParentId { get; set; }

        [Required(ErrorMessage = "Please enter the menu text.")]
        [StringLength(60, ErrorMessage = "The menu text can be at most 60 characters.")]
        public string Label { get; set; }

        public string LinkType { get; set; }
        public Guid? PageId { get; set; }

        [StringLength(300)]
        public string Url { get; set; }

        public string Target { get; set; }

        [StringLength(160)]
        public string Description { get; set; }

        [Range(0, 100000)]
        public int SortOrder { get; set; }

        public bool IsVisible { get; set; }
    }

    public class MenuItemEditViewModel
    {
        public MenuItemForm Form { get; set; }
        public MenuAdminView Menu { get; set; }
        public IList<PageOption> Pages { get; set; }
        public bool IsNew { get; set; }
        public bool CanEdit { get; set; }
        public string Notice { get; set; }
    }

    public class PageListRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string Status { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int SectionCount { get; set; }
        public int DraftCount { get; set; }
    }

    public class SectionRow
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public int SortOrder { get; set; }
        public bool IsVisible { get; set; }
        public bool HasDraft { get; set; }

        /// <summary>"split_feature" becomes "Split feature".</summary>
        public string TypeLabel
        {
            get
            {
                var t = (Type ?? "").Replace("_", " ");
                return t.Length == 0 ? t : char.ToUpperInvariant(t[0]) + t.Substring(1);
            }
        }
    }

    /// <summary>Page details and search-engine text. The address (path) is not editable, so links never break.</summary>
    public class PageEditForm
    {
        public Guid Id { get; set; }
        public string Path { get; set; }

        [Required(ErrorMessage = "Please enter the page title.")]
        [StringLength(150, ErrorMessage = "The title can be at most 150 characters.")]
        public string Title { get; set; }

        [StringLength(300, ErrorMessage = "The summary can be at most 300 characters.")]
        public string Summary { get; set; }

        public string Status { get; set; }
        public Guid? SeoId { get; set; }

        [StringLength(70, ErrorMessage = "The search title can be at most 70 characters.")]
        public string MetaTitle { get; set; }

        [StringLength(170, ErrorMessage = "The search description can be at most 170 characters.")]
        public string MetaDescription { get; set; }

        public IList<SectionRow> Sections { get; set; }
    }

    public class PageEditViewModel
    {
        public PageEditForm Form { get; set; }
        public bool CanEdit { get; set; }
        public bool CanPublish { get; set; }
        public string Notice { get; set; }
    }

    public class SectionContent
    {
        public Guid Id { get; set; }
        public Guid PageId { get; set; }
        public string Type { get; set; }
        public string Data { get; set; }
        public string DraftData { get; set; }
        public bool IsVisible { get; set; }
        public string PageTitle { get; set; }
        public string PagePath { get; set; }
        public string PageStatus { get; set; }
    }

    public class SectionEditViewModel
    {
        public SectionContent Section { get; set; }
        public IList<TheArqamSchools.Web.Infrastructure.JsonContentFields.Field> Fields { get; set; }
        public string Signature { get; set; }
        public bool ShowingDraft { get; set; }
        public bool CanEdit { get; set; }
        public bool CanPublish { get; set; }
        public string Notice { get; set; }
        public IList<string> Errors { get; set; }
    }

    public class FranchiseRow
    {
        public Guid Id { get; set; }
        public string ReferenceNo { get; set; }
        public string Status { get; set; }
        public string FullName { get; set; }
        public string Mobile { get; set; }
        public string Whatsapp { get; set; }
        public string Email { get; set; }
        public string City { get; set; }
        public string District { get; set; }
        public string Province { get; set; }
        public string Address { get; set; }
        public string ProposedCity { get; set; }
        public string ProposedArea { get; set; }
        public string BuildingPlan { get; set; }
        public string BuildingArea { get; set; }
        public string Experience { get; set; }
        public string InvestmentRange { get; set; }
        public string StartTimeline { get; set; }
        public string Message { get; set; }
        public string SourcePage { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>News post or event edit form. Lengths match the dbo.news / dbo.events columns.</summary>
    public class NewsEditForm
    {
        public static readonly string[] Categories = { "achievement", "campus", "announcement" };

        public Guid Id { get; set; }
        public string Kind { get; set; }

        [Required(ErrorMessage = "Please enter a title.")]
        [StringLength(160, MinimumLength = 3, ErrorMessage = "The title must be 3 to 160 characters.")]
        public string Title { get; set; }

        [StringLength(120)]
        [RegularExpression(@"^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, digits and hyphens only.")]
        public string Slug { get; set; }

        [StringLength(300, ErrorMessage = "The summary can be at most 300 characters.")]
        public string Summary { get; set; }

        [StringLength(20000, ErrorMessage = "The text is too long.")]
        public string Text { get; set; }

        public string Body { get; set; }
        public Guid? BranchId { get; set; }
        public string Category { get; set; }
        public bool IsFeatured { get; set; }
        public DateTimeOffset? StartsAt { get; set; }
        public DateTimeOffset? EndsAt { get; set; }
        public bool IsAllDay { get; set; }

        [StringLength(200)]
        public string Venue { get; set; }

        public string Status { get; set; }
    }

    public class NewsEditViewModel
    {
        public NewsEditForm Form { get; set; }
        public IList<CampusListRow> Campuses { get; set; }
        public bool IsNew { get; set; }
        public bool CanEdit { get; set; }
        public bool CanPublish { get; set; }
        public string Notice { get; set; }
        public string StartDate { get; set; }
        public string StartTime { get; set; }
        public string EndDate { get; set; }
        public string EndTime { get; set; }
        /// <summary>Photos of the post (empty for a new post, which must be saved first).</summary>
        public IList<TheArqamSchools.Web.Data.PostImage> Photos { get; set; }
        /// <summary>True when this user may add, order and remove photos (the update permission, and the publish permission for posts that are not drafts).</summary>
        public bool CanEditPhotos { get; set; }
    }
}
