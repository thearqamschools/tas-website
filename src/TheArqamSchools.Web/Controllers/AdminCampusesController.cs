using System;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/campuses: list, add and edit campuses. branches.view to see the list,
    /// branches.create to add, branches.update to edit, branches.publish to show or hide a
    /// campus on the website, branches.delete to archive one. Changes appear on the
    /// website within a minute (the public pages cache campus data for 60 seconds).
    /// </summary>
    [AdminAuthorize("branches.view")]
    public class AdminCampusesController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            var q = Request.Unvalidated.QueryString["q"];
            ViewBag.Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            ViewBag.CanCreate = AdminAuth.Current(HttpContext).Can("branches.create");
            return View("~/Views/Admin/Campuses/Index.cshtml", CampusAdminRepository.List(q));
        }

        [HttpGet]
        [AdminAuthorize("branches.create")]
        public ActionResult New()
        {
            return EditView(new CampusEditForm { Status = "coming_soon", AdmissionStatus = "closed" }, true, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("branches.create")]
        [ActionName("New")]
        public ActionResult NewPost(CampusEditForm form)
        {
            return Save(form ?? new CampusEditForm(), true);
        }

        [HttpGet]
        public ActionResult Edit(Guid id, string notice)
        {
            var form = CampusAdminRepository.Get(id);
            if (form == null) return HttpNotFound();
            return EditView(form, false, notice == "saved" ? "Changes saved. The website shows them within a minute." :
                notice == "archived" ? "The campus is archived and hidden from the website." :
                notice == "restored" ? "The campus is restored." : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("branches.update")]
        [ActionName("Edit")]
        public ActionResult EditPost(Guid id, CampusEditForm form)
        {
            if (CampusAdminRepository.Get(id) == null) return HttpNotFound();
            if (form == null) form = new CampusEditForm();
            form.Id = id;
            return Save(form, false);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("branches.delete")]
        public ActionResult Archive(Guid id, bool archived)
        {
            if (CampusAdminRepository.Get(id) == null) return HttpNotFound();
            CampusAdminRepository.SetArchived(id, archived, AdminAuth.Current(HttpContext).Id);
            ClearPublicCache();
            return Redirect("/admin/campuses/" + id.ToString("D") + "?notice=" + (archived ? "archived" : "restored"));
        }

        private ActionResult Save(CampusEditForm form, bool isNew)
        {
            var actor = AdminAuth.Current(HttpContext);
            // An empty city choice ("Choose a city" with a new city typed instead) is not a binding error;
            // the city rules are checked below.
            ModelState.Remove("CityId");
            Clean(form);
            if (string.IsNullOrEmpty(form.Slug) && !string.IsNullOrEmpty(form.Name))
            {
                form.Slug = CampusAdminRepository.Slugify(form.Name);
                ModelState.Remove("Slug");
            }
            if (!CampusAdminRepository.Statuses.Contains(form.Status))
                ModelState.AddModelError("Status", "Please choose a status.");
            if (!CampusAdminRepository.AdmissionStatuses.Contains(form.AdmissionStatus))
                ModelState.AddModelError("AdmissionStatus", "Please choose an admission status.");

            var cities = CampusAdminRepository.Cities();
            if (form.CityId == Guid.Empty && string.IsNullOrEmpty(form.NewCityName))
                ModelState.AddModelError("CityId", "Please choose a city, or type a new city name.");
            else if (form.CityId != Guid.Empty && !cities.Any(c => c.Id == form.CityId))
                ModelState.AddModelError("CityId", "Please choose a city from the list.");
            if (!string.IsNullOrEmpty(form.NewCityName) && CampusAdminRepository.Slugify(form.NewCityName).Length == 0)
                ModelState.AddModelError("NewCityName", "Please type the city name in English letters.");
            if (string.IsNullOrEmpty(form.Slug) && ModelState.IsValidField("Name"))
                ModelState.AddModelError("Slug", "Please type a web address using English letters, for example model-town.");

            if (!ModelState.IsValid) return EditView(form, isNew, null);

            if (!string.IsNullOrEmpty(form.NewCityName))
                form.CityId = CampusAdminRepository.FindOrCreateCity(form.NewCityName, actor.Id);
            var conflict = CampusAdminRepository.Conflict(form);
            if (conflict != null)
            {
                ModelState.AddModelError("Slug", conflict);
                return EditView(form, isNew, null);
            }

            var id = CampusAdminRepository.Save(form, isNew, actor.Can("branches.publish"), actor.Id);
            ClearPublicCache();
            return Redirect("/admin/campuses/" + id.ToString("D") + "?notice=saved");
        }

        private static void Clean(CampusEditForm f)
        {
            Func<string, string> t = v => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            f.Name = t(f.Name); f.Slug = t(f.Slug); f.Code = t(f.Code); f.Area = t(f.Area); f.AddressLine = t(f.AddressLine);
            f.Phone = t(f.Phone); f.PhoneAlt = t(f.PhoneAlt); f.Whatsapp = t(f.Whatsapp); f.Email = t(f.Email);
            f.Timings = t(f.Timings); f.GoogleMapsUrl = t(f.GoogleMapsUrl); f.Overview = t(f.Overview);
            f.PrincipalName = t(f.PrincipalName); f.PrincipalTitle = t(f.PrincipalTitle); f.PrincipalMessage = t(f.PrincipalMessage);
            f.AdmissionNote = t(f.AdmissionNote); f.NewCityName = t(f.NewCityName);
            if (f.Slug != null) f.Slug = f.Slug.ToLowerInvariant();
            if (f.Code != null) f.Code = f.Code.ToUpperInvariant();
        }

        /// <summary>The homepage campus finder is cached for 60 seconds; clear it so a change shows at once.</summary>
        private static void ClearPublicCache()
        {
            Cache.Remove("home:cities");
            Cache.Remove("home:campuses");
        }

        private ActionResult EditView(CampusEditForm form, bool isNew, string notice)
        {
            var actor = AdminAuth.Current(HttpContext);
            string publicUrl = null;
            if (!isNew && form.IsPublished && !form.IsArchived)
            {
                var city = CampusAdminRepository.Cities().FirstOrDefault(c => c.Id == form.CityId);
                if (city != null) publicUrl = "/campuses/" + city.Slug + "/" + form.Slug;
            }
            return View("~/Views/Admin/Campuses/Edit.cshtml", new CampusEditViewModel
            {
                Form = form,
                Cities = CampusAdminRepository.Cities(),
                IsNew = isNew,
                CanEdit = isNew ? actor.Can("branches.create") : actor.Can("branches.update"),
                CanPublish = actor.Can("branches.publish"),
                CanArchive = !isNew && actor.Can("branches.delete"),
                Notice = notice,
                PublicUrl = publicUrl
            });
        }
    }
}
