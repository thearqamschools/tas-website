# Third-party skills in this folder

| Folder | Upstream | Commit | License |
|---|---|---|---|
| `impeccable/` | https://github.com/pbakaus/impeccable | `8409a564d1baee90b2dde4f7a8f06288838e3921` (v4.8.0) | Apache-2.0 (+ MIT notice for ehmo/platform-design-skills) |
| `emil-design-eng/`, `review-animations/`, `animation-performance/`, `mobile-native/`, `break-ui/` | https://github.com/emilkowalski/skills | `2d228630ba1446365f614a108cb8252f1feda202` | MIT |

Each folder keeps its upstream `LICENSE` and an `UPSTREAM.md` listing exactly what was taken and
what was changed. Only Markdown instructions were copied: no scripts, binaries, hooks or packages.

**Not included:** Taste Skill (https://github.com/Leonxlnx/taste-skill, MIT, commit
`12af471f627f866411b4bd785b19646d3672784c`, v2.1.5). Its workflow depends on running eight Node 22
scripts with a headless Chrome and on downloading images from open collections, and it deals a new
art direction for each build. That conflicts with this project's rules (no unreviewed code, no new
runtime dependencies, keep the existing design, real school photos only).

`tas-branding/`, `aspnet-mvc-standards/` and `website-qa/` are written for this project.
