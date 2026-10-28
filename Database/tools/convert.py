"""
Converts the live PostgreSQL schema of The Arqam Schools website (database
arqam_content) into a SQL Server 2025 script. Reads metadata dumped by psql
(cols.txt, cons.txt, idx.txt) and writes 01_schema.sql.

Rules (documented in the output header):
  uuid -> UNIQUEIDENTIFIER (DEFAULT NEWSEQUENTIALID()); timestamptz -> DATETIMEOFFSET(3);
  boolean -> BIT; jsonb / text[] -> NVARCHAR(MAX) + ISJSON check; enums -> NVARCHAR + CHECK IN;
  text -> NVARCHAR(n) from its length check, else NVARCHAR(450) when indexed, else NVARCHAR(MAX);
  regex checks -> REGEXP_LIKE (SQL Server 2025, compatibility level 170);
  ON DELETE SET NULL / RESTRICT -> NO ACTION (handled by the application; most rows are soft-deleted);
  ON DELETE CASCADE kept unless it would create a second cascade path (SQL Server error 1785).
"""
import re, sys, collections

D = sys.argv[1] if len(sys.argv) > 1 else "."
SKIP_TABLES = set()

ENUMS = {
    "achievement_type": "exam_result,student,award,sports,competition,other",
    "admission_status": "open,closed,waiting_list,opening_soon,contact_campus",
    "branch_status": "active,coming_soon,inactive",
    "building_plan": "own,rent,undecided",
    "category_type": "news,event,gallery,download",
    "contact_type": "general,admissions,careers,feedback,media,other",
    "content_status": "draft,in_review,published,archived",
    "employment_type": "full_time,part_time,contract,visiting,internship",
    "franchise_status": "new,under_review,contacted,meeting_scheduled,approved,rejected",
    "job_application_status": "received,shortlisted,interview,offered,hired,rejected,withdrawn",
    "job_status": "draft,published,closed,archived",
    "link_target": "self,blank",
    "media_visibility": "public,private",
    "message_status": "new,in_progress,resolved,spam",
}
DOMAINS = {
    "slug": ("NVARCHAR(120)", r"REGEXP_LIKE({c}, N'^[a-z0-9]+(-[a-z0-9]+)*$')"),
    "email_address": ("NVARCHAR(254)", r"REGEXP_LIKE({c}, N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')"),
    "phone_number": ("NVARCHAR(20)", r"REGEXP_LIKE({c}, N'^\+?[0-9 ()-]{{7,20}}$')"),
    "academic_year": ("NVARCHAR(7)", r"REGEXP_LIKE({c}, N'^[0-9]{{4}}-[0-9]{{2}}$')"),
}

# ---------------------------------------------------------------- load metadata
cols = collections.OrderedDict()
for line in open(f"{D}/cols.txt", encoding="utf-8"):
    p = line.rstrip("\n").split("|")
    if len(p) < 13:
        continue
    t, c, pos, dt, udt, dom, nullable, default, prec, scale, ident, gen, genexpr = p[:13]
    cols.setdefault(t, []).append(dict(name=c, dt=dt, udt=udt, dom=dom, null=nullable == "YES", default=default,
                                       prec=prec, scale=scale, ident=ident == "YES", gen=gen == "ALWAYS", genexpr=genexpr))

cons = collections.defaultdict(list)
for line in open(f"{D}/cons.txt", encoding="utf-8"):
    p = line.rstrip("\n").split("|", 3)
    if len(p) == 4 and p[0] != "-":
        cons[p[0]].append(dict(name=p[1], type=p[2], defn=p[3]))

idx = collections.defaultdict(list)
for line in open(f"{D}/idx.txt", encoding="utf-8"):
    p = line.rstrip("\n").split("|", 2)
    if len(p) == 3:
        idx[p[0]].append(dict(name=p[1], defn=p[2]))

def cols_of(defn):
    m = re.search(r"\(([^()]*)\)", defn)
    return [x.strip() for x in m.group(1).split(",")] if m else []

