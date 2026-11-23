#!/usr/bin/env python3
# فاز ۱۲۴ — راستی‌آزمایی زنده: هدر صفحه (page-h) و بنرهای notice در همهٔ صفحات هدف
import re, subprocess, sys

B = "http://localhost:3000"
T = "/tmp/uiv5"
subprocess.run(["rm", "-rf", T]); subprocess.run(["mkdir", "-p", T])
JAR = f"{T}/c.txt"

def curl(url, out, extra=None):
    cmd = ["curl", "-s", "-b", JAR, "-c", JAR, "-o", out]
    if extra:
        cmd += extra
    cmd.append(url)
    subprocess.run(cmd, check=True)

# ورود
curl(f"{B}/Auth/Login", f"{T}/login.html")
html = open(f"{T}/login.html", encoding="utf-8").read()
lt = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]*)"', html)
r = subprocess.run(["curl", "-s", "-b", JAR, "-c", JAR, "-o", "/dev/null",
                    "--data-urlencode", f"__RequestVerificationToken={lt.group(1)}",
                    "--data-urlencode", "identifier=admin",
                    "--data-urlencode", "secret=Admin@1234", f"{B}/Auth/Login"])
# پذیرش اعلان مجوز (در صورت وجود)
curl(f"{B}/License/Notice", f"{T}/notice.html")
nt = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]*)"', open(f"{T}/notice.html", encoding="utf-8").read())
if nt:
    subprocess.run(["curl", "-s", "-b", JAR, "-c", JAR, "-o", "/dev/null",
                    "--data-urlencode", f"__RequestVerificationToken={nt.group(1)}", f"{B}/License/Ack"])

PAGES = [
    ("", "داشبورد", ["page-h", "notice notice-"]),
    ("Parking", "پارکینگ", ["page-h"]),
    ("Visitors", "مراجعان", ["page-h"]),
    ("Cards", "کارت‌ها", ["page-h"]),
    ("Access", "کنترل تردد", ["page-h"]),
    ("Devices", "دستگاه‌ها", ["page-h", "notice notice-"]),
    ("Shifts", "شیفت‌ها", ["page-h", "notice notice-"]),
    ("DeviceGateway", "دروازهٔ دستگاه‌ها", ["page-h"]),
    ("Users", "کاربران", ["page-h"]),
    ("Audit", "ممیزی", ["page-h"]),
    ("Reports", "گزارش‌ساز", ["page-h"]),
    ("License", "مجوز", ["page-h"]),
    ("Settings", "تنظیمات", ["page-h"]),
    ("Org", "چارت سازمانی", ["page-h"]),
    ("Payroll", "حقوق و دستمزد", ["page-h"]),
    ("Requests", "درخواست‌ها", ["page-h"]),
    ("Self", "پرتال من", ["page-h"]),
    ("Notifications", "اعلان‌ها", ["page-h", "notice notice-ok"]),
]

fail = 0
for path, name, needles in PAGES:
    url = f"{B}/{path}" if path else B
    curl(url, f"{T}/p.html")
    src = open(f"{T}/p.html", encoding="utf-8").read()
    missing = [n for n in needles if n not in src]
    status = "OK " if not missing else "FAIL"
    if missing:
        fail += 1
    print(f"{status} {name:14s} {'پیدا نشد: ' + ', '.join(missing) if missing else ''}")

print(f"\nنتیجه: {len(PAGES) - fail}/{len(PAGES)} سبز")
sys.exit(1 if fail else 0)
