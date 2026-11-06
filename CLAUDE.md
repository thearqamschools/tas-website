# CLAUDE.md: The Arqam Schools website

Guidance for Claude Code when working in this repository. Facts here come from the
code on `main` (commit d0c49c4) and from `README.md`. When this file and the code
disagree, the code wins: tell the user and update this file.

## What this is

The public website of **The Arqam Schools**, rebuilt from Next.js + Supabase
(PostgreSQL) as:

- **ASP.NET MVC 5.3.0** on **.NET Framework 4.8**, C# **7.3**, classic (non-SDK) web project
- **SQL Server 2025**, compatibility level **170** (required for `REGEXP_LIKE` in CHECK constraints)
- Data access with **Dapper 2.1.35** over `System.Data.SqlClient`; JSON with **Newtonsoft.Json 13.0.3**
- Target hosting: **IIS on Windows Server**; local development in **Visual Studio 2022** / IIS Express (`https://localhost:44300`)
- Frontend: plain CSS (`Content/site.css`) and plain JS (`Scripts/site.js`); no npm, bundler or JS framework

Phase status (from README): phase 1 (database) and phase 2a (public site core) done;
2b (full homepage, contact/careers/franchise forms, Turnstile, email), 3 (admin panel)
and 4 (SEO extras, search, security review, IIS guide) not started.

## Layout

```
Database/                 00–05 SQL scripts, run in order; tools/ = Python generators (PostgreSQL → SQL Server)
src/TheArqamSchools.sln
src/TheArqamSchools.Web/
  App_Start/              FilterConfig, RouteConfig
  Controllers/            Home, Pages (catch-all CMS), Campuses, Admissions, Seo, Status
  Data/                   Db (connections), Cache (60 s MemoryCache), static Dapper repositories
  Infrastructure/         PageBuilder, LegacyUrls (301s in Application_BeginRequest), RateLimiter
  Helpers/                Site, Media, RichText, Icons, Contact
  Models/                 POCOs + view models; Forms.cs holds validated form models
  Views/                  _Layout, _Header, _Footer, Pages/Show, Shared/Sections/_<type>.cshtml, Campuses/Detail
  Content/ Scripts/ media/
tools/compile-check/      Mono C# compile check with stub MVC/Dapper types (does not compile views)
```

## How the site works

- Routes (`App_Start/RouteConfig.cs`): `/status`, `/sitemap.xml`, `/robots.txt`, `/`,
  `POST /admissions/inquiry`, `/campuses/{city}`, `/campuses/{city}/{slug}`,
  `POST /campuses/{city}/{slug}/message`, then a catch-all `{*path}` → `PagesController.Show`.
- CMS pages live in `dbo.pages` + `dbo.page_sections`. Each section has a `type` and JSON `data`;
  `Views/Pages/Show.cshtml` renders known types with `Views/Shared/Sections/_<type>.cshtml` and
  silently skips unknown types. A new section type needs both the partial and an entry in the
  `known` set in `Show.cshtml`.
- Menus come from `navigation_items`; an item shows a dropdown only when it has visible children.
- Site-wide settings are JSON values in `dbo.site_settings` (`is_public = 1`), read through
  `SiteSettingsRepository`.
- Redirects: `LegacyUrls.Handle` applies `dbo.redirects`, maps `/branches/*` → `/campuses/*`
  and strips trailing slashes, for GET/HEAD only.
- Errors: there is no global `HandleErrorAttribute`; `Application_Error` (Global.asax.cs) writes the error page in
  place with the right status (400 validation/anti-forgery, 404 → NotFound, 503 + Retry-After when the database is
  unavailable, else 500 → stand-alone `Error.cshtml`, which never touches the database), no-store, no redirect.
  `tools/release-check/` holds the Windows runtime checks for this (run in the Windows build workflow).
- Database outages: `Db.Open` pauses new connections for 10 s after a failure; menus, settings, footer offices,
  redirects and the homepage finder use `Cache.GetOrLastGood` (last good copy, else empty), so the homepage still
  renders. CMS pages need the database and show the error page.
- Images are served from `media/public-media/...` at `/media/public-media/...`. Build media
  URLs with `Helpers/Media.Url`.

## Coding conventions

- **C# 7.3 and .NET Framework 4.8 only.** No .NET Core/.NET 5+ APIs, no newer C# syntax
  (no records, `init`, nullable reference types, switch expressions, file-scoped namespaces).
- Match the existing style: static repositories in `Data/`, one `using (var db = Db.Open())`
  per query, `/// <summary>` comments in plain English explaining *why*.
- **SQL always through Dapper with parameters.** Never concatenate user input into SQL.
  Use `EscapeLike` (see `CampusRepository`) for LIKE searches.
