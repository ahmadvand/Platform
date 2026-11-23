#!/usr/bin/env python3
# راستی‌آزمایی فاز ۸۳ب روی DLL کامپایل‌شده (ویوها در .NET 8 با Source Generator
# داخل HrAttendance.Web.dll کامپایل می‌شوند — رشته‌ها UTF-16LE هستند).
import pathlib

dll = pathlib.Path("/home/z/my-project/aspnet/src/HrAttendance.Web/bin/Debug/net8.0/HrAttendance.Web.dll")
data = dll.read_bytes()

def has_u16(s: str) -> bool:
    return s.encode("utf-16-le") in data

def has_u8(s: str) -> bool:
    return s.encode("utf-8") in data

present = [
    "مجوز نصب — لازم برای راه‌اندازی",                 # عنوان جدید گام مجوز (ویو)
    "سامانه فقط با مجوز معتبر راه‌اندازی می‌شود",       # متن سربرگ جدید (ویو)
    "MSG_LIC_REQUIRED",                                  # گارد سمت کلاینت (ویو)
    'name="licenseContent"',                             # ورودی مجوز (ویو)
    "ساخت جفت‌کلید",                                     # کادر جفت‌کلید — باید بماند
    "راه‌اندازی سامانه فقط با مجوز ممکن است",            # پیام سرور (کنترلر)
    "مجوز نصب ثبت نشده است — سامانه فقط با مجوز اجرا می‌شود",  # Missing (سرویس)
]
absent = [
    'name="licenseMode"',            # رادیو حالت‌ها — حذف
    'name="trialDays"',              # فیلد دورهٔ آزمایشی — حذف
    "طول دورهٔ آزمایشی (روز)",       # برچسب فیلد حذف‌شده
    "دورهٔ آزمایشی ۳۰ روزه",         # متن قدیم Strict
    "id=\"licOff\"",                 # رادیو «بدون قفل»
]

ok = True
print("== باید موجود باشند (UTF-16LE در DLL) ==")
for s in present:
    found = has_u16(s)
    print(("OK  " if found else "FAIL") + " | " + s)
    ok = ok and found
print("== باید غایب باشند ==")
for s in absent:
    gone = not has_u16(s)
    print(("OK  " if gone else "FAIL") + " | " + s)
    ok = ok and gone
print("RESULT:", "PASS" if ok else "FAIL")
