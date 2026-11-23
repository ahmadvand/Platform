@echo off
chcp 65001 >nul
title Install HrDeviceGateway Service
REM ============================================================
REM  نصب سرویس ویندوزی HrDeviceGateway
REM  این فایل باید با دسترسی Administrator اجرا شود (راست‌کلیک → Run as administrator)
REM ============================================================

net session >nul 2>nul
if %errorlevel% neq 0 (
    echo [خطا] این اسکریپت باید با دسترسی Administrator اجرا شود.
    pause
    exit /b 1
)

set SVC_NAME=HrDeviceGateway
set EXE_PATH=%~dp0publish\HrDeviceGateway.exe

if not exist "%EXE_PATH%" (
    echo [خطا] فایل %EXE_PATH% یافت نشد — ابتدا publish.bat را اجرا کنید.
    pause
    exit /b 1
)

echo توقف سرویس قبلی (در صورت وجود)…
sc stop %SVC_NAME% >nul 2>nul
timeout /t 2 /nobreak >nul

echo حذف نصب قبلی (در صورت وجود)…
sc delete %SVC_NAME% >nul 2>nul

echo ایجاد سرویس %SVC_NAME% …
sc create %SVC_NAME% binPath= "%EXE_PATH%" start= auto DisplayName= "HR Device Gateway"
if %errorlevel% neq 0 (
    echo [خطا] ایجاد سرویس ناموفق بود.
    pause
    exit /b 1
)

sc description %SVC_NAME% "Device Management Service - ADMS bridge + ZKTeco TCP (HR Attendance System)"

REM --- ثبت منبع Event Viewer (فاز ۱۲) — لاگ‌های سرویس با منبع اختصاصی دیده می‌شوند ---
echo ثبت منبع Event Viewer (HrDeviceGateway)…
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (-not [System.Diagnostics.EventLog]::SourceExists('HrDeviceGateway')) { New-EventLog -LogName Application -SourceName HrDeviceGateway }"

%windir%\system32\reg.exe add "HKLM\SYSTEM\CurrentControlSet\Services\%SVC_NAME%" /v FailureActions /t REG_BINARY /d 00000000000000000000000003000000140000000100000060ea00000100000060ea00000000000060ea0000 /f >nul
sc failure %SVC_NAME% reset= 86400 actions= restart/5000/restart/10000/restart/60000 >nul

echo شروع سرویس…
sc start %SVC_NAME%

echo.
echo ============================================================
echo  ✔ سرویس نصب و شروع شد.
echo    - کانفیگ پویا از پنل ادمین فعال است: سامانه ^> دستگاه‌ها ^> تنظیمات گیت‌وی
echo      (شروع/توقف پایش، فاصله‌ها، پورت ADMS، دستگاه‌ها — بدون ویرایش فایل و بدون ری‌استارت)
echo    - پل ADMS روی پورت پیکربندی‌شده گوش می‌دهد
echo    - لاگ‌ها: Event Viewer ^> Windows Logs ^> Application (منبع: HrDeviceGateway)
echo    - سلامت سرویس: http://localhost:8081/health
echo    - حالت کنسول برای اشکال‌زدایی: cd publish ^&^& HrDeviceGateway.exe
echo ============================================================
pause
