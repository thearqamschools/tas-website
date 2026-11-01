using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheArqamSchools.Web.Models
{
    /// <summary>
    /// Admission inquiry from /admissions (saved in dbo.admission_inquiries).
    /// Lengths and formats match the database checks.
    /// </summary>
    public class AdmissionInquiryForm
    {
        public Guid? BranchId { get; set; }
        public Guid? ClassLevelId { get; set; }

        [Required(ErrorMessage = "Please enter your name.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Please enter your name (2 to 120 characters).")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Please enter a phone number.")]
        [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Please enter a valid phone number, for example 0300-1234567.")]
        public string Phone { get; set; }

        [StringLength(254)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address, or leave it empty.")]
        public string Email { get; set; }

        [StringLength(2000, ErrorMessage = "Please keep your message under 2,000 characters.")]
        public string Message { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Please agree that we may contact you.")]
        public bool Consent { get; set; }

        /// <summary>Hidden field: people leave it empty, spam bots fill it in.</summary>
        public string Website { get; set; }
    }

    /// <summary>Message from a campus page (admissions go to dbo.admission_inquiries, the rest to dbo.contact_messages).</summary>
    public class CampusMessageForm
    {
        public static readonly string[] Types = { "admissions", "general", "careers", "feedback", "media", "other" };

        [Required]
        public Guid BranchId { get; set; }

        [Required(ErrorMessage = "Please choose what your message is about.")]
        public string Type { get; set; }

        [Required(ErrorMessage = "Please enter your name.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Please enter your name (2 to 120 characters).")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Please enter a phone number.")]
        [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Please enter a valid phone number, for example 0300-1234567.")]
        public string Phone { get; set; }

        [StringLength(254)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address, or leave it empty.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please write your message.")]
        [StringLength(2000, MinimumLength = 2, ErrorMessage = "Please write your message (up to 2,000 characters).")]
        public string Message { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Please agree that we may contact you.")]
        public bool Consent { get; set; }

        public string Website { get; set; }
    }

    /// <summary>General message from /contact (saved in dbo.contact_messages). Phone or email is required, as the database requires.</summary>
    public class ContactForm
    {
        public static readonly string[] Types = { "general", "feedback", "careers", "media", "other" };

        public string Type { get; set; }

        [Required(ErrorMessage = "Please enter your name.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Please enter your name (2 to 120 characters).")]
        public string Name { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Please enter a valid phone number, for example 0300-1234567.")]
        public string Phone { get; set; }

        [StringLength(254)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [StringLength(200, ErrorMessage = "Please keep the subject under 200 characters.")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Please write your message.")]
        [StringLength(3000, MinimumLength = 2, ErrorMessage = "Please write your message (up to 3,000 characters).")]
        public string Message { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Please agree that we may contact you.")]
        public bool Consent { get; set; }

        /// <summary>Hidden field: people leave it empty, spam bots fill it in.</summary>
        public string Website { get; set; }
    }

    public class ContactSectionViewModel
    {
        public ContactForm Form { get; set; }
        public bool Sent { get; set; }
        public bool RateLimited { get; set; }
    }

    /// <summary>Franchise application from /franchise/apply (saved in dbo.franchise_applications). Lengths and formats match the database checks.</summary>
    public class FranchiseForm
    {
        public static readonly string[] BuildingPlans = { "own", "rent", "undecided" };

        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Please enter your full name (2 to 120 characters).")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Please enter your mobile number.")]
        [StringLength(20)]
        [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Please enter a valid mobile number, for example 0300-1234567.")]
        public string Mobile { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Please enter a valid WhatsApp number, or leave it empty.")]
        public string Whatsapp { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [StringLength(254)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please enter the city where you live.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "Please enter your city (2 to 80 characters).")]
        public string City { get; set; }

        [StringLength(80)] public string Province { get; set; }

        [Required(ErrorMessage = "Please enter the city where you want to open the campus.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "Please enter the proposed city (2 to 80 characters).")]
        public string ProposedCity { get; set; }

        [Required(ErrorMessage = "Please enter the area or locality.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Please enter the proposed area (2 to 120 characters).")]
        public string ProposedArea { get; set; }

        public string BuildingPlan { get; set; }
        [StringLength(80)] public string BuildingArea { get; set; }
        [StringLength(2000, ErrorMessage = "Please keep this under 2,000 characters.")] public string Experience { get; set; }
        [StringLength(80)] public string StartTimeline { get; set; }
        [StringLength(3000, ErrorMessage = "Please keep your message under 3,000 characters.")] public string Message { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Please agree that we may contact you.")]
        public bool Consent { get; set; }

        public string Website { get; set; }
    }

    public class FranchiseSectionViewModel
    {
        public FranchiseForm Form { get; set; }
        public bool Accepting { get; set; }
        public string ClosedMessage { get; set; }
        public string ResponseTime { get; set; }
        public IList<string> StartTimelines { get; set; }
        public bool Sent { get; set; }
        public string ReferenceNo { get; set; }
        public bool RateLimited { get; set; }
    }

    /// <summary>Everything the admission inquiry section needs to draw the form.</summary>
    public class AdmissionInquiryViewModel
    {
        public AdmissionInquiryForm Form { get; set; }
        public IList<Campus> Campuses { get; set; }
        public IList<ClassLevel> Classes { get; set; }
        public bool Sent { get; set; }
        public bool RateLimited { get; set; }
    }

    public class CampusDirectoryViewModel
    {
        public string Query { get; set; }
        public string City { get; set; }
        public string District { get; set; }
        public IList<Campus> Results { get; set; }
        public IList<FilterOption> Cities { get; set; }
        public IList<FilterOption> Districts { get; set; }
        public int TotalPublished { get; set; }
        public bool IsFiltered { get { return !string.IsNullOrEmpty(Query) || !string.IsNullOrEmpty(City) || !string.IsNullOrEmpty(District); } }
        /// <summary>Set on /campuses/{city}: the form posts back to the city page.</summary>
        public string FormAction { get; set; }
    }
}