# columns used in keys/indexes need a bounded length
indexed = collections.defaultdict(set)
for t, lst in cons.items():
    for k in lst:
        if k["type"] in ("p", "u", "f"):
            m = re.match(r"(PRIMARY KEY|UNIQUE|FOREIGN KEY) \(([^)]*)\)", k["defn"])
            if m:
                for c in m.group(2).split(","):
                    indexed[t].add(c.strip())
        if k["type"] == "f":
            m = re.search(r"REFERENCES (\w+)\(([^)]*)\)", k["defn"])
            if m:
                for c in m.group(2).split(","):
                    indexed[m.group(1)].add(c.strip())
for t, lst in idx.items():
    for i in lst:
        if "USING gin" in i["defn"]:
            continue
        m = re.search(r"USING btree \((.*?)\)(?: WHERE|$)", i["defn"])
        if m:
            for part in m.group(1).split(","):
                cm = re.match(r"\s*(?:lower\(\()?(\w+)", part)
                if cm:
                    indexed[t].add(cm.group(1))

bool_cols = {t: {c["name"] for c in lst if c["udt"] == "bool"} for t, lst in cols.items()}

# ---------------------------------------------------------------- length checks -> column sizes
maxlen = collections.defaultdict(dict)
handled_checks = set()
for t, lst in cons.items():
    for k in lst:
        if k["type"] != "c":
            continue
        d = k["defn"]
        m = re.fullmatch(r"CHECK \(\(length\((\w+)\) <= (\d+)\)\)", d)
        if m:
            maxlen[t][m.group(1)] = int(m.group(2)); handled_checks.add((t, k["name"])); continue
        m = re.fullmatch(r"CHECK \(\(\(length\((\w+)\) >= (\d+)\) AND \(length\(\1\) <= (\d+)\)\)\)", d)
        if m:
            maxlen[t][m.group(1)] = int(m.group(3))
            k["defn"] = f"CHECK (LEN({m.group(1)}) >= {m.group(2)})" if int(m.group(2)) > 0 else None
            if k["defn"] is None:
                handled_checks.add((t, k["name"]))
        # length bound inside a compound check
        for mm in re.finditer(r"length\((\w+)\) <= (\d+)", d):
            maxlen[t].setdefault(mm.group(1), int(mm.group(2)))

# ---------------------------------------------------------------- expression translation
PG_LEFTOVERS = re.compile(r"::|~|jsonb_|ANY \(|ARRAY\[|num_nonnulls|cardinality|split_part|regexp_|\bVALUE\b")

def translate(expr, table):
    e = expr
    # casts
    e = re.sub(r"\('(-?\d+)'::integer\)::numeric", r"\1", e)
    e = re.sub(r"\((-?\d+)\)::numeric", r"\1", e)
    e = re.sub(r"'([^']*)'::(?:text|date|character varying|[a-z_]+)", lambda m: f"N'{m.group(1)}'", e)
    # regex
    e = re.sub(r"\((\w+) !~ N'((?:[^']|'')*)'\)", lambda m: f"(NOT REGEXP_LIKE({m.group(1)}, N'{m.group(2)}'))", e)
    e = re.sub(r"\((\w+) ~\* N'((?:[^']|'')*)'\)", lambda m: f"(REGEXP_LIKE({m.group(1)}, N'{m.group(2)}', 'i'))", e)
    e = re.sub(r"\((\w+) ~ N'((?:[^']|'')*)'\)", lambda m: f"(REGEXP_LIKE({m.group(1)}, N'{m.group(2)}'))", e)
    # json
    e = re.sub(r"jsonb_typeof\((\w+)\) = N'object'", r"ISJSON(\1, OBJECT) = 1", e)
    e = re.sub(r"jsonb_typeof\((\w+)\) = N'array'", r"ISJSON(\1, ARRAY) = 1", e)
    e = re.sub(r"\(jsonb_array_length\(\w+\) <= \d+\)", "(1 = 1)", e)  # list size checked by the application
    e = re.sub(r"\(cardinality\(\w+\) <= \d+\)", "(1 = 1)", e)
    # ANY(ARRAY[...]) -> IN (...)
    e = re.sub(r"(\w+) = ANY \(ARRAY\[([^\]]*)\]\)", lambda m: f"{m.group(1)} IN ({m.group(2)})", e)
    # num_nonnulls(a, b) -> count of non-null values
    e = re.sub(r"num_nonnulls\(([^)]*)\)", lambda m: "(" + " + ".join(f"(CASE WHEN {c.strip()} IS NULL THEN 0 ELSE 1 END)" for c in m.group(1).split(",")) + ")", e)
    e = re.sub(r"\blength\(", "LEN(", e)
    # bare boolean columns -> col = 1 (not when already compared)
    for b in bool_cols.get(table, ()):
        e = re.sub(rf"\bNOT {b}\b(?!\s*(=|<>|IS))", f"{b} = 0", e)
        e = re.sub(rf"(?<![\w.]){b}\b(?!\s*(=|<>|IS|,|\)\s*=))", f"{b} = 1", e)
        e = e.replace(f"{b} = 1 = 1", f"{b} = 1")
    if PG_LEFTOVERS.search(e):
        return None
    return bracket(e, table)

