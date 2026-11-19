using System.Web.Mvc;
using Dapper;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Infrastructure.Auth;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>The admin panel start page (/admin) and the 404 for unknown /admin addresses.</summary>
    [AdminAuthorize]
    public class AdminDashboardController : Controller
    {
        public ActionResult Index()
        {
            var user = AdminAuth.Current(HttpContext);
            var model = new AdminDashboardViewModel { User = user };
            if (user.Can("inquiries.view"))
            {
                using (var db = Db.Open())
                {
                    model.NewAdmissionInquiries = db.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM dbo.admission_inquiries WHERE status = N'new' AND deleted_at IS NULL");
                    model.NewContactMessages = db.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM dbo.contact_messages WHERE status = N'new' AND deleted_at IS NULL");
                }
            }
            return View("~/Views/Admin/Dashboard/Index.cshtml", model);
        }

        /// <summary>Unknown /admin/... addresses: signed-in users get a 404; others are sent to sign in first.</summary>
        public ActionResult NotFound()
        {
            return HttpNotFound();
        }
    }
}
