# Staging and release runbook — 31 October 2026

Status: prepared, not deployed. Existing development branch: `claude/phase10-design`.
Staging host, URL, certificate, SQL endpoint and server access have not been supplied.
Do not substitute the old Claude artifact: it cannot submit forms or query SQL.
No production deployment, DNS change, purchase or main merge is authorized.

## 1. Isolated server and database

Use a supported Windows Server with IIS, ASP.NET 4.x/.NET Extensibility, ISAPI
extensions/filters, Static Content and .NET Framework 4.8. Build with VS 2022
Build Tools (ASP.NET workload and 4.8 targeting pack). SQL Server **2025** and
compatibility **170** are required by the existing REGEXP_LIKE constraints.
Use a separate staging instance or isolated staging database; never copy production
student, parent, staff or enquiry records. Do not run `00` against a shared instance:
it names `TheArqamSchools` and changes isolation settings with rollback immediate.

For a dedicated disposable SQL instance, privately copy `Database/00_create_database.sql`,
replace its placeholder password with a unique generated staging password and run it
as the provisioning identity. Keep this copy outside Git and the web root. Run
`01_schema.sql`, `02_seed_data.sql`, optional `03_fulltext.sql`, then `04` and `05`,
connected explicitly to the staging database. Record results and migration rows.
Do not edit the committed historical scripts. No new schema migration is required.

Use a dedicated application principal. Do not run the site as sysadmin, db_owner,
the deployment identity, or the provisioning login. The historical `00` grants
broad reader/writer/execute roles; narrow these before exposing staging. The current
public site needs SELECT on pages, page_sections, seo_metadata, site_settings,
navigation_items, navigation_menus, media, branches, cities, districts,
branch_classes, class_levels, branch_facilities, facilities, head_offices and redirects;
INSERT on admission_inquiries and contact_messages; UPDATE only hit_count and
last_hit_at on redirects. Include UPDATE on the enquiry reference sequences if SQL
requires it for their defaults (inspect the schema). Test with the actual identity.
Do not grant SELECT on users/password or enquiry records to make `/status` pass;
the local developer status route counts additional administrative tables and is not
a public health endpoint. Keep `/status` blocked at the staging access layer.

## 2. Build and stage files

Record the development commit SHA. Restore the exact packages.config versions,
then run the existing Windows build workflow (Release and Razor precompile) and
`tools/release-check/Run-RuntimeChecks.ps1`. Obtain an MSBuild file-system publish
artifact, including bin/roslyn, Global.asax, Views, media, Content and Scripts.
Do not publish source, tools, Database, packages, Git metadata, test harnesses or
release-test.aspx. Check the artifact's file list and checksum before upload.

Create a separate IIS site `TAS-Staging` and CLR v4 Integrated application pool,
64-bit, ApplicationPoolIdentity (or a dedicated managed service identity for remote
SQL). Use versioned release directories, e.g. `D:\Sites\TAS-Staging\releases\<SHA>`.
Give the pool read/execute only on the release; grant modify only on required
upload/private-data paths outside executable code. Disable directory browsing and
WebDAV; keep private/App_Data and configuration inaccessible. Do not enable writes
to bin, Views, scripts or Web.config for the application identity.

## 3. HTTPS and server-only settings

Bind a supplied staging hostname to HTTPS 443 with SNI and a valid trusted
certificate. Use a provider preview hostname if available; do not change production
DNS. Allow HTTP only for a redirect to HTTPS. Limit SQL network access to the web
server/administrators; never expose SQL publicly. Restrict staging by VPN/IP allowlist
or an access gateway, and verify it from an authorized external browser.

Before starting the site, set the deployed configuration privately:

- compilation debug=false; customErrors mode=On; retain httpErrors Custom/Auto,
  request filtering, all security headers, secure/HttpOnly/SameSite cookies.
- Site:RequireHttps=true and Site:Url equal to the **staging** HTTPS URL. The current
  Web.Release.config substitutes https://www.thearqam.com: override it in the staging
  artifact, then verify canonical and sitemap URLs before making staging accessible.
- ArqamDb points only to staging; use Encrypt=True;TrustServerCertificate=False,
  a certificate-valid SQL hostname, and a short connection timeout (e.g. 5 seconds).
  Prefer integrated service authentication; otherwise obtain a dedicated SQL secret
  from the operator. Never store credentials in scripts, build logs, Git or chat.
- Protect connectionStrings with server-supported ASP.NET protected configuration,
  restrict file ACLs, and back up the encryption key securely for recovery. Provision
  any explicit machineKey securely, consistently per environment, never in Git.

There are currently no SMTP/Turnstile integrations to configure. Do not claim email
delivery, contact/careers/franchise submissions or administrator access works.
If Cloudflare is later used, require origin HTTPS/Full (strict); validate rate limiting
behind the chosen proxy. Current client keys use UserHostAddress; do not blindly
trust X-Forwarded-For from arbitrary clients.

