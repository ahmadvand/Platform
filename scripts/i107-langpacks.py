#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# فاز ۱۰۷ — ترجمهٔ کلیدهای جدید «فصل منظرهٔ محافظ صفحه» (درخواست کاربر: انتخاب دستی فصل)
# ۱) DictEn استاتیک در HrLang.cs (انگلیسی)
# ۲) بسته‌های زبان تعبیه‌شده en/ru/ar در App_Data/langpacks/*.json — در هر بوت ادغام می‌شوند
# idempotent — اجرای دوباره کلید تکراری نمی‌سازد
import json, io, re, sys

HR_LANG = "/home/z/my-project/aspnet/src/Platform.Core/Services/HrLang.cs"
PACKS = "/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks"

KEYS = {
    "فصل منظرهٔ محافظ صفحه": {
        "en": "Screen-saver scenery season",
        "ru": "Сезон пейзажа экранной заставки",
        "ar": "موسم مشهد حافظ الشاشة",
    },
    "خودکار — بر اساس تاریخِ امروز (شمسی)": {
        "en": "Automatic — based on today's date (Jalali)",
        "ru": "Автоматически — по сегодняшней дате (джалали)",
        "ar": "تلقائي — حسب تاريخ اليوم (الشمسي)",
    },
    "بهار — شکوفه، زنبور و پروانه، باران بهاری، رفتگر شکوفه‌ها": {
        "en": "Spring — blossoms, bees & butterflies, spring rain, petal sweeper",
        "ru": "Весна — цветение, пчёлы и бабочки, весенний дождь, уборщик лепестков",
        "ar": "الربيع — الأزهار والنحل والفراشات، مطر الربيع، الكنس الذي يجمع الأزهار",
    },
    "تابستان — باغ میوه‌دار، گل‌ها و زنبورها، بادکنک قاصدک": {
        "en": "Summer — orchard, flowers & bees, dandelion seeds",
        "ru": "Лето — фруктовый сад, цветы и пчёлы, семена одуванчика",
        "ar": "الصيف — بستان مثمر، أزهار ونحل، بذور الهندباء",
    },
    "پاییز — برگ‌های رنگارنگ، پرستوهای مهاجر، باران نم‌نم": {
        "en": "Autumn — colorful leaves, migrating flock, drizzle",
        "ru": "Осень — разноцветные листья, перелётные птицы, морось",
        "ar": "الخريف — أوراق ملونة، طيور مهاجرة، مطر خفيف",
    },
    "زمستان — برف و مه، آدم‌برفی، گنجشک و خرگوش سفید": {
        "en": "Winter — snow & fog, snowman, sparrow and white hare",
        "ru": "Зима — снег и туман, снеговик, воробей и белый заяц",
        "ar": "الشتاء — ثلج وضباب، رجل الثلج، عصفور وأرنب أبيض",
    },
    "برای بسته‌های محافظ صفحهٔ فصلی؛ بسته‌های بدون فصل این گزینه را نادیده می‌گیرند. «خودکار» فصل را از تاریخ شمسی امروز برمی‌گزیند و انتخاب دستی برای پیش‌نمایش یا سلیقه است.": {
        "en": "For seasonal screen-saver packages; packages without seasons ignore this option. \u201cAutomatic\u201d picks the season from today's Jalali date — the manual choice is for previewing or personal taste.",
        "ru": "Для сезонных пакетов экранной заставки; пакеты без сезонов игнорируют этот параметр. «Автоматически» выбирает сезон по сегодняшней дате джалали — ручной выбор для предпросмотра или вкуса.",
        "ar": "لحزم حافظ الشاشة الموسمية؛ الحزم بدون فصول تتجاهل هذا الخيار. «تلقائي» يختار الموسم حسب تاريخ اليوم الشمسي — والاختيار اليدوي للمعاينة أو الذوق الشخصي.",
    },
}

# ---------- ۱) DictEn استاتیک ----------
src = io.open(HR_LANG, encoding="utf-8").read()
added_cs = 0
anchor = '        ["رمز نادرست است — دوباره تلاش کنید"] = "Wrong password — try again",'
assert anchor in src, "anchor for DictEn insert not found"
block = "\n".join(
    '        ["%s"] = %s,' % (k, json.dumps(v["en"], ensure_ascii=False))
    for k, v in KEYS.items()
)
if "فصل منظرهٔ محافظ صفحه" not in src:
    src = src.replace(anchor, anchor + "\n        // ---------- فاز ۱۰۷ — فصل منظرهٔ محافظ صفحه (انتخاب دستی/خودکار) ----------\n" + block, 1)
    added_cs = len(KEYS)
    io.open(HR_LANG, "w", encoding="utf-8").write(src)
print("DictEn keys added:", added_cs)

# ---------- ۲) بسته‌های زبان ----------
for code in ("en", "ru", "ar"):
    path = f"{PACKS}/hr-lang-{code}.json"
    pack = json.load(io.open(path, encoding="utf-8"))
    ent = pack.setdefault("entries", {})
    added = 0
    for k, v in KEYS.items():
        if k not in ent:
            ent[k] = v[code]
            added += 1
    io.open(path, "w", encoding="utf-8").write(
        json.dumps(pack, ensure_ascii=False, indent=2) + "\n"
    )
    print(f"langpack {code}: keys added = {added}, total = {len(ent)}")

print("done")
