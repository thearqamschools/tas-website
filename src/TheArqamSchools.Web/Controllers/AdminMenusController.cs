using System;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /admin/menus: the website menus (header, utility bar, footer). navigation.view to
    /// see them, navigation.create to add an item, navigation.update to edit or hide one.
    /// Items are hidden instead of deleted. Changes show on the website at once.
    /// </summary>
    [AdminAuthorize("navigation.view")]
    public class AdminMenusController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.CanCreate = AdminAuth.Current(HttpContext).Can("navigation.create");
            return View("~/Views/Admin/Menus/Index.cshtml", MenuAdminRepository.All());
        }

        [HttpGet]
        [AdminAuthorize("navigation.create")]
        public ActionResult New(Guid menuId)
        {
            var menu = MenuAdminRepository.Menu(menuId);
            if (menu == null) return HttpNotFound();
            var next = menu.Items.Count == 0 ? 10 : menu.Items.Max(i => i.SortOrder) + 10;
            return EditView(new MenuItemForm { MenuId = menuId, LinkType = "page", Target = "self", IsVisible = true, SortOrder = next }, menu, true, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("navigation.create")]
        [ActionName("New")]
        public ActionResult NewPost(Guid menuId, MenuItemForm form)
        {
            var menu = MenuAdminRepository.Menu(menuId);
            if (menu == null) return HttpNotFound();
            if (form == null) form = new MenuItemForm();
            form.MenuId = menuId;
            return Save(form, menu, true);
        }

        [HttpGet]
        public ActionResult Edit(Guid id, bool saved = false)
        {
            var form = MenuAdminRepository.GetItem(id);
            if (form == null) return HttpNotFound();
            form.LinkType = form.PageId.HasValue ? "page" : "url";
            return EditView(form, MenuAdminRepository.Menu(form.MenuId), false, saved ? "Saved. The website menu is updated." : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("navigation.update")]
        [ActionName("Edit")]
        public ActionResult EditPost(Guid id, MenuItemForm form)
        {
            var existing = MenuAdminRepository.GetItem(id);
            if (existing == null) return HttpNotFound();
            if (form == null) form = new MenuItemForm();
            form.Id = id;
            form.MenuId = existing.MenuId;
            return Save(form, MenuAdminRepository.Menu(existing.MenuId), false);
        }

        private ActionResult Save(MenuItemForm f, MenuAdminView menu, bool isNew)
        {
            f.Label = string.IsNullOrWhiteSpace(f.Label) ? null : f.Label.Trim();
            f.Description = string.IsNullOrWhiteSpace(f.Description) ? null : f.Description.Trim();
            f.Url = string.IsNullOrWhiteSpace(f.Url) ? null : f.Url.Trim();
            if (f.Target != "blank") f.Target = "self";

            if (f.LinkType == "url")
            {
                f.PageId = null;
                if (f.Url == null)
                    ModelState.AddModelError("Url", "Please enter the link.");
                else if (f.Url.Any(char.IsWhiteSpace) || !((f.Url.StartsWith("/") && !f.Url.StartsWith("//")) || f.Url.StartsWith("https://", StringComparison.Ordinal)))
                    ModelState.AddModelError("Url", "The link must start with / (a page on this website) or https://.");
            }
            else
            {
                f.LinkType = "page";
                f.Url = null;
                if (!f.PageId.HasValue || !MenuAdminRepository.Pages().Any(p => p.Id == f.PageId.Value))
                    ModelState.AddModelError("PageId", "Please choose a page.");
            }

            if (f.ParentId.HasValue)
            {
                var parent = menu.Items.FirstOrDefault(i => i.Id == f.ParentId.Value);
                if (parent == null || parent.Depth != 0 || parent.Id == f.Id)
                    ModelState.AddModelError("ParentId", "Please choose a main menu item from the list.");
                else if (!isNew && menu.Items.Any(i => i.ParentId == f.Id))
                    ModelState.AddModelError("ParentId", "This item has its own sub-items, so it must stay a main item.");
            }

            if (!ModelState.IsValid) return EditView(f, menu, isNew, null);
            var id = MenuAdminRepository.Save(f, isNew, AdminAuth.Current(HttpContext).Id);
            return Redirect("/admin/menus/items/" + id.ToString("D") + "?saved=true");
        }

        private ActionResult EditView(MenuItemForm form, MenuAdminView menu, bool isNew, string notice)
        {
            var actor = AdminAuth.Current(HttpContext);
            return View("~/Views/Admin/Menus/Edit.cshtml", new MenuItemEditViewModel
            {
                Form = form, Menu = menu, Pages = MenuAdminRepository.Pages(), IsNew = isNew, Notice = notice,
                CanEdit = isNew ? actor.Can("navigation.create") : actor.Can("navigation.update")
            });
        }
    }
}
