#!/usr/bin/env python3
"""Audit controllers: list every action + its auth attributes; find unprotected ones."""
import os, re

ROOTS = [
    "/home/z/my-project/aspnet/src/Platform.Web",
    "/home/z/my-project/aspnet/src/modules",
]
SKIP = ("bin", "obj", "node_modules", "wwwroot")

action_re = re.compile(
    r'^(?:\s*)(public|private|protected)?\s*(?:async\s+)?(?:Task(?:<[^>]+>)?|IActionResult|'
    r'IEnumerable<[^>]+>|List<[^>]+>|async Task|void|JsonResult|FileResult|IFormFile|[A-Za-z<>?\[\]]+)\s+'
    r'([A-Z][A-Za-z0-9_]*)\s*\(')

for root in ROOTS:
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP]
        for fn in sorted(filenames):
            if not fn.endswith("Controller.cs"):
                continue
            p = os.path.join(dirpath, fn)
            rel = os.path.relpath(p, "/home/z/my-project/aspnet/src")
            lines = open(p, encoding="utf-8").read().splitlines()
            cls_perm = None
            for i, ln in enumerate(lines):
                m = re.search(r'\[HasPerm\("([^"]+)"(?:,\s*"([^"]+)")?\)\]', ln)
                if m and 'class' in "".join(lines[i:i+4]):
                    cls_perm = m.group(1)
            print(f"### {rel}  [class: {cls_perm}]")
            for i, ln in enumerate(lines):
                m = re.match(r'\s*(?:\[HasPerm\("([^"]+)"(?:,\s*"([^"]+)")?\)\]\s*)?public\s+(?:async\s+)?(?:Task<[^>]+>|IActionResult|IFormFile|void|JsonResult|FileResult|[A-Za-z]+Result|IEnumerable<[^>]+>|\w+)\s+([A-Z]\w+)\s*\(', ln)
                if not m:
                    continue
                perm = m.group(1)
                # look back up to 4 lines for attributes
                back = "\n".join(lines[max(0, i-4):i])
                if not perm and 'HasPerm' in back:
                    mm = re.search(r'HasPerm\("([^"]+)"(?:,\s*"([^"]+)")?\)', back)
                    if mm: perm = mm.group(1)
                anon = "AllowAnonymous" in back or "AllowAnonymous" in ln
                flags = []
                if perm: flags.append("PERM=" + perm)
                if anon: flags.append("ALLOW-ANON")
                if not perm and not anon and not cls_perm: flags.append("!! UNPROTECTED")
                if not perm and not anon and cls_perm: flags.append("(class)")
                print(f"   {m.group(3):38s} {' '.join(flags)}")
            print()
