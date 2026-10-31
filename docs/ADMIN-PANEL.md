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
| Pages and sections | `/admin/pages` | `pages.view`, `pages.update` (drafts), `pages.publish` |
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
- Links must start with `/`, `#`, `https://`, `mailto:` or `tel:`.

## Not built yet

- Media library (uploading images), homepage editing (the homepage is in the code),
  news, events, gallery, downloads, careers and franchise screens.
- Region- or campus-limited roles.
- Viewing the audit log in the panel (it is in the database).
