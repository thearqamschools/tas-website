# The Arqam Schools website: ASP.NET MVC 5 + SQL Server 2025

The new version of the website, moved from Next.js + Supabase (PostgreSQL) to:

- **ASP.NET MVC 5** on **.NET Framework 4.8**, hosted on **Windows Server / IIS**;
- **SQL Server 2025**.

The previous version (`arqam-web`) is unchanged and remains the reference for every page and feature.

## Progress

| Phase | Contents | Status |
|---|---|---|
| 1 | SQL Server 2025 database (all 51 tables, keys, checks, indexes), current content and roles, images, Visual Studio solution, status page | **Done**, scripts run on SQL Server 2025 |
| 2a | Public website: design, header/footer, simplified navigation, CMS pages, About Us / Academics / Admissions / Campuses consolidated, campus directory and detail pages, admission and campus inquiry forms, redirects, sitemap | **Done**, tested (179/179 checks) |
| 2b | Homepage in full, contact, careers and franchise forms, Turnstile, email notifications | Next |
| 3 | Admin panel: sign-in with two-factor, roles and permissions, pages/homepage/menus editors, media library, branches, head offices, franchise applications, CSV export | Planned |
| 4 | SEO (sitemaps, redirects, sharing images), search, email, security review, IIS deployment guide | Planned |

## Folder layout

```
Database/
  00_create_database.sql   creates [TheArqamSchools], compatibility level 170, the website login
  01_schema.sql            51 tables, primary/unique keys, checks, indexes, foreign keys, sequences
  02_seed_data.sql         content and reference data (no applications, messages or user accounts)
  03_fulltext.sql          optional site search (Full-Text Search) and the audit-log index
  04_simplify_navigation.sql  phase 2a content change: backs up to archive.*_20261024, simplifies the menu,
                           consolidates the four main pages, archives old subpages, adds 301 redirects
  05_franchise_header_button.sql  red header button becomes "Franchise"; Franchise hidden from the main menu
  tools/convert.py         generates 01_schema.sql from the PostgreSQL schema
  tools/export_data.py     generates 02_seed_data.sql from the current content
src/
  TheArqamSchools.sln
  TheArqamSchools.Web/     ASP.NET MVC 5 project (.NET Framework 4.8)
    media/public-media/    the 25 website images (served at /media/public-media/...)
tools/compile-check/       C# compile check on Linux (Mono + stand-ins for MVC and Dapper)
```

## Install on Windows (developer machine or server)

1. **SQL Server 2025** (any edition; Express works). Install "Full-Text and Semantic Extractions for Search" too if you want site search.
2. In **SQL Server Management Studio**, run in order:
   1. `Database/00_create_database.sql`. Change the `arqam_web` password in it first.
   2. `Database/01_schema.sql`, connected to `TheArqamSchools`.
   3. `Database/02_seed_data.sql`.
   4. `Database/03_fulltext.sql` (optional).
   5. `Database/04_simplify_navigation.sql`. Safe to run twice; the second run prints "already applied".
   6. `Database/05_franchise_header_button.sql`. Also safe to run twice.
3. Open `src/TheArqamSchools.sln` in **Visual Studio 2022**. Make sure the "ASP.NET and web development" workload and the **.NET Framework 4.8 targeting pack** are installed.
4. Right-click the solution, then **Restore NuGet Packages**. If any reference still shows a warning, run `Update-Package -reinstall` in the Package Manager Console.
5. In `Web.config`, set the `ArqamDb` connection string. The default uses Windows sign-in to `localhost`.
6. Press **F5**, then open `https://localhost:44300/status`. It should show:
   - **Connected**;
   - compatibility level **170**;
   - REGEXP_LIKE **Yes**;
   - the row counts: 9 roles, 104 permissions, 23 pages, 14 homepage sections, 2 head offices.

## How the move to SQL Server was done

