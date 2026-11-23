#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ممیزی دقیق کلیدهای هر بستهٔ زبان نسبت به DictEn (مرجع: HrLang.cs).
خروجی: شمار کلیدهای جاافتادهٔ هر زبان + ذخیرهٔ فهرست جاافتاده‌ها در missing/<code>.json
"""
import json, os, re

SRC = "/home/z/my-project/aspnet/src/Platform.Core/Services/HrLang.cs"
PACKS = "/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks"
KEYS_JSON = "/home/z/my-project/scripts/langpacks/keys.json"
MISSING_DIR = "/home/z/my-project/scripts/langpacks/missing"

src = open(SRC, encoding="utf-8").read()
start = src.index("DictEn")
start = src.index("{", start)
depth = 0
end = start
for i, ch in enumerate(src[start:], start):
    if ch == "{":
        depth += 1
    elif ch == "}":
        depth -= 1
        if depth == 0:
            end = i
            break
body = src[start:end]
pairs = re.findall(r'\[\s*"(.*?)"\s*\]\s*=\s*"(.*?)"\s*,', body, re.S)
dicten = {}
for k, v in pairs:
    k = k.replace('\\"', '"').replace("\\\\", "\\")
    v = v.replace('\\"', '"').replace("\\\\", "\\")
    dicten[k] = v
print(f"DictEn (HrLang.cs): {len(dicten)} keys")

try:
    keys = json.load(open(KEYS_JSON, encoding="utf-8"))
    key_list = [it["fa"] if isinstance(it, dict) else it for it in keys]
    print(f"keys.json: {len(key_list)} keys — set-diff vs DictEn: "
          f"only_in_keysjson={len(set(key_list)-set(dicten))}, only_in_dicten={len(set(dicten)-set(key_list))}")
except Exception as e:
    print(f"keys.json: ERR {e}")

os.makedirs(MISSING_DIR, exist_ok=True)
total_missing = 0
for f in sorted(os.listdir(PACKS)):
    if not f.endswith(".json"):
        continue
    code = f.replace("hr-lang-", "").replace(".json", "")
    d = json.load(open(os.path.join(PACKS, f), encoding="utf-8"))
    ent = d.get("entries", {})
    missing = [k for k in dicten if k not in ent]
    extra = [k for k in ent if k not in dicten]
    total_missing += len(missing)
    with open(os.path.join(MISSING_DIR, f"{code}.json"), "w", encoding="utf-8") as fh:
        json.dump(missing, fh, ensure_ascii=False, indent=0)
    print(f"{code:4s}: entries={len(ent):5d}  missing={len(missing):5d}  extra={len(extra)}")
print(f"TOTAL missing: {total_missing}")
