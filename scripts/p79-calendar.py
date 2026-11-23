#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۷۹ — مهاجرت نمایش/ساختار از Jalali.* (شمسی ثابت) به HrCal.* (تقویم فعال سامانه).

مپ متدها (فقط متدهای «نمایش/ساختار ماه» — متدهای کانونیِ بی‌تقویم دست‌نخورده می‌مانند):
  Jalali.FmtFa(       → HrCal.FmtNamed(      (نام‌دار با نام ماه)
  Jalali.FmtLong(     → HrCal.FmtLong(
  Jalali.FmtTime(     → HrCal.FmtTime(
  Jalali.Fmt(         → HrCal.Fmt(           (عددی)
  Jalali.ToJalali(    → HrCal.ToParts(       (اجزای تقویم فعال)
  Jalali.FromJalali(  → HrCal.FromParts(
  Jalali.MonthRange(  → HrCal.MonthRange(
  Jalali.MonthGrid(   → HrCal.MonthGrid(
  Jalali.MonthLength( → HrCal.MonthLength(
  Jalali.MonthName(   → HrCal.MonthName(
  Jalali.WeekdayName( → HrCal.WeekdayName(

دست‌نخورده (کانونی/بی‌تقویم): DateKey، ParseDateKey، Num، ToPersianDigits،
MinutesToHm، DiffDays، AddDaysKey، ValidatePassword، IsFriday.

خروجی: گزارش تعداد جایگزینی به تفکیک فایل + مجموع.
"""
import re
import sys
from pathlib import Path

ROOT = Path("/home/z/my-project/aspnet/src")
EXCLUDE_PARTS = ("/wwwroot/dl/", "/bin/", "/obj/", "/tools/", "/tests/", "LicenseStudio", "TranslationStudio", "/publish/")

# ترتیب مهم است: طولانی‌ترها اول تا «Jalali.Fmt(» داخل «Jalali.FmtFa(» را نگیرد
MAP = [
    ("Jalali.FmtFa(",     "HrCal.FmtNamed("),
    ("Jalali.FmtLong(",   "HrCal.FmtLong("),
    ("Jalali.FmtTime(",   "HrCal.FmtTime("),
    ("Jalali.Fmt(",       "HrCal.Fmt("),
    ("Jalali.ToJalali(",  "HrCal.ToParts("),
    ("Jalali.FromJalali(","HrCal.FromParts("),
    ("Jalali.MonthRange(","HrCal.MonthRange("),
    ("Jalali.MonthGrid(", "HrCal.MonthGrid("),
    ("Jalali.MonthLength(","HrCal.MonthLength("),
    ("Jalali.MonthName(", "HrCal.MonthName("),
    ("Jalali.WeekdayName(","HrCal.WeekdayName("),
]

total = 0
files_changed = 0
per_pattern = {old: 0 for old, _ in MAP}

for path in list(ROOT.rglob("*.cs")) + list(ROOT.rglob("*.cshtml")):
    p = str(path)
    if any(x in p for x in EXCLUDE_PARTS):
        continue
    try:
        text = path.read_text(encoding="utf-8")
    except Exception as e:
        print(f"⚠ خواندن ناموفق {p}: {e}", file=sys.stderr)
        continue
    new = text
    file_count = 0
    for old, repl in MAP:
        n = new.count(old)
        if n:
            new = new.replace(old, repl)
            per_pattern[old] += n
            file_count += n
    if file_count:
        path.write_text(new, encoding="utf-8")
        files_changed += 1
        total += file_count
        print(f"{file_count:>4}  {path.relative_to(ROOT)}")

print("-" * 60)
for old, _ in MAP:
    print(f"{per_pattern[old]:>5}  {old:<22}→ {old.replace('Jalali.', 'HrCal.').replace('FmtFa(', 'FmtNamed(').replace('ToJalali(', 'ToParts(').replace('FromJalali(', 'FromParts(')}")
print(f"جمع: {total} جایگزینی در {files_changed} فایل")

# باقی‌مانده‌ها باید فقط متدهای کانونی باشند
leftover = {}
for path in list(ROOT.rglob("*.cs")) + list(ROOT.rglob("*.cshtml")):
    p = str(path)
    if any(x in p for x in EXCLUDE_PARTS):
        continue
    text = path.read_text(encoding="utf-8")
    for m in set(re.findall(r"Jalali\.\w+\(", text)):
        leftover.setdefault(m, 0)
        leftover[m] += text.count(m)
print("-" * 60)
print("باقی‌ماندهٔ Jalali.* (باید فقط کانونی‌ها باشند):")
for k in sorted(leftover):
    print(f"  {leftover[k]:>4}  {k}")
