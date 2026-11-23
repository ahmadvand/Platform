#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""تکمیل جهانی‌سازی دراپ‌داون‌ها و متن‌های ثابت UI

ترنسفورم‌های امن:
  fix1) <option> بدون value که متنش @HrT.T("X") است → درج value="X"
        (مقدار کانونیک فارسی؛ در زبان انگلیسی فرم مقدار درست پست می‌کند)
  fix2a) placeholder/title/data-modal-title استاتیک → wrap با @HrT.T
         (attribute دابل‌کوتیشن به تک‌کوتیشن تبدیل می‌شود تا Razor نشکند)
  fix2b) متن استاتیک بین > و < (بدون @ { } " ') → @HrT.T("...")
  fix2c) پیشوند استاتیک قبل از @  — «کل مخزن: @HrT.Num(x)»
  fix2d) پسوند استاتیک بعد از @HrT.Xxx(...) — «@HrT.Num(x) دقیقه</td>»
کلیدهای جدید → /tmp/i18n_new_keys.json
"""
import re, glob, os, json

ROOT = "/home/z/my-project/aspnet/src"
EXCLUDE = ("/wwwroot/dl/", "/bin/", "/obj/")
FA_LETTER = re.compile(r'[\u0621-\u064A\u066E-\u06D3\u0698\u067E\u0686\u06AF\u06CC\u06A9]')
FA = re.compile(r'[\u0600-\u06FF]')

keys = set()

def note(k):
    k = k.strip()
    if k and FA.search(k) and len(k) >= 2:
        keys.add(k)

def ok_static(s):
    """متن کاملاً استاتیک و امن برای wrap مستقیم"""
    return bool(FA_LETTER.search(s)) and not re.search(r'[@{}<>"\'\\]', s)

# ---------- fix1 ----------
OPT_T = re.compile(r'(<option\b([^>]*)>)(\s*@HrT\.T\("([^"]+)"\))')

def fix1(src):
    n = 0
    def repl(m):
        nonlocal n
        attrs, key = m.group(2), m.group(4)
        if 'value=' in attrs or not key.strip():
            return m.group(0)
        note(key)
        n += 1
        return f'<option value="{key}"{attrs}>{m.group(3)}'
    return OPT_T.sub(repl, src), n

# ---------- fix2a: attributeهای استاتیک ----------
ATTR = re.compile(r"""((?:placeholder|title|data-modal-title)\s*=\s*)(["'])([^"'@]*[\u0600-\u06FF][^"'@]*)\2""")

def attr_fix(ln):
    def repl(m):
        val = m.group(3).strip()
        if not ok_static(val):
            return m.group(0)
        note(val)
        return f"{m.group(1)}'@HrT.T(\"{val}\")'"
    return ATTR.sub(repl, ln)

# ---------- fix2b: متن استاتیک بین تگ‌ها ----------
TAG_TEXT = re.compile(r'>([^<>@\n]{2,})<')

def tag_fix(ln):
    def repl(m):
        frag = m.group(1).strip()
        if len(frag) > 160 or not ok_static(frag):
            return m.group(0)
        return ">" + f'@HrT.T("{frag}")' + "<"
    return TAG_TEXT.sub(repl, ln)

# ---------- fix2c: پیشوند استاتیک قبل از @ ----------
PREFIX_AT = re.compile(r'>([^<>@\n{}"]*[\u0600-\u06FF][^<>@\n{}"]*?)\s*(@[^<>\n]+</)')

def prefix_fix(ln):
    def repl(m):
        frag = m.group(1).strip()
        if not ok_static(frag):
            return m.group(0)
        return ">" + f'@HrT.T("{frag}")' + " " + m.group(2)
    return PREFIX_AT.sub(repl, ln)

# ---------- fix2d: پسوند استاتیک بعد از @HrT.Xxx(...) ----------
SUFFIX_AT = re.compile(r'(@HrT\.[A-Za-z]+\([^()<>\n]{1,48}\))\s+([^<>@\n{}"]*[\u0600-\u06FF][^<>@\n{}"]*?)\s*(</)')

def suffix_fix(ln):
    def repl(m):
        frag = m.group(2).strip()
        if not ok_static(frag):
            return m.group(0)
        return m.group(1) + " " + f'@HrT.T("{frag}")' + m.group(3)
    return SUFFIX_AT.sub(repl, ln)

def fix2(src):
    n = 0
    lines = src.split("\n")
    for i, ln in enumerate(lines):
        orig = ln
        if ln.lstrip().startswith("@:") or "<code" in ln or 'dir="ltr"' in ln:
            continue
        ln = attr_fix(ln)
        ln = tag_fix(ln)
        ln = prefix_fix(ln)
        ln = suffix_fix(ln)
        if ln != orig:
            lines[i] = ln
            n += 1
    return "\n".join(lines), n

def main():
    files = []
    for base in ["HrAttendance.Web/Views", "modules"]:
        files += [p.replace("\\", "/") for p in glob.glob(os.path.join(ROOT, base, "**", "*.cshtml"), recursive=True)]
    files = [p for p in files if not any(x in p for x in EXCLUDE)]
    t1 = t2 = 0
    changed = 0
    for p in sorted(files):
        src = open(p, encoding="utf-8").read()
        src2, n1 = fix1(src)
        src3, n2 = fix2(src2)
        if src3 != src:
            open(p, "w", encoding="utf-8").write(src3)
            changed += 1
            t1 += n1
            t2 += n2
    json.dump(sorted(keys), open("/tmp/i18n_new_keys.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"files_changed={changed} value_added={t1} lines_wrapped={t2} new_keys={len(keys)}")

if __name__ == "__main__":
    main()
