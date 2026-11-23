#!/usr/bin/env python3
# فاز ۱۱۳ — سنجش پوشش ترجمه: کلیدهای DictEn در HrLang.cs در برابر بسته‌های زبان پویا
import json, glob, os, re, sys

SRC = '/home/z/my-project/aspnet/src'
HRLANG = os.path.join(SRC, 'Platform.Core/Services/HrLang.cs')
PACKS = os.path.join(SRC, 'Platform.Web/App_Data/langpacks')

def unescape(s):
    # ارزیابی escapeهای C# در متن سورس: \n \\ \" \'
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c == '\\' and i + 1 < len(s):
            n = s[i+1]
            if n == 'n': out.append('\n'); i += 2; continue
            if n == '\\': out.append('\\'); i += 2; continue
            if n == '"': out.append('"'); i += 2; continue
            if n == '\'': out.append('\''); i += 2; continue
        out.append(c); i += 1
    return ''.join(out)

def dicten_keys():
    txt = open(HRLANG, encoding='utf-8').read()
    # فقط بدنهٔ DictEn (از تعریف تا بسته‌شدن دیکشنری) — کلیدها فارسی‌اند
    m = re.search(r'Dictionary<string, string> DictEn = new\(StringComparer\.Ordinal\)\s*\{', txt)
    if not m: sys.exit('DictEn not found')
    seg = txt[m.end():]
    end = seg.find('\n    };')
    if end < 0: sys.exit('DictEn end not found')
    seg = seg[:end]
    raw = re.findall(r'^\s*\["(.+?)"\] = "', seg, re.M)
    return [unescape(k) for k in raw]

keys = dicten_keys()
print('DictEn keys:', len(keys))

packs = {}
for f in sorted(glob.glob(os.path.join(PACKS, 'hr-lang-*.json'))):
    code = os.path.basename(f)[8:-5]
    d = json.load(open(f))
    packs[code] = (d, f, set(d['entries'].keys()))

base = set(keys)
for code in ['en', 'ar', 'ru', 'ka', 'ky', 'prs', 'ps', 'tg', 'tk', 'uz']:
    d, f, ek = packs[code]
    missing = base - ek
    print(f'{code}: pack={len(ek)}  missing-vs-DictEn={len(missing)}')

# کلیدهای جاافتادهٔ مشترک ar و ru (کلیدهای تازه)
d_ar, f_ar, ek_ar = packs['ar']
d_ru, f_ru, ek_ru = packs['ru']
new_ar = sorted(base - ek_ar)
new_ru = sorted(base - ek_ru)
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'langgap')
os.makedirs(out, exist_ok=True)
json.dump(new_ar, open(os.path.join(out, 'missing-ar.json'), 'w'), ensure_ascii=False, indent=1)
json.dump(new_ru, open(os.path.join(out, 'missing-ru.json'), 'w'), ensure_ascii=False, indent=1)
print('\nmissing ar:', len(new_ar), '| missing ru:', len(new_ru), '| saved to', out)
print('\nنمونهٔ ۳۰ کلید جاافتادهٔ عربی:')
for k in new_ar[:30]: print('  -', k)
