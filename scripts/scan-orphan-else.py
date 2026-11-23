#!/usr/bin/env python3
"""Scan all Razor views for orphan 'else' / 'else if' lines that are NOT
preceded by a closing brace — i.e. continuation lines that can't attach to
any @if chain (they render as literal text — the Access/Index.cshtml bug)."""
import glob, os

VIEWS = "/home/z/my-project/aspnet/src/HrAttendance.Web/Views"
issues = 0
for path in glob.glob(os.path.join(VIEWS, "**", "*.cshtml"), recursive=True):
    with open(path, encoding="utf-8") as fh:
        lines = fh.read().split("\n")
    for i, line in enumerate(lines):
        s = line.strip()
        if s == "else" or s.startswith("else if"):
            if i == 0:
                issues += 1
                print(f"{path}:1 FIRST LINE ORPHAN: {s!r}")
            else:
                prev = lines[i - 1].strip()
                if not prev.endswith("}"):
                    issues += 1
                    print(f"{path}:{i+1}: prev={prev[:60]!r} -> {s[:60]!r}")
print(f"scan complete — {issues} potential orphan(s) found")
