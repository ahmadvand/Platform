#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""استخراج جفت‌های کلید فارسی ← ترجمهٔ انگلیسی از HrLang.cs (دیکشنری DictEn)."""
import json
import re
import sys

SRC = "/home/z/my-project/aspnet/src/HrPlatform.Core/Services/HrLang.cs"
OUT = "/home/z/my-project/scripts/langpacks/keys.json"

text = open(SRC, encoding="utf-8").read()

# جفت‌های ["فارسی"] = "English" — معناشناسی ایندکسر C#: تکرار کلید = مقدار آخر
pairs = re.findall(r'\[\s*"((?:[^"\\]|\\.)*)"\s*\]\s*=\s*"((?:[^"\\]|\\.)*)"', text)


def unescape(s: str) -> str:
    return (s.replace('\\"', '"').replace("\\\\", "\\")
             .replace("\\n", "\n").replace("\\t", "\t").replace("\\r", "\r"))


d = {}
order = []
for fa, en in pairs:
    fa, en = unescape(fa), unescape(en)
    if fa not in d:
        order.append(fa)
    d[fa] = en

items = [{"i": n, "fa": fa, "en": d[fa]} for n, fa in enumerate(order)]
json.dump(items, open(OUT, "w", encoding="utf-8"), ensure_ascii=False)
print(f"total unique keys: {len(items)}")
for it in items[:3] + items[len(items)//2:len(items)//2+2]:
    print(f"  [{it['i']}] {it['fa'][:60]}  =>  {it['en'][:60]}")