## 4. Backups and rollback rehearsal

Before **any** staging change, back up its database with CHECKSUM to an access-controlled
non-web directory. Use a unique timestamped file, not an overwrite. Run RESTORE
VERIFYONLY WITH CHECKSUM, then restore into another isolated database and run DBCC
CHECKDB there. VERIFYONLY alone is not a restore rehearsal. Back up uploaded/private
files, deployed configuration, protected-configuration keys and IIS configuration
(`appcmd add backup TAS-Staging-<timestamp>`). Encrypt backup storage and the off-server
copy. Record backup locations, retention, owner and restore duration; grant the web
pool no backup access. Proposed initial policy for owner approval: daily full backups,
30-day retention; choose full recovery/log backups if the required data-loss window
is shorter. Never claim backups or a schedule exist until verified.

Example commands for an operator on the **dedicated staging SQL instance**:

```sql
BACKUP DATABASE [TheArqamSchools]
  TO DISK = N'D:\SQLBackups\TAS-staging-YYYYMMDD-HHMMSS.bak'
  WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY
  FROM DISK = N'D:\SQLBackups\TAS-staging-YYYYMMDD-HHMMSS.bak'
  WITH CHECKSUM;
```

For a failed code-only release, stop staging pool, switch its physical path back to
the previous known-good release with its matching configuration, start, and repeat
smoke checks. These changes have no schema migration; preserve enquiry data.
For a database rollback, stop writes, take an additional backup, restore the rehearsed
backup to a **new isolated database**, validate it, and switch the staging connection
string. Obtain owner acceptance for any lost enquiries; never silently overwrite a
live database. Restore matching media/configuration/key material. Record who can
execute rollback and the agreed recovery-time/data-loss targets before release.

## 5. Required acceptance evidence

Run PowerShell 7: `./tools/release-check/Test-Staging.ps1 -BaseUrl <staging HTTPS URL>`.
It does not establish database correctness. Then record all of the following:

1. External HTTPS and certificate-chain check; secure cookies; no private files,
   App_Data, Web.config, source or detailed error disclosure. `/status` inaccessible.
2. Add at least four **synthetic** published campuses in the disposable staging DB,
   plus inactive/unpublished/archived/deleted fixtures. Homepage shows at most three
   eligible records, directory still returns all eligible records, ordering is stable,
   city/district/name filters work, hostile LIKE/HTML input stays safe. Remove fixtures.
3. Admission and campus-message submissions: obtain real antiforgery cookies/tokens;
   submit synthetic example.com records; verify exactly one correct database row and
   redirect-after-post. Verify invalid IDs/fields fail, missing/tampered CSRF fails,
   honeypot saves nothing and sixth valid attempt within ten minutes is limited.
   Confirm stored rows with a separate operator identity; remove only the test rows.
4. Stop/disconnect only staging SQL, recycle staging pool (cold cache), request `/`:
   503 with safe text, confirmed phone, Retry-After 60, no-store and no redirect;
   repeat warm-cache/expired-cache cases. Restart SQL and confirm `/` becomes 200.
   Cached data may remain visible for its existing 60-second lifetime; no new stale
   cache was added. Failed form writes must never display successful submission.
5. On a private test copy, check managed 400/403/404/500, request-validation failures,
   missing CSRF, IIS-native missing static files, blocked private paths, oversized
   requests and unsupported verbs. Preserve status; never expose stack traces or
   turn every client error into 500. Remove test-only endpoints before opening access.
6. Check every public page in Edge/Chrome, Firefox and Safari; 320, 390, 768, 1024,
   1440 px; keyboard/focus, no sideways scrolling, JS off, reduced motion, zoom,
   real iOS/Android and a screen reader. Include the new outage page. Keep screenshots.
7. Rehearse backup restore and code rollback, then record a directly reachable URL,
   exact tested SHA, test logs and outstanding failures. A static artifact is not staging.

## 6. Go-live gate

Resolve the Admin Panel/product scope and missing form modules; obtain W1–W7's
original wording and recorded school decisions; confirm Lahore phone/address and
replace labelled photographs. Pass the above staging checks and backups/rollback.
Present results to the owner. Only after explicit production approval: provision the
approved hosting/domain, take fresh backups, merge the approved SHA, deploy its
matching artifact/configuration, validate production privately, then make approved
DNS changes and monitor failures/form persistence. Do not infer approval from this guide.

References: [IIS error-response behavior](https://learn.microsoft.com/en-us/iis/configuration/system.webserver/httperrors/),
[ASP.NET error handling](https://learn.microsoft.com/en-us/troubleshoot/developer/webapps/aspnet/development/custom-error-reporting-page),
[backup checksums](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/enable-or-disable-backup-checksums-during-backup-or-restore-sql-server?view=sql-server-ver17),
[RESTORE VERIFYONLY](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-verifyonly-transact-sql?view=sql-server-ver17).
