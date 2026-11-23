#!/usr/bin/env python3
"""Find permission catalog/seed files (excluding duplicate trees)."""
import os, re

ROOT = "/home/z/my-project/aspnet/src"
SKIP = ("bin", "obj", "node_modules", ".git", "wwwroot", "finalverify")
targets = ["payroll.bonus.compute", "biometrics.transfer", "parking.barrier", "access.zones.manage"]
hits = {t: [] for t in targets}
group_re = re.compile(r'(Permissions?|Perms?|PermGroup|PermDef)')

for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in SKIP]
    for fn in filenames:
        p = os.path.join(dirpath, fn)
        ext = os.path.splitext(fn)[1].lower()
        if ext not in (".cs", ".cshtml", ".sql", ".json", ".js"):
            continue
        if ext == ".js" and "site" not in fn:
            continue
        try:
            with open(p, encoding="utf-8") as f:
                txt = f.read()
        except Exception:
            continue
        rel = os.path.relpath(p, ROOT)
        for t in targets:
            if t in txt:
                hits[t].append(rel)

for t in targets:
    print("###", t)
    for r in sorted(set(hits[t])):
        print("   ", r)
