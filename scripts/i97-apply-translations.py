# -*- coding: utf-8 -*-
# فاز ۹۷ — اعمال ترجمه‌های دست‌نویس روی ۸ بستهٔ زبان (src + sc-run)
# ورودی: i97-tr-data1.py (KA/UZ) + i97-tr-data2.py (TK/KY/TG/AR/PS)
# قواعد:
#  1) کلیدهای TR مستقیماً روی entries بسته اعمال می‌شوند (وجود یا نبود — اضافه/به‌روزرسانی).
#  2) prs: برای کلیدهای ترجمه‌نشده (value == انگلیسی) که متن عربی/فارسی دارند، value = key
#     (دری با فارسی مبدأ هم‌خانواده است)؛ کلیدهای لاتین-محض دست‌نخورده.
import json, re, io, os, sys, shutil

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from i97_tr_data1 import KA, UZ
from i97_tr_data2 import TK, KY, TG, AR, PS

ROOT = '/home/z/my-project'
TR = {'ka': KA, 'uz': UZ, 'tk': TK, 'ky': KY, 'tg': TG, 'ar': AR, 'ps': PS}
LANGS = ['ka', 'uz', 'prs', 'tk', 'ky', 'tg', 'ar', 'ps']
SRC = f'{ROOT}/aspnet/src/Platform.Web/App_Data/langpacks'
RUN = f'{ROOT}/aspnet/sc-run/App_Data/langpacks'

def has_arabic(s):
    return any('\u0600' <= ch <= '\u06FF' or '\u0750' <= ch <= '\u077F' or '\uFB50' <= ch <= '\uFEFF' for ch in s)

report = {}
for lang in LANGS:
    p = f'{SRC}/hr-lang-{lang}.json'
    d = json.load(io.open(p, encoding='utf-8'))
    es = d['entries']
    before = len(es)
    applied = updated = 0
    # ۱) ترجمه‌های دست‌نویس
    if lang in TR:
        for k, v in TR[lang].items():
            if k in es and es[k] == v:
                continue
            es[k] = v
            applied += 1
    # ۲) prs — کلیدهای فال‌بک انگلیسی با متن عربی‌داشته → خودِ متن مبدأ
    if lang == 'prs':
        for k, v in list(es.items()):
            if has_arabic(k) and k not in {kk for kk, _ in []} and v != k and re.search(r'[A-Za-z]', v) and not has_arabic(v):
                es[k] = k
                updated += 1
    report[lang] = (before, len(es), applied, updated)
    io.open(p, 'w', encoding='utf-8').write(json.dumps(d, ensure_ascii=False, indent=2) + '\n')
    # کپی به sc-run
    dst = f'{RUN}/hr-lang-{lang}.json'
    if os.path.exists(RUN):
        shutil.copyfile(p, dst)

for lang, (b, a, ap, up) in report.items():
    print(f'{lang}: entries {b} -> {a} (applied={ap}, prs-to-key={up})')
print('OK — packs updated in src + sc-run')
