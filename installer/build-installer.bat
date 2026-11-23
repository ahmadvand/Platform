@echo off
chcp 65001 >nul
rem ======================================================================
rem فاز ۹۴ — ساخت payload نصب‌کننده روی ویندوز (نسخهٔ ویندوزی build-installer.sh)
rem usage:  build-installer.bat [نسخه]          مثلاً:  build-installer.bat 2.7.10
rem بدون آرگومان، نسخهٔ پیش‌فرض 2.7.10 ساخته می‌شود.
rem ----------------------------------------------------------------------
rem فاز ۹۶ (رفع خطای کاربر): اجرای مستقیم build-installer.ps1 روی ویندوز با خطای
rem   "File ... is not digitally signed. You cannot load file ... on this system"
rem   (PSSecurityException / UnauthorizedAccess) متوقف می‌شد — سیاست اجرای (Execution
rem   Policy) ویندوزِ مشتری روی Restricted است. این .bat خودش PowerShell را با
rem   -ExecutionPolicy Bypass صدا می‌زند، پس دابل‌کلیک/اجرا از cmd همیشه کار می‌کند
rem   — بدون نیاز به تغییر تنظیمات ویندوز یا امضای دیجیتال فایل.
rem ----------------------------------------------------------------------
rem ⚠️ اگر به‌هر دلیل خواستید فایل ps1 را «مستقیم» در PowerShell اجرا کنید:
rem    powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -Version 2.7.10
rem    یا در همان نشست:   Set-ExecutionPolicy -Scope Process Bypass
rem ======================================================================
setlocal
set "VER=%~1"
if "%VER%"=="" (
  echo شمارهٔ نسخه را وارد کنید [پیش‌فرض 2.7.10]:
  set /p VER=
)
if "%VER%"=="" set "VER=2.7.10"
echo == ساخت payload نسخهٔ %VER% ==
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1" -Version "%VER%"
echo.
pause
