# Staging deployment: Windows Server, IIS and SQL Server 2025

This guide sets up a **staging** copy of The Arqam Schools website. It does not cover production.
Production follows the same steps on its own server and database, and only after the owner gives
written approval for that specific deployment (see CLAUDE.md, "Safety constraints").

- Staging must never use production data. Load the repository seed scripts only, or synthetic test data.
- Staging must not be indexed by search engines. Step 7 explains how.
- Every value in angle brackets, such as `<staging-host>`, is a placeholder to fill in on the server.
  None of these values belong in git.

## 0. What you need

| Item | Minimum |
|---|---|
| Server | Windows Server 2022 or later (check the SQL Server 2025 system requirements if SQL Server runs on the same machine), with 2 vCPU and 4 GB RAM (8 GB if SQL Server is on the same machine) |
| IIS | Web Server role with ASP.NET 4.8, .NET Extensibility 4.8, ISAPI Extensions and Filters, Static Content, Default Document, HTTP Errors, Request Filtering, Dynamic Compression, URL Rewrite 2.1 (optional, for the HTTP-to-HTTPS rule) |
| .NET | .NET Framework 4.8 runtime (included in Server 2022) |
| SQL Server | SQL Server **2025** (Express is enough for staging). Compatibility level **170** is required because the CHECK constraints use `REGEXP_LIKE`. |
| Certificate | A trusted TLS certificate for `<staging-host>`, for example Let's Encrypt through win-acme, or a certificate from the hosting provider |
| Build machine | Visual Studio 2022 with the ASP.NET workload and the .NET Framework 4.8 targeting pack, or the GitHub Actions "Windows build check" artifact |

## 1. Build the release package

1. Check out the release branch at the exact commit that was reviewed.
2. In Visual Studio, select **Release**, then **Restore NuGet Packages**, then **Build**.
3. Publish with **Build > Publish > Folder**, using the Release configuration. `Web.Release.config` then:
   - removes `debug="true"`;
   - sets `customErrors mode="On"`;
   - sets `Site:Url` to `https://www.thearqam.com`. On staging, override this in IIS (step 5).
4. Optional: compile the Razor views on the build machine:
   `aspnet_compiler -v / -p <publish-folder> -errorstack`. The GitHub "Windows build check" workflow does this on pushes to the branches listed in `.github/workflows/windows-build.yml`.
5. Zip the publish folder as `tas-website-<commit>-<yyyyMMdd>.zip` and keep it. This zip is the rollback point for the next release.

## 2. Database (SQL Server 2025)

1. Back up the current staging database before every change, even on staging (see step 8).
2. In SSMS, open `Database/00_create_database.sql`. Replace `CHANGE-THIS-to-a-long-random-password`
   **only in your local copy**, using a new random password of 24 or more characters. Never commit it.
3. Run `00` to `05` in order. On an existing database, run only the numbered scripts it does not have yet.
   `dbo.schema_migrations` lists what has already been applied.
4. Check the database: `SELECT compatibility_level FROM sys.databases WHERE name = 'TheArqamSchools'` must return **170**.
5. The website login `arqam_web` has only `db_datareader`, `db_datawriter` and `EXECUTE`. Do not give it `db_owner` or `sysadmin`.
6. If SQL Server is on a different machine, open TCP 1433 only to the web server's IP address, and enable
   **Force Encryption** with a trusted certificate.

## 3. IIS site

