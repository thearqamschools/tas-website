# Release readiness — 31 October 2026

Baseline: `claude/phase10-design`, `2f5228c`. Not ready for production.

## Implemented

- Homepage SQL now selects TOP (3) with a stable ID tie-breaker, preserving publication,
  activity, archive and deletion filters. The full directory query is unchanged.
  The baseline already used All().Take(3); this moves the limit into SQL.
- Application_Error now renders a standalone, database-independent safe response
  in place, including failures during shared layout rendering. Homepage SQL failures
  return 503, Retry-After 60 and no-store; no fabricated campus records or stale-data
  cache is introduced. This is a temporary-unavailability page, not the full homepage.
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

- W1, W2, W3, W4, W5, W6, W7: **original definitions unavailable**. Searched tracked
  repository documentation across fetched branches and all older turns returned by
  the referenced ChatGPT chat. It says they are pending but does not define them.
  Obtain the original Phase 8/9 decision register from the school/previous project
  owner. No substitute decisions have been invented.
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
