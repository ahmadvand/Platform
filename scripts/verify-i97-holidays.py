#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# فاز ۹۷ — راستی‌آزمایی تقویم تعطیلات با هر سه تقویم نمایشی + درستی ستون روز هفته
import re, subprocess, sys

BASE = "http://localhost:3000"
JAR = "/tmp/hr-j97"
TMP = "/tmp/hr-v97.html"
CURL = ["curl", "-s", "-b", JAR, "-c", JAR]

def sh(args, data=None):
    cmd = list(CURL) + ["-o", TMP] + args
    if data:
        cmd += ["-d", data]
    subprocess.run(cmd, capture_output=True, text=True)
    try:
        return open(TMP, encoding="utf-8", errors="replace").read()
    except FileNotFoundError:
        return ""

def code(path):
    return subprocess.run(["curl", "-s", "-o", "/dev/null", "-w", "%{http_code}", "-b", JAR,
                           "--max-time", "15", BASE + path],
                          capture_output=True, text=True).stdout.strip()

# ---------- لاگین (در صورت نبود نشست فعال) ----------
probe = subprocess.run(["curl", "-s", "-o", "/dev/null", "-w", "%{http_code}", "-b", JAR, "-c", JAR,
                        BASE + "/Holidays"], capture_output=True, text=True).stdout.strip()
if probe == "200":
    print("LOGIN: already-authenticated (session alive)")
else:
    html = sh([BASE + "/Auth/Login"])
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    if not m:
        print("NO-TOKEN"); sys.exit(1)
    lc = subprocess.run(["curl", "-s", "-b", JAR, "-c", JAR, "-o", "/dev/null", "-w", "%{http_code}",
                         "-d", "identifier=admin", "-d", "secret=Admin@1234",
                         "-d", "__RequestVerificationToken=" + m.group(1), BASE + "/Auth/Login"],
                        capture_output=True, text=True).stdout.strip()
    print("LOGIN:", lc)
    assert lc == "302"

# گیت مجوز
nhtml = sh([BASE + "/License/Notice"])
m2 = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', nhtml)
if m2 and ("License/Ack" in nhtml or "پذیرش" in nhtml):
    sh(["-o", "/dev/null", "-d", "__RequestVerificationToken=" + m2.group(1), BASE + "/License/Ack"])
    print("LICENSE-ACK: done")

def set_cal(val):
    html = sh([BASE + "/Holidays"])
    m3 = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    sh(["-w", "%{http_code}",
        "-d", "calendar=" + val, "-d", "__RequestVerificationToken=" + m3.group(1),
        BASE + "/Settings/SaveCalendar"])

def holidays_info():
    html = sh([BASE + "/Holidays"])
    cal = re.search(r'data-cal="(\w+)"', html)
    h2 = re.search(r'<h2>([^<]*تقویم[^<]*)</h2>', html)
    chip2 = re.findall(r'<strong style="color:var\(--ink-2\)">([^<]*)</strong>', html)
    col = None
    for row in re.findall(r'<tr>(.*?)</tr>', html, re.S):
        cells = re.findall(r'<td[^>]*>', row)
        for i, c in enumerate(cells):
            if 'class="today' in c:
                col = i
    return (cal.group(1) if cal else "?"), (h2.group(1).strip() if h2 else "?"), \
           (chip2[0] if chip2 else "?"), col

ok = True
# ---------- شمسی ----------
set_cal("jalali")
c, h2, chip, col = holidays_info()
print("JALALI:  cal=%s  header=%r  chip=%r  today-col=%s (wait 2=دوشنبه)" % (c, h2, chip, col))
if c != "jalali" or "آبان" not in h2 or col != 2: ok = False

# ---------- قمری ----------
set_cal("hijri")
c, h2, chip, col = holidays_info()
print("HIJRI:   cal=%s  header=%r  chip=%r  today-col=%s (wait 2=دوشنبه)" % (c, h2, chip, col))
if c != "hijri" or col != 2: ok = False
if not any(mn in h2 for mn in ("محرم", "صفر", "ربیع", "جمادی", "رجب", "شعبان", "رمضان", "شوال", "ذی")): ok = False
if not any(yh in h2 for yh in ("۱۴۴۶", "۱۴۴۷", "۱۴۴۸", "۱۴۴۹")): ok = False

# ---------- ثبت تعطیلی قمری در ماه جاری قمری + حذف ----------
html = sh([BASE + "/Holidays"])
hy = re.search(r"تقویم [^<]*</h2>", html)
# سال/ماه قمری جاری از همان صفحه (فرم فرزندان hidden jy + select jm)
jy_cur = re.search(r'name="jy" value="(\d+)"', html)
jm_cur = re.search(r'<select name="jm">.*?<option value="(\d+)" selected=', html, re.S)
jy_v, jm_v = jy_cur.group(1), jm_cur.group(1)
m4 = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
r = sh(["-d", "jy=" + jy_v, "-d", "jm=" + jm_v, "-d", "jd=20", "-d", "title=تست قمری فاز ۹۷",
        "-d", "__RequestVerificationToken=" + m4.group(1), BASE + "/Holidays/Add"])
added = "h-title" in sh([BASE + "/Holidays"])
import sqlite3
con = sqlite3.connect("/home/z/my-project/aspnet/sc-run/hr.db")
row = con.execute("select Date,JYear,JMonth,JDay from Holidays where Title='تست قمری فاز ۹۷'").fetchall()
good = bool(row) and row[0][0].startswith(("2026", "2027")) and str(row[0][1]) == jy_v
print("ADD-HIJRI:", "cell-shown" if added else "CELL-MISSING", "| DB:", row, "| jy/jm:", jy_v, jm_v)
if not good: ok = False
html = sh([BASE + "/Holidays"])
m5 = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
hid = re.search(r'name="id" value="([^"]+)"[^>]*><input type="hidden" name="y" value="' + jy_v + '"', html)
if hid:
    sh(["-o", "/dev/null", "-d", "id=" + hid.group(1), "-d", "y=" + jy_v, "-d", "m=" + jm_v,
        "-d", "__RequestVerificationToken=" + m5.group(1), BASE + "/Holidays/Delete"])
    con2 = sqlite3.connect("/home/z/my-project/aspnet/sc-run/hr.db")
    left = con2.execute("select count(*) from Holidays where Title='تست قمری فاز ۹۷'").fetchone()[0]
    print("CLEANUP:", "deleted (rows left: %d)" % left)

# ---------- میلادی ----------
set_cal("gregorian")
c, h2, chip, col = holidays_info()
print("GREGOR:  cal=%s  header=%r  chip=%r  today-col=%s (wait 2=دوشنبه)" % (c, h2, chip, col))
if c != "gregorian" or col != 2: ok = False

# ---------- بازگشت به شمسی ----------
set_cal("jalali")
c, h2, chip, col = holidays_info()
print("RESTORE: cal=%s  header=%r" % (c, h2))

# ---------- دود تست مسیرها ----------
ROUTES = ["/", "/Employees", "/Employees/Import", "/Attendance", "/Attendance/Exports",
          "/Holidays", "/Settings", "/Export/Employees", "/Langs", "/Setup"]
bad = [r for r in ROUTES if code(r) not in ("200", "302")]
print("ROUTES:", "ALL-OK" if not bad else "BAD=%s" % bad)
print("RESULT:", "PASS" if ok and not bad else "FAIL")
sys.exit(0 if ok and not bad else 1)
