"""
Exports the website's current content (no personal data, no test accounts)
from PostgreSQL (arqam_content) as a SQL Server 2025 seed script.
Writes 02_seed_data.sql and copies the referenced public images to media/.
"""
import json, os, re, shutil, subprocess, sys

D = sys.argv[1]
MEDIA_OUT = sys.argv[2]
STORAGE = "/home/claude/storage"

# Content and reference data only. Applications, messages, logs and the local
# test accounts are deliberately left out.
TABLES = [
    "permissions", "roles", "role_permissions",
    "regions", "districts", "cities", "class_levels", "facilities", "categories",
    "media_folders", "media", "seo_metadata",
    "branches", "branch_classes", "branch_facilities", "branch_images",
    "pages", "page_sections", "homepage_content", "navigation_menus", "navigation_items",
    "site_settings", "redirects", "testimonials",
    "news", "events", "gallery_albums", "gallery_images", "achievements", "downloads", "job_postings",
    "admission_cycles", "admission_stages",
    "franchise_settings", "head_offices",
]
SKIP_COLS = {"search"}
COMPUTED = {("permissions", "resource"), ("permissions", "action"), ("media", "visibility")}

def psql(q):
    return subprocess.check_output(["su", "postgres", "-c", f"psql -d arqam_content -At -c \"{q}\""]).decode()

coltypes = {}
for line in open(f"{D}/cols.txt", encoding="utf-8"):
    p = line.rstrip("\n").split("|")
    if len(p) < 13:
        continue
    coltypes.setdefault(p[0], {})[p[1]] = p[4]

# Columns that point at user accounts are cleared: the local test accounts are not exported.
USER_COLS = set()
for line in open(f"{D}/cons.txt", encoding="utf-8"):
    p = line.rstrip("\n").split("|", 3)
    if len(p) == 4 and p[2] == "f":
        m = re.match(r"FOREIGN KEY \((\w+)\) REFERENCES users\(id\)", p[3])
        if m:
            USER_COLS.add((p[0], m.group(1)))

def lit(v, udt):
    if v is None:
        return "NULL"
    if udt == "bool":
        return "1" if v else "0"
    if udt in ("int2", "int4", "int8", "numeric"):
        return str(v)
    if udt in ("jsonb", "_text"):
        v = json.dumps(v, ensure_ascii=False, separators=(",", ":"))
    return "N'" + str(v).replace("'", "''") + "'"

out = ["""-- =============================================================================
-- THE ARQAM SCHOOLS: CONTENT AND REFERENCE DATA (SQL Server 2025)
-- Exported from the current website by Database/tools/export_data.py.
-- Roles and permissions, menus, pages, homepage, settings, Samanabad Campus,
-- both head offices, franchise settings and the image records.
-- No applications, messages, logs or user accounts are included.
-- Run once on an empty database, after 01_schema.sql.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
"""]
# Some tables reference each other (branches <-> seo_metadata, media <-> branches),
# so constraints are switched off while loading and fully re-checked afterwards.
for t in TABLES:
    out.append(f"ALTER TABLE [dbo].[{t}] NOCHECK CONSTRAINT ALL;")

media_paths = []
counts = {}
for t in TABLES:
    order = "parent_id nulls first, sort_order" if t in ("navigation_items", "media_folders") else "1"
    if t == "media":
        rows_raw = psql(f"select row_to_json(x) from (select * from {t} where deleted_at is null and bucket = 'public-media') x")
    else:
        rows_raw = psql(f"select row_to_json(x) from (select * from {t} order by {order}) x")
    rows = [json.loads(l) for l in rows_raw.splitlines() if l.strip()]
    counts[t] = len(rows)
    if not rows:
        continue
    names = [c for c in rows[0] if c not in SKIP_COLS and (t, c) not in COMPUTED]
    out.append(f"\n-- {t} ({len(rows)})")
    collist = ", ".join(f"[{c}]" for c in names)
    for i in range(0, len(rows), 50):
        chunk = rows[i:i + 50]
        vals = ",\n  ".join("(" + ", ".join("NULL" if (t, c) in USER_COLS else lit(r[c], coltypes[t][c]) for c in names) + ")" for r in chunk)
        out.append(f"INSERT INTO [dbo].[{t}] ({collist}) VALUES\n  {vals};")
    if t == "media":
        media_paths = [r["storage_path"] for r in rows]

out.append("")
for t in TABLES:
    out.append(f"ALTER TABLE [dbo].[{t}] WITH CHECK CHECK CONSTRAINT ALL;")
out.append("\n-- Continue reference numbers after any existing ones.\nCOMMIT TRANSACTION;\nGO\n")
open(f"{D}/02_seed_data.sql", "w", encoding="utf-8").write("\n".join(out) + "\n")

copied = 0
for p in media_paths:
    src = os.path.join(STORAGE, "public-media", p)
    dst = os.path.join(MEDIA_OUT, "public-media", p)
    if os.path.exists(src):
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        copied += 1
    else:
        print("missing file", p)
print(json.dumps(counts))
print("media files copied", copied, "of", len(media_paths))
