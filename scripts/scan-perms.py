#!/usr/bin/env python3
"""Scan HasPerm usages and permission catalog."""
import os, re, json, collections

ROOT = "/home/z/my-project/aspnet/src"
SKIP = ("bin", "obj", "node_modules", ".git", "wwwroot/lib")

usages = {}   # perm -> list[(file, line)]
catalog_hits = []

for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in SKIP]
    for fn in filenames:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(dirpath, fn)
        try:
            with open(p, encoding="utf-8") as f:
                lines = f.readlines()
        except Exception:
            continue
        rel = os.path.relpath(p, ROOT)
        for i, ln in enumerate(lines, 1):
            for m in re.finditer(r'HasPerm\("([^"]+)"', ln):
                usages.setdefault(m.group(1), []).append((rel, i))

print("=== HasPerm usages: %d distinct keys ===" % len(usages))
for k in sorted(usages):
    print(k)
print()
print("=== per-file counts (top 40) ===")
byfile = collections.Counter()
for k, v in usages.items():
    for rel, i in v:
        byfile[rel] += 1
for rel, c in byfile.most_common(40):
    print(c, rel)