def bracket(e, table):
    """Wraps this table's column names in [ ] outside string literals (key, type, action are T-SQL keywords)."""
    names = sorted((c["name"] for c in cols[table]), key=len, reverse=True)
    pat = re.compile(r"(?<![\w\[.])(" + "|".join(map(re.escape, names)) + r")(?![\w\]])")
    parts = re.split(r"(N'(?:[^']|'')*'|'(?:[^']|'')*')", e)
    return "".join(p if i % 2 else pat.sub(r"[\1]", p) for i, p in enumerate(parts))

# ---------------------------------------------------------------- column types
def sql_type(t, c):
    n = c["name"]
    if c["udt"] == "uuid":
        return "UNIQUEIDENTIFIER"
    if c["udt"] == "timestamptz":
        return "DATETIMEOFFSET(3)"
    if c["udt"] == "bool":
        return "BIT"
    if c["udt"] in ("int4",):
        return "INT"
    if c["udt"] == "int8":
        return "BIGINT"
    if c["udt"] == "int2":
        return "SMALLINT"
    if c["udt"] == "date":
        return "DATE"
    if c["udt"] == "numeric":
        return f"DECIMAL({c['prec'] or 18},{c['scale'] or 0})"
    if c["udt"] in ("jsonb", "_text"):
        return "NVARCHAR(MAX)"
    if c["udt"] in ENUMS:
        return f"NVARCHAR({max(len(v) for v in ENUMS[c['udt']].split(',')) + 4})"
    if c["dom"] in DOMAINS:
        return DOMAINS[c["dom"]][0]
    if c["udt"] == "text":
        if n in maxlen[t]:
            # NVARCHAR(n) allows at most 4000; longer limits stay as length checks on NVARCHAR(MAX).
            return f"NVARCHAR({maxlen[t][n]})" if maxlen[t][n] <= 4000 else "NVARCHAR(MAX)"
        if n in indexed[t]:
            return "NVARCHAR(450)"
        return "NVARCHAR(MAX)"
    raise SystemExit(f"unmapped type {t}.{n} {c['udt']}")

def sql_default(c):
    d = c["default"]
    if not d:
        return None
    if d == "now()":
        return "SYSDATETIMEOFFSET()"
    if d == "gen_random_uuid()":
        return "NEWSEQUENTIALID()"
    if d in ("false", "true"):
        return "0" if d == "false" else "1"
    if re.fullmatch(r"-?\d+", d):
        return d
    if d in ("'{}'::jsonb",):
        return "N'{}'"
    if d in ("'[]'::jsonb", "'{}'::text[]"):
        return "N'[]'"
    m = re.fullmatch(r"'((?:[^']|'')*)'::\w+", d)
    if m:
        return f"N'{m.group(1)}'"
    raise SystemExit(f"unmapped default {c['name']}: {d}")

