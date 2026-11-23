#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۸۱ — ادغام PART1+PART2 در DictEn و تولید ref-en.json"""
import json, re, sys, importlib.util, os

def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m

p1 = load('d1', '/home/z/my-project/scripts/dict-en-part1.py').PART1
p2 = load('d2', '/home/z/my-project/scripts/dict-en-part2.py').PART2
TR = {**p1, **p2}

missing = json.load(open('/home/z/my-project/scripts/missing-keys.json'))
notr = [k for k in missing if k not in TR]
if notr:
    print('WITHOUT TRANSLATION:', len(notr))
    for k in notr[:40]:
        print(' -', repr(k))

SRC = '/home/z/my-project/aspnet/src/HrPlatform.Core/Services/HrLang.cs'
src = open(SRC, encoding='utf-8').read()

def esc(s):
    return s.replace('\\', '\\\\').replace('"', '\\"')

# نقطهٔ درج: قبل از بسته‌شدن DictEn (نخستین \n    };)
marker = '\n    };'
idx = src.find(marker)
assert idx > 0, 'DictEn closing not found'
block = ['\n        // ---------- فاز ۸۱ — چندزبانه‌سازی کامل (PDF/Excel/Setup/پیام‌ها/Razor) ----------']
added = 0
for k in missing:
    if k not in TR:
        continue
    block.append(f'        ["{esc(k)}"] = "{esc(TR[k])}",')
    added += 1
new_src = src[:idx] + '\n' + '\n'.join(block) + src[idx:]
open(SRC, 'w', encoding='utf-8').write(new_src)
print('INSERTED:', added, 'entries into DictEn')

# بازتولید بستهٔ مرجع TranslationStudio
os.system('python3 /home/z/my-project/scripts/extract-ref-lang.py')
