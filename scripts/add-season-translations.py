#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۱۰۶ — افزودن کلیدهای انتخاب فصل محافظ چهارفصل به بسته‌های زبان en/ru/ar (مسیر رسمی).
کلیدها مستقیم از Views/Settings/Index.cshtml استخراج می‌شوند تا با متن مبدأ یک‌به‌یک (Ordinal) بخوانند."""
import json, re, io

ROOT = "/home/z/my-project"
VIEW = f"{ROOT}/aspnet/src/Platform.Web/Views/Settings/Index.cshtml"
PACKS = ["en", "ru", "ar"]

html = io.open(VIEW, encoding="utf-8").read()

# برچسب فیلد
lab = re.search(r'@HrT\.T\("([^"]*فصل صحنه[^"]*)"\)', html)
assert lab, "label not found"
label_key = lab.group(1)

# گزینه‌ها: value → متن فارسی مبدأ
opts = {}
for m in re.finditer(r'<option value="(auto|spring|summer|autumn|winter)"[^>]*>@HrT\.T\("([^"]+)"\)</option>', html):
    opts[m.group(1)] = m.group(2)
assert set(opts) == {"auto", "spring", "summer", "autumn", "winter"}, f"options parsed: {list(opts)}"

# ترجمه‌ها (value → en/ru/ar)
TR = {
    label_key: {
        "en": "Four-season screensaver scene",
        "ru": "Сезон сцены четырёхсезонного хранителя экрана",
        "ar": "موسم مشهد حافظ الشاشة لأربعة فصول",
    },
    opts["auto"]: {
        "en": "Automatic — detected from the system date",
        "ru": "Автоматически — определяется по дате системы",
        "ar": "تلقائي — يُحدَّد حسب تاريخ النظام",
    },
    opts["spring"]: {
        "en": "Spring — blossoms, bees and butterflies",
        "ru": "Весна — цветы, пчёлы и бабочки",
        "ar": "الربيع — الأزهار والنحل والفراشات",
    },
    opts["summer"]: {
        "en": "Summer — lush fruit orchard",
        "ru": "Лето — зелёный плодоносящий сад",
        "ar": "الصيف — بستان أخضر مثمر",
    },
    opts["autumn"]: {
        "en": "Autumn — colorful falling leaves",
        "ru": "Осень — разноцветный листопад",
        "ar": "الخريف — أوراق ملونة متساقطة",
    },
    opts["winter"]: {
        "en": "Winter — snow, fog and snowy trees",
        "ru": "Зима — снег, туман и заснеженные деревья",
        "ar": "الشتاء — الثلج والضباب والأشجار المثلجة",
    },
}

for code in PACKS:
    p = f"{ROOT}/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-{code}.json"
    d = json.load(io.open(p, encoding="utf-8"))
    entries = d["entries"]
    added = 0
    for key, langs in TR.items():
        if key not in entries:
            entries[key] = langs[code]
            added += 1
        elif entries[key] != langs[code]:
            print(f"[{code}] key exists with different value — kept: {key[:40]}")
    with io.open(p, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"[{code}] +{added} keys → {len(entries)} entries")
print("keys used:")
for k in TR:
    print("  -", repr(k))
