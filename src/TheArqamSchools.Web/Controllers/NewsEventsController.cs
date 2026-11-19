using System;
using System.Linq;
using System.Web.Mvc;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// /news-events: published news posts and events with filter buttons (All, Achievement,
    /// Event, Campus, Announcement), and /news-events/{slug} for one item. Nothing is shown
    /// until the school publishes posts in the admin panel; there is no sample content.
    /// </summary>
    public class NewsEventsController : Controller
    {
        public const int ListLimit = 60;

        public ActionResult Index(string type)
        {
            var filter = NewsRepository.FilterKeys.Contains(type) ? type : null;
            return View("~/Views/NewsEvents/Index.cshtml", new NewsListViewModel
            {
                // Every item is sent; the filter buttons hide and show them without reloading.
                Items = NewsRepository.Published(null, ListLimit),
                Filter = filter,
                Meta = new PageMeta
                {
                    Title = "News & Events | The Arqam Schools",
                    Description = "News, achievements, events and announcements from The Arqam Schools.",
                    CanonicalPath = "/news-events",
                }
            });
        }

        public ActionResult Detail(string slug)
        {
            slug = PageBuilder.Slug(slug);
            var item = slug == null ? null : NewsRepository.PublishedBySlug(slug);
            if (item == null) return HttpNotFound();
            ViewBag.Meta = new PageMeta
            {
                Title = item.Title + " | The Arqam Schools",
                Description = Helpers.SimpleFormat.Plain(item.Summary),
                CanonicalPath = item.Url,
            };
            ViewBag.Photos = PostImageRepository.List(item.Kind, item.Id);
            if (item.CoverUrl != null) ((PageMeta)ViewBag.Meta).ImageUrl = item.CoverUrl;
            return View("~/Views/NewsEvents/Detail.cshtml", item);
        }
    }
}
