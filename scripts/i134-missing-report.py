#!/usr/bin/env python3.13
# -*- coding: utf-8 -*-
# فاز ۱۱۳ — گزارش کلیدهای غایب هر بستهٔ زبان نسبت به: (۱) کلیدهای فاز ۱۱۲ (هاب داده/تقویم/
# محافظ صفحه/انتخابگر) و (۲) کلیدهای جدید فاز ۱۱۳ (ویزارد سه‌تبی + قفل محافظ صفحه)
import json, pathlib

ROOT = pathlib.Path("/home/z/my-project")
LP = ROOT / "aspnet/src/Platform.Web/App_Data/langpacks"

# کلیدهای فاز ۱۱۲ — از دیکشنری i127 (فقط کلیدها)
import re
i127 = (ROOT / "aspnet/src/Platform.Web/wwwroot/dl/source/scripts/i127-addkeys-phase112.py").read_text(encoding="utf-8")
sec = i127.split("NEW = {")[1].split("\n}\n")[0]
keys112 = re.findall(r'^\s*"((?:[^"\\]|\\.)*)":\s*\{', sec, re.M)
keys112 = [k.replace('\\"', '"') for k in keys112]

# کلیدهای فاز ۱۱۳ (سورس ویرایش‌شدهٔ این جلسه)
SRC113 = [
    ROOT / "aspnet/src/Platform.Web/Views/Setup/Index.cshtml",
    ROOT / "aspnet/src/Platform.Web/Views/Settings/Index.cshtml",
    ROOT / "aspnet/src/Platform.Web/Views/Shared/_Layout.cshtml",
    ROOT / "aspnet/src/Platform.Web/Modules/Core/Controllers/AccountController.cs",
    ROOT / "aspnet/src/Platform.Web/Modules/Core/Controllers/SettingsController.cs",
    ROOT / "aspnet/src/Platform.Web/wwwroot/js/site.js",
]
RX = re.compile(r'(?:HrT\.)?[TF]\(\s*"((?:[^"\\]|\\.)*)"|window\.hrT\(\s*\'((?:[^\'\\]|\\.)*)\'')
keys113 = {}
for f in SRC113:
    txt = f.read_text(encoding="utf-8")
    for m in RX.finditer(txt):
        k = (m.group(1) or m.group(2) or "").replace('\\"', '"').replace("\\'", "'")
        if k:
            keys113[k] = None
keys113 = list(keys113)

# بارگذاری بسته‌ها
packs = {}
for p in sorted(LP.glob("hr-lang-*.json")):
    code = p.stem.replace("hr-lang-", "")
    if code == "fa":
        continue
    packs[code] = json.loads(p.read_text(encoding="utf-8")).get("entries", {})

missing = {c: [k for k in keys112 if k not in e] for c, e in packs.items()}
print("کلیدهای فاز ۱۱۲:", len(keys112))
for c, e in sorted(packs.items(), key=lambda x: -len(x[1])):
    print(f"  {c}: کل فایل={len(e)} — غایب از فاز۱۱۲={len(missing[c])}")

m113 = {c: [k for k in keys113 if k not in e and k in INTEREST] if False else [k for k in keys113 if k not in e] for c, e in packs.items()}
# کلیدهای ۱۱۳ که واقعاً جدیدند (بقیهٔ T()های استخراج‌شده از قبل در فایل‌ها هستند)
OLD = set()
for c, e in packs.items():
    OLD.update(e.keys())
new113 = [k for k in keys113 if k not in OLD]
print("\nکلیدهای جدید فاز ۱۱۳:", len(new113))
for k in new113:
    print("  +", k[:100])
