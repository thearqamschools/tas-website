using System.Collections.Generic;
using System.Web.Mvc;

namespace TheArqamSchools.Web.Models
{
    public class PageViewModel
    {
        public Page Page { get; set; }
        public IList<PageSection> Sections { get; set; }
        public IList<Breadcrumb> Breadcrumbs { get; set; }
        public PageMeta Meta { get; set; }
        public AdmissionInquiryViewModel Inquiry { get; set; }
        public CampusDirectoryViewModel Directory { get; set; }
        /// <summary>Validation errors from a form posted on this page.</summary>
        public ModelStateDictionary FormErrors { get; set; }
    }

    public class CampusPageViewModel
    {
        public Campus Campus { get; set; }
        public IList<CampusClass> Classes { get; set; }
        public IList<string> Facilities { get; set; }
        public IList<Breadcrumb> Breadcrumbs { get; set; }
        public PageMeta Meta { get; set; }
        public CampusMessageForm Form { get; set; }
        public bool Sent { get; set; }
        public bool SentAsAdmission { get; set; }
        public bool RateLimited { get; set; }
    }
}
