#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# فاز ۹۹ (بازسازی) — کپیِ extract_keys.py با مسیر اصلاح‌شدهٔ Platform.Core
# (نسخهٔ اصلی به‌عمد دست نمی‌خورد — مسیر قدیمی HrPlatform.Core منسوخ است)
import json
import re

SRC = "/home/z/my-project/aspnet/src/Platform.Core/Services/HrLang.cs"
OUT = "/home/z/my-project/scripts/langpacks/keys.json"

text = open(SRC, encoding="utf-8").read()

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
