#!/usr/bin/env python3
# فاز ۱۱۳ (مرحلهٔ دوم — ادغام) — نتایج رانر ترجمهٔ انبوه + کلیدهای فاز ۱۱۶ (ممیزی) در همهٔ بسته‌ها
# ورودی: scripts/langgap/langgap/results/result-{code}.json (fa → ترجمه)
# خروجی: بسته‌های hr-lang-{code}.json به‌روز + گزارش پوشش
import json, glob, os, sys

SRC = '/home/z/my-project/aspnet/src'
PACKS = os.path.join(SRC, 'Platform.Web/App_Data/langpacks')
RES = '/home/z/my-project/scripts/langgap/langgap/results'

# کلیدهای تغییرنام‌یافتهٔ فاز ۱۱۶ (حسابرسی → ممیزی) — ترجمهٔ دستی باکیفیت برای همهٔ زبانها
PHASE116 = {
    'ممیزی': {
        'ar': 'ممارسة المراجعة والتفتيش', 'ru': 'Аудит (проверка действий)',
        'ka': 'აუდიტი', 'ky': 'Текшерүү (аудит)', 'prs': 'ممیزی (بازرسی)', 'ps': 'ممیزی (کتنه)',
        'tg': 'Таҳлили тафтиш (аудит)', 'tk': 'Dedowersük (audyt)', 'uz': 'Tekshiruv (audit)',
    },
    'گزارش ممیزی — {0} رویداد اخیر': {
        'ar': 'تقرير المراجعة — {0} حدث أخير', 'ru': 'Отчёт аудита — последние {0} событий',
        'ka': 'აუდიტის ანგარიში — {0} ბოლო მოვლენა', 'ky': 'Текшерүү отчёту — {0} акыркы окуя',
        'prs': 'گزارش ممیزی — {0} رویداد اخیر', 'ps': 'د ممیزی راپور — {0} وروستي پېښې',
        'tg': 'Ҳисоботи аудит — {0} воқеаи охирин', 'tk': 'Audyt hasabaty — {0} iň soňky waka',
        'uz': 'Audit hisoboti — {0} oxirgi hodisa',
    },
    'مشاهده گزارش ممیزی': {
        'ar': 'عرضة تقرير المراجعة', 'ru': 'Просмотр отчёта аудита',
        'ka': 'აუდიტის ანგარიშის ნახვა', 'ky': 'Текшерүү отчётун көрүү',
        'prs': 'مشاهده گزارش ممیزی', 'ps': 'د ممیزی راپور کتل',
        'tg': 'Дидани ҳисоботи аудит', 'tk': 'Audyt hasabatyny görkezmegi',
        'uz': 'Audit hisobotini ko\u2018rish',
    },
    'راستی‌آزمایی زنجیره': {
        'ar': 'التحقق من سلامة السلسلة', 'ru': 'Проверка целостности цепочки',
        'ka': 'ჯაჭვის მთლიანობის შემოწმება', 'ky': 'Чынжыр бүтүндүгүн текшерүү',
        'prs': 'بررسی سلامت زنجیره', 'ps': 'د لړ ګدړې کتنه',
        'tg': 'Санҷиши тамомии занҷира', 'tk': 'Zynjyryň bitewüligini barlamak',
        'uz': 'Zanjir butunligini tekshirish',
    },
}

def merge(code):
    pack_path = os.path.join(PACKS, f'hr-lang-{code}.json')
    d = json.load(open(pack_path, encoding='utf-8'))
    ent = d['entries']
    added = 0
    # ۱) نتایج رانر (اگر هست)
    res_path = os.path.join(RES, f'result-{code}.json')
    if os.path.exists(res_path):
        results = json.load(open(res_path, encoding='utf-8'))
        for k, v in results.items():
            if isinstance(v, str) and v.strip() and k not in ent:
                ent[k] = v
                added += 1
    # ۲) کلیدهای فاز ۱۱۶
    for k, langs in PHASE116.items():
        if code in langs and k not in ent:
            ent[k] = langs[code]
            added += 1
    with open(pack_path, 'w', encoding='utf-8') as f:
        json.dump(d, f, ensure_ascii=False, indent=2)
        f.write('\n')
    return added, len(ent)

# DictEn keys — برای گزارش پوشش
import re
def dicten_keys():
    txt = open(os.path.join(SRC, 'Platform.Core/Services/HrLang.cs'), encoding='utf-8').read()
    m = re.search(r'Dictionary<string, string> DictEn = new\(StringComparer\.Ordinal\)\s*\{', txt)
    seg = txt[m.end():]
    seg = seg[:seg.find('\n    };')]
    raw = re.findall(r'^\s*\["(.+?)"\] = "', seg, re.M)
    def unesc(s):
        o, i = [], 0
        while i < len(s):
            c = s[i]
            if c == '\\' and i + 1 < len(s):
                n = s[i+1]
                if n == 'n': o.append('\n'); i += 2; continue
                if n == '\\': o.append('\\'); i += 2; continue
                if n == '"': o.append('"'); i += 2; continue
            o.append(c); i += 1
        return ''.join(o)
    return [unesc(k) for k in raw]

keys = dicten_keys()
print('DictEn keys:', len(keys))
for code in ['ar', 'ru', 'ka', 'ky', 'prs', 'ps', 'tg', 'tk', 'uz']:
    added, total = merge(code)
    cover = sum(1 for k in keys if k in json.load(open(os.path.join(PACKS, f'hr-lang-{code}.json'), encoding='utf-8'))['entries'])
    print(f'{code}: +{added} → {total} کلید | پوشش DictEn: {cover}/{len(keys)} = {100*cover//len(keys)}%')
