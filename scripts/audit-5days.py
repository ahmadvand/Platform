#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ممیزی جامع پنج روز اخیر (فاز ۸۶→۹۷) — سورس + اپ زندهٔ پورت 3000"""
import subprocess, re, os, sys, json

BASE = "http://127.0.0.1:3000"
T = "/home/z/my-project/scripts/.audit5d"
os.makedirs(T, exist_ok=True)
JAR = f"{T}/cookies.txt"
ROOTP = "/home/z/my-project"
SRC = "/home/z/my-project/aspnet/src"

def sh(cmd):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True).stdout

def curl_get(path, jar=True):
    b = f"-b {JAR} -c {JAR}" if jar else ""
    return subprocess.run(f'curl -s {b} -o "{T}/page.html" -w "%{{http_code}}" "{BASE}{path}"',
                          shell=True, capture_output=True, text=True).stdout, open(f"{T}/page.html", encoding="utf-8", errors="replace").read()

def grep_file(path, pattern, flags=""):
    p = subprocess.run(["rg", "-c", pattern, path] , capture_output=True, text=True)
    return p.returncode == 0

results = []

def check(phase, item, where, ok, detail=""):
    results.append((phase, item, where, ok, detail))
    mark = "✓" if ok else "✗"
    print(f"  {mark} [{phase}] {item}" + (f" — {detail}" if detail else ""))

# ---------- 1) لاگین ----------
print("== login ==")
subprocess.run(f'curl -s -c {JAR} -o "{T}/login.html" "{BASE}/Auth/Login"', shell=True, capture_output=True, text=True)
html = open(f"{T}/login.html", encoding="utf-8", errors="replace").read()
LT = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]*)"', html)
tok = LT.group(1) if LT else ""
subprocess.run(f'curl -s -b {JAR} -c {JAR} -o /dev/null -H "Content-Type: application/x-www-form-urlencoded" '
               f'--data-urlencode "__RequestVerificationToken={tok}" '
               f'--data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" {BASE}/Auth/Login',
               shell=True, capture_output=True, text=True)
code, notice = curl_get("/License/Notice")
if 'action="/License/Ack"' in notice:
    NT = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]*)"', notice)
    subprocess.run(f'curl -s -b {JAR} -c {JAR} -o /dev/null --data-urlencode "__RequestVerificationToken={NT.group(1) if NT else ""}" {BASE}/License/Ack',
                   shell=True, capture_output=True, text=True)
print(f"  login ok (notice has ack form: {'action=\"/License/Ack\"' in notice})")

# ---------- 2) اپ زنده ----------
print("== live app (sc-run deployed) ==")
code, dash = curl_get("/")
check("live", "داشبورد 200", "live", code == "200", f"code={code}")

# فاز ۹۱ — تب‌بندی تنظیمات + فاز ۸۶ — انتخاب تقویم + فاز ۹۴ — اطلاعات واقعی بانک + اسکرین‌سیور
code, settings = curl_get("/Settings")
check("live", "Settings 200", "live", code == "200", f"code={code}")
for needle, phase in [
    ("tab-locale", "فاز۹۱ فرم زبانه‌ای + زبان/تقویم/ارز"),
    ("tab-screensaver", "فاز۹۵ اسکرین‌سیور تب"),
    ("ssMinutes", "اسکرین‌سیور دقیقه"),
    ("ssSeconds", "اسکرین‌سیور ثانیه ← لازمِ کاربر"),
    ("ssRequirePassword", "اسکرین‌سیور رمز"),
    ("curSel", "فاز۹۱ ارز سلکت"),
    ("اطلاعات واقعی ثبت‌شده", "فاز۹۴ اطلاعات بانک"),
    ("tab-db", "تب پایگاه داده"),
]:
    check("live", needle, "Settings", needle in settings, phase)

# تقویم انتخابی فعلی + آپشن قمری
m = re.search(r'name="calendar"[^>]*>.*?</select>', settings, re.S)
if m:
    sel = m.group(0)
    opts = re.findall(r'<option value="([^"]+)"', sel)
    cur = re.findall(r'<option value="([^"]+)" selected', sel)
    check("live", "آپشن‌های تقویم", "Settings", len(opts) >= 3, f"opts={opts} cur={cur}")
