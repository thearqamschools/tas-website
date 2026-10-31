# Handover brief for Claude Code: The Arqam Schools website

Written 31 October 2026. Read this first, then `CLAUDE.md` (rules), then `docs/release/READINESS.md` and `docs/release/STAGING-RUNBOOK.md`.

## Update, 31 October 2026 (evening): read this part first

- **Latest code is now on branch `claude/urdu-roman-english-kiukjf`.** It contains everything
  from `claude/phase10-design` (merged in) plus the work below. Nothing is merged into `main`.
- **Hosting chosen by the owner:** SmarterASP.NET shared Windows hosting (60-day trial, MSSQL 2025
  available) for a **testing** site at `https://thearqamschools.com` (domain on Cloudflare). The site
  stays hidden from search engines (`Site:NoIndex`). Steps: `docs/release/SMARTERASP-DEPLOY.md`.
  Not deployed yet: the owner has the panel access on Monday.
- **New since the section below was written:**
  - `Site:NoIndex` testing mode (robots.txt `Disallow: /`, noindex meta tag and header);
  - deploy workflow `.github/workflows/deploy-smarterasp.yml` (uploads only from branch
    `deploy/smarterasp`; dry run on the development branch), `tools/deploy/Publish-Ftp.ps1`,
    `Database/hosting/smarterasp_setup.sql`;
  - rate limits use the real visitor address behind Cloudflare (`Infrastructure/ClientAddress.cs`);
  - **admin panel** (see `docs/ADMIN-PANEL.md`): sign-in with required two-step verification,
    first Super Admin setup, dashboard, inquiry and message inbox with CSV export, pages and
    section editing with drafts, campuses, menus, settings, users and roles, audit log;
  - email to the school when a form is sent (`Infrastructure/Notifications.cs`, off until mail
    settings exist).
- **Testing status:** every push passes the Windows build check (MSBuild, Razor precompile,
  runtime checks) and the deploy dry run. None of the admin panel has run against a database
  or in a browser yet; test it on the SmarterASP site first.
- **Still open from the list below:** media library, Phase 2b forms (contact, careers,
  franchise), Turnstile, news/events/gallery/downloads, campus import, school confirmations
  W1–W7, error-log location, region/campus-limited roles.

## 1. Where the code is

- Repository: `thearqamschools/tas-website`
- **Latest code: branch `claude/phase10-design`** (same commit as `claude/release-prep-combined`). Start all new work from here, on a new branch.
- `main` is still the original phase 2a code (d0c49c4). Nothing has been merged into `main`.
- Open pull requests form a chain. Each one builds on the one before, so merge them in this order, and only with the owner's approval:
  1. `claude/agents-skills-setup` → `main` (PR #1: CLAUDE.md, agents, skills)
  2. `fix/batch-a-ui-improvements` → `claude/agents-skills-setup` (PR #2)
  3. `claude/phase8-verified-baseline` → `fix/batch-a-ui-improvements` (PR #3: Windows build workflow)
  4. `claude/phase9-ux-audit` (homepage prototype)
  5. `claude/phase10-design` (PR #4: full redesign plus release preparation)
- Stack: ASP.NET MVC 5.3, .NET Framework 4.8, C# 7.3, Dapper, SQL Server 2025, IIS. Do not change the stack.
- Helpers in the repo: agents in `.claude/agents/` (aspnet-mvc-developer, sql-server-specialist, ui-ux-designer, qa-security-reviewer), skills in `.claude/skills/` (tas-branding, aspnet-mvc-standards, website-qa and design skills).

## 2. What is done

- **Phase 1:** database scripts `Database/00` to `05`.
- **Phase 2a:** public site core (CMS pages, menus, campuses, admissions inquiry form, SEO, sitemap, redirects).
- **Phase 8:** a Windows build check on GitHub Actions (`.github/workflows/windows-build.yml`): MSBuild, Razor view compilation and runtime checks.
- **Phase 9–10:** full redesign in the "Warm Classroom" style across every page: homepage, about, academics, admissions, campuses, forms, and system pages (404, error).
- **Release preparation:**
  - the homepage shows at most 3 campuses, while the `/campuses` directory lists all of them;
  - the homepage keeps working when the database is down (it uses the last good copy), and other pages return 503;
  - error pages render in place with the correct status code (400, 404, 500 or 503) and no technical details;
  - a staging runbook for IIS, HTTPS, server-side secrets, backups and rollback.
- The last Windows build passed (run 41213369818).

## 3. What is left (suggested order)

1. **Admin panel (Phase 3).** It does not exist: no login, no permissions, no content editing. Roles and permission tables are seeded, but nothing in the code uses them. Build login, roles, audit log and screens to read form submissions and edit pages, campuses and menus.
2. **Phase 2b.**
   - Email notification when a form is submitted.
   - Contact, careers and franchise forms.
   - Turnstile spam protection.
   - News, events, gallery and downloads lists.
   - Finish the `/privacy` page.
3. **Campus data:** only Samanabad is in the seed data. A campus import is needed, as a new numbered SQL script.
4. **School confirmations (do not invent these):**
   - W1: Lahore phone 042-36520008 and the Lahore address are unconfirmed.
   - W2: calling Samanabad the "main campus".
   - W3: linking both Facebook pages.
   - W4: real photos with parent consent.
   - W5: Urdu pages.
   - W6: the source of the About quote.
   - W7: the site blue versus the logo blue.
5. **Owner approvals still pending:**
   - a permanent error log location (this changes Web.config);
   - a 5 s database connection timeout on the server.
6. **Staging and go-live:**
   - get a Windows Server with IIS and SQL Server 2025;
   - follow `docs/release/STAGING-RUNBOOK.md`;
   - test on real devices;
   - then merge, deploy and change DNS, each only with the owner's written approval;
   - recover access to the thearqam.com domain from the previous developer first.
7. **Known small gap:** `RateLimiter` uses `Request.UserHostAddress`, which is the proxy address behind a CDN.

## 4. How to work

- Follow `CLAUDE.md` exactly. Most important:
  - one focused branch per change, then a pull request;
  - never push to or merge into `main` yourself;
  - new database changes go in a new numbered script (`06_...sql`); never edit `00`–`05`;
  - parameterised SQL only;
  - keep anti-forgery, honeypot, rate limit, validation and security headers;
  - add new files to the `.csproj`;
  - no new packages without approval.
- Branding:
  - the name is always **The Arqam Schools**;
  - approved figures only: 350+ campuses, 250,000+ students, 20,000+ Hafiz-e-Quran;
  - official contacts are as in `CLAUDE.md`;
  - do not change the logo;
  - English and Urdu only, never Hindi.
- Never deploy, touch a live database, change DNS or use real student/parent data without the owner's explicit approval.
- Testing has been paused by the owner for now. Still make sure the Windows build workflow stays green on each push, and say plainly in each PR what was and was not tested.
