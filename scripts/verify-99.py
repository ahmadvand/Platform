#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۹۹ — راستی‌آزمایی زندهٔ نسخهٔ 2.7.12: روسی + مرکز درون‌ریزی/برون‌ریزی + برون‌ریزی‌های جدید"""
import subprocess, re, json

BASE = "http://127.0.0.1:3000"
T = "/home/z/my-project/scripts/.audit99"
JAR = f"{T}/cookies.txt"
ok_all = True

def curl(path, data=None):
    cmd = f'curl -sL -b {JAR} -c {JAR} -o "{T}/p.html" -w "%{{http_code}}" --max-time 25 "{BASE}{path}"'
    if data is not None:
        cmd = f'curl -sL -b {JAR} -c {JAR} -o "{T}/p.html" -w "%{{http_code}}" --max-time 25 -X POST "{BASE}{path}"'
    subprocess.run(cmd, shell=True, capture_output=True, text=True)
    try:
        return open(f"{T}/p.html", encoding="utf-8", errors="replace").read()
    except Exception:
        return ""

def code_of(path):
    r = subprocess.run(f'curl -sL -b {JAR} -c {JAR} -o "{T}/p.html" -w "%{{http_code}}" --max-time 25 "{BASE}{path}"',
                       shell=True, capture_output=True, text=True)
    return r.stdout.strip()

import os
os.makedirs(T, exist_ok=True)

# ---------- login ----------
subprocess.run(f'curl -s -c {JAR} -o "{T}/login.html" "{BASE}/Auth/Login"', shell=True, capture_output=True, text=True)
html = open(f"{T}/login.html", encoding="utf-8", errors="replace").read()
LT = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]*)"', html)
subprocess.run(f'curl -s -b {JAR} -c {JAR} -o /dev/null -H "Content-Type: application/x-www-form-urlencoded" '
               f'--data-urlencode "__RequestVerificationToken={LT.group(1) if LT else ""}" '
               f'--data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" {BASE}/Auth/Login',
               shell=True, capture_output=True, text=True)
notice = curl("/License/Notice")
if 'action="/License/Ack"' in notice:
    NT = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]*)"', notice)
    subprocess.run(f'curl -s -b {JAR} -c {JAR} -o /dev/null --data-urlencode "__RequestVerificationToken={NT.group(1) if NT else ""}" {BASE}/License/Ack',
                   shell=True, capture_output=True, text=True)

def check(name, cond, detail=""):
    global ok_all
    mark = "✓" if cond else "✗"
    if not cond: ok_all = False
    print(f"  {mark} {name}" + (f" — {detail}" if detail else ""))

print("== نسخه و روسی ==")
import html as _html
langs = _html.unescape(curl("/Langs"))
check("پنل زبان‌ها — روسی در فهرست", "Русский" in langs, "روسی در فهرست")
check("پرچم روسی (ru.svg) سرو می‌شود", code_of("/img/flags/ru.svg") == "200")
check("پرچم ru در پنل زبان‌ها", "/img/flags/ru.svg" in langs)
dash = curl("/")
fm = re.search(r'(2\.7\.\d+|[۰-۹]+\.[۰-۹]+\.[۰-۹]+)', dash)
ver = (fm.group(1) if fm else "?").translate(str.maketrans("۰۱۲۳۴۵۶۷۸۹", "0123456789"))
check("نسخهٔ فوتر = 2.7.12", ver == "2.7.12", ver)

print("== مرکز درون‌ریزی/برون‌بری ==")
dt = curl("/DataTransfer")
check("GET /DataTransfer 200", "درون‌ریزی و برون‌بری" in dt)
for needle, label in [
    ("/Export/Employees", "کارت پرسنل"), ("/Export/Attendance?date=", "کارت تردد روزانه"),
    ("/Attendance/Exports", "کارت تاریخچهٔ تردد"), ("/Payroll", "کارت حقوق"),
    ("/DataTransfer/Companies", "کارت شرکت‌ها"), ("/DataTransfer/Users", "کارت کاربران"),
    ("/DataTransfer/Roles", "کارت نقش‌ها"), ("/Org/ExportDepartments", "کارت واحدها"),
    ("/Org/ExportPositions", "کارت پست‌ها"), ("/Holidays/Export", "کارت تعطیلات"),
    ("/Visitors/ExportVisitors", "کارت بازدیدکنندگان"), ("/Visitors/ExportVisitLogs", "کارت رهگیری"),
    ("/Parking/ExportVehicles", "کارت خودروها"), ("/Parking/ExportPermits", "کارت مجوز پارکینگ"),
    ("/Langs", "کارت بستهٔ ترجمه"), ("/Employees/Import", "درون‌ریزی پرسنل"),
    ("/Employees/ImportTemplate", "قالب درون‌ریزی"),
]:
    check(label, needle in dt)

print("== برون‌ریزی‌های جدید (HTTP + امضای xlsx) ==")
for path in ["/DataTransfer/Companies", "/DataTransfer/Users", "/DataTransfer/Roles",
             "/Org/ExportDepartments", "/Org/ExportPositions", "/Holidays/Export",
             "/Visitors/ExportVisitors", "/Visitors/ExportVisitLogs",
             "/Parking/ExportVehicles", "/Parking/ExportPermits", "/Export/Employees"]:
    c = code_of(path)
    head = open(f"{T}/p.html", "rb").read(2)
    check(f"GET {path}", c == "200" and head == b"PK", f"code={c}")

print("== DB: زبان روسی seeded ==")
import sqlite3
db = sqlite3.connect("/home/z/my-project/aspnet/sc-run/hr.db")
defs = dict(db.execute("SELECT Code, Title FROM HrLangDefs").fetchall())
check("HrLangDefs دارد ru", "ru" in defs, str(defs.get("ru")))
cnt = db.execute("SELECT COUNT(*) FROM HrLangEntries WHERE LangCode='ru'").fetchone()[0]
check("HrLangEntries روسی >= 3000", cnt >= 3000, f"{cnt} کلید")

print(f"\n== SUMMARY: {'ALL GREEN' if ok_all else 'FAILURES PRESENT'} ==")
