#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""بسته‌بندی نهایی بسته‌های زبان: بستهٔ انگلیسی + راهنمای فارسی + zip واحد در wwwroot/dl.

خروجی: HR-Language-Packs.zip — حاوی hr-lang-*.json + README-IMPORT.txt
محل: aspnet/src/HrAttendance.Web/wwwroot/dl/ (کنار بستهٔ سورس — قابل دانلود از /dl/)
"""
import json
import os
import shutil
import zipfile

BASE = "/home/z/my-project/scripts/langpacks"
DL = "/home/z/my-project/aspnet/src/HrAttendance.Web/wwwroot/dl"
OUT = f"{BASE}/out"
ZIP_PATH = f"{DL}/HR-Language-Packs.zip"

KEYS = json.load(open(f"{BASE}/keys.json", encoding="utf-8"))

README = """بسته‌های زبان سامانه منابع انسانی (HR-Language-Packs)
========================================================
تاریخ ساخت: ۲۰۲۶-۱۱-۰۴ — سازگار با قالب درون‌ریزی پنل «زبان‌ها» (format: hr-lang-pack, version: 1)

محتویات این بسته (هر فایل یک زبان):
  hr-lang-ar.json  — العربية (عربی — راست‌به‌چپ)
  hr-lang-de.json  — Deutsch (آلمانی)
  hr-lang-en.json  — English (انگلیسی — به‌روزرسانی ترجمهٔ داخلی)
  hr-lang-fr.json  — Français (فرانسوی)
  hr-lang-ru.json  — Русский (روسی)
  hr-lang-tr.json  — Türkçe (ترکی)
  hr-lang-zh.json  — 简体中文 (چینی ساده‌شده)

روش وارد کردن (توسط ادمین — بدون کد و بدون ری‌استارت):
  ۱) با حساب مدیر ارشد وارد سامانه شوید.
  ۲) منوی «زبان‌ها» (/Langs) را باز کنید.
  ۳) دکمهٔ «درون‌ریزی بستهٔ ترجمه» را بزنید.
  ۴) فایل hr-lang-XX.json موردنظر را انتخاب و تأیید کنید.
  ۵) زبان بلافاصله فعال می‌شود و در سوئیچر زبان (نوار بالایی و صفحهٔ ورود) ظاهر می‌شود.

نکته‌ها:
  - درون‌ریزی «ادغامی» است: ترجمه‌های موجود به‌روز می‌شوند، ترجمه‌های جدید اضافه می‌شوند
    و هیچ‌چیز حذف نمی‌شود — پس بازدرونیری همان فایل بی‌خطر است.
  - زبان مبدأ سامانه «فارسی» است؛ هر رشته‌ای که در بسته نباشد به‌صورت فارسی نمایش داده می‌شود.
  - بستهٔ en ترجمه‌های داخلی انگلیسی را با اولویت بازنویسی می‌کند (برای شخصی‌سازی).
  - هر بسته را می‌توانید جداگانه هم zip کنید؛ پنل /Langs فایل json یا zip حاوی json را می‌پذیرد.
  - کلیدهای سامانه با هر به‌روزرسانی ممکن است بیشتر شوند؛ دوباره بستهٔ جدید را درون‌ریزی کنید.
"""


def main():
    os.makedirs(OUT, exist_ok=True)

    # ۱) بستهٔ انگلیسی — مستقیم از دیکشنری داخلی (بدون LLM)
    en = {"format": "hr-lang-pack", "version": 1, "code": "en",
          "title": "English", "dir": "ltr",
          "entries": {it["fa"]: it["en"] for it in KEYS if it["en"]}}
    json.dump(en, open(f"{OUT}/hr-lang-en.json", "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    print(f"en pack: {len(en['entries'])} entries")

    # ۲) راهنما
    readme_path = f"{OUT}/README-IMPORT.txt"
    open(readme_path, "w", encoding="utf-8").write(README)

    # ۳) zip واحد کنار سورس
    os.makedirs(DL, exist_ok=True)
    if os.path.exists(ZIP_PATH):
        os.remove(ZIP_PATH)
    with zipfile.ZipFile(ZIP_PATH, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for name in sorted(os.listdir(OUT)):
            if name.endswith(".json") or name.endswith(".txt"):
                z.write(f"{OUT}/{name}", name)
    size = os.path.getsize(ZIP_PATH)
    print(f"zip: {ZIP_PATH} ({size/1024:.0f} KB)")
    with zipfile.ZipFile(ZIP_PATH) as z:
        for n in z.namelist():
            print(f"   {n}  ({z.getinfo(n).file_size/1024:.0f} KB)")


if __name__ == "__main__":
    main()
