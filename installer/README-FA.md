# نصب خودکار سامانه منابع انسانی روی ویندوز — مثل برنامه‌های نصبی MSI

فاز ۹۲ — به درخواست کاربر: «نصب راحت که خودش IIS و Site و ASP.NET را نصب کند و در آخر نرم‌افزار بالا بیاید.»
فاز ۹۴ — نسخهٔ ویندوزی اسکریپت‌های ساخت (build-installer.ps1 / make-update-package.ps1) — روی خودِ ویندوز، بدون نیاز به لینوکس.

## دو راه ساخت payload نصب‌کننده

### الف) روی ویندوز (فاز ۹۴ — جدید)
```
cd پوشهٔ پروژه
installer\build-installer.bat 2.7.11
rem یا مستقیم:
powershell -NoProfile -ExecutionPolicy Bypass -File installer\build-installer.ps1 -Version 2.7.11
```
اگر [Inno Setup 6](https://jrsoftware.org/isdl.php) نصب باشد، بلافاصله `dist\HR-Platform-Setup-2.7.11.exe` هم به‌طور خودکار کامپایل می‌شود؛ وگرنه دستور ISCC که در پایان چاپ می‌شود را بعد از نصب Inno Setup اجرا کنید.

## ⚠️ رفع خطای معروف: «File ... is not digitally signed» (فاز ۹۶)

اگر فایل ps1 را **مستقیم** در PowerShell اجرا کنید:
```
.\installer\build-installer.ps1 -Version "2.7.11"
```
و ویندوز این خطا را داد، فایل «گم» نشده است — **سیاست اجرای (Execution Policy) ویندوز روی Restricted است** و اجرای اسکریپت‌های امضاندارنشده را می‌بندد:
```
build-installer.ps1 : File D:\...\build-installer.ps1 is not digitally signed.
You cannot run this script on the current system. ...
    + CategoryInfo          : SecurityError: (:) [], PSSecurityException
    + FullyQualifiedErrorId : UnauthorizedAccess
```

**راه‌حل‌ها (به ترتیب پیشنهادی):**

1. **از فایل bat استفاده کنید (ساده‌ترین — همان کار با دابل‌کلیک):**
   ```
   installer\build-installer.bat 2.7.11
   ```
   این فایل خودش PowerShell را با `-ExecutionPolicy Bypass` صدا می‌زند؛ روی هر ویندوزی بدون هیچ تنظیمی کار می‌کند.

2. **اجرای مستقیم ps1 با Bypass (فقط برای همان یک اجرا):**
   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -Version 2.7.11
   ```

3. **باز کردن سیاست فقط برای نشست فعلی PowerShell** (بعد از بستن پنجره برمی‌گردد):
   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\installer\build-installer.ps1 -Version 2.7.11
   ```

4. **اگر فایل را از اینترنت/zip گرفته‌اید** ممکن است Windows «قفل» (Mark of the Web) داشته باشد:
   ```powershell
   Unblock-File .\installer\build-installer.ps1
   ```

> توصیهٔ امنیتی: از تغییر دائمی سیاست با `-Scope LocalMachine` خودداری کنید؛ `Bypass` فقط برای نشست/فراخوانیِ همان اسکریپت کافی است.

### ب) روی لینوکس (فاز ۹۲ — قبلی)
```
./installer/build-installer.sh 2.7.11
```

در هر دو حالت خروجی میانی `installer/HR-Platform-Setup-payload/app` است (خروجی dotnet publish win-x64).
(اختیاری برای نصب آفلاین: فایل `dotnet-hosting-8.0.x-win.exe` را در `installer/HR-Platform-Setup-payload/prereq/` بگذارید — از سایت مایکروسافت)

## دو راه نصب

### الف) نصب‌کنندهٔ تک‌فایلی (HR-Platform-Setup.exe — تجربهٔ MSI مانند)
1. payload را بسازید (بالا — ویندوز یا لینوکس)
2. روی ویندوز با Inno Setup 6 کامپایل کنید:
   ```
   set HRP_VERSION=2.7.5
   ISCC.exe installer\HR-Platform-Setup.iss
   ```
   خروجی: `dist\HR-Platform-Setup-2.7.5.exe`
3. روی سرور مقصد: اجرای `HR-Platform-Setup-2.7.5.exe` به‌عنوان Administrator — همان `Next → Next` و تمام:
   - IIS و قابلیت‌های لازم فعال می‌شود
   - NET 8 Hosting Bundle نصب می‌شود (از prereq آفلاین یا دانلود رسمی)
   - سایت IIS + Application Pool (No Managed Code) روی پورت ۸۰ ساخته می‌شود
   - وظیفهٔ زمان‌بندی‌شدهٔ «HR Platform Updater» ثبت می‌شود
   - سامانه بالا می‌آید و مرورگر روی **ویزارد راه‌اندازی اولیه** باز می‌شود

### ب) نصب با PowerShell (بدون Inno Setup)
```
payload را بسازید:  installer\build-installer.bat 2.7.5   (ویندوز)  یا  ./installer/build-installer.sh 2.7.11   (لینوکس)
پوشهٔ installer/ را به سرور ویندوزی ببرید و:
.\installer\Install-HRPlatform.ps1 -AppVersion "2.7.5" -Port 80 [-InstallDir "C:\HrPlatform"]
```

## پارامترهای Install-HRPlatform.ps1

| پارامتر | پیش‌فرض | توضیح |
|---|---|---|
| `-InstallDir` | `C:\HrPlatform` | محل نصب فایل‌ها و بانک |
| `-Port` | `80` | پورت سایت IIS |
| `-SiteName` | `HRPlatform` | نام سایت در IIS |
| `-AppPoolName` | `HRPlatformPool` | نام اپ‌پول (No Managed Code) |

## به‌روزرسانی بعدی — بدون نصب دوباره

### گام ۱ — ساخت بستهٔ به‌روزرسانی (روی دستگاه توسعه)

**ویندوز (فاز ۹۴):**
```powershell
cd پوشهٔ پروژه
powershell -ExecutionPolicy Bypass -File scripts\make-update-package.ps1 -Version 2.7.6 -NotesFa "رفع اشکال دستگاه‌ها" -NotesEn "Device fixes"
rem سوییچ‌های اختیاری:  -MinVersion 2.7.5   -IncludeAppSettings   -Out D:\dist   -FromPublish "D:\publish\آماده"
```

**لینوکس:**
```bash
./scripts/make-update-package.sh 2.7.6 --notes-fa "رفع اشکال دستگاه‌ها" --notes-en "Device fixes"
```

خروجی هر دو یکسان است: `dist/hr-update-2.7.6.zip` — شامل `manifest.json` + `bin/` + `wwwroot/` (بدون پوشهٔ dl) + `web.config`.

**نکته‌ها:**
- نسخهٔ بسته **باید جدیدتر از نسخهٔ نصب‌شده** باشد وگرنه پنل آن را نمی‌پذیرد.
- `appsettings.json` به‌طور پیش‌فرض داخل بسته نیست تا رشتهٔ اتصال/تنظیمات نصب مقصد بازنویسی نشود؛ فقط با سوییچ `-IncludeAppSettings` (یا `--appsettings`) بسته می‌شود.
- **به‌روزرسانی فقط یک ماژول:** هر ماژول یک DLL است (مثل `HrModules.Payroll.dll`)؛ اگر فقط همان تغییر کرده، بسته را فقط با همان DLL (و در صورت لزوم DLLهای وابسته) بسازید — کافی است یک پوشهٔ zip با ساختار زیر دستی بسازید:
  ```
  manifest.json        ← {"format":"hr-update-pack","version":"2.7.6","notes":{"fa":"…","en":"…"}}
  bin/HrModules.Payroll.dll
  ```
  (`App_Data` از هر بسته‌ای نادیده گرفته می‌شود — بانک، تنظیمات رمز `hrsettings.bin` و آپلودها هرگز بازنویسی نمی‌شوند.)

### گام ۲ — نصب از پنل ادمین
1. در پنل ادمین: **به‌روزرسانی سامانه** ← بارگذاری zip ← اعتبارسنجی خودکار (قالب، نسخه، امنیت مسیرها) ← دکمهٔ **نصب به‌روزرسانی**
   (پیش از نصب، پشتیبان خودکار از کد فعلی گرفته می‌شود)
2. سرویس را ری‌استارت کنید (IIS Manager یا `Stop-Website`/`Start-Website`) — وظیفهٔ
   «HR Platform Updater» فایل‌های در صف را خودکار جای‌گذاری می‌کند و مهاجرت‌های بانک
   داده در بوت بعدی خودکار اجرا می‌شوند. جزئیات کامل در خود صفحهٔ به‌روزرسانی پنل هست.
3. **بازگردانی در صورت نیاز:** همان صفحه ← جدول «پشتیبان‌های نصب» ← بازگردانی ← ری‌استارت.

## چیزهایی که دست نمی‌خورند
- `App_Data` (بانک، تنظیمات رمز hrsettings.bin، آپلودها، بسته‌های در صف) از هر به‌روزرسانی/پشتیبان بازنویسی نمی‌شود.
- ویزارد راه‌اندازی اولیه، فایل مجوز و MachineId سرور دست‌نخورده می‌مانند.
