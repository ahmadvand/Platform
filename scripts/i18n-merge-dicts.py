#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ادغام ترجمه‌های جدید در DictEn (HrLang.cs) و ref-en.json (TranslationStudio)"""
import json, re, time

TRANS = json.load(open("/tmp/en_translations.json", encoding="utf-8"))
HR = "/home/z/my-project/aspnet/src/HrPlatform.Core/Services/HrLang.cs"
REF = "/home/z/my-project/tools/TranslationStudio/Resources/ref-en.json"

def safe(v):
    """مقدار امن برای رشتهٔ C# — کوتیشن دابل و بک‌اسلش"""
    v = v.replace("\\", "\u2044").replace('"', "'").replace("\n", " ").replace("\r", " ").replace("\t", " ")
    return v.strip()

# --- ۱) DictEn ---
src = open(HR, encoding="utf-8").read()
existing = set(re.findall(r'^\s*\["([^"]+)"\]\s*=', src, re.M))
new = {k: safe(v) for k, v in TRANS.items() if k not in existing and v and v.strip()}
print(f"DictEn existing={len(existing)} new_to_add={len(new)}")

def sortkey(k):
    # مرتب‌سازی فارسی ساده بر اساس کد
    return [ord(c) for c in k]

lines = []
for k in sorted(new, key=sortkey):
    v = new[k]
    if len(k) + len(v) > 150:
        lines.append(f'        ["{k}"] =\n            "{v}",')
    else:
        lines.append(f'        ["{k}"] = "{v}",')
section = (
    "\n        // ---------- فاز ۸۰ — تکمیل پوشش ترجمه (دراپ‌داون‌ها، متن‌های ثابت UI، پیام‌های JS) ----------\n"
    + "\n".join(lines) + "\n"
)
marker = "    };\n}"
assert marker in src, "DictEn closing marker not found"
src = src.replace(marker, section + "    };\n}", 1)
open(HR, "w", encoding="utf-8").write(src)
print("HrLang.cs updated ->", len(re.findall(r'^\s*\["([^"]+)"\]\s*=', open(HR, encoding="utf-8").read(), re.M)), "keys")

# --- ۲) ref-en.json ---
ref = json.load(open(REF, encoding="utf-8"))
before = len(ref)
for k, v in TRANS.items():
    if k not in ref:
        ref[k] = safe(v)
json.dump(ref, open(REF, "w", encoding="utf-8"), ensure_ascii=False, indent=1, sort_keys=True)
print(f"ref-en.json {before} -> {len(ref)}")
