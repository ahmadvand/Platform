@echo off
chcp 65001 >nul
title Uninstall HrDeviceGateway Service
REM باید با دسترسی Administrator اجرا شود

net session >nul 2>nul
if %errorlevel% neq 0 (
    echo [خطا] این اسکریپت باید با دسترسی Administrator اجرا شود.
    pause
    exit /b 1
)

set SVC_NAME=HrDeviceGateway

echo توقف سرویس…
sc stop %SVC_NAME%
timeout /t 3 /nobreak >nul

echo حذف سرویس…
sc delete %SVC_NAME%

echo.
echo ✔ سرویس حذف شد. (فایل‌ها در پوشه publish باقی می‌مانند)
pause
