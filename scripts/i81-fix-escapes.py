#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۸۱ — ترمیم escape دوگانه در بلاک فاز ۸۱ DictEn:
خطوط «["key"] = "value",» را unescape→esc صحیح می‌کند تا کوتیشن‌های درون کلید/مقدار نشکنند."""
import re

SRC = "/home/z/my-project/aspnet/src/HrPlatform.Core/Services/HrLang.cs"
MARK = "        // ---------- فاز ۸۱ — تکمیل پوشش پیام‌های سرویس/کنترلر (پیام چندزبانه) ----------"

UNESC = {"n": "\n", "r": "\r", "t": "\t", '"': '"', "\\": "\\", "'": "'"}

def unesc(s: str) -> str:
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c == "\\" and i + 1 < len(s):
            out.append(UNESC.get(s[i+1], s[i+1])); i += 2; continue
        out.append(c); i += 1
    return "".join(out)

def cesc(s: str) -> str:
    return (s.replace("\\", "\\\\").replace('"', '\\"')
             .replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t"))

LINE = re.compile(r'^\s*\["((?:\\.|[^"\\])*)"\]\s*=\s*"((?:\\.|[^"\\])*)"\s*,\s*$')

lines = open(SRC, encoding="utf-8").read().split("\n")
mi = next((i for i, l in enumerate(lines) if l.strip() == MARK.strip()), None)
if mi is None:
    raise SystemExit("marker not found")
fixed = skipped = 0
for i in range(mi + 1, len(lines)):
    l = lines[i]
    if not l.strip().startswith('["'):
        break  # پایان بلاک
    m = LINE.match(l)
    if not m:
        print("!! unparseable:", i + 1, l[:100]); continue
    k_raw, v_raw = m.group(1), m.group(2)
    # اگر unescape دوباره کاراکتر کوتیشن «خارج از escape صحیح» بدهد یعنی خراب است —
    # بازنویسی یکنواخت: unesc کامل سپس cesc کامل (idempotent)
    k, v = unesc(k_raw), unesc(v_raw)
    new = f'        ["{cesc(k)}"] = "{cesc(v)}",'
    if new != l:
        lines[i] = new; fixed += 1
    else:
        skipped += 1

open(SRC, "w", encoding="utf-8").write("\n".join(lines))
print(f"fixed={fixed} unchanged={skipped}")
