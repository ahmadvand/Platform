@echo off
chcp 65001 >nul
title Publish HrDeviceGateway
echo ============================================================
echo  انتشار سرویس ویندوزی مدیریت دستگاه‌های تردد (C#/.NET 8)
echo ============================================================
echo.

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [خطا] .NET 8 SDK نصب نیست — از https://dotnet.microsoft.com/download/dotnet/8.0 نصب کنید
    pause
    exit /b 1
)

dotnet publish "%~dp0HrDeviceGateway\HrDeviceGateway.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0publish"
if %errorlevel% neq 0 (
    echo [خطا] انتشار ناموفق بود
    pause
    exit /b 1
)

echo.
echo ✔ خروجی: device-service-csharp\publish\HrDeviceGateway.exe
echo.
echo گام‌های بعدی:
echo   1) فایل publish\appsettings.json را بررسی کنید — فقط آدرس سرور (ApiBase) و
echo      حساب پل (svc_bridge) مهم است؛ بقیه تنظیمات از پنل ادمین خوانده می‌شود:
echo      سامانه ^> دستگاه‌ها ^> تنظیمات گیت‌وی
echo   2) اجرای install-service.bat با دسترسی Administrator
echo.
pause
