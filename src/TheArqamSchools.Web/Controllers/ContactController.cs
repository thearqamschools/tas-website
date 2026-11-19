using System;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// Forms on CMS pages: the message form on /contact (saved in dbo.contact_messages)
    /// and the franchise application on /franchise/apply (dbo.franchise_applications).
    /// Same protections as the admission form: anti-forgery token, honeypot, rate limit,
    /// server-side validation matching the database checks, redirect after success.
    /// </summary>
    public class ContactController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Send(ContactForm form)
        {
            if (form == null) form = new ContactForm();
            if (!string.IsNullOrEmpty(form.Website)) return Redirect("/contact?sent=1#contact");

            var model = PageBuilder.Build("/contact", Request);
            if (model == null || model.Contact == null) return HttpNotFound();
            model.Contact.Form = form;

            // The database counts length after trimming; check the trimmed values too.
            if (form.Name != null && form.Name.Trim().Length < 2) ModelState.AddModelError("Name", "Please enter your name (2 to 120 characters).");
            if (form.Message != null && form.Message.Trim().Length < 2) ModelState.AddModelError("Message", "Please write your message (up to 3,000 characters).");
            if (!ContactForm.Types.Contains(form.Type))
                ModelState.AddModelError("Type", "Please choose what your message is about.");
            if (string.IsNullOrWhiteSpace(form.Phone) && string.IsNullOrWhiteSpace(form.Email))
                ModelState.AddModelError("Phone", "Please give a phone number or an email address so we can reply.");
            if (!ModelState.IsValid)
            {
                model.FormErrors = ModelState;
                return View("~/Views/Pages/Show.cshtml", model);
            }
            if (!RateLimiter.Allow("contact", PageBuilder.ClientKey(Request), 5, TimeSpan.FromMinutes(10)))
            {
                model.Contact.RateLimited = true;
                return View("~/Views/Pages/Show.cshtml", model);
            }

            var id = InquiryRepository.SaveGeneralMessage(form.Type, form.Name.Trim(), Clean(form.Phone), Clean(form.Email), Clean(form.Subject),
                form.Message.Trim(), "/contact");
            Notifications.ContactMessage(id, form.Type, form.Name.Trim(), null);
            return Redirect("/contact?sent=1#contact");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Franchise(FranchiseForm form)
        {
            if (form == null) form = new FranchiseForm();
            if (!string.IsNullOrEmpty(form.Website)) return Redirect("/franchise/apply#apply");

            var model = PageBuilder.Build("/franchise/apply", Request);
            if (model == null || model.Franchise == null) return HttpNotFound();
            if (!model.Franchise.Accepting) return Redirect("/franchise/apply#apply");
            model.Franchise.Form = form;

            Action<string, string, string> min2 = (key, value, message) => { if (value != null && value.Trim().Length < 2) ModelState.AddModelError(key, message); };
            min2("FullName", form.FullName, "Please enter your full name (2 to 120 characters).");
            min2("City", form.City, "Please enter your city (2 to 80 characters).");
            min2("ProposedCity", form.ProposedCity, "Please enter the proposed city (2 to 80 characters).");
            min2("ProposedArea", form.ProposedArea, "Please enter the proposed area (2 to 120 characters).");
            if (!string.IsNullOrEmpty(form.BuildingPlan) && !FranchiseForm.BuildingPlans.Contains(form.BuildingPlan))
                ModelState.AddModelError("BuildingPlan", "Please choose an option from the list.");
            if (!string.IsNullOrWhiteSpace(form.StartTimeline) && model.Franchise.StartTimelines.Count > 0
                && !model.Franchise.StartTimelines.Contains(form.StartTimeline))
                ModelState.AddModelError("StartTimeline", "Please choose an option from the list.");
            if (!ModelState.IsValid)
            {
                model.FormErrors = ModelState;
                return View("~/Views/Pages/Show.cshtml", model);
            }
            if (!RateLimiter.Allow("franchise", PageBuilder.ClientKey(Request), 3, TimeSpan.FromMinutes(30)))
            {
                model.Franchise.RateLimited = true;
                return View("~/Views/Pages/Show.cshtml", model);
            }

            form.FullName = form.FullName.Trim(); form.Mobile = form.Mobile.Trim(); form.Email = form.Email.Trim();
            form.City = form.City.Trim(); form.ProposedCity = form.ProposedCity.Trim(); form.ProposedArea = form.ProposedArea.Trim();
            form.Whatsapp = Clean(form.Whatsapp); form.Province = Clean(form.Province); form.BuildingArea = Clean(form.BuildingArea);
            form.Experience = Clean(form.Experience); form.StartTimeline = Clean(form.StartTimeline); form.Message = Clean(form.Message);
            if (string.IsNullOrEmpty(form.BuildingPlan)) form.BuildingPlan = null;

            var saved = FranchiseRepository.Save(form, "/franchise/apply");
            Notifications.FranchiseApplication(saved.Item1, saved.Item2, form.FullName, form.ProposedCity);
            return Redirect("/franchise/apply?ref=" + saved.Item2 + "#apply");
        }

        private static string Clean(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
    }
}
