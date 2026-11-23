#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۸۱ — ادغام ترجمه‌های /tmp/en_translations.json در DictEn (HrLang.cs) — idempotent"""
import json, re

SRC = "/home/z/my-project/aspnet/src/HrPlatform.Core/Services/HrLang.cs"
missing = json.load(open("/tmp/missing-keys.json", encoding="utf-8"))
trans = json.load(open("/tmp/en_translations.json", encoding="utf-8"))
# کلید خراب‌شدهٔ ترجمهٔ دستی
trans.setdefault("• {0} ({1}) — {2} دقیقه تأخیر", "• {0} ({1}) — {2} minutes late")

todo = [k for k in missing if k in trans and trans[k].strip()]
print(f"missing={len(missing)} translated={len(todo)}")

def cesc(s):
    return (s.replace("\\", "\\\\").replace('"', '\\"')
             .replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t"))

src = open(SRC, encoding="utf-8").read()

# کلیدهای موجود در DictEn — برای idempotency
m = re.search(r"(DictEn\s*=\s*new\(StringComparer\.Ordinal\)\s*\{)(.*?)(\n    \};)", src, re.S)
if not m:
    raise SystemExit("DictEn block not found")
head, body, tail = m.group(1), m.group(2), m.group(3)
pat = re.compile(r'^\s*\["((?:[^"\\]|\\.)*)"\]\s*=\s*"((?:[^"\\]|\\.)*)"\s*,\s*$', re.M)
existing = set()
for mm in pat.finditer(body):
    k = mm.group(1).replace('\\"', '"').replace("\\\\", "\\").replace("\\n", "\n")
    existing.add(k)

# توازن جای‌نگهدارها
def holes(s):
    return sorted(re.findall(r"\{(\d+)", s))
bad = [k for k in todo if k in existing]
new_lines = []
skipped = []
for k in todo:
    if k in existing:
        skipped.append(k); continue
    if holes(k) != holes(trans[k]):
        print("!! placeholder mismatch:", k[:60], "->", trans[k][:60]); continue
    new_lines.append(f'        ["{cesc(k)}"] = "{cesc(trans[k])}",')

marker = "        // ---------- فاز ۸۱ — تکمیل پوشش پیام‌های سرویس/کنترلر (پیام چندزبانه) ----------"
block_start = src.find(marker)
if block_start == -1:
    insert_block = marker + "\n" + "\n".join(new_lines) + "\n"
    src = src[:m.end(1)] + "\n" + insert_block + body + tail
else:
    # جایگذاری مجدد بلاک (idempotent)
    end = src.find("\n    };", block_start)
    old_block = src[block_start:end]
    old_keys = set(re.findall(r'\["((?:[^"\\]|\\.)*)"\]', old_block))
    fresh = [l for l in new_lines if re.match(r'^\s*\["((?:[^"\\]|\\.)*)"\]', l).group(1) not in old_keys]
    if fresh:
        src = src[:block_start] + old_block.rstrip("\n") + "\n" + "\n".join(fresh) + src[end:]

open(SRC, "w", encoding="utf-8").write(src)
print(f"added {len(new_lines) - len(skipped)} new keys (skipped already-present: {len(skipped)})")
