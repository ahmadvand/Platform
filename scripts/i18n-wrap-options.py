#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""wrap کامل متن استاتیک UI در ویوها با HrT.T — فاز جهانی‌سازی (گام ۲)
الگوها: option خالص، label خالص، th خالص، h2/h3/h4 خالص، button خالص،
<div class="hint"> خالص، <b> خالص (درون callout)، و صفات placeholder/title/data-modal-title استاتیک.
قواعد ایمنی: فقط متن بدون @ < " { } ' — بقیه دستی. کلیدها در /tmp/i18n_keys.json.
"""
import glob, re, os, json

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")
FA = re.compile(r"[\u0600-\u06FF]")

def has_fa(s): return bool(FA.search(s))

def ok_static(s):
    return bool(s.strip()) and has_fa(s) and not any(ch in s for ch in "@<>\"{}'`\\")

keys = []

def W(text):
    keys.append(text)
    return f'@HrT.T("{text}")'

stats = {}
files_changed = 0

def sub_count(name, src, rx, repl):
    n = 0
    def _r(m):
        nonlocal n
        text = m.group(2)
        if not ok_static(text):
            return m.group(0)
        n += 1
        return m.group(1) + W(text) + m.group(3)
    out = rx.sub(_r, src)
    stats[name] = stats.get(name, 0) + n
    return out, n

for p in glob.glob(os.path.join(ROOT, "**", "*.cshtml"), recursive=True):
    pp = p.replace(os.sep, "/")
    if any(x in pp for x in EXCLUDE):
        continue
    src = open(p, encoding="utf-8").read()
    before = src
    src, _ = sub_count("option", src, re.compile(r"(<option[^>]*>)([^<]*)(</option>)"), None)
    src, _ = sub_count("label", src, re.compile(r"(<label[^>]*>)([^<]*)(</label>)"), None)
    src, _ = sub_count("th", src, re.compile(r"(<th[^>]*>)([^<]*)(</th>)"), None)
    src, _ = sub_count("h", src, re.compile(r"(<h[234][^>]*>)([^<]*)(</h[234]>)"), None)
    src, _ = sub_count("button", src, re.compile(r"(<button[^>]*>)([^<]*)(</button>)"), None)
    src, _ = sub_count("hint", src, re.compile(r'(<div class="hint"[^>]*>)([^<]*)(</div>)'), None)
    src, _ = sub_count("b", src, re.compile(r"(<b>)([^<]*)(</b>)"), None)
    # صفات استاتیک — کوتیشن تکی برای اطمینان Razor
    def attr_repl(m):
        text = m.group(2)
        if not ok_static(text):
            return m.group(0)
        return f"{m.group(1)[:-1]}'{W(text)}'"
    for aname in ("placeholder", "title", "data-modal-title"):
        n0 = len(keys)
        src = re.sub(rf'({aname}=")([^"]*)"', attr_repl, src)
        stats[aname] = stats.get(aname, 0) + len(keys) - n0
    if src != before:
        open(p, "w", encoding="utf-8").write(src)
        files_changed += 1

json.dump(sorted(set(keys)), open("/tmp/i18n_keys.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
total = sum(stats.values())
print(f"files={files_changed} total_wrapped={total}")
for k, v in sorted(stats.items()):
    print(f"  {k}: {v}")