else:
    check("live", "آپشن‌های تقویم", "Settings", False, "select یافت نشد")

# فاز ۸۱/پنج‌روزه — برون‌ریزی
for path in ["/Export/Employees", "/Export/Attendance"]:
    r = subprocess.run(f'curl -s -b {JAR} -c {JAR} -o "{T}/exp.bin" -w "%{{http_code}}" "{BASE}{path}"',
                       shell=True, capture_output=True, text=True).stdout
    sz = os.path.getsize(f"{T}/exp.bin")
    ok = r == "200" and sz > 500
    check("live", f"GET {path}", "live", ok, f"code={r} size={sz}")

# درون‌ریزی (ماژول Org)
for path, needle in [
    ("/Employees/Import", "Import form"),
    ("/Employees/ImportTemplate", None),
]:
    code, html2 = curl_get(path)
    check("live", f"GET {path}", "live", code == "200", f"code={code}")

# فاز ۹۲ — Updates + اعلان‌ها + سوییچ زبان
for path, needle in [
    ("/Updates", "فاز۹۲ به‌روزرسانی بسته‌ای"),
    ("/Notifications", "فاز۹۲ اعلان‌ها"),
    ("/Ui/Lang?l=en&r=/", "فاز۹۳ گذر زبان"),
]:
    code, html2 = curl_get(path)
    check("live", f"GET {path}", "live", code == "200", f"code={code} {needle}")
# بازگشت به فارسی برای ادامهٔ ممیزی (تست en فقط سالمی گذر را می‌سنجد)
curl_get("/Ui/Lang?l=fa&r=/")

# تقویم تعطیلات — نمایش باید از data-cal پیروی کند (نام ماه‌ها در همان تقویم)
code, hol = curl_get("/Holidays")
check("live", "GET /Holidays 200", "live", code == "200", f"code={code}")
mc = re.search(r'data-cal="(\w+)"', hol)
check("live", "هولی‌دی:data-cal", "/Holidays", mc is not None, f"cal={mc.group(1) if mc else '?'}")
MONTH_MARKS = {"hijri": ["جمادی", "شعبان", "رمضان", "محرم"],
               "jalali": ["فروردین", "بهمن", "اسفند", "دی"],
               "gregorian": ["Jan", "Feb", "Mar", "Dec"]}
for needle in MONTH_MARKS.get(mc.group(1) if mc else "jalali", MONTH_MARKS["jalali"]):
    check("live", f"هولی‌دی ماه:{needle}", "/Holidays", needle in hol)
check("live", "هولی‌دی:چیپ تقویم نمایشی", "/Holidays", "تقویم نمایشی" in hol)

# فاز ۹۴ — مرکز دانلود ابزار ویندوزی
code, dl = curl_get("/dl/")
for needle in ["Windows-Tools", "Docs-FA", "customer-FULL", "customer-MINIMAL", "source"]:
    check("live", f"dl-center:{needle}", "/dl/", needle in dl)
v979 = ("2.7.9" in dl) or ("۲.۷.۹" in dl)
check("live", "بدون نسخهٔ کهنهٔ 2.7.9", "/dl/", not v979)

# منوها — پیوند برون‌ریزی/درون‌ریزی در منوی کناری؟
m = re.search(r'<aside.*?</aside>', dash, re.S) or re.search(r'<nav.*?</nav>', dash, re.S)
menu_text = m.group(0) if m else dash
# درون‌ریزی/برون‌ریزی در جای خودشان: دکمه‌های صفحهٔ پرسنل + تردد روزانه + پنل زبان‌ها + قالب برون‌برد تردد
code, emp = curl_get("/Employees")
check("live", "دکمهٔ درون‌ریزی پرسنل", "/Employees", 'href="/Employees/Import"' in emp)
check("live", "دکمهٔ برون‌ریزی اکسل پرسنل", "/Employees", 'href="/Export/Employees"' in emp)
code, att = curl_get("/Attendance")
check("live", "دکمهٔ برون‌ریزی اکسل تردد", "/Attendance", "href=\"/Export/Attendance?date=" in att)
code, langs = curl_get("/Langs")
check("live", "درون‌ریزی بستهٔ ترجمه در پنل زبان", "/Langs", "/Langs/ImportForm" in langs)
check("live", "برون‌ریزی بستهٔ ترجمه در پنل زبان", "/Langs", "/Langs/Export?code=fa" in langs)

