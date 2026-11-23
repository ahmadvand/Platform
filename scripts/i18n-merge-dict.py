#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Merge EN translations into HrLang.DictEn (new block) and rebuild ref-en.json."""
import json, re, sys, importlib.util

def load_batch(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return getattr(m, name)

B1 = load_batch("/home/z/my-project/scripts/i18n-batch1.py", "B1")
B2 = load_batch("/home/z/my-project/scripts/i18n-batch2.py", "B2")
B3 = load_batch("/home/z/my-project/scripts/i18n-batch3.py", "B3")
TR = {}
for b in (B1, B2, B3): TR.update(b)

missing = json.load(open("/home/z/my-project/scripts/missing-keys.json", encoding="utf-8"))
translated = {k: TR[k] for k in missing if k in TR and TR[k] != k}
untranslated = [k for k in missing if k not in translated]
print("missing:", len(missing), "translated:", len(translated), "left untranslated:", len(untranslated))

HL = "/home/z/my-project/aspnet/src/HrPlatform.Core/Services/HrLang.cs"
src = open(HL, encoding='utf-8').read()

def esc(s):
    return s.replace('\\', '\\\\').replace('"', '\\"')

# insert a new block right before the final "    };"
block_lines = [
    "",
    "        // ---------- فاز ۸۱b — تکمیل پوشش فاز ۸۱ (PDF/Excel/Setup/کلیدها/مقادیر مرجع) ----------",
]
for k in sorted(translated):
    block_lines.append(f'        ["{esc(k)}"] = "{esc(translated[k])}",')
block = "\n".join(block_lines) + "\n"

idx = src.rfind("    };")
assert idx > 0
src = src[:idx] + block + "\n" + src[idx:]
open(HL, "w", encoding="utf-8").write(src)
print("HrLang.cs updated; new block entries:", len(translated))

# ---- rebuild ref-en.json from the full DictEn in HrLang.cs ----
entries = {}
dict_start = src.find("DictEn = new")
body = src[dict_start:]
for m in re.finditer(r'\["((?:[^"\\]|\\.)*)"\]\s*=\s*"((?:[^"\\]|\\.)*)"', body):
    k = m.group(1).replace('\\"', '"').replace('\\\\', '\\')
    v = m.group(2).replace('\\"', '"').replace('\\\\', '\\')
    entries[k] = v
out = "/home/z/my-project/tools/TranslationStudio/Resources/ref-en.json"
json.dump(entries, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1, sort_keys=True)
print("ref-en.json rebuilt:", len(entries), "entries")

# leftovers for review
json.dump(untranslated, open("/home/z/my-project/scripts/untranslated-keys.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("untranslated list saved:", len(untranslated))
