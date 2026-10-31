---
name: aspnet-mvc-standards
description: Coding standards for The Arqam Schools ASP.NET MVC 5.3 / .NET Framework 4.8 / C# 7.3 website - controllers, Razor views, Dapper repositories, forms, routes, csproj entries and numbered SQL scripts. Use whenever writing or reviewing C#, Razor or SQL in this repository.
---

# ASP.NET MVC 5.3 standards for this repository

Grounded in `CLAUDE.md` and the current code. `CLAUDE.md` wins if anything here disagrees.

## Platform limits

- .NET Framework **4.8**, ASP.NET MVC **5.3.0**, Razor **3.3.0**, C# **7.3** (`<LangVersion>7.3</LangVersion>`).
- Not allowed: .NET Core / .NET 5+ APIs, `Microsoft.Extensions.*`, records, `init`, nullable
  reference types, switch expressions, `using` declarations, file-scoped namespaces,
  default interface members, ranges/indices (`^1`, `..`).
- Allowed: tuples `(string Key, string Value)` (as in `SiteSettingsRepository`), expression-bodied
  members, `out var`, pattern matching with `is`.
- Packages come from `packages.config` (Dapper 2.1.35, Mvc 5.3.0, Newtonsoft.Json 13.0.3, ...).
  Do not add or upgrade packages without the user's approval.

## Structure and patterns

- **Controllers** (`Controllers/`): thin. Build view models with `PageBuilder` or repositories,
  return views by explicit path where the existing code does (`"~/Views/Pages/Show.cshtml"`).
  404s set `Response.StatusCode = 404` and `TrySkipIisCustomErrors = true` and return
  `~/Views/Shared/NotFound.cshtml`.
- **Routes** (`App_Start/RouteConfig.cs`): specific routes go **before** the catch-all `Page`
  route `{*path}`. POST-only routes use `HttpMethodConstraint("POST")`. URLs are lowercase,
  no trailing slash.
- **Data** (`Data/`): static repository classes; each method opens `using (var db = Db.Open())`
  and runs one Dapper call with an anonymous parameter object. Snake_case columns map to
  PascalCase properties automatically. Cache shared, rarely changing data with
  `Cache.Get(key, loader)` (60 seconds).
- **CMS sections**: new section type = new partial `Views/Shared/Sections/_<type>.cshtml` + add
  the type to the `known` set in `Views/Pages/Show.cshtml`. Section JSON is read through
  `PageSection.Text`, `Media.Url`, `RichText.Render`.
- **Models**: POCOs in `Models/`; form models in `Models/Forms.cs` with DataAnnotations that match
  the database CHECK constraints (same regex, same lengths).
- **csproj**: add every new `.cs` as `<Compile Include>` and every new `.cshtml`, `.css`, `.js`
  as `<Content Include>` in `TheArqamSchools.Web.csproj`. Missing entries break the Windows build.

## Security checklist (every change)

- SQL only through Dapper parameters. LIKE searches use `EscapeLike`. Never build SQL from input.
- Razor encodes output. `Html.Raw` only for `RichText.Render` output or JSON-LD with `</` escaped.
- CMS links through `Site.IsSafeHref`; media URLs through `Media.Url`.
- Query strings that may contain `<`: read with `Request.Unvalidated`, clean with
  `PageBuilder.Clean` / `PageBuilder.Slug`, mark the action `[ValidateInput(false)]` only then.
- Public forms: `[HttpPost]`, `[ValidateAntiForgeryToken]`, `@Html.AntiForgeryToken()`, hidden
  `Website` honeypot, `RateLimiter.Allow(bucket, PageBuilder.ClientKey(Request), 5, 10 min)`,
  server-side validation, **trim before validating length**, redirect after success (PRG).
- Do not touch `Web.config` security headers, request filtering of `private`, connection
  strings or `Web.Release.config` unless the user asked.

## Database scripts

- Never edit `Database/00`–`05`. New changes go in the next numbered file (`06_<topic>.sql`).
- Copy the pattern of `04_simplify_navigation.sql` / `05_franchise_header_button.sql`:
  `SET NOCOUNT ON; SET XACT_ABORT ON;` → guard on `dbo.schema_migrations` with `SET NOEXEC ON`
  → backups `SELECT * INTO archive.<table>_YYYYMMDD` → `BEGIN TRANSACTION` → changes →
  `INSERT INTO dbo.schema_migrations (id)` → `COMMIT` → `SET NOEXEC OFF`.
- Keep `REGEXP_LIKE` / `ISJSON` checks; soft-delete or hide instead of deleting.
- Database changes need a separate review before merging.

## Before handing over

- Run `tools/compile-check/check.sh` when Mono is available (C# only; it does not compile views).
- State clearly what was not checked: Visual Studio build, Razor view compilation (`MvcBuildViews`
  is off), IIS.
