# Upstream source

- Skill: `impeccable`, version 4.8.0 (`package.json`), Claude Code build in `.claude/skills/impeccable/`
- Repository: https://github.com/pbakaus/impeccable
- Commit: `8409a564d1baee90b2dde4f7a8f06288838e3921` (2026-10-28)
- Author: Paul Bakaus
- License: Apache License 2.0, copied unchanged in `LICENSE`; third-party notices in `NOTICE.md`
  (the `reference/ios.md` and `reference/android.md` files derive from ehmo's
  `platform-design-skills`, MIT)

## Files taken
- `SKILL.md`
- `reference/` (42 entries, Markdown only)

## Deliberately not taken
- `scripts/` (shell launcher that downloads a prebuilt engine binary on first run, plus Node/JS
  browser helpers). Not reviewed for execution and not allowed by this project's rules.
- Upstream `.claude/settings.json` hooks (`SessionStart`, `PostToolUse` on Edit/Write, `Stop`), which
  would run the launcher automatically.
- Upstream helper agents in `.claude/agents/impeccable-*.md`.
- The npm CLI (`npx impeccable install`) and the Claude Code plugin marketplace route.

## Changes made for The Arqam Schools (2026-10-28) — modified files, as required by Apache-2.0 §4(b)
- `SKILL.md`: added `disallowed-tools: Bash` to the frontmatter, and inserted a
  "TAS project adaptation" note directly after the frontmatter. The upstream text is otherwise unchanged.
- No file in `reference/` was changed.
