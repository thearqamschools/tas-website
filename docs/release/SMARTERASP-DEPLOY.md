# Testing deployment on SmarterASP.NET (thearqamschools.com)

Status: **live for testing since 2 November 2026** at https://thearqamschools.com (hidden from search engines). The site stays hidden from search engines
(`Site:NoIndex = true`) during testing. Every step that changes the live site,
DNS or the database needs the owner's go-ahead.

## 1. SmarterASP.NET control panel

1. **Databases → MSSQL → Create database**, version **MSSQL 2025**. Note the
   server name, database name, user and password (keep them private).
2. **Websites**: note the site folder and the **FTP** host and user.
3. Add the domain `thearqamschools.com` (and `www.thearqamschools.com`) to the
   site, and install the free SSL certificate offered by the panel.
4. Note the site's **IP address** (needed for Cloudflare DNS) and its temporary
   test address, if one is shown.

## 2. Database

**Easiest (no SSMS):** after the `DB_CONNECTION_STRING` secret is set (step 3), push the
branch `deploy/smarterasp-db`. The workflow `Database setup (SmarterASP testing)` checks the
server is SQL Server 2025, runs the scripts below on an empty database, and on later runs
only the numbered change scripts (each one runs once).

**By hand (SSMS, "Remote Connect" from the panel details):** connect to the new, empty
database and run, in this order:

1. `Database/hosting/smarterasp_setup.sql` (replaces `00`, which cannot run on shared hosting)
2. `Database/01_schema.sql`
3. `Database/02_seed_data.sql`
4. `Database/04_simplify_navigation.sql`
5. `Database/05_franchise_header_button.sql`
6. `Database/06_contact_franchise_forms.sql` (adds the contact and franchise application forms)

Skip `03_fulltext.sql` (optional, and Full-Text Search may not be offered).

## 3. GitHub settings (repository → Settings → Secrets and variables → Actions)

| Name | Kind | Value |
|---|---|---|
| `FTP_SERVER` | secret | FTP host only, e.g. `win1234.site4now.net` |
| `FTP_USERNAME` | secret | FTP user |
| `FTP_PASSWORD` | secret | FTP password |
| `DB_CONNECTION_STRING` | secret | `Data Source=<server>;Initial Catalog=<db>;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=True;Application Name=TheArqamSchools.Web` |
| `ADMIN_SETUP_KEY` | secret | a random key of 24+ characters; remove after the first Super Admin exists |
| `FTP_REMOTE_DIR` | variable | the site folder on the FTP server, e.g. `/site1` |
| `SITE_URL` | variable | `https://thearqamschools.com` (default) |
| `SITE_REQUIRE_HTTPS` | variable | `true`; `false` only until the certificate works |
| `SMOKE_URL` | variable | optional address to check after upload |
| `MAIL_HOST`, `MAIL_USER`, `MAIL_PASSWORD`, `MAIL_FROM`, `MAIL_NOTIFY_TO` | secrets | optional: email to the school when a form is sent |
| `MAIL_PORT`, `MAIL_ENABLE_SSL` | variables | optional, default `587` / `true` |

**Recommended protection:** create a GitHub **Environment** named `smarterasp-testing`
(Settings → Environments), allow only the branch `deploy/smarterasp`, add the owner as
a required reviewer, and put the secrets above in that environment instead of the
repository. Then add `environment: smarterasp-testing` to the deploy job. Every upload
then waits for the owner's click, and other branches cannot read the secrets.

**Fixed machine key:** shared hosting may change the ASP.NET machine key when the app
restarts, which signs every admin out. Before go-live, add a fixed `<machineKey>` to the
server's Web.config (generated once, kept only on the server).

## 4. Upload (only with the owner's go-ahead)

Push the tested commit to the branch `deploy/smarterasp`. The workflow
`Deploy to SmarterASP (testing)` builds in Release mode, checks the Razor views,
writes the settings above into the published `Web.config`, checks the output and
uploads it over FTPS (with `app_offline.htm` while files change). Every push to
the development branch runs the same steps as a dry run without uploading.

## 5. Cloudflare DNS

- `A` record `thearqamschools.com` → the SmarterASP IP; `CNAME` `www` → `thearqamschools.com`.
- During testing use **DNS only (grey cloud)**. If the proxy (orange cloud) is turned
  on later, set SSL/TLS mode to **Full (strict)**; the site reads the real visitor
  address from Cloudflare for its form limits.

## 6. First sign-in

1. Open `https://thearqamschools.com/admin/setup`, enter the setup key and create
   the first Super Admin.
2. Sign in at `/admin/login` and set up the authenticator app (required).
3. Delete the `ADMIN_SETUP_KEY` secret and deploy again; the setup page then stays off.

## 7. Check

`/robots.txt` must say `Disallow: /`. Then go through the pages, both forms, the
admin inbox, campus and page editing, on a phone and a computer.

## What happened on the first deployment (2 November 2026)

- The trial plan allows one database and one FTP user. The existing database was MSSQL 2022;
  it was empty, so it was deleted and recreated as **MSSQL 2025** (`db_ad0939_tasweb`,
  server `sql8012.site4now.net`). The upload uses the default FTP user with folder `/site1`.
- The database was set up by the `Database setup (SmarterASP testing)` workflow.
- `FtpWebRequest` failed on sub-folders (550), so `Publish-Ftp.ps1` now uses `curl.exe`.
- Every page answered 401 until the account's **Security Lock** was turned off
  (SmarterASP account Dashboard: verify the account with two-step verification).
- The free SSL (Let's Encrypt, HTTP validation) needs `/.well-known/` to be served as static
  files without the HTTPS redirect; `.well-known/web.config` does that. It failed while the
  Security Lock was on (401 in the validation log) and succeeded after.
- DNS at Cloudflare: `A @` and `A www` → `208.98.35.184`, **DNS only** (grey cloud).
- GitHub variables now: `SITE_URL` and `SMOKE_URL` = `https://thearqamschools.com`,
  `FTP_REMOTE_DIR` = `/site1`.