out = []
w = out.append
w("""-- =============================================================================
-- THE ARQAM SCHOOLS: DATABASE SCHEMA FOR SQL SERVER 2025
-- Generated from the website's PostgreSQL schema (20 migrations) by
-- Database/tools/convert.py. Run after 00_create_database.sql.
--
-- Type mapping: uuid -> UNIQUEIDENTIFIER, timestamptz -> DATETIMEOFFSET(3),
-- boolean -> BIT, jsonb and text[] -> NVARCHAR(MAX) holding JSON (ISJSON
-- checks), enums -> NVARCHAR with CHECK (... IN ...), text -> NVARCHAR(n).
-- Pattern checks use REGEXP_LIKE (SQL Server 2025, compatibility level 170).
--
-- Moved to the application (ASP.NET MVC), because SQL Server does them
-- differently: permission checks (PostgreSQL row level security), audit log,
-- status history, reference numbers, updated_at/updated_by, "media in use"
-- protection, and clearing references when a row is deleted (ON DELETE SET
-- NULL becomes NO ACTION; most content rows are soft-deleted anyway).
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
""")

skipped_checks = []
fks = []
for t, clist in cols.items():
    if t in SKIP_TABLES:
        continue
    lines = []
    for c in clist:
        n = c["name"]
        if c["udt"] == "tsvector":
            continue  # full-text search: see 03_fulltext.sql
        if c["gen"]:
            if t == "permissions" and n in ("resource", "action"):
                expr = "CAST(LEFT([key], CHARINDEX(N'.', [key]) - 1) AS NVARCHAR(100))" if n == "resource" else "CAST(SUBSTRING([key], CHARINDEX(N'.', [key]) + 1, 100) AS NVARCHAR(100))"
                lines.append(f"    [{n}] AS {expr} PERSISTED")
                continue
            if t == "media" and n == "visibility":
                lines.append("    [visibility] AS CAST(CASE WHEN bucket LIKE N'public-%' THEN N'public' ELSE N'private' END AS NVARCHAR(10)) PERSISTED")
                continue
            if t == "branches" and n == "area_slug":
                lines.append("    -- area_slug: set by the application from [area] (lowercase, hyphens)")
                lines.append("    [area_slug] NVARCHAR(120) NULL")
                continue
            raise SystemExit(f"unhandled generated column {t}.{n}")
        typ = sql_type(t, c)
        parts = [f"    [{n}] {typ}"]
        if c["ident"]:
            parts.append("IDENTITY(1,1)")
        parts.append("NULL" if c["null"] else "NOT NULL")
        d = None if c["ident"] else sql_default(c)
        if d:
            parts.append(f"CONSTRAINT [DF_{t}_{n}] DEFAULT {d}")
        lines.append(" ".join(parts))
    if t == "users":
        lines += [
            "    [password_hash] NVARCHAR(400) NULL",            # ASP.NET Identity PasswordHasher (PBKDF2)
            "    [security_stamp] NVARCHAR(100) NULL",
            "    [email_confirmed] BIT NOT NULL CONSTRAINT [DF_users_email_confirmed] DEFAULT 0",
            "    [two_factor_enabled] BIT NOT NULL CONSTRAINT [DF_users_two_factor_enabled] DEFAULT 0",
            "    [authenticator_key] NVARCHAR(200) NULL",        # TOTP secret (encrypted by the application)
            "    [lockout_end] DATETIMEOFFSET(3) NULL",
            "    [access_failed_count] INT NOT NULL CONSTRAINT [DF_users_access_failed_count] DEFAULT 0",
            "    [last_sign_in_at] DATETIMEOFFSET(3) NULL",
        ]
    # constraints
    for k in cons.get(t, []):
        if k["type"] == "p":
            pc = cols_of(k["defn"])
            lines.append(f"    CONSTRAINT [PK_{t}] PRIMARY KEY CLUSTERED ({', '.join(f'[{x}]' for x in pc)})")
    for c in clist:
        if c["udt"] in ENUMS:
            vals = ", ".join(f"N'{v}'" for v in ENUMS[c["udt"]].split(","))
            lines.append(f"    CONSTRAINT [CK_{t}_{c['name']}_values] CHECK ([{c['name']}] IN ({vals}))")
        if c["dom"] in DOMAINS:
            lines.append(f"    CONSTRAINT [CK_{t}_{c['name']}_format] CHECK ({DOMAINS[c['dom']][1].format(c='[' + c['name'] + ']')})")
        if c["udt"] == "jsonb" or c["udt"] == "_text":
            lines.append(f"    CONSTRAINT [CK_{t}_{c['name']}_json] CHECK ([{c['name']}] IS NULL OR ISJSON([{c['name']}], VALUE) = 1)")
    for k in cons.get(t, []):
        if k["type"] != "c" or (t, k["name"]) in handled_checks or not k["defn"]:
            continue
        d = k["defn"]
        if d == "CHECK (id)":
            lines.append(f"    CONSTRAINT [CK_{t}_single_row] CHECK ([id] = 1)")
            continue
        inner = re.fullmatch(r"CHECK \((.*)\)", d).group(1)
        tr = translate(inner, t)
        if tr is None:
            skipped_checks.append(f"{t}.{k['name']}: {d}")
            lines.append(f"    -- {k['name']}: validated in the application ({d})")
            continue
        lines.append(f"    CONSTRAINT [CK_{t}_{k['name'].removeprefix(t + '_')}] CHECK ({tr})")
    # turn comma separation right (comments must not get commas)
    body = []
    real = [l for l in lines if not l.lstrip().startswith("--")]
    last_real = real[-1]
    for l in lines:
        if l.lstrip().startswith("--"):
            body.append(l)
        else:
            body.append(l + ("" if l is last_real else ","))
    w(f"CREATE TABLE [dbo].[{t}] (\n" + "\n".join(body) + "\n);\nGO\n")
    for k in cons.get(t, []):
        if k["type"] == "f":
            fks.append((t, k))

