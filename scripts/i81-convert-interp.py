#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۸۱ — تبدیل امن پیام‌های درون‌یابی‌شدهٔ UI به HrT.F:
  ToastOk($"متن {x} متن")  →  ToastOk(HrT.F("متن {0} متن", x))
فقط حالت‌های ساده: حفره‌های {ident} / {ident.Prop} بدون فرمت‌کننده/کوتیشن/شرط.
اسامی مقصد: ToastOk/ToastErr/CreateErr/Err/AjaxResult/ToastDone/Errors.Add/Warnings.Add
"""
import os, re

ROOT = "/home/z/my-project/aspnet/src"
SKIP_DIRS = {"bin", "obj", "wwwroot", "node_modules", ".git"}
FA = re.compile(r'[\u0600-\u06FF]')

CALLS = r'(?:ToastOk|ToastErr|CreateErr|Err)\('
CALLS2 = r'(?:AjaxResult|ToastDone)\([^,)]*,'
ADD = r'(?:res\.)?(?:Errors|Warnings)\.Add\('
PAT = re.compile(r'(' + CALLS + r'|' + CALLS2 + r'|' + ADD + r')\s*\$"((?:[^"{]|\{[^{}]*\})*)"\)')
PAT_DONE = re.compile(r'(ToastDone\([^,)]*,)\s*\$"((?:[^"{]|\{[^{}]*\})*)"\s*,\s*("[^"]*")\)')

HOLE = re.compile(r'\{([A-Za-z_@][A-Za-z0-9_@.]*)\}')
BAD_HOLE = re.compile(r'\{[^}]*[(",:]|\{[^}]*\(')

def walk():
    for dp, dns, fns in os.walk(ROOT):
        dns[:] = [d for d in dns if d not in SKIP_DIRS]
        for fn in fns:
            if fn.endswith(".cs"):
                yield os.path.join(dp, fn)

def cesc(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')

def to_f(head, body, tail="))"):
    if not FA.search(body):
        return None
    if BAD_HOLE.search(body):
        return None
    holes = HOLE.findall(body)
    fmt = HOLE.sub(lambda h: "{" + str(holes.index(h.group(1))) + "}", body)
    args = ", ".join(holes)
    return head + 'HrT.F("' + cesc(fmt) + '"' + (", " + args if args else "") + tail

changed = {}
for p in walk():
    src = open(p, encoding="utf-8").read()
    new = PAT_DONE.sub(lambda m: to_f(m.group(1), m.group(2), ")," + m.group(3) + ")") or m.group(0), src)
    new = PAT.sub(lambda m: to_f(m.group(1), m.group(2)) or m.group(0), new)
    if new != src:
        n = sum(1 for _ in PAT.finditer(src)) + sum(1 for _ in PAT_DONE.finditer(src))
        changed[os.path.relpath(p, ROOT)] = n
        open(p, "w", encoding="utf-8").write(new)

print("Converted per file:")
for f, n in sorted(changed.items(), key=lambda x: -x[1]):
    print(f"  {n:3d}  {f}")
print(f"TOTAL: {sum(changed.values())} conversions in {len(changed)} files")
