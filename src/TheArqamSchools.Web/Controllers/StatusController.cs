using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Dapper;
using TheArqamSchools.Web.Data;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Controllers
{
    /// <summary>
    /// Developer check after installation: is the database reachable, is it
    /// SQL Server 2025 at compatibility level 170, and was the content loaded?
    /// Only answers requests from the server itself.
    /// </summary>
    public class StatusController : Controller
    {
        private static readonly string[] Tables =
        {
            "roles", "permissions", "role_permissions", "pages", "page_sections", "homepage_content",
            "navigation_items", "site_settings", "media", "branches", "head_offices", "admission_stages", "users"
        };

        public ActionResult Index()
        {
            if (!Request.IsLocal)
                return HttpNotFound();

            var model = new StatusViewModel();
            try
            {
                using (var db = Db.Open())
                {
                    model.DatabaseConnected = true;
                    model.ServerVersion = db.ExecuteScalar<string>("SELECT CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(50)) + N' ' + CAST(SERVERPROPERTY('Edition') AS NVARCHAR(100))");
                    model.CompatibilityLevel = db.ExecuteScalar<int>("SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()");
                    model.FullTextInstalled = db.ExecuteScalar<int>("SELECT CAST(FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') AS INT)") == 1;
                    try
                    {
                        model.RegexSupported = db.ExecuteScalar<int>("SELECT CASE WHEN REGEXP_LIKE(N'abc-1', N'^[a-z]+-[0-9]$') THEN 1 ELSE 0 END") == 1;
                    }
                    catch (Exception)
                    {
                        model.RegexSupported = false;
                    }
                    foreach (var t in Tables)
                        model.Counts.Add(new KeyValuePair<string, int>(t, db.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.[" + t + "]")));
                }
            }
            catch (Exception ex)
            {
                model.DatabaseConnected = false;
                model.DatabaseError = ex.Message;
            }
            return View(model);
        }
    }
}
