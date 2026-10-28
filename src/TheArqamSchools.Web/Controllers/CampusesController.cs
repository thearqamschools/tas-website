using System;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Helpers;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /campuses is the CMS page with the campus directory (PagesController).
    /// This controller serves the city pages (/campuses/faisalabad) and each
    /// campus page (/campuses/faisalabad/samanabad), and its contact form.
    /// </summary>
    public class CampusesController : Controller
    {
        [ValidateInput(false)]
        public ActionResult City()
        {
            var city = RouteData.Values["city"] as string;
            var c = CampusRepository.City(PageBuilder.Slug(city));
            if (c == null) return NotFound();
            var model = PageBuilder.Build("/campuses", Request);
            if (model == null) return NotFound();
            model.Directory = PageBuilder.Directory(Request.Unvalidated.QueryString["q"], c.Slug, null);
            model.Directory.FormAction = "/campuses/" + c.Slug;
            model.Breadcrumbs.Insert(model.Breadcrumbs.Count - 1, new Breadcrumb(model.Page.Title, model.Page.Path));
            model.Breadcrumbs[model.Breadcrumbs.Count - 1] = new Breadcrumb(c.Name, null);
            model.Meta.Title = "Campuses in " + c.Name + " | The Arqam Schools";
            model.Meta.Description = "Campuses of The Arqam Schools in " + c.Name + ": addresses, phone numbers, WhatsApp, classes and directions.";
            model.Meta.CanonicalPath = "/campuses/" + c.Slug;
            ViewBag.CityName = c.Name;
            return View("~/Views/Pages/Show.cshtml", model);
        }

        public ActionResult Detail(string city, string slug)
        {
            var campus = CampusRepository.Get(PageBuilder.Slug(city), PageBuilder.Slug(slug));
            if (campus == null) return NotFound();
            var model = BuildModel(campus);
            model.Sent = Request.QueryString["sent"] == "1" || Request.QueryString["sent"] == "admission";
            model.SentAsAdmission = Request.QueryString["sent"] == "admission";
            model.Form = new CampusMessageForm { BranchId = campus.Id, Type = "admissions" };
            return View("Detail", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Message(string city, string slug, CampusMessageForm form)
        {
            var campus = CampusRepository.Get(PageBuilder.Slug(city), PageBuilder.Slug(slug));
            if (campus == null || form == null || form.BranchId != campus.Id) return NotFound();

            // Spam bots fill in the hidden field: pretend it worked and save nothing.
            if (!string.IsNullOrEmpty(form.Website))
                return Redirect(campus.Url + "?sent=1#contact");

            if (!CampusMessageForm.Types.Contains(form.Type))
                ModelState.AddModelError("Type", "Please choose what your message is about.");

            var model = BuildModel(campus);
            model.Form = form;
            if (!ModelState.IsValid) return View("Detail", model);

            if (!RateLimiter.Allow("campus-message", PageBuilder.ClientKey(Request), 5, TimeSpan.FromMinutes(10)))
            {
                model.RateLimited = true;
                return View("Detail", model);
            }

            var email = string.IsNullOrWhiteSpace(form.Email) ? null : form.Email.Trim();
            if (form.Type == "admissions")
            {
                InquiryRepository.SaveAdmissionInquiry(campus.Id, null, form.Name.Trim(), form.Phone.Trim(), email, form.Message.Trim(), campus.Url);
                return Redirect(campus.Url + "?sent=admission#contact");
            }
            InquiryRepository.SaveContactMessage(form.Type, campus.Id, form.Name.Trim(), form.Phone.Trim(), email, form.Message.Trim(), campus.Url);
            return Redirect(campus.Url + "?sent=1#contact");
        }

        private static CampusPageViewModel BuildModel(Campus campus)
        {
            var model = new CampusPageViewModel
            {
                Campus = campus,
                Classes = CampusRepository.Classes(campus.Id),
                Facilities = CampusRepository.Facilities(campus.Id),
            };
            model.Breadcrumbs = new[]
            {
                new Breadcrumb("Home", "/"), new Breadcrumb("Campuses", "/campuses"),
                new Breadcrumb(campus.CityName, "/campuses/" + campus.CitySlug), new Breadcrumb(campus.Name, null)
            }.ToList();

            var seo = PageRepository.Seo(campus.SeoId);
            var cover = Media.Url(campus.CoverBucket, campus.CoverPath);
            model.Meta = new PageMeta
            {
                Title = seo != null && !string.IsNullOrWhiteSpace(seo.MetaTitle) ? seo.MetaTitle : campus.Name + ", " + campus.CityName + " | The Arqam Schools",
                Description = seo != null && !string.IsNullOrWhiteSpace(seo.MetaDescription) ? seo.MetaDescription
                    : campus.Name + " of The Arqam Schools, " + campus.FullAddress + ". Contact details, classes and directions.",
                CanonicalPath = campus.Url,
                NoIndex = seo != null && seo.RobotsNoindex,
                ImageUrl = Media.IsPlaceholder(campus.CoverAlt) ? null : cover,
                StructuredDataJson = StructuredData(campus),
            };
            return model;
        }

        /// <summary>schema.org School data for search engines: only fields the campus record actually has.</summary>
        private static string StructuredData(Campus c)
        {
            var phones = new[] { c.Phone, c.PhoneAlt }.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@context", "https://schema.org" },
                { "@type", "School" },
                { "name", "The Arqam Schools, " + c.Name },
                { "url", Site.Absolute(c.Url) },
                { "parentOrganization", new { type = "EducationalOrganization", name = "The Arqam Schools", url = Site.Absolute("/") } },
                { "address", new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@type", "PostalAddress" }, { "streetAddress", c.AddressLine }, { "addressLocality", c.CityName }, { "addressCountry", "PK" }
                    } },
            };
            if (phones.Length > 0) data["telephone"] = phones[0];
            if (!string.IsNullOrWhiteSpace(c.Email)) data["email"] = c.Email;
            return JsonConvert.SerializeObject(data).Replace("\"type\":\"EducationalOrganization\"", "\"@type\":\"EducationalOrganization\"").Replace("</", "<\\/");
        }

        private ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/NotFound.cshtml");
        }
    }
}