# ---------------------------------------------------------------- unique constraints and indexes
nullable = {t: {c["name"] for c in lst if c["null"]} for t, lst in cols.items()}
w("-- Unique keys (nullable columns use filtered indexes, so several NULLs are allowed as in PostgreSQL) ----")
for t, lst in cons.items():
    for k in lst:
        if k["type"] != "u":
            continue
        uc = cols_of(k["defn"])
        nn = [] if "NULLS NOT DISTINCT" in k["defn"] else [c for c in uc if c in nullable[t]]
        where = (" WHERE " + " AND ".join(f"[{c}] IS NOT NULL" for c in nn)) if nn else ""
        w(f"CREATE UNIQUE NONCLUSTERED INDEX [UQ_{k['name']}] ON [dbo].[{t}] ({', '.join(f'[{c}]' for c in uc)}){where};")
w("GO\n")

def idx_where(cond, t):
    tr = translate(cond, t)
    if tr is None:
        return None
    tr = tr.replace("(", " ( ").replace(")", " ) ")
    tr = re.sub(r"\s+", " ", tr).strip()
    # strip redundant outer parentheses
    while tr.startswith("( ") and tr.endswith(" )"):
        depth = 0; ok = True
        for i, ch in enumerate(tr):
            depth += ch == "("; depth -= ch == ")"
            if depth == 0 and i < len(tr) - 1:
                ok = False; break
        if not ok:
            break
        tr = tr[2:-2]
    # filtered-index predicates may not contain parentheses (other than IN lists)
    return tr.replace("( ", "").replace(" )", "").replace("(", "").replace(")", "")

