# Release readiness — 31 October 2026

Baseline: `claude/phase10-design`, `2f5228c`. Not ready for production.
Updated after combining the Codex (a68a47e) and Claude (2d67e56) release-preparation commits.

## Implemented

- Homepage SQL selects TOP (@count), count fixed at 3 (`CampusRepository.Featured`), with a stable
  ID tie-breaker, preserving publication, activity, archive and deletion filters. The full
  directory query is unchanged.
- Database outages: `Db.Open` pauses new connections for 10 seconds after a connection failure
  (one request probes afterwards). Menus, settings, footer offices, redirects and the homepage
  finder use their last good copy (else empty), so the **homepage keeps rendering (200)**; with a
  cold cache it shows without menus and campus cards. No campus records are fabricated.
  Pages that need the database answer **503 with Retry-After 60**, no-store, safe text.
- Application_Error renders the error page in place (the 404 page or the stand-alone,
  database-independent error view), with a fixed plain fallback if rendering fails.
- Managed HTTP exceptions retain their error status; request validation and CSRF
  failures return 400; unexpected failures return 500. Removed the redirect-based
  customErrors fallback and global HandleError filter that masked status handling.
  IIS keeps explicitly handled responses and uses safe custom native errors.
- Added isolated ASP.NET runtime checks to Windows CI and a staging smoke script.
  No database schema or production configuration changed.

## Actual Admin Panel audit

| Capability | Evidence | Result |
|---|---|---|
| Login, password verification, sessions/cookies, MFA, recovery, lockout | No auth controller/service/startup, Admin area or admin views; sessionState Off | Not implemented |
| User permissions and campus scope enforcement | No authorization/permission implementation in controllers | Not implemented |
| CMS editing of pages/homepage/navigation/media/campuses | Public read-only repositories/views; no editor routes or write controllers | Not implemented |
| User/role administration | users/roles/permissions/user_roles/role_permissions are database schema only | Not implemented |
| Enquiry management/export/audit | Public forms insert rows; no inbox, follow-up screen, export or audit service | Not implemented |

An `/admin` link or the seeded permission records cannot supply these features.
README still marks Admin Panel as planned. InquiryRepository's comment that an admin
panel lists submissions is aspirational and is not evidence of an implemented feature.
Do not issue administrator credentials or promise an operational admin URL.

Admission inquiries and campus messages have actual POST handlers, antiforgery,
honeypot, validation and in-memory rate limiting. Database-backed successful writes
have not been re-tested here because no SQL instance is available. Contact/careers/
franchise submissions, email notifications and Turnstile are still absent.

## School decisions and content

- W1–W7 are defined in the Phase 9 decision register (project files,
  `agents-setup/phase9/PHASE9-PROTOTYPE-DECISIONS.md`, approved direction 30 Oct 2026). All are still open:
  - W1: Lahore head office number 042-36520008 (unconfirmed; shown in the footer).
  - W2: "main campus" wording for Samanabad (in the campus overview and seed text).
  - W3: link both official Facebook pages (footer links only TASHeadOffice; thearqamfsdOfficial is not linked site-wide).
  - W4: photography plan: shoot, parent consent, uniform and scarf policy, Hifz class rules. Required before production.
  - W5: Urdu pages in the first release, and who proofreads them.
  - W6: source of the About page quote ("The basic aim and idea of The Arqam Schools is to earn the pleasure of Allah through the acts of education.", attributed to "The Arqam Schools").
  - W7: aligning the site blue with the logo blue.
- Lahore seed phone **042-36520008** is explicitly unconfirmed in CLAUDE.md.
  The stored office address is **163-Tufail Block, Canal Bank Housing Scheme,
  Lahore, Pakistan**; its map URL is a search built from that address, not independent
  verification. Seed email/mobile/WhatsApp fields are null. Obtain written school
  confirmation of phone, address and any missing contact channels before release;
  do not infer or populate missing values.
- Labelled photo placeholders and the draft privacy page need school approval/content.

## Verification and limits

Local Windows verification: C# 7.3 compilation against actual existing NuGet packages
and installed .NET Framework assemblies passed; all Razor views passed
aspnet_compiler (4.8.9037.0). This was a direct compiler check, not a VS/MSBuild build.
Eight real ASP.NET-hosted regression requests passed: 400/403/404/429/500 preserve status,
request validation and missing-CSRF POST return 400, cold-cache database outage homepage returns 503.
(Superseded by the combined version: the runtime checks now expect the homepage to return 200 during
an outage and a CMS page to return 503. See the Claude test evidence below.)

Claude test evidence (Linux: Mono 6.8 + nginx + SQL Server 2025 in Docker, synthetic campuses only,
customErrors On; not IIS): homepage shows 3 of 4 synthetic campuses and /campuses lists all 4; HTTP and
security suite 58/58 functional checks (4 IIS-only header checks informational); 19 pages at 320/390/768/1024/1440 px
with no overflow, AA contrast and no console errors; 95 screenshots pixel-identical to 2f5228c; SQL stopped
with warm cache: homepage 200 from the last good copy, CMS pages error page in about 0.01 s, redirects still 301;
cold start with SQL down: homepage 200 without menus or campus cards; recovery within about 11 s; a form value
containing `<script>` returns 400 with no details leaked.
All eight verify no-store, no error redirect or private error marker/stack disclosure; the outage
also verifies Retry-After and its visitor message. The test copy uses an unreachable
local SQL port with a one-second timeout, no real data, and test-only pages outside
the repository website. These checks do not exercise the IIS native pipeline.

Pending: actual IIS/SQL integration, valid form saves/search and four-campus fixtures,
IIS-native error/security behavior, backup/restore rehearsal, full responsive and
cross-browser/device/screen-reader testing. Previous 58/58 and visual results in the
referenced chat are historical reports, not newly executed evidence.

No IIS/W3SVC or SQL Server service, VS Build Tools/MSBuild 17 or .NET SDK was found
locally. Package restore was authorized and used only the existing pinned versions.
No compatible staging server/access/certificate/SQL endpoint was provided; therefore
there is no working public staging preview yet. Follow STAGING-RUNBOOK.md to complete
the environment-dependent checks and the go-live gate. Never point staging at production.
