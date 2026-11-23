#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۸۱ — اسکن رشته‌های فارسیِ ترجمه‌نشده در کد C#.
خروجی: فایل/خط/متن — فقط مواردی که داخل HrT.T/HrT.F نباشند.
فایل‌های مستثنی: HrLang.cs (دیکشنری)، Jalali.cs (نام ماه/روز)، داده‌های ثابت.
"""
import os, re, json, sys

ROOT = "/home/z/my-project/aspnet/src"
FA = re.compile(r'[\u0600-\u06FF]')
EXCLUDE_FILES = ("HrLang.cs", "Jalali.cs")
# متدهایی که رشتهٔ فارسی می‌پذیرند و خودش ترجمه می‌کنند
WRAP_OK = ("HrT.T(", "HrT.F(", "HrT.Digits(", "HrT.Num(", "Tr(", "Translate(")

results = []
for dirpath, dirs, files in os.walk(ROOT):
    if any(x in dirpath for x in ("/obj/", "/bin/")):
        continue
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        if fn in EXCLUDE_FILES:
            continue
        path = os.path.join(dirpath, fn)
        try:
            with open(path, encoding="utf-8") as f:
                lines = f.readlines()
        except Exception:
            continue
        for i, line in enumerate(lines):
            s = line.strip()
            if s.startswith("//") or s.startswith("///") or s.startswith("*"):
                continue
            # فقط داخل string literal
            for m in re.finditer(r'"([^"]*)"', line):
                if not FA.search(m.group(1)):
                    continue
                before = line[:m.start()]
                # در همان خط قبل از literal داخل HrT باشد؟
                if any(w in line for w in WRAP_OK):
                    # اگر کل literal داخل فراخوانی HrT است → قبول؛ تقریب: HrT در خط موجود
                    if any(w in before for w in WRAP_OK) or re.search(r'HrT\.[TF]\([^)]*$', before):
                        break_flag = False
                        continue
                # رشتهٔ data-محور؟ (مقایسه enum / مقدار پایگاه داده) — خط شامل == یا switch یا case
                if re.search(r'(==|!=|case\s|switch|\.StartsWith|\.Contains|\.Equals)\s*$', before) or 'case ' in line[:m.start()]:
                    continue
                results.append({
                    "file": path.replace(ROOT + "/", ""),
                    "line": i + 1,
                    "text": m.group(1)[:120],
                })
                break  # یک مورد در هر خط کافی است

out = "/home/z/my-project/scripts/audit-i18n-result.json"
json.dump(results, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
by_file = {}
for r in results:
    by_file[r["file"]] = by_file.get(r["file"], 0) + 1
for f, n in sorted(by_file.items(), key=lambda kv: -kv[1]):
    print(f"{n:4d}  {f}")
print("TOTAL:", len(results))
