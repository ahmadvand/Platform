#!/usr/bin/env python3
"""Compare HasPerm usages vs RbacService catalog + DbSeeder seeds."""
import os, re

ROOT = "/home/z/my-project/aspnet/src"
used = set()
for rel in ["Platform.Web", "modules", "Platform.Core"]:
    base = os.path.join(ROOT, rel)
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "node_modules")]
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            p = os.path.join(dirpath, fn)
            with open(p, encoding="utf-8") as f:
                txt = f.read()
            for m in re.finditer(r'HasPerm\("([^"]+)"', txt):
                used.add(m.group(1))

rbac = os.path.join(ROOT, "Platform.Core/Services/RbacService.cs")
txt = open(rbac, encoding="utf-8").read()
cat = set(re.findall(r'"([a-z]+\.[a-z.\-]+)"', txt))

seeder = os.path.join(ROOT, "Platform.Web/Data/DbSeeder.cs")
stxt = open(seeder, encoding="utf-8").read()
seeded = set(re.findall(r'"([a-z]+\.[a-z.\-]+)"', stxt))

print("used: %d | catalog: %d | seeder keys: %d" % (len(used), len(cat), len(seeded)))
print("\n== USED but NOT in catalog ==")
for k in sorted(used - cat):
    print("  MISSING-CAT:", k)
print("\n== USED but NOT in seeder ==")
for k in sorted(used - seeded):
    print("  MISSING-SEED:", k)
print("\n== In catalog but NEVER used ==")
for k in sorted(cat - used):
    print("  UNUSED:", k)