1. Copy the publish folder to `D:\Sites\tas-staging\releases\<commit>\`. Point the site at
   `D:\Sites\tas-staging\current`, a directory junction created with
   `mklink /J D:\Sites\tas-staging\current D:\Sites\tas-staging\releases\<commit>`.
   With a junction, a rollback means switching it back to an earlier folder.
2. Create an application pool `tas-staging` with these settings:
   - .NET CLR v4.0, Integrated pipeline;
   - identity `ApplicationPoolIdentity`;
   - Start Mode `AlwaysRunning` (optional).
3. Create the site `tas-staging`:
   - physical path `D:\Sites\tas-staging\current`;
   - binding `https`, port 443, host `<staging-host>`, with the TLS certificate;
   - an `http` binding on port 80 only for the redirect to HTTPS.
4. Folder permissions:
   - `IIS AppPool\tas-staging` gets **Read** on the site folder;
   - it also gets **Modify** only on `App_Data\` and `media\public-media\` (uploads, once the admin panel exists).
5. `Web.config` already has these protections. Do not remove them:
   - the security headers (`X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`, `Permissions-Policy`);
   - `removeServerHeader`;
   - the `private` hidden segment;
   - `httpOnlyCookies` and `requireSSL`;
   - no version headers.

## 4. HTTPS

- The application requires HTTPS for every MVC page (GET requests are redirected to HTTPS, other requests
  are refused) unless `Site:RequireHttps` is `false`. Never set it to `false` on a server.
- Static files (CSS, images) are not redirected by the application. Add a URL Rewrite rule on the site
  that sends every `http://` request to `https://` with a 301.
- Cookies are marked `Secure`. Forms will not work over plain HTTP. This is intended.
- After HTTPS has worked for a few days on **production**, add HSTS in IIS (HTTP Response Headers) with
  `Strict-Transport-Security: max-age=31536000`. Do not add HSTS on staging with a temporary certificate.
- Check TLS with an SSL test tool: TLS 1.2 or higher only, no SSL 3 and no TLS 1.0 or 1.1.

## 5. Secrets and settings (on the server only)

Set these in **IIS Manager > site > Configuration Editor or Connection Strings / Application Settings**.
IIS stores them in the site's `web.config` on the server. They are never committed to git.

| Setting | Staging value |
|---|---|
| Connection string `ArqamDb` | `Data Source=<sql-host>;Initial Catalog=TheArqamSchools;User ID=arqam_web;Password=<secret>;Encrypt=True;TrustServerCertificate=False;Connect Timeout=5;Application Name=TheArqamSchools.Web` |
| App setting `Site:Url` | `https://<staging-host>` |

- Use `TrustServerCertificate=True` only if SQL Server is on the same machine and has no trusted certificate.
- `Connect Timeout=5` limits how long the first request waits when the database is down. After that,
  the site pauses new connections for 10 seconds and serves the homepage from its last good copy.
- **Every release folder gets a fresh `web.config` from the package**, so these server values must be put back
  each time. Keep them in a server-only file, `D:\Sites\tas-staging\secrets\web.config.values.txt`, readable only by
  administrators. After copying a new release, set them again in IIS Manager on the site, *before* switching the
  junction. A Release transform that holds the real values must never be committed.
- To protect the secrets further, encrypt the section on the server:
  `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\aspnet_regiis.exe -pef "connectionStrings" D:\Sites\tas-staging\releases\<commit>`.
  Run it for every new release folder.
- Once email and Turnstile exist (phase 2b), their keys go in the same place. No secrets exist in the code today.

## 6. First checks after deployment

1. Open `https://<staging-host>/status` **from the server itself**. It answers only local requests. Check:
   - **Connected**;
   - compatibility level **170**;
   - REGEXP_LIKE **Yes**;
   - row counts for each table.
2. Open these pages and check each answer:
   - `/`, `/about`, `/admissions`, `/campuses` and one campus page: 200;
   - `/does-not-exist`: the "Page not found" page with status 404;
   - `/error`: the error page with status 500.
3. Submit the admission inquiry and the campus contact form with **synthetic data**, for example
   "Test Parent Synthetic", 0300-1234567 and test@example.com. Check each row in `dbo.admission_inquiries` and
   `dbo.contact_messages`, then retire the test rows (step 9).
4. Check the response headers with `curl -I https://<staging-host>/`. They must include the four security
   headers, and there must be no `Server`, `X-Powered-By` or `X-AspNet-Version` header.
5. Error handling check: request `https://<staging-host>/a%3Cb` from another machine. It should show the
   error page with status 400, and no redirect or error details.
