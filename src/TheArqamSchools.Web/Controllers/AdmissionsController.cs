using System;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>The admission inquiry form on /admissions. The page itself is a CMS page.</summary>
    public class AdmissionsController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Inquiry(AdmissionInquiryForm form)
        {
            if (form == null) form = new AdmissionInquiryForm();
            if (!string.IsNullOrEmpty(form.Website))
                return Redirect("/admissions?sent=1#inquiry");

            var model = PageBuilder.Build("/admissions", Request);
            if (model == null || model.Inquiry == null) return HttpNotFound();
            model.Inquiry.Form = form;
            model.Inquiry.Sent = false;

            if (form.BranchId.HasValue && !model.Inquiry.Campuses.Any(c => c.Id == form.BranchId.Value))
                ModelState.AddModelError("BranchId", "Please choose a campus from the list.");
            if (form.ClassLevelId.HasValue && !model.Inquiry.Classes.Any(c => c.Id == form.ClassLevelId.Value))
                ModelState.AddModelError("ClassLevelId", "Please choose a class from the list.");

            if (!ModelState.IsValid)
            {
                model.FormErrors = ModelState;
                return View("~/Views/Pages/Show.cshtml", model);
            }

            if (!RateLimiter.Allow("admission-inquiry", PageBuilder.ClientKey(Request), 5, TimeSpan.FromMinutes(10)))
            {
                model.Inquiry.RateLimited = true;
                return View("~/Views/Pages/Show.cshtml", model);
            }

            InquiryRepository.SaveAdmissionInquiry(form.BranchId, form.ClassLevelId, form.Name.Trim(), form.Phone.Trim(),
                string.IsNullOrWhiteSpace(form.Email) ? null : form.Email.Trim(),
                string.IsNullOrWhiteSpace(form.Message) ? null : form.Message.Trim(), "/admissions");
            return Redirect("/admissions?sent=1#inquiry");
        }
    }
}
