#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""پست-پس: هر <option> بدون صفت value که متنش @HrT.T("X") شده → value="X" اضافه می‌شود
تا مقدار پست‌شده همیشه مقدار خام (زبان-مستقل) بماند و فقط برچسب ترجمه شود."""
import glob, re, os

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")

pat = re.compile(r"<option((?:(?!value=)[^>])*)>@HrT\.T\(\"([^\"]*)\"\)</option>")
fixed = 0
files = 0
for p in glob.glob(os.path.join(ROOT, "**", "*.cshtml"), recursive=True):
    pp = p.replace(os.sep, "/")
    if any(x in pp for x in EXCLUDE):
        continue
    src = open(p, encoding="utf-8").read()
    def _r(m):
        attrs, raw = m.group(1), m.group(2)
        return f'<option value="{raw}"{attrs}>{raw}</option>' if False else f'<option{attrs} value="{raw}">@HrT.T("{raw}")</option>'
    new, n = pat.subn(_r, src)
    if n:
        open(p, "w", encoding="utf-8").write(new)
        fixed += n
        files += 1
print(f"files={files} options_value_fixed={fixed}")
