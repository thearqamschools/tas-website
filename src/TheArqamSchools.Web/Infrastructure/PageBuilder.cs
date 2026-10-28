using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Helpers;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// Loads a CMS page with its sections and prepares the data that live
    /// sections need (the admission inquiry form and the campus directory).
    /// </summary>
    public static class PageBuilder
    {
        public static PageViewModel Build(string path, HttpRequestBase request)
        {
            var page = PageRepository.PublishedByPath(path);
            if (page == null) return null;
            var model = new PageViewModel
            {
                Page = page,
                Sections = PageRepository.VisibleSections(page.Id),
                Breadcrumbs = Breadcrumbs(page),
            };
            model.Meta = Meta(page, model.Sections);

            if (model.Sections.Any(s => s.Type == "admission_inquiry"))
            {
                model.Inquiry = new AdmissionInquiryViewModel
                {
                    Form = new AdmissionInquiryForm { Consent = false },
                    Campuses = CampusRepository.All(),
                    Classes = ClassLevelRepository.All(),
                    Sent = request != null && request.QueryString["sent"] == "1",
                };
                Guid campus;
                if (request != null && Guid.TryParse(request.QueryString["campus"], out campus)) model.Inquiry.Form.BranchId = campus;
            }

            if (model.Sections.Any(s => s.Type == "campus_directory"))
            {
                // Search terms are read without ASP.NET request validation: they are cleaned here,
                // passed to SQL as parameters and HTML-encoded on output, so "<" in a search is harmless.
                var qs = request != null ? request.Unvalidated.QueryString : new System.Collections.Specialized.NameValueCollection();
                model.Directory = Directory(qs["q"], qs["city"], qs["district"]);
            }
            return model;
        }

        public static CampusDirectoryViewModel Directory(string q, string city, string district)
        {
            q = Clean(q, 100);
            city = Slug(city);
            district = Slug(district);
            return new CampusDirectoryViewModel
            {
                Query = q,
                City = city,
                District = district,
                Results = CampusRepository.Search(q, city, district),
                Cities = CampusRepository.Cities(),
                Districts = CampusRepository.Districts(),
                TotalPublished = CampusRepository.CountPublished(),
            };
        }

        private static IList<Breadcrumb> Breadcrumbs(Page page)
        {
            var list = new List<Breadcrumb> { new Breadcrumb("Home", "/") };
            var parts = page.Path.Trim('/').Split('/');
            if (parts.Length > 1)
            {
                var parent = PageRepository.PublishedByPath("/" + parts[0]);
                if (parent != null) list.Add(new Breadcrumb(parent.Title, parent.Path));
            }
            list.Add(new Breadcrumb(page.Title, null));
            return list;
        }

        public static PageMeta Meta(Page page, IList<PageSection> sections)
        {
            var seo = PageRepository.Seo(page.SeoId);
            string image = null;
            var withImage = sections.FirstOrDefault(s => s.Data["image"] != null && !Media.IsPlaceholder(Media.Alt(s.Data["image"])));
            if (withImage != null) image = Media.Url(withImage.Data["image"]);
            return new PageMeta
            {
                Title = seo != null && !string.IsNullOrWhiteSpace(seo.MetaTitle) ? seo.MetaTitle : page.Title + " | The Arqam Schools",
                Description = seo != null && !string.IsNullOrWhiteSpace(seo.MetaDescription) ? seo.MetaDescription : page.Summary,
                CanonicalPath = seo != null && !string.IsNullOrWhiteSpace(seo.CanonicalUrl) ? seo.CanonicalUrl : page.Path,
                NoIndex = seo != null && seo.RobotsNoindex,
                ImageUrl = image,
            };
        }

        public static string Clean(string s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }

        /// <summary>Only lowercase letters, digits and hyphens are accepted in city and district filters.</summary>
        public static string Slug(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().ToLowerInvariant();
            return s.Length <= 120 && s.All(ch => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-') ? s : null;
        }

        public static string ClientKey(HttpRequestBase request)
        {
            return request.UserHostAddress ?? "unknown";
        }
    }
}
