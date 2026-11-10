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
7. `Database/07_news_and_flat_menu.sql` (news categories, flat main menu, footer "Explore")
8. `Database/08_about_timeline_leadership.sql` (empty, hidden timeline and leadership sections on About)
9. `Database/09_owner_review_changes.sql` (owner's review: menus, About/Academics/Admissions layout, Facebook link, Privacy Policy and Terms of Service)
10. `Database/10_faisalabad_region_campuses.sql` (93 Faisalabad region campuses, with their cities and districts)
11. `Database/11_owner_second_review.sql` (motto, Facebook link, class names, level ages and key points, journey 1993).
    It contains Arabic text and is saved as UTF-8: by hand on Windows use `sqlcmd ... -f 65001 -i Database\11_owner_second_review.sql`,
    or check in SSMS that the Arabic looks right before pressing Execute. The GitHub workflow (Linux sqlcmd) reads it correctly.
12. `Database/12_approved_content_v1_4.sql` (approved content v1.4: head offices, About, Academics, Admissions, FAQ,
    footer; adds head_offices.mobile and facebook_url; UTF-8, same note as 11)
13. `Database/13_undo_content_except_about.sql` (undoes 12 everywhere except About Us, from the 12 backups)
14. `Database/14_about_notes_and_sample_news.sql` (About: journey heading and text, Vision seal, Core Values badge; five "Sample:" news posts; UTF-8)
15. `Database/15_about_photos_and_core_values.sql` (founder and CEO photos on About; Core Values without "We believe in:" and the statement)
16. `Database/16_photo_asad_sakhawat_ali.sql` (photo of Mr. Asad Sakhawat Ali on About > Leadership)
17. `Database/17_news_event_photos.sql` (table `post_images`: photos of news posts and events). The site's
    application pool needs write permission on `media\public-media\` (uploads go to `news\` and `events\`) and on
    `App_Data\removed-media\` (removed photos are moved there, not deleted). Check with one test upload after deploying.

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

## 2 November 2026: News & Events update

- `07_news_and_flat_menu` and `08_about_timeline_leadership` ran on the testing database
  through `deploy/smarterasp-db` (04-06 reported "already applied").
- The site (commit 2e0d91e) was uploaded through `deploy/smarterasp`; the quick check
  answered 200 for all pages. `/news-events` is now in that check too.

## 3 November 2026: owner's second review and Faisalabad region campuses

- `09_owner_review_changes` (2 November), `10_faisalabad_region_campuses` and `11_owner_second_review`
  ran on the testing database through `deploy/smarterasp-db`; earlier scripts reported "already applied".
- The site (commit 8e5cbf2) was uploaded through `deploy/smarterasp` (156 files); the quick check answered
  200 for every page.
- Open items for the owner: founding year (1992 or 1993), Google Maps share links for both head offices,
  the real address of Pir Mahal Campus (hidden for now), two phone numbers (Multan 061-457888,
  Fatyana 0405-312248), the other regions' campus lists, Helvetica Inserat web-font licence.

## 6 November 2026: approved website content v1.4

- `12_approved_content_v1_4` ran on the testing database through `deploy/smarterasp-db` (earlier scripts:
  "already applied"). The site (commit 7338cf2, header lock-up in Scheherazade New, content v1.4, brand colours
  and fonts) was uploaded through `deploy/smarterasp` (160 files); every page in the quick check answered 200.
- Same day, owner's decision: `13_undo_content_except_about` ran through `deploy/smarterasp-db`; the site (commit
  4d61a5b) was uploaded. The first upload stopped with FTP 550 on `bin/TheArqamSchools.Web.dll` (file still in use
  on the server) and left the "being updated" page; re-running the failed job uploaded all 156 files and every page
  answered 200. If this happens again, re-run the failed job (Actions → the run → Re-run failed jobs).

**Before launch (Site:NoIndex = false):** archive the five sample posts from Database/14 (web addresses starting
`sample-`) in the admin panel (News & Events), so they leave the homepage, /news-events and the sitemap.
- 6 November 2026 (later): `14_about_notes_and_sample_news` ran through `deploy/smarterasp-db`; the site (commit
  4fecbd1) was uploaded (158 files) and every page in the quick check answered 200.
- 7 November 2026: `15_about_photos_and_core_values` ran through `deploy/smarterasp-db`; the site (commit 971227d)
  went up on the second attempt (FTP 550 on `bin/TheArqamSchools.Web.dll` again; "Re-run failed jobs" uploaded
  all 161 files and every page answered 200).
- 7 November 2026: `16_photo_asad_sakhawat_ali` ran through `deploy/smarterasp-db`; the site went up as commit
  fc9e09b and then cacbb67 (leadership cards show only the role), 162 files each, every page 200.
- 10 November 2026: `17_news_event_photos` ran through `deploy/smarterasp-db`; the site (commit 781b8ef, news and
  event photos) went up on the first attempt and every page answered 200. Still to confirm: one photo upload in the
  admin panel (write permission on `media\public-media` and `App_Data\removed-media`).