w("-- Indexes (PostgreSQL partial indexes become filtered indexes) ------------------------------")
skipped_idx = []
for t, lst in idx.items():
    for i in lst:
        d = i["defn"]
        if i["name"].endswith("_pkey") or any(k["name"] == i["name"] for k in cons.get(t, [])):
            continue
        if "USING gin" in d:
            skipped_idx.append(f"{i['name']} (full-text / trigram: see 03_fulltext.sql)")
            continue
        m = re.match(r"CREATE (UNIQUE )?INDEX (\w+) ON public\.(\w+) USING btree \((.*?)\)(?: WHERE (.*))?$", d)
        if not m:
            skipped_idx.append(f"{i['name']}: {d}"); continue
        uniq, name, _, colspec, cond = m.groups()
        cl = []
        for part in colspec.split(","):
            part = part.strip()
            cm = re.fullmatch(r"lower\(\((\w+)\)::text\)", part)
            if cm:
                cl.append(f"[{cm.group(1)}]")  # case-insensitive collation makes lower() unnecessary
                continue
            cm = re.fullmatch(r"(\w+)( DESC)?", part)
            if not cm:
                cl = None; break
            cl.append(f"[{cm.group(1)}]{cm.group(2) or ''}")
        if cl is None:
            skipped_idx.append(f"{name}: {d}"); continue
        where = ""
        if cond:
            wc = idx_where(cond, t)
            if wc is None or " OR " in wc:
                skipped_idx.append(f"{name}: filter not supported, created without it")
            else:
                where = f" WHERE {wc}"
        w(f"CREATE {'UNIQUE ' if uniq else ''}NONCLUSTERED INDEX [{name}] ON [dbo].[{t}] ({', '.join(cl)}){where};")
w("GO\n")

# ---------------------------------------------------------------- foreign keys (cascade path check)
cascade_edges = []  # (parent, child, fkname)
fk_out = []
for t, k in fks:
    if "REFERENCES auth." in k["defn"]:
        continue  # Supabase Auth: sign-in data now lives in [users] itself (see the columns added below)
    m = re.match(r"FOREIGN KEY \(([^)]*)\) REFERENCES (\w+)\(([^)]*)\)(.*)", k["defn"])
    fc, parent, pc, rest = m.groups()
    action = "NO ACTION"
    if "ON DELETE CASCADE" in rest:
        action = "CASCADE"
    fk_out.append([t, k["name"], fc, parent, pc, action])

def paths_ok(edges):
    # every (ancestor, table) pair reachable by at most one cascade path, and no cycles
    children = collections.defaultdict(list)
    for p, c, _ in edges:
        children[p].append(c)
    for start in list(children):
        count = collections.Counter()
        stack = [(start, frozenset([start]))]
        while stack:
            node, seen = stack.pop()
            for ch in children.get(node, []):
                if ch in seen:
                    return False
                count[ch] += 1
                if count[ch] > 1:
                    return False
                stack.append((ch, seen | {ch}))
    return True

downgraded = []
edges = []
for f in fk_out:
    if f[5] == "CASCADE":
        trial = edges + [(f[3], f[0], f[1])]
        if f[3] == f[0] or not paths_ok(trial):
            f[5] = "NO ACTION"; downgraded.append(f"{f[0]}.{f[1]}")
        else:
            edges = trial

w("-- Foreign keys ----------------------------------------------------------------------------")
for t, name, fc, parent, pc, action in fk_out:
    w(f"ALTER TABLE [dbo].[{t}] ADD CONSTRAINT [FK_{name}] FOREIGN KEY ({', '.join(f'[{x.strip()}]' for x in fc.split(','))}) "
      f"REFERENCES [dbo].[{parent}] ({', '.join(f'[{x.strip()}]' for x in pc.split(','))}) ON DELETE {action};")
w("GO\n")

w("-- Sequences for reference numbers (ARQ-2026-000001, FR-2026-00001), used by the application --")
w("CREATE SEQUENCE [dbo].[admission_reference_seq] AS BIGINT START WITH 1 INCREMENT BY 1;")
w("CREATE SEQUENCE [dbo].[franchise_reference_seq] AS BIGINT START WITH 1 INCREMENT BY 1;")
w("GO\n")

notes = ["", "/* Notes from the conversion", "   Checks left to the application:"] + [f"     - {s}" for s in skipped_checks] + \
        ["   Indexes not converted:"] + [f"     - {s}" for s in skipped_idx] + \
        ["   Cascades turned into NO ACTION (SQL Server allows one cascade path):"] + [f"     - {s}" for s in downgraded] + ["*/"]
w("\n".join(notes))
open(f"{D}/01_schema.sql", "w", encoding="utf-8").write("\n".join(out) + "\n")
print("tables", len(cols), "fks", len(fk_out), "cascade", len(edges), "downgraded", len(downgraded), "skipped checks", len(skipped_checks), "skipped idx", len(skipped_idx))