- **Same data model:** the same tables and column names as before, so every feature maps one-to-one.
- **Type mapping:**

  | PostgreSQL | SQL Server |
  |---|---|
  | uuid | UNIQUEIDENTIFIER |
  | timestamptz | DATETIMEOFFSET(3) |
  | boolean | BIT |
  | jsonb, text[] | NVARCHAR(MAX) with ISJSON checks |
  | enums | NVARCHAR with CHECK (… IN …) |
  | text | NVARCHAR(n), sized from the old length rules |

- **SQL Server 2025 features used:**
  - `REGEXP_LIKE` keeps the old format rules (slugs, phone numbers, emails, paths) inside the database;
  - `ISJSON(x, OBJECT)` checks JSON content;
  - filtered indexes replace PostgreSQL partial indexes.
- **Moved into the application,** because SQL Server works differently:
  - permission checks, which were PostgreSQL row level security;
  - the audit log;
  - status history;
  - reference numbers, from the two SQL Server sequences;
  - updated_at and updated_by;
  - "file in use" protection;
  - clearing links when a row is deleted.

  These are rebuilt in phases 2–3 and tested there.
- **Sign-in:** previously handled by Supabase Auth, now stored in `dbo.users` (password hash, two-factor key, lockout). The first Super Admin is created by a one-time setup page in phase 3.
- **Not exported:** the local test accounts, applications and messages.

## Phase 2a changes

- **Navigation:** About Us, Academics, Admissions and Campuses are direct links (no dropdown) on desktop and mobile. Student Life and Parents keep dropdowns. An item gets a dropdown only when it has visible children in `navigation_items`.
- **Pages:** About (intro, Vision & Mission, Philosophy, Why Choose, Islamic Values), Academics (overview, Excellence, Teaching, Levels, Student Development), Admissions (hero, Why Choose, Process, Eligibility & Requirements, Levels, inquiry form, FAQ), Campuses (hero, search and city/district filters, cards, inquiry). All content stays in `pages` / `page_sections`.
- **New section types:** `vision_mission`, `academic_levels`, `info_list`, `admission_inquiry`, `campus_directory`.
- **Wording and figures:** "branches" became "campuses"; 350+ campuses, 250,000+ students, 20,000+ Hafiz-e-Quran.
- **301 redirects** (table `dbo.redirects`): `/about/vision-mission`, `/about/philosophy`, `/about/values`, `/about/why-choose-us` → anchors on `/about`; `/academics/levels`, `/academics/teaching` → anchors on `/academics`; `/branches/*` → `/campuses/*`. Functional routes are not redirected.
- **Forms:** `POST /admissions/inquiry` saves to `admission_inquiries`; `POST /campuses/{city}/{slug}/message` saves admissions messages to `admission_inquiries` and the rest to `contact_messages`. Anti-forgery token, honeypot, rate limit 5 per 10 minutes per IP, server-side validation.
- **SEO:** titles, descriptions, canonical tags, Open Graph, JSON-LD (School on campus pages), `/sitemap.xml` without archived pages, `/robots.txt`.
- **Phase 1 fixes:** `NVARCHAR(5000)` became `NVARCHAR(MAX)` (SQL Server's limit is 4000); JSON checks use `ISJSON(col, VALUE)` so scalar JSON values are accepted.
- **Header button (28 October 2026):** the red header button (setting `header.cta_apply`) says "Franchise" and links to `/franchise`. The Franchise menu item is hidden, not deleted. Admissions stays in the main menu.
- **HTTPS:** `RequireHttps` applies only when the appSetting `Site:RequireHttps` is `true`.

## Testing

Phase 2a was run end to end on Linux: SQL Server 2025 in Docker with all five scripts, and the site on Mono 6.8 (fastcgi-mono-server4 behind nginx) compiled against the real NuGet packages. 179 automated checks passed (HTTP plus Playwright in Chromium): status codes, every redirect, navigation on desktop and mobile, keyboard use, no sideways scrolling at 390/768/1024/1440 px, images and links, campus search with hostile input, and both forms (validation, honeypot, anti-forgery, saved rows).

**Still to check on Windows:** building in Visual Studio 2022 and running under IIS. Mono differs from .NET Framework in small ways (for example it ignores the `sameSite` cookie attribute), so report any error you see there.