6. Database outage check, on staging only:
   1. Stop the SQL Server service.
   2. Load `/`. It should return 200 with the cached homepage.
   3. Load `/about`. It should show the error page with status 500 quickly.
   4. Start the service again.
   5. Within about 70 seconds everything should work normally.
7. Real-device and screen-reader pass: Android Chrome, iPhone Safari, desktop Edge, Chrome and Firefox, then
   NVDA or VoiceOver on the homepage, admissions form and campus page.

## 7. Keep staging private

Use one or both of these:

- **IP restriction:** IIS > IP Address and Domain Restrictions. Allow only office and developer IPs.
- **Basic authentication:** for staging only, over HTTPS only.

Also add the response header `X-Robots-Tag: noindex, nofollow` to the staging site in IIS
(HTTP Response Headers). Do not add it to production.

## 8. Backups

Before every deployment, take a copy-only backup, so the normal backup chain is not disturbed:

```sql
DECLARE @f NVARCHAR(260) = N'D:\Backups\TheArqamSchools_' + FORMAT(SYSDATETIME(), 'yyyyMMdd_HHmm') + N'_predeploy.bak';
BACKUP DATABASE [TheArqamSchools] TO DISK = @f WITH COPY_ONLY, COMPRESSION, CHECKSUM, STATS = 10;  -- remove COMPRESSION on SQL Server Express
RESTORE VERIFYONLY FROM DISK = @f WITH CHECKSUM;
```

- **Regular schedule:** for production, take a full backup daily and a log backup every 15 minutes in the FULL
  recovery model. For staging, take a full backup daily. Keep at least 14 days, plus one copy off the server.
- **Site files:** keep the last three `releases\<commit>` folders and their zips. Also back up
  `media\public-media\` daily, because uploads live there.
- **Restore test:** once a month, restore the latest backup to a scratch database and open `/status`
  against it. A backup that has never been restored is not proven.

## 9. Test data

Use only synthetic data, such as made-up names, `0300-1234567`-style numbers and `example.com` emails.
After testing, retire it rather than deleting it. For form rows in `dbo.admission_inquiries` and
`dbo.contact_messages`, set `status = N'spam'` and `deleted_at = SYSDATETIMEOFFSET()`, which the CHECK
constraints allow. Never copy production data into staging.

## 10. Rollback

**Application:**

1. Point the junction back to the previous release:
   `rmdir D:\Sites\tas-staging\current` and then `mklink /J D:\Sites\tas-staging\current D:\Sites\tas-staging\releases\<previous-commit>`.
   `rmdir` on a junction removes only the link, not the folder.
2. Recycle the `tas-staging` application pool.
3. Repeat the checks in step 6, parts 1 and 2.

**Database:**

- Do this only if the release ran a new numbered script and the old code cannot work with the new schema.
- Restore the pre-deployment backup:

First stop the `tas-staging` application pool, so it does not reconnect during the restore. Then run:

```sql
USE [master];
ALTER DATABASE [TheArqamSchools] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [TheArqamSchools] FROM DISK = N'D:\Backups\TheArqamSchools_<stamp>_predeploy.bak' WITH REPLACE, CHECKSUM;
ALTER DATABASE [TheArqamSchools] SET MULTI_USER;
```

Start the application pool again and repeat the checks in step 6.

- Restoring loses every form submission made after the backup. Export new `dbo.admission_inquiries` and
  `dbo.contact_messages` rows first.
- Change scripts `04` and later keep their changed rows in `archive.*_YYYYMMDD` tables. A small data
  change can usually be undone from those tables without a full restore.

## 11. Going live (production, owner approval required)

Each step needs the owner's explicit written approval for that action:

1. Merge the reviewed PR chain into `main` in the agreed order.
2. Build the release from `main` (step 1).
3. Set up the production server and database (steps 2 to 5), with production secrets.
4. Take a backup (step 8).
5. Deploy (step 3) and check (step 6).
6. Point DNS for `www.thearqam.com` at the new server. This needs access to the domain, which is
   currently held by a previous developer.
7. Watch the site for 24 hours, keeping the rollback (step 10) ready.
