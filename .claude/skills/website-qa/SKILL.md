---
name: website-qa
description: QA and test-report checklist for The Arqam Schools website - regression areas, security checks, responsive and accessibility checks, synthetic test data and honest reporting of what ran and what could not. Use after any change and before asking for review or opening a pull request.
---

# Website QA for The Arqam Schools

Grounded in `CLAUDE.md` and the current code. There is **no automated test suite or CI in the
repository yet**, so every check below is run by hand or by an ad-hoc script, and reported honestly.

## Ground rules

- **Never report a check as passed unless you ran it in this session and saw the result.**
  Quote the command or the steps, and the outcome.
- Use only a local or disposable database and **synthetic data** (made-up names,
  `0300-1234567`-style numbers, `@example.com` emails). Never real student, parent, staff or
  production data. Remove test rows afterwards.
- Do not deploy, do not connect to a live database, do not change files while reviewing.

## 1. Pick the checks that match the change

| Changed | Check at least |
|---|---|
| C# (`Controllers/`, `Data/`, `Infrastructure/`, `Helpers/`, `Models/`) | compile check, affected routes, forms, security list |
| Razor views / `site.css` / `site.js` | affected pages at 390 / 768 / 1024 / 1440 px, keyboard, reduced motion, brand |
| `RouteConfig` or `LegacyUrls` | every route below and every redirect |
| `Database/0N_*.sql` | run twice on a local DB, second run prints "already applied", backups exist, `/status` counts |
| Any text | `tas-branding` quick check |

## 2. Regression areas

- Routes: `/`, `/about`, `/academics`, `/admissions`, `/campuses`, `/campuses/faisalabad`,
  `/campuses/faisalabad/samanabad`, `/faq`, `/franchise`, an unknown path (404), `/sitemap.xml`,
  `/robots.txt`, `/status` (local only).
- Redirects (301): `/about/vision-mission`, `/about/philosophy`, `/about/values`,
  `/about/why-choose-us`, `/academics/levels`, `/academics/teaching`, `/branches/...` →
  `/campuses/...`, trailing slash removed, query string kept.
- Navigation: About Us, Academics, Admissions, Campuses as direct links; Student Life and Parents
  dropdowns; red header button = Franchise; mobile `<dialog>` menu opens, traps focus, closes on Escape.
- Campus directory: search, city and district filters, hostile input (`<script>`, `%`, `_`, `'`)
  is shown encoded and matches nothing unexpected.
- Forms (`POST /admissions/inquiry`, `POST /campuses/{city}/{slug}/message`): required-field
  errors, phone/email formats, consent, honeypot filled → no row saved, missing anti-forgery
  token → rejected, 6th post in 10 minutes → rate-limited message, valid post → row in
  `admission_inquiries` / `contact_messages` and redirect with `?sent=`.

## 3. Security

- No string-built SQL; `Html.Raw` only on safe content; links via `Site.IsSafeHref`.
- Anti-forgery, honeypot, rate limit and validation still on every form.
- Security headers in `Web.config` unchanged; `private` still hidden.
- No secrets, connection strings or personal data in the diff (`git diff`).

## 4. Compatibility

- C# 7.3 / .NET Framework 4.8 / MVC 5.3 only; no new packages; new files listed in the `.csproj`.

## 5. Accessibility and layout

- No horizontal scroll at 390 / 768 / 1024 / 1440 px; keyboard-only use works; visible focus;
  labels and `aria-invalid` / `aria-describedby` on errors; `prefers-reduced-motion: reduce`
  removes movement.

## 6. Report format

```
Verdict: ready | needs changes | blocked
Findings: High / Medium / Low — file:line, problem, realistic failure, suggested fix, owner agent
Ran: <command or steps> → <actual result>
Not run: <check> — <reason>
Always state: Visual Studio build, Razor view compilation, IIS run — done or not done.
```

On Linux these three Windows checks cannot run; say so every time.
