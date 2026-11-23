#!/usr/bin/env python3
"""Inject RBAC guards into existing API route handlers (PLAN Phase 0 - P0.1/P0.7)."""
import re, pathlib

BASE = pathlib.Path("/home/z/my-project/src/app/api")

# route path -> {method: permission}
GUARDS = {
    "dashboard": {"GET": "dashboard.view"},
    "employees": {"GET": "employees.view", "POST": "employees.manage"},
    "employees/[id]": {"GET": "employees.view", "PATCH": "employees.manage", "DELETE": "employees.manage"},
    "org": {"GET": "org.view", "POST": "org.manage"},
    "shifts": {"GET": "attendance.view", "POST": "attendance.manage", "PATCH": "attendance.manage"},
    "attendance": {"GET": "attendance.view", "POST": "attendance.manage"},
    "leaves": {"GET": "attendance.view", "POST": "attendance.manage", "PATCH": "attendance.approve"},
    "missions": {"GET": "attendance.view", "POST": "attendance.manage", "PATCH": "attendance.approve"},
    "permits": {"GET": "attendance.view", "POST": "attendance.manage", "PATCH": "attendance.approve"},
    "zones": {"GET": "access.view", "POST": "access.manage", "PATCH": "access.manage"},
    "access": {"GET": "access.view", "POST": "access.manage", "PATCH": "access.manage", "PUT": "access.manage"},
    "visitors": {"GET": "visitors.view", "POST": "visitors.manage", "PATCH": "visitors.manage"},
    "contractors": {"GET": "visitors.view", "POST": "visitors.manage"},
    "alerts": {"GET": "access.view", "PATCH": "access.manage"},
    "contracts": {"GET": "employees.view", "POST": "employees.manage", "PATCH": "employees.manage"},
    "decrees": {"GET": "employees.view", "POST": "employees.manage", "PATCH": "employees.manage"},
    "seed": {"GET": "dashboard.view", "POST": "users.manage"},
}

IMPORT_LINE = 'import { guard } from "@/lib/auth";\n'
DYNAMIC_RE = re.compile(r'export const dynamic = "force-dynamic";\n')
HANDLER_RE = re.compile(r'(export async function (GET|POST|PATCH|PUT|DELETE)\([^)]*\)\s*(?::\s*[^{]+)?\{\n)')

changed = []
for rpath, perms in GUARDS.items():
    f = BASE / rpath / "route.ts"
    if not f.exists():
        print(f"SKIP (missing): {rpath}")
        continue
    src = f.read_text()
    if 'from "@/lib/auth"' in src:
        print(f"SKIP (guarded): {rpath}")
        continue
    # 1. import after dynamic line or after last import
    if DYNAMIC_RE.search(src):
        src = DYNAMIC_RE.sub(lambda m: m.group(0) + IMPORT_LINE, src, count=1)
    else:
        lines = src.splitlines(keepends=True)
        idx = max(i for i, l in enumerate(lines) if l.startswith("import "))
        lines.insert(idx + 1, IMPORT_LINE)
        src = "".join(lines)
    # 2. inject guard at start of each handler
    def repl(m):
        method = m.group(2)
        perm = perms.get(method)
        if not perm:
            return m.group(1)
        return (m.group(1) +
                f'  const denied = await guard(req, "{perm}");\n'
                f'  if (denied) return denied;\n')
    src, n = HANDLER_RE.subn(repl, src)
    if n == 0:
        print(f"WARN: no handlers matched in {rpath}")
        continue
    f.write_text(src)
    changed.append((rpath, n))

for r, n in changed:
    print(f"GUARDED {r}: {n} handlers")
print(f"Total: {len(changed)} files")