- Database names are `snake_case`; C# properties are `PascalCase`
  (`DefaultTypeMap.MatchNamesWithUnderscores = true`).
- Every public form: `[HttpPost]`, `[ValidateAntiForgeryToken]`, `@Html.AntiForgeryToken()`,
  the hidden `Website` honeypot, `RateLimiter.Allow(...)`, server-side validation that matches
  the database CHECK constraints (see `Models/Forms.cs`), then redirect after a successful post.
- Output is HTML-encoded by Razor. Use `Html.Raw` only for content already made safe
  (`RichText.Render`, JSON-LD with `</` escaped). Pass CMS links through `Site.IsSafeHref`.
- Read query strings that may contain `<` through `Request.Unvalidated` and clean them
  (`PageBuilder.Clean` / `PageBuilder.Slug`); do not turn off request validation globally.
- New `.cs`, `.cshtml` and content files must be added to `TheArqamSchools.Web.csproj`
  (classic project format; files are not picked up automatically, except `media\**`).
- Keep accessibility as built: skip link, labelled controls, `aria-*` on menus and errors,
  keyboard support, no horizontal scrolling at 390 / 768 / 1024 / 1440 px.
- Packages use `packages.config` restored into `src/packages/` (git-ignored). Do not add or
  upgrade packages without the user's approval.

## Database rules

- Scripts in `Database/` are numbered and run in order by hand (SSMS). `00`–`03` are the base
  install; `04` and `05` are change scripts.
- **New database changes go in a new numbered script** (`06_...sql` and so on). Never edit
  `00`–`05` after they have been run.
- Follow the pattern of `04` and `05`: guard with `dbo.schema_migrations`
  (`SET NOEXEC ON` when already applied), back up changed rows into the `archive` schema with
  a dated suffix (`*_YYYYMMDD`), do the work in one transaction with `SET XACT_ABORT ON`,
  insert the `schema_migrations` row, and make the script safe to run twice.
- Keep the existing CHECK constraints (`REGEXP_LIKE`, `ISJSON`) and soft-delete columns
  (`deleted_at`, `archived_at`). Prefer hiding (`is_visible = 0`, `status = 'archived'`) over deleting.
- Logic that PostgreSQL did in the database (permissions, audit log, status history,
  reference numbers from the two sequences, `updated_at` / `updated_by`) must be done in
  application code. None of it exists yet; build it before the admin panel writes data.

## Branding and content rules

- **Approved content source:** `docs/content/TAS-Website-Content.md` (v1.4, 6 November 2026) is the single
  approved source for website text. Use only what it says; mark anything missing as `[TO BE PROVIDED]`.
- The name is always **The Arqam Schools**. Never "The Arqam School", "Arqam School",
  "Dar-e-Arqam" or "Darul Arqam" (Dar-e-Arqam Schools is a different organisation).
- Motto / only tagline: **فِي الدُّنْيَا حَسَنَةً وَفِي الْآخِرَةِ حَسَنَةً** — "Excellence in this world and excellence in the Hereafter".
  "Learn Today, Lead Tomorrow" is not ours: never use it. "Knowledge with values. Education with purpose." is a
  values statement for the Core Values section only.
- Key figures: **350+ branches, 250,000+ students, 20,000+ Hifz-e-Quran students**; founded in Faisalabad in 1993.
  Do not invent or change figures, fees, dates, results, addresses, programmes or phone numbers.
  Do not advertise transport or smart classrooms; do not publish fees or board results.
- Official contacts: Head Office, Lahore: 163-Tufail Block, Canal Bank Housing Scheme, Lahore 54000; 042-36520008;
  mobile 0331-1304001; https://www.facebook.com/TASHeadOffice. Head Office, Faisalabad: Sanora Colony, Samanabad,
  Faisalabad; 041-2660265, 041-2406479; mobile/WhatsApp 0339-2406479; https://www.facebook.com/thearqamschoolsfsd.
  Email thearqamschools@gmail.com; www.thearqam.com. Never use the old Lahore number 042-36520235.
- Colours: Arqam Red #EC3237, Arqam Blue #3E4095, Seal Navy #2B3990, Deep Red #D62839 (buttons), Ink #1B1D45,
  Muted #545675, Blue Tint #EEF0F9, Red Tint #FDECED, Line #D9DBEC. Fonts: Helvetica Inserat (upper case) for the
  name and big headlines, Poppins for everything else, Scheherazade New for the Arabic motto.
- Tone: professional, warm, clear; modern education with Islamic values. No guaranteed
  results or exaggerated claims. If a fact is missing, mark it as needing confirmation.
