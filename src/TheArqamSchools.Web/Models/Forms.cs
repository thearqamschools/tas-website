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
