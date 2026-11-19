# Admin panel

Address: `/admin`. Built in ASP.NET MVC 5 inside the website (no extra packages).

## Sign-in and security

- Email and password (PBKDF2-SHA256, 100,000 rounds), then a 6-digit code from an
  authenticator app (RFC 6238). **Two-step verification is required for every user**
  and is set up on first sign-in.
- 5 wrong passwords or codes lock the account for 15 minutes; each connection is
  rate limited; each code works once; the same message is shown for an unknown email,
  a wrong password and a locked account.
- Session cookie protected with the machine key: HttpOnly, Secure, SameSite=Strict,
  path `/admin`; ends after 60 minutes idle or 12 hours. Changing the password or
  authenticator, or deactivating a user, ends their other sessions (security stamp).
- `/admin/setup` creates the first Super Admin, only while none exists and only with
  the server-side `Admin:SetupKey`.
- All admin pages are no-store and noindex; every form has an anti-forgery token.
- Changes are written to `dbo.audit_logs` (who, what, old and new values). Passwords,
  hashes and authenticator keys are never logged.

## Screens and permissions

| Screen | Address | Permission keys |
|---|---|---|
| Dashboard | `/admin` | any signed-in user |
| Admission inquiries | `/admin/inquiries` | `inquiries.view`, `inquiries.update` (status) |
| Messages | `/admin/messages` | `inquiries.view`, `inquiries.update` |
| Franchise applications | `/admin/franchise` | `franchise.view`, `franchise.update` (status), `franchise.export` (CSV) |
| Pages and sections | `/admin/pages` | `pages.view`, `pages.update` (drafts), `pages.publish` |
| News & Events | `/admin/news` | news: `news.view`, `.create`, `.update`, `.publish`; events: `events.view`, `.create`, `.update`, `.publish` |
| Campuses | `/admin/campuses` | `branches.view`, `.create`, `.update`, `.publish`, `.delete` (archive) |
| Menus | `/admin/menus` | `navigation.view`, `.create`, `.update` |
| Settings | `/admin/settings` | `settings.view`, `settings.update` |
| Users | `/admin/users` | `users.manage` |
| Change password | `/admin/account/password` | any signed-in user |

Super Admin has every permission. Role assignments limited to a region or campus
(`user_roles.region_id` / `branch_id`) are not used yet; only network-wide
assignments count.

## Rules built in

- Nothing is deleted: users are deactivated, campuses archived, menu items and
  page sections hidden.
- Only a Super Admin manages Super Admin accounts; there is always at least one
  active Super Admin; nobody can deactivate themselves.
- Page addresses cannot be changed. Section editing changes words, links, numbers
  and switches only; technical values stay as they are.
- Content Editors (pages.update without pages.publish) save drafts; a Content
  Manager or Super Admin publishes or discards them.
- News and event text is plain text (no HTML); a blank line starts a new paragraph.
  Without the publish permission, posts are saved as drafts.
- The About page has hidden "Our journey" (timeline) and "Our leadership" sections
  (script 08). Fill them in Pages > About Us, then press "Show on website".
- Links must start with `/`, `#`, `https://`, `mailto:` or `tel:`.

## Known limits (from the independent review, 31 October 2026)

- A new user, or a user whose two-step verification was reset, sets up the authenticator
  with the temporary password alone; there is no forced password change yet. Hand
  temporary passwords over privately and ask users to change them at once.
- Authenticator keys are stored unencrypted in `dbo.users` (protect database backups).
  The "each code only once" check is per server process.
- A publisher's save leaves a waiting draft in place; publishing that draft later
  replaces the publisher's change. The section page says when a draft is waiting.

## Not built yet

- Media library (uploading images), homepage editing (the homepage is in the code),
  gallery, downloads and careers screens; franchise form settings
  (accepting applications, start timelines) are read from `dbo.franchise_settings` but not editable yet.
- Region- or campus-limited roles.
- Viewing the audit log in the panel (it is in the database).
