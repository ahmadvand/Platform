# -*- coding: utf-8 -*-
# ======================================================================
# فاز ۱۰۲ — اعتبارسنجی و بسته‌بندی بستهٔ ترجمهٔ روسی
# ورودی: scripts/langpacks/out/hr-lang-ru.json
# خروجی: download/HR-Langpack-Russian-2.7.7.zip  (json + راهنمای فارسی)
#        + کپی در مرکز دانلود زندهٔ sc-run
# هیچ سورس برنامه‌ای تغییر نمی‌کند.
# ======================================================================
import json, os, re, shutil, zipfile

ROOT = "/home/z/my-project"
CS = f"{ROOT}/aspnet/src/Platform.Core/Services/HrLang.cs"
PACK = f"{ROOT}/scripts/langpacks/out/hr-lang-ru.json"
ZIP = f"{ROOT}/download/HR-Langpack-Russian-2.7.7.zip"
DL = f"{ROOT}/aspnet/sc-run/wwwroot/dl/HR-Langpack-Russian-2.7.7.zip"

FA_RE = re.compile(r'[\u0600-\u06FF]')
PLACEHOLDER_RE = re.compile(r'\{\d+\}')
TAG_RE = re.compile(r'</?[a-zA-Z][^>]*>')


def extract_dicten():
    src = open(CS, encoding="utf-8").read()
    m = re.search(r'DictEn\s*=\s*new[^(]*\((.*?)\n    \};', src, re.S)
    body = m.group(1) if m else src
    pairs = {}
    for mm in re.finditer(r'\[\s*("(?:[^"\\]|\\.)*")\s*\]\s*=\s*("(?:[^"\\]|\\.)*")', body):
        try:
            k = json.loads(mm.group(1)); v = json.loads(mm.group(2))
        except Exception:
            continue
        pairs[k] = v
    return pairs


README = """بستهٔ ترجمهٔ روسی — سامانه یکپارچه منابع انسانی (نسخهٔ ۲.۷.۷)
================================================================
فایل: hr-lang-ru.json — Русский (روسی — چپ‌به‌راست)
قالب: hr-lang-pack نسخهٔ ۱ (همان قالب درون‌ریزی پنل «زبان‌ها»)

روش وارد کردن — توسط مدیر ارشد، بدون کد و بدون ری‌استارت:
  ۱) با حساب مدیر ارشد وارد سامانه شوید.
  ۲) منوی «تنظیمات» ← «زبان‌ها» (مسیر /Langs) را باز کنید.
  ۳) روی «درون‌ریزی بستهٔ ترجمه» کلیک کنید.
  ۴) فایل hr-lang-ru.json را انتخاب و تأیید کنید.
  ۵) زبان «Русский» بلافاصله فعال می‌شود و در سوئیچر زبان
     (نوار بالایی و صفحهٔ ورود) ظاهر می‌شود؛ با انتخاب آن،
     کل رابط کاربری به روسی (چپ‌به‌راست) نمایش داده می‌شود.

نکته‌ها:
  • درون‌ریزی «ادغامی» است: ترجمه‌های موجود به‌روز و ترجمه‌های جدید
    اضافه می‌شوند و هیچ‌چیز حذف نمی‌شود — بازدرون‌ریزی همان فایل بی‌خطر است.
  • هر رشته‌ای که در بسته نباشد به‌صورت فارسی (زبان مبدأ) نمایش داده می‌شود.
  • اگر این فایل در پوشهٔ App_Data/langpacks سرور کپی شده باشد،
    در هر راه‌اندازی به‌طور خودکار هم ادغام می‌شود (نصب تازه/خودترمیم).
  • پنل /Langs فایل json یا zip حاوی json را می‌پذیرد؛ این بسته هر دو را دارد.
  • با به‌روزرسانی سامانه ممکن است کلیدهای تازه‌ای افزوده شود؛ کافی است
    نسخهٔ جدید بسته را دوباره درون‌ریزی کنید.
"""


def main():
    dicten = extract_dicten()
    pack = json.load(open(PACK, encoding="utf-8"))
    entries = pack.get("entries", {})

    print(f"DictEn keys : {len(dicten)}")
    print(f"pack code   : {pack.get('code')} / {pack.get('title')} / dir={pack.get('dir')}")
    print(f"pack entries: {len(entries)}")

    missing = [k for k in dicten if k not in entries]
    extra = [k for k in entries if k not in dicten]
    print(f"missing keys: {len(missing)}")
    if missing:
        for k in missing[:10]:
            print("  MISS:", k[:70])
    print(f"extra keys  : {len(extra)}")

    empty = [k for k, v in entries.items() if not v.strip()]
    print(f"empty values: {len(empty)}")

    # فارسی مانده در ترجمه (فقط کلیدهایی که خودشان فارسی‌اند → مقدار هم نباید فارسی باشد)
    persian_left = [k for k, v in entries.items()
                    if FA_RE.search(k) and FA_RE.search(v)]
    print(f"persian-left: {len(persian_left)}")
    for k in persian_left[:10]:
        print("  FA:", k[:50], "=>", entries[k][:50])

    # placeholder integrity
    ph_bad = []
    for k, v in entries.items():
        kp = sorted(PLACEHOLDER_RE.findall(k))
        vp = sorted(PLACEHOLDER_RE.findall(v))
        if kp != vp:
            ph_bad.append((k, v))
    print(f"placeholder-mismatch: {len(ph_bad)}")
    for k, v in ph_bad[:5]:
        print("  PH:", k[:50], "=>", v[:50])

    # tag integrity
    tag_bad = []
    for k, v in entries.items():
        kt = sorted(TAG_RE.findall(k))
        vt = sorted(TAG_RE.findall(v))
        if kt != vt:
            tag_bad.append((k, v))
    print(f"tag-mismatch: {len(tag_bad)}")
    for k, v in tag_bad[:5]:
        print("  TAG:", k[:50], "=>", v[:50])

    ok = (not missing) and (not empty) and (not ph_bad) and (not tag_bad) \
         and pack.get("code") == "ru" and pack.get("dir") == "ltr"
    print("VALIDATION:", "PASS" if ok else "FAIL")
    if not ok:
        raise SystemExit(1)

    # بستهٔ zip نهایی
    os.makedirs(os.path.dirname(ZIP), exist_ok=True)
    tmp = ZIP + ".tmp"
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        z.write(PACK, "hr-lang-ru.json")
        z.writestr("README-IMPORT-FA.txt", README)
    os.replace(tmp, ZIP)
    shutil.copyfile(ZIP, DL)
    print(f"zip -> {ZIP} ({os.path.getsize(ZIP)} bytes)")
    print(f"zip -> {DL}")


if __name__ == "__main__":
    main()
