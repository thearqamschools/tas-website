---
name: tas-branding
description: The Arqam Schools brand and content rules for the website - official name, approved figures, official contacts, tone, languages, logo, colours, type and imagery. Use whenever writing or reviewing visitor-facing text, page content, SEO text, images or visual design for this site.
---

# The Arqam Schools: brand and content rules

These rules come from `CLAUDE.md` and the existing site (`Content/site.css`, `_Header.cshtml`,
`_Footer.cshtml`). `CLAUDE.md` wins if anything here disagrees.

## Name and wording

- Always **The Arqam Schools**. Never "The Arqam School", "Arqam School", "Dar-e-Arqam" or "Darul Arqam".
- Visitor-facing text says **campus / campuses**, never "branch / branches" (the table is still `dbo.branches`).
- Use Pakistani educational terms: Play Group to Matric, Hifz, Hifz-e-Quran, parents, campus, academic session.

## Facts you may use

- **350+ campuses, 250,000+ students, 20,000+ Hafiz-e-Quran.** Do not change these.
- Head office: Sanora Colony, Samanabad, Faisalabad, Pakistan.
- Phone 041-2660265, 041-2406479; mobile / WhatsApp 0339-2406479; www.thearqam.com.
- Facebook: https://www.facebook.com/TASHeadOffice and https://www.facebook.com/thearqamfsdOfficial.
- The Lahore head office number in `Database/02_seed_data.sql` (042-36520008) is **unconfirmed**.

Never invent fees, dates, results, rankings, accreditations, partnerships, programmes, campus
details, testimonials or phone numbers. If something is missing, write a clear placeholder and
flag it as "needs confirmation".

## Positioning and tone

- Message: **Knowledge with values. Education with purpose.** Modern education rooted in Islamic
  values, with character, discipline and confidence. Balanced: not only religious, not only academic.
- Tone: professional, warm, respectful, clear, family-friendly. No guaranteed results, careers or
  exaggerated marketing. Official notices put clarity before creativity.
- Parent notices open with "Assalamualaikum, Respected Parents," and give date, time, required
  action, deadline and the official contacts.

## Languages

- English and Urdu only. Never Hindi or Devanagari script.
- Urdu: natural, professional Pakistani Urdu; "معزز والدین" for formal parent communication.
  Keep names, dates, phone numbers and class names clear.

## Logo

- Use `Content/brand/the-arqam-schools-logo-official.png` exactly as it is. Never redraw, recolour,
  crop, stretch, add effects to, or replace it. Keep its `alt` text and size attributes.

## Visual tokens (from `Content/site.css`)

- Colours: `--blue #1e3a8a` (brand navy, links, eyebrows), `--red #dc2626` (primary buttons, focus
  ring), `--ink #0f172a`, `--muted #475569`, `--line #e2e8f0`, `--grey #f1f5f9`, `--warm #f8f3e9`.
  Use the tokens, not new hex values. Check contrast (WCAG AA) for any new pairing.
- Type: "Source Serif 4" (`--serif`) for `.h1`/`.h2` headings, "Inter" (`--sans`) for everything
  else. Both are self-hosted in `Content/fonts/`; do not add web-font CDNs.
- Section tones: white, grey, warm, navy. Radius `--r-sm 6px`, `--r-md 12px`, `--r-lg 20px`.

## Imagery

- Real school photographs only, in `media/public-media/...`. Images whose alt text starts with
  "Placeholder" are stand-ins until the school sends real photos; never present them as real.
- Students look natural and age-appropriate, Pakistani, in neat modest uniforms; female students
  with a scarf/hijab where the context requires it. No stock-photo poses, no Western-only settings.
- Never use photos or names of real students, parents or staff in tests or demos.

## Quick check before you finish

1. Name written correctly everywhere?
2. Every figure, phone number and address matches the list above?
3. Tone fits the audience (parents, students, staff)?
4. Logo untouched; tokens used; contrast fine?
5. Anything unconfirmed clearly flagged?