# ---------- 3) سورس ----------
print("== source markers ==")
SRCS = {
 "فاز۸۶ تقویم": ["AppSettings.Calendar|CalendarMode|name=\"calendar\""],
 "فاز۸۶ فرمول‌ساز Undo": ["undo|Undo"],
 "فاز۸۶ فرمول‌ساز به‌فارسی": ["به فارسی"],
 "فاز۸۷ زبان‌های ۷گانه": ["\"prs\"|\"ps\"|\"ka\"|\"ky\"|\"uz\"|\"tg\"|\"tk\""],
 "فاز۸۷ ImportEmbeddedLangPacks": ["ImportEmbeddedLangPacksAsync"],
 "فاز۸۸ IHostEnvironment": ["InitNewDatabaseAsync.*IHostEnvironment"],
 "فاز۸۹ ارقام بومی": ["ToPersianDigits|HrDigits"],
 "فاز۸۹ پیکر تاریخ": ["date.*picker|calendarPicker|HrDatePick|data-datepick"],
 "فاز۹۰ private.pem": ["privatePem|private.pem"],
 "فاز۹۰ زنگ اعلان": ["bell|zang|notif.*click"],
 "فاز۹۰ پورت 1433": ["1433"],
 "فاز۹۱ ارز": ["Currency.*catalog|CurrencyCode|currency"],
 "فاز۹۲ HrL10n": ["HrL10n"],
 "فاز۹۲ hrT/hrF": ["hrT\\(|hrF\\("],
 "فاز۹۳ updates.manage": ["updates\\.manage"],
 "فاز۹۳ پرچم SVG": ["hr-flag|HrFlags"],
 "فاز۹۳ LangSwitch": ["LangSwitch"],
 "فاز۹۴ make-update-package.ps1": ["make-update-package"],
 "فاز۹۴ build-installer.ps1": ["build-installer"],
 "فاز۹۴ MaskConnectionString": ["MaskConnectionString"],
 "فاز۹۵ دراپ‌داون anchor": ["transform:none|transform: none"],
 "فاز۹۵ nowrap btn": ["white-space:\\s*nowrap"],
 "اسکرین‌سیور js": ["hr-screensaver"],
 "فاز۹۷ تقویم تعطیلات DisplayMode": ["DisplayMode|displayCal|display-cal"],
 "فاز۹۷ ماه‌های قمری": ["شعبان|Sha[ʻ'`]?ban|شعبانِ"],
 "برون‌ریزی Export": ["ExportController|reports\\.export"],
 "درون‌ریزی Import": ["ImportCommit|ImportPreview|ImportTemplate|employees\\.import|org\\.employees"],
}
for label, pats in SRCS.items():
    hits = []
    for pat in pats:
        # جستجو در سورس اپ + پوشهٔ installer/ در ریشهٔ پروژه (فاز ۹۴)
        p = subprocess.run(["rg", "-l", "-e", pat, SRC, f"{ROOTP}/installer",
                            "--glob", "!**/bin/**", "--glob", "!**/obj/**",
                            "--glob", "!**/dl/**", "--glob", "!**/langpacks/**"],
                           capture_output=True, text=True)
        for line in p.stdout.splitlines():
            hits.append(line.replace(SRC + "/", ""))
    check("src", label, "source", len(hits) > 0, f"{len(hits)} فایل")

# ---------- جمع‌بندی ----------
bad = [r for r in results if not r[3]]
print(f"\n== SUMMARY: {len(results)} checks, {len(bad)} FAIL ==")
for r in bad:
    print(f"  ✗ [{r[0]}] {r[1]} ({r[2]}) {r[4]}")
json.dump([{"phase": r[0], "item": r[1], "where": r[2], "ok": r[3], "detail": r[4]} for r in results],
          open(f"{T}/audit.json", "w"), ensure_ascii=False, indent=1)