- Languages: English and Urdu only (Roman Urdu for chat). Never Hindi or Devanagari script.
- Do not redesign, recolour or distort the official logo
  (`Content/brand/the-arqam-schools-logo-official.png`).
- Images whose alt text starts with "Placeholder" are stand-ins until the school sends real photos.

## Safety constraints

- **Never deploy, publish to IIS, or run scripts against a live/production database** without
  the user's explicit written approval for that specific action, and a fresh backup first.
- Do not commit secrets. The real connection string and keys are set on the server only
  (IIS Manager or Release transforms). Do not change the `ArqamDb` connection string,
  `Web.Release.config`, the `arqam_web` login, or deployment settings unless the user asks.
- Do not push to or merge into `main` directly. Work on a branch and open a pull request;
  push only when the user asks.
- Do not delete content, pages or data rows; archive or hide them.
- Do not change existing database schema scripts; add a new numbered script instead.
- Do not weaken security features (anti-forgery, honeypot, rate limit, safe-link checks,
  security headers in `Web.config`, request filtering of `private`).
- **Never use real student, parent, staff or production data for testing**, and never copy
  production data into a development or test database. Use synthetic test data only
  (made-up names, `0300-1234567`-style numbers, `example.com` emails), and remove it after testing.

## Development workflow

1. Start from the latest `main` on a new branch (`claude/<short-topic>`).
2. Make one focused change per branch / pull request.
3. Database: use a **local or disposable** SQL Server 2025 instance only (Express or Docker is
   enough), never a live or shared production server. Run `Database/00`–`05` in order (set a
   local-only password for `arqam_web` in `00` first; never reuse or commit production
   credentials), then open `/status` locally: it should show Connected, compatibility level 170,
   REGEXP_LIKE Yes, and row counts.
4. Build on Windows: open `src/TheArqamSchools.sln` in Visual Studio 2022 (ASP.NET workload +
   .NET Framework 4.8 targeting pack), Restore NuGet Packages, F5.
5. On Linux (cloud sessions): `tools/compile-check/check.sh` checks that the C# compiles with
   Mono; it does not compile Razor views. Phase 2a was also run end to end with SQL Server 2025
   in Docker and Mono 6.8 (fastcgi-mono-server4 behind nginx); those test scripts are not in
   the repository.
6. After every change, before asking for review:
   - run the tests relevant to the change (compile check, changed pages and forms by hand or
     with Playwright, new SQL scripts run twice on a local database);
   - check MVC 5.3 / .NET Framework 4.8 / C# 7.3 compatibility (no newer APIs, syntax or packages);
   - check security: parameterised SQL, encoded output, anti-forgery and validation on forms,
     safe links, no secrets or real personal data in the diff;
   - confirm brand text and figures.
7. Report results honestly. List every test that could not run and why, especially the
   Windows-only checks: Visual Studio build, Razor view compilation and running under IIS.
   Never describe untested work as tested.
8. Describe in the pull request what changed, which scripts must be run, and what was tested.

## Working with several agents

- Give each agent one clear responsibility and a named set of files before it starts.
- Do not let two agents edit the same file at the same time; finish and hand over one change
  before another agent touches that file.
- Database changes (new scripts in `Database/`, or code that writes data) need a separate
  review by an agent or person who did not write them, before they are merged.
- The final summary to the user says which agent changed what, and what each one tested.

## Admin panel and deployment (added 31 October 2026)

- Admin panel under `/admin`: see `docs/ADMIN-PANEL.md`. Controllers `Admin*Controller.cs`, views in
  `Views/Admin/`, styles in `Content/admin.css`, sign-in code in `Infrastructure/Auth/`. Guard every
  admin action with `[AdminAuthorize("permission.key")]`, add `[ValidateAntiForgeryToken]` to every
  POST, and write `AuditLog.Write` in the same transaction as the change.
- Testing deployment to SmarterASP.NET: `docs/release/SMARTERASP-DEPLOY.md`. Uploads happen only
  from branch `deploy/smarterasp`, with the owner's approval.
- `Site:NoIndex = true` keeps the testing site out of search engines; set it to false only at launch.

## Known gaps (from the October 2026 audit)

- No automated tests or CI in the repository.
- Never built in Visual Studio or run under IIS yet (`MvcBuildViews` is `false`).
- Form notification emails and the admin inbox exist but have not yet run against a real mail server or database.
- Rate limits trust `CF-Connecting-IP` only from Cloudflare's published ranges (`ClientAddress.cs`); update the list if Cloudflare changes it.
- Only one campus (Samanabad) in the seed data; `/privacy` is a draft.
- Franchise, contact and careers forms and news/events/gallery/downloads lists are not built.
- README says HTTPS is enforced only when `Site:RequireHttps` is `true`; the code enforces it
  unless the setting is `false`.
