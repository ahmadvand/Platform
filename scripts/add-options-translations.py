#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۱۰۸ — افزودن کلیدهای «تنظیمات اختصاصی بسته» (options) به بسته‌های زبان en/ru/ar.
هر کلید در سورس (نما/کنترلر) وجودش assert می‌شود تا متن مبدأ و کلید یک‌به‌یک بمانند."""
import json, io

ROOT = "/home/z/my-project"
CTRL = f"{ROOT}/aspnet/src/Platform.Web/Modules/Core/Controllers/SettingsController.cs"
VIEW = f"{ROOT}/aspnet/src/Platform.Web/Views/Settings/Index.cshtml"

c = io.open(CTRL, encoding="utf-8").read()
v = io.open(VIEW, encoding="utf-8").read()

# (کلید فارسیِ langpack، الگوی جست‌وجو در سورس، ترجمه‌ها) — کلیدهای دارای " در C# با \" escape شده‌اند
KEYS = [
    ("تنظیمات اختصاصی بستهٔ فعال (اختیاری)", v, v, {
        "en": "Custom options of the active package (optional)",
        "ru": "Собственные настройки активного пакета (необязательно)",
        "ar": "إعدادات مخصصة الحزمة النشطة (اختياري)",
    }),
    ("به‌صورت JSON — کلیدهای مجاز در توضیح هر بسته آمده است؛ خالی بگذارید یعنی پیش‌فرض خود بسته. از فعال‌شدن بعدیِ محافظ اعمال می‌شود.", v, v, {
        "en": "As JSON — allowed keys are listed in each package description; leave empty for the package defaults. Applied from the next screensaver activation.",
        "ru": "В формате JSON — допустимые ключи указаны в описании пакета; пусто = значения по умолчанию пакета. Применяется со следующей активации хранителя экрана.",
        "ar": "بصيغة JSON — المفاتيح المسموحة مذكورة في وصف كل حزمة؛ اتركه فارغاً لاستخدام افتراضيات الحزمة. يُطبَّق من التنشيط القادم لحافظ الشاشة.",
    }),
    ("ذخیرهٔ تنظیمات بسته", v, v, {
        "en": "Save package options",
        "ru": "Сохранить настройки пакета",
        "ar": "حفظ إعدادات الحزمة",
    }),
    ("تنظیمات اختصاصی بسته (JSON)", v, v, {
        "en": "Package custom options (JSON)",
        "ru": "Собственные настройки пакета (JSON)",
        "ar": "إعدادات مخصصة الحزمة (JSON)",
    }),
    ('JSON تنظیمات بسته معتبر نیست — نمونهٔ درست: {"burstsPerMin":8}', c,
     'JSON تنظیمات بسته معتبر نیست — نمونهٔ درست: {\\"burstsPerMin\\":8}', {
        "en": 'Package options JSON is not valid — correct example: {"burstsPerMin":8}',
        "ru": 'JSON настроек пакета недействителен — правильный пример: {"burstsPerMin":8}',
        "ar": 'JSON إعدادات الحزمة غير صالح — مثال صحيح: {"burstsPerMin":8}',
    }),
    ('تنظیمات بسته باید یک شیء JSON باشد (مثل {"burstsPerMin":8})', c,
     'تنظیمات بسته باید یک شیء JSON باشد (مثل {\\"burstsPerMin\\":8})', {
        "en": 'Package options must be a JSON object (e.g. {"burstsPerMin":8})',
        "ru": 'Настройки пакета должны быть объектом JSON (например {"burstsPerMin":8})',
        "ar": 'يجب أن تكون إعدادات الحزمة كائن JSON (مثل {"burstsPerMin":8})',
    }),
    ("تنظیمات بسته بیش از حد بزرگ است (حداکثر ۲۰۰۰ نویسه JSON)", c, c, {
        "en": "Package options are too large (max 2,000 JSON characters)",
        "ru": "Настройки пакета слишком большие (максимум 2000 символов JSON)",
        "ar": "إعدادات الحزمة كبيرة جداً (الحد الأقصى 2000 محرف JSON)",
    }),
    ("بخش options باید یک شیء JSON باشد (تنظیمات اختصاصی بسته)", c, c, {
        "en": "The options section must be a JSON object (package custom options)",
        "ru": "Секция options должна быть объектом JSON (собственные настройки пакета)",
        "ar": "يجب أن يكون قسم options كائن JSON (إعدادات مخصصة الحزمة)",
    }),
    ("بخش options بیش از حد بزرگ است (حداکثر ۲۰۰۰ نویسه JSON)", c, c, {
        "en": "The options section is too large (max 2,000 JSON characters)",
        "ru": "Секция options слишком большая (максимум 2000 символов JSON)",
        "ar": "قسم options كبير جداً (الحد الأقصى 2000 محرف JSON)",
    }),
    ("تنظیمات اختصاصی بستهٔ «{0}» ذخیره شد — از فعال‌شدن بعدیِ محافظ اعمال می‌شود", c, c, {
        "en": "Custom options of package \"{0}\" saved — applied from the next screensaver activation",
        "ru": "Собственные настройки пакета «{0}» сохранены — применяются со следующей активации хранителя экрана",
        "ar": "حُفظت الإعدادات المخصصة للحزمة «{0}» — تُطبَّق من التنشيط القادم لحافظ الشاشة",
    }),
    ("بستهٔ محافظ صفحه یافت نشد", c, c, {
        "en": "Screensaver package not found",
        "ru": "Пакет хранителя экрана не найден",
        "ar": "لم يُعثر على حزمة حافظ الشاشة",
    }),
]

for key, _, needle, _ in KEYS:
    assert needle in (c + v), "کلید در سورس نیست: " + key[:60]

for code in ("en", "ru", "ar"):
    p = f"{ROOT}/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-{code}.json"
    d = json.load(io.open(p, encoding="utf-8"))
    entries = d["entries"]
    added = 0
    for key, _, _, tr in KEYS:
        if key not in entries:
            entries[key] = tr[code]
            added += 1
    with io.open(p, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"[{code}] +{added} keys → {len(entries)} entries")
