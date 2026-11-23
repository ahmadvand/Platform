# ==============================================================================
# فاز ۹۲ — وظیفهٔ زمان‌بندی‌شدهٔ ویندوز «HR Platform Updater»
# هر ۵ دقیقه اجرا می‌شود: اگر apply.json موجود باشد یعنی ادمین در پنل، «نصب به‌روزرسانی»
# را زده اما فایل‌های در حال اجرا قفل بودند؛ این اسکریپت اپ‌پول را می‌ایستاند، فایل‌های
# استیجینگ را جای‌گذاری می‌کند، علامت را حذف می‌کند و اپ‌پول را دوباره روشن می‌کند —
# نسخهٔ جدید بدون دخالت دستی فعال می‌شود.
# ==============================================================================
[CmdletBinding()]
param(
    [string]$InstallDir = "C:\HrPlatform",
    [string]$AppPoolName = "HRPlatformPool"
)
$ErrorActionPreference = "SilentlyContinue"
$flag = Join-Path $InstallDir "App_Data\updates\apply.json"
if (-not (Test-Path $flag)) { exit 0 }

function Log($m) { Add-Content -Path (Join-Path $InstallDir "logs\update-runner.log") -Value ("[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $m) }
Log "apply.json یافت شد — شروع نصب به‌روزرسانی"

$staging = $null
try {
    $j = Get-Content $flag -Raw | ConvertFrom-Json
    $staging = $j.staging
} catch { Log "خواندن apply.json ناموفق: $_"; exit 1 }

if (-not $staging -or -not (Test-Path $staging)) { Log "استیجینگ غایب: $staging — علامت حذف می‌شود"; Remove-Item $flag -Force; exit 1 }

Import-Module WebAdministration -ErrorAction SilentlyContinue
Import-Module IISAdministration -ErrorAction SilentlyContinue

# ۱) توقف اپ‌پول تا قفل فایل‌ها باز شود
Log "توقف اپ‌پول $AppPoolName"
Stop-WebAppPool -Name $AppPoolName
$deadline = (Get-Date).AddSeconds(60)
do {
    Start-Sleep -Seconds 2
    $state = (Get-WebAppPoolState -Name $AppPoolName).Value
} while ($state -ne "Stopped" -and (Get-Date) -lt $deadline)

# ۲) کپی فایل‌های استیجینگ روی نصب (همان قواعد UpdateApplier: App_Data و manifest و logs رد می‌شوند)
try {
    Get-ChildItem -Path $staging -Directory | Where-Object { $_.Name -notin @("App_Data","logs") } | ForEach-Object {
        $dest = Join-Path $InstallDir $_.Name
        Copy-Item -Recurse -Force -Path $_.FullName -Destination $dest
    }
    Get-ChildItem -Path $staging -File | Where-Object { $_.Name -ne "manifest.json" -and $_.Name -notlike "appsettings*" } | ForEach-Object {
        Copy-Item -Force -Path $_.FullName -Destination $InstallDir
    }
    Remove-Item $flag -Force
    Log "فایل‌ها جای‌گذاری شد — راه‌اندازی دوبارهٔ اپ‌پول"
} catch {
    Log "خطای جای‌گذاری: $_"
    exit 1
}

# ۳) اجرای دوبارهٔ اپ‌پول
Start-WebAppPool -Name $AppPoolName
Log "به‌روزرسانی کامل شد ✓"
