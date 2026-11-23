# -*- coding: utf-8 -*-
# فاز ۱۰۴ن۴ — بستهٔ نهایی قابل درون‌ریزی: HR-Langpack-Russian-2.7.7.zip (۳۰۲۱ کلید کامل — شامل ۲ کلید دیالوگ با خط جدید واقعی)
import json, os, re, shutil, zipfile

ROOT = "/home/z/my-project"
SRC_PACK = f"{ROOT}/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-ru.json"
OUT_PACK = f"{ROOT}/scripts/langpacks/out/hr-lang-ru.json"
ZIP = f"{ROOT}/download/HR-Langpack-Russian-2.7.7.zip"

d = json.load(open(SRC_PACK, encoding="utf-8"))
assert d["format"] == "hr-lang-pack" and d["code"] == "ru" and d["dir"] == "ltr"
assert len(d["entries"]) == 3101 and all(str(v).strip() for v in d["entries"].values()), "pack invalid"

# همگام‌سازی نسخهٔ out برای اسکریپت‌های بعدی
shutil.copyfile(SRC_PACK, OUT_PACK)

README = """بستهٔ ترجمهٔ روسی — سامانه یکپارچه منابع انسانی (نسخهٔ ۲.۷.۷)
================================================================
فایل: hr-lang-ru.json — Русский (روسی — چپ‌به‌راست)
تعداد کلیدها: ۳۱۰۱ — پوشش کامل کلیدهای سامانه (شامل فاز ۱۱۲: هاب داده/تقویم چندگانه/محافظ صفحه/جدول پرسنل) (برندهایی مثل ZKTeco/Suprema
بدون ترجمه می‌مانند؛ سیاست همهٔ بسته‌های زبان همین است).
قالب: hr-lang-pack نسخهٔ ۱ (همان قالب درون‌ریزی پنل «زبان‌ها»)

روش وارد کردن — توسط مدیر ارشد، بدون کد و بدون ری‌استارت:
  ۱) با حساب مدیر ارشد وارد سامانه شوید.
  ۲) منوی «تنظیمات» ← «زبان‌ها» (مسیر /Langs) را باز کنید.
  ۳) روی «درون‌ریزی بستهٔ ترجمه» کلیک کنید.
  ۴) فایل hr-lang-ru.json را انتخاب و تأیید کنید.
  ۵) زبان «Русский» بلافاصله فعال می‌شود و در سوئیچر زبان نوار بالایی
     ظاهر می‌شود؛ با انتخاب آن، کل رابط کاربری روسی (چپ‌به‌راست) می‌شود.

نکته‌ها:
  • اگر زبان «ru» قبلاً وجود داشته باشد، همین بسته با آن ادغام می‌شود
    (ترجمه‌های تازه جایگزین، بقیه دست‌نخورده).
  • کش ترجمه بلافاصله باطل می‌شود؛ نیازی به ری‌استارت نیست.
  • برای ویرایش متن‌ها همین فایل را تغییر دهید و دوباره درون‌ریزی کنید.
"""

os.makedirs(os.path.dirname(ZIP), exist_ok=True)
tmp = ZIP + ".tmp"
with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    z.writestr("hr-lang-ru.json", json.dumps(d, ensure_ascii=False, indent=0))
    z.writestr("README-IMPORT-FA.txt", README)
os.replace(tmp, ZIP)
sz = os.path.getsize(ZIP)
print(f"zip: {ZIP} ({sz/1024:.0f} KB) — 3101 keys")
