#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۸۱ — تبدیل کامل صفات مرکب Razor به HrT.F:
  data-confirm="حذف قلم «@i.Title»؟"  →  data-confirm="@HrT.F("حذف قلم «{0}»؟", i.Title)"
توکنایزر: هر @ حفره است اگر @(...) متوازن یا @ident[.prop]*(call)? باشد؛ بقیه متن ثابت.
تبدیل فقط اگر ≥۱ حفره و متن ثابت فارسی. اجرا با آرگومان apply = اعمال واقعی.
"""
import os, re, sys

ROOT = "/home/z/my-project/aspnet/src"
SKIP_DIRS = {"bin", "obj", "wwwroot", "node_modules", ".git"}
FA = re.compile(r'[\u0600-\u06FF]')
APPLY = len(sys.argv) > 1 and sys.argv[1] == "apply"

IDENT_HOLE = re.compile(r'[A-Za-z_]\w*(?:\?\.\w+|\.\w+)*(?:\([^()]*\))?')
ATTR = re.compile(r'\b(data-modal-title|data-confirm|title|placeholder)="([^"]*)"')

def parse(val):
    """None یا (fmt, args) — fmt با {i}؛ شرط: ≥۱ حفره + متن ثابت فارسی"""
    fmt, args, static, i, n = [], [], [], 0, 0
    def flush():
        if static:
            fmt.append("".join(static))
            static.clear()
    while i < len(val):
        c = val[i]
        hole = None
        if c == "@" and i + 1 < len(val):
            nxt = val[i + 1]
            if nxt == "(":
                depth, j = 1, i + 2
                while j < len(val) and depth:
                    if val[j] == "(":
                        depth += 1
                    elif val[j] == ")":
                        depth -= 1
                    j += 1
                if depth == 0:
                    hole = val[i:j]
            else:
                m = IDENT_HOLE.match(val, i + 1)
                if m:
                    hole = val[i:m.end()]
        if hole:
            flush()
            fmt.append("{" + str(n) + "}")
            n += 1
            args.append(hole[1:])
            i += len(hole)
            continue
        static.append(c)
        i += 1
    flush()
    if n == 0 or not FA.search(" ".join(fmt)):
        return None
    return ("".join(fmt), args)

def build(attr, val):
    r = parse(val)
    if r is None:
        return None
    fmt, args = r
    return attr + '="@HrT.F("' + fmt.replace('"', '\\"') + '", ' + ", ".join(args) + ')"'

def fix_line(ln):
    out, pos, n = [], 0, 0
    for m in ATTR.finditer(ln):
        new = build(m.group(1), m.group(2))
        if new:
            out.append(ln[pos:m.start()])
            out.append(new)
            pos = m.end()
            n += 1
    out.append(ln[pos:])
    return ("".join(out), n)

changed = {}
for dp, dns, fns in os.walk(ROOT):
    dns[:] = [d for d in dns if d not in SKIP_DIRS]
    for fn in fns:
        if not fn.endswith(".cshtml"):
            continue
        p = os.path.join(dp, fn)
        src = open(p, encoding="utf-8").read()
        lines = src.split("\n")
        n = 0
        for i, ln in enumerate(lines):
            if "@" not in ln or not FA.search(ln):
                continue
            if "HrT.T(" in ln or "@T(" in ln or "HrT.F(" in ln:
                continue  # خطوط نیمه‌ترجمه — دستی/بعداً
            new, k = fix_line(ln)
            if k:
                lines[i] = new
                n += k
        if n:
            changed[os.path.relpath(p, ROOT)] = n
            if APPLY:
                open(p, "w", encoding="utf-8").write("\n".join(lines))

for f, n in sorted(changed.items(), key=lambda x: -x[1]):
    print(f"  {n:3d}  {f}")
print(f"TOTAL: {sum(changed.values())} attrs in {len(changed)} files {'APPLIED' if APPLY else '(dry)'}")
