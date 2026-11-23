#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۸۱ — طبقه‌بندی هیت‌های ممیزی i18n:
  cmp     = مقایسه/سوییچ (مقدار کانونیک DB — نباید ترجمه شود)
  entity  = انتساب به خاصیت موجودیت/ذخیره در DB (کانونیک)
  seed    = فایل سیدر (دادهٔ نمایشی)
  log     = پیام لاگر (غیر UI)
  ui      = پیام کاربر (Toast/Json/خطا/برچسب) — باید HrT شود
خروجی: JSON طبقه‌بندی‌شده برای بازبینی
"""
import json, re

RES = json.load(open("/home/z/my-project/scripts/audit-i18n-result.json", encoding="utf-8"))
ROOT = "/home/z/my-project/aspnet/src/"

CMP_PAT = re.compile(r'(==|!=|case\s|switch|StartsWith|Contains\(|Equals\()')
WRITE_PAT = re.compile(r'(\.\w+\s*=\s*"(?:[^"\\]|\\.)*[\u0600-\u06FF]|new\s+\w+\s*{[^}]*=\s*"[^"]*[\u0600-\u06FF]|Status\s*=|Title\s*=\s*")')
UI_PAT = re.compile(r'(Toast(Ok|Err)|Json\(|message\s*=|Message\s*=|Error[s]?\s*=|Warnings?\s*\.Add|Errors?\s*\.Add|reason|Reason|Add\(HrT|ViewBag\.\w+\s*=|return\s+(Content|BadRequest|NotFound)|Set\(.*"|Alert|hint|Hint|label|Label|title|Title)')

def classify(path, line_no, text):
    if "/Data/" in path and ("Seeder" in path or "Seed" in path.split("/")[-1]):
        return "seed"
    if "Seeder" in path.split("/")[-1]:
        return "seed"
    try:
        with open(ROOT + path, encoding="utf-8") as f:
            lines = f.readlines()
        ln = lines[line_no - 1] if line_no <= len(lines) else ""
    except Exception:
        return "ui"
    if CMP_PAT.search(ln):
        return "cmp"
    if WRITE_PAT.search(ln):
        return "entity"
    if UI_PAT.search(ln):
        return "ui"
    return "ui"  # پیش‌فرض: بررسی دستی

out = {"cmp": {}, "entity": {}, "seed": {}, "ui": {}}
for path, hits in RES["cs"].items():
    for h in hits:
        line_no = h[0] if isinstance(h, list) and isinstance(h[0], int) else h[0]
        # ساختار audit: [line, text, kind] یا [line, text]
        txt = h[1] if len(h) > 1 else ""
        cat = classify(path, line_no, txt)
        out[cat].setdefault(path, []).append([line_no, txt])

for cat in out:
    n = sum(len(v) for v in out[cat].values())
    print(f"{cat:8s}: {n:4d} hits in {len(out[cat])} files")
json.dump(out, open("/home/z/my-project/scripts/audit-i18n-classified.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
