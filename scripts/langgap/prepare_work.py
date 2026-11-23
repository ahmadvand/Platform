#!/usr/bin/env python3
# فاز ۱۱۳ (مرحلهٔ دوم) — آماده‌سازی فهرست کار ترجمه برای هر بستهٔ پایه
# خروجی: work-{code}.json = [[fa_key, en_ref], ...] مرتب بر اساس ارجاع انگلیسی موجود
import json, os, re, sys

SRC = '/home/z/my-project/aspnet/src'
HRLANG = os.path.join(SRC, 'Platform.Core/Services/HrLang.cs')
PACKS = os.path.join(SRC, 'Platform.Web/App_Data/langpacks')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'langgap')

# کلید + معادل انگلیسی از DictEn
import re
def dicten_pairs():
    txt = open(HRLANG, encoding='utf-8').read()
    m = re.search(r'Dictionary<string, string> DictEn = new\(StringComparer\.Ordinal\)\s*\{', txt)
    seg = txt[m.end():]
    end = seg.find('\n    };')
    seg = seg[:end]
    out = {}
    for mm in re.finditer(r'^\s*\["(.+?)"\] = "(.*?)",?\s*$', seg, re.M):
        k, v = mm.group(1), mm.group(2)
        out[unescape(k)] = unescape(v)
    return out

def unescape(s):
    o, i = [], 0
    while i < len(s):
        c = s[i]
        if c == '\\' and i + 1 < len(s):
            n = s[i+1]
            if n == 'n': o.append('\n'); i += 2; continue
            if n == '\\': o.append('\\'); i += 2; continue
            if n == '"': o.append('"'); i += 2; continue
            if n == '\'': o.append('\''); i += 2; continue
        o.append(c); i += 1
    return ''.join(o)

pairs = dicten_pairs()
print('DictEn pairs:', len(pairs))
LANGS = ['ka', 'ky', 'prs', 'ps', 'tg', 'tk', 'uz']
for code in LANGS:
    pack = json.load(open(os.path.join(PACKS, f'hr-lang-{code}.json'), encoding='utf-8'))
    have = set(pack['entries'].keys())
    work = [[k, pairs.get(k, '')] for k in pairs if k not in have]
    json.dump(work, open(os.path.join(OUT, f'work-{code}.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    print(f'{code}: {len(work)} کلید برای ترجمه')
print('done')
