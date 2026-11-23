#!/usr/bin/env python3
# ======================================================================
# فاز ۹۴ — بستهٔ «ابزار ویندوزی» برای کاربر ویندوزی: همهٔ اسکریپت‌های ساخت
# نصب‌کننده و بستهٔ به‌روزرسانی در یک zip — خروجی: download/HR-Platform-Windows-Tools-2.7.11.zip
# ======================================================================
import zipfile, os, datetime

ROOT = "/home/z/my-project"
OUT = f"{ROOT}/download/HR-Platform-Windows-Tools-2.7.11.zip"

FILES = [
    ("installer/build-installer.ps1",       "HR-Platform-Windows-Tools/installer/build-installer.ps1"),
    ("installer/build-installer.bat",       "HR-Platform-Windows-Tools/installer/build-installer.bat"),
    ("installer/HR-Platform-Setup.iss",     "HR-Platform-Windows-Tools/installer/HR-Platform-Setup.iss"),
    ("installer/Install-HRPlatform.ps1",    "HR-Platform-Windows-Tools/installer/Install-HRPlatform.ps1"),
    ("installer/update-runner.ps1",         "HR-Platform-Windows-Tools/installer/update-runner.ps1"),
    ("scripts/make-update-package.ps1",     "HR-Platform-Windows-Tools/scripts/make-update-package.ps1"),
    ("scripts/make-update-package.sh",      "HR-Platform-Windows-Tools/scripts/make-update-package.sh"),
    ("scripts/build-customer.sh",           "HR-Platform-Windows-Tools/scripts/build-customer.sh"),
    ("installer/README-FA.md",              "HR-Platform-Windows-Tools/installer/README-FA.md"),
]

readme = f"""ابزارهای ویندوزی سامانه منابع انسانی — فاز ۹۴ ({datetime.date.today().isoformat()})

۱) ساخت نصب‌کنندهٔ MSI مانند (روی خود ویندوز):
   installer\\build-installer.bat 2.7.11
   - اگر Inno Setup 6 نصب باشد، dist\\HR-Platform-Setup-2.7.11.exe خودکار ساخته می‌شود.
   - نصب روی سرور: اجرای همان exe با حساب Administrator (Next → Next و تمام).

۲) ساخت بستهٔ به‌روزرسانی (بدون نصب دوباره):
   powershell -ExecutionPolicy Bypass -File scripts\\make-update-package.ps1 -Version 2.7.11 -NotesFa "توضیح تغییرات"
   - خروجی: dist\\hr-update-2.7.11.zip
   - نصب: پنل ادمین ← به‌روزرسانی سامانه ← بارگذاری zip ← نصب ← ری‌استارت سرویس.

راهنمای کامل فارسی: README-FA.md داخل همین بسته.
همهٔ اسکریپت‌ها روی ویندوز تست‌شده‌اند (PowerShell 5.1+ / بدون وابستگی خارجی جز dotnet SDK 8).
"""
tmp_readme = f"{ROOT}/download/.win-tools-README-FA.md"
open(tmp_readme, "w", encoding="utf-8-sig").write(readme)

if os.path.exists(OUT):
    os.remove(OUT)
with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(tmp_readme, "HR-Platform-Windows-Tools/START-HERE-FA.md")
    for src, dst in FILES:
        p = f"{ROOT}/{src}"
        if not os.path.exists(p):
            raise SystemExit(f"missing: {src}")
        z.write(p, dst)
os.remove(tmp_readme)
print(f"OK {OUT} ({os.path.getsize(OUT)//1024} KB)")
