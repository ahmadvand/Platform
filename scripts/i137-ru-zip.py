# -*- coding: utf-8 -*-
# فاز ۱۱۳ — بستهٔ نهایی روسی: HR-Langpack-Russian-2.7.9.zip (۳۱۶۳ کلید — شامل کلیدهای فاز ۱۱۲/۱۱۳)
import json, os, shutil, zipfile

ROOT = "/home/z/my-project"
SRC_PACK = f"{ROOT}/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-ru.json"
OUT_PACK = f"{ROOT}/scripts/langpacks/out/hr-lang-ru.json"
ZIP = f"{ROOT}/download/HR-Langpack-Russian-2.7.9.zip"

d = json.load(open(SRC_PACK, encoding="utf-8"))
assert d["format"] == "hr-lang-pack" and d["code"] == "ru" and d["dir"] == "ltr"
n = len(d["entries"])
assert n == 3163 and all(str(v).strip() for v in d["entries"].values()), f"pack invalid: {n}"

shutil.copyfile(SRC_PACK, OUT_PACK)

README = """بستهٔ ترجمهٔ روسی — سامانه یکپارچه منابع انسانی (نسخهٔ ۲.۷.۹)
================================================================
فایل: hr-lang-ru.json — Русский (روسی — چپ‌به‌راست)
تعداد کلیدها: ۳۱۶۳ — پوشش کامل کلیدهای سامانه (شامل فاز ۱۱۲: هاب داده/تقویم چندگانه/محافظ
صفحه/جدول پرسنل و فاز ۱۱۳: ویزارد سه‌تبی/قفل محافظ صفحه) (برندهایی مثل ZKTeco/Suprema بدون
ترجمه می‌مانند؛ سیاست همهٔ بسته‌های زبان همین است).
قالب: hr-lang-pack نسخهٔ ۱ (همان قالب درون‌ریزی پنل «زبان‌ها»)

روش وارد کردن — توسط مدیر ارشد، بدون کد و بدون ری‌استارت:
  ۱) با حساب مدیر ارشد وارد سامانه شوید.
  ۲) منوی «تنظیمات» ← «زبان‌ها» (مسیر /Langs) را باز کنید.
  ۳) روی «درون‌ریزی بستهٔ ترجمه» کلیک کنید.
  ۴) فایل hr-lang-ru.json را انتخاب و تأیید کنید.
  ۵) زبان «Русский» بلافاصله فعال می‌شود و در سوئیچر زبان نوار بالایی
     ظاهر می‌شود؛ با انتخاب آن، کل رابط کاربری روسی (چپ‌به‌راست) می‌شود.
"""

TMP = f"{ROOT}/download/.ru-pack-tmp"
shutil.rmtree(TMP, ignore_errors=True)
os.makedirs(TMP)
open(f"{TMP}/README-FA.txt", "w", encoding="utf-8-sig").write(README)
shutil.copyfile(SRC_PACK, f"{TMP}/hr-lang-ru.json")

if os.path.exists(ZIP):
    os.remove(ZIP)
with zipfile.ZipFile(ZIP, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(f"{TMP}/README-FA.txt", "HR-Langpack-Russian/README-FA.txt")
    z.write(f"{TMP}/hr-lang-ru.json", "HR-Langpack-Russian/hr-lang-ru.json")
shutil.rmtree(TMP)
print(f"OK {ZIP} ({os.path.getsize(ZIP)//1024} KB) — {n} کلید")
