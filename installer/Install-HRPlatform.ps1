# ==============================================================================
# فاز ۹۲ — نصب‌کنندهٔ خودکار سامانه منابع انسانی روی ویندوز (درخواست کاربر:
#   «نصب راحت مثل MSI که خودش IIS و ASP.NET و سایت را نصب کند و در آخر نرم‌افزار بالا بیاید»)
# اجرا: PowerShell (Run as Administrator):
#   .\Install-HRPlatform.ps1 -AppVersion "2.7.5" [-SiteName "HRPlatform"] [-Port 80] [-InstallDir "C:\HrPlatform"]
# کارهای خودکار:
#   ۱) فعال‌سازی قابلیت‌های IIS (Windows Features)
#   ۲) نصب .NET 8 Hosting Bundle (اگر نبود — از پوشهٔ prereq یا دانلود رسمی مایکروسافت)
#   ۳) ساخت پوشه‌ها و کپی فایل‌های برنامه + دسترسی IIS_IUSRS
#   ۴) ساخت Application Pool (No Managed Code) و Website با پورت انتخابی + فایروال
#   ۵) ثبت وظیفهٔ زمان‌بندی‌شدهٔ «HR Platform Updater» برای نصب خودکار به‌روزرسانی‌ها
#   ۶) راه‌اندازی سایت + انتظار برای /health + باز کردن مرورگر روی ویزارد راه‌اندازی اولیه
# ==============================================================================
[CmdletBinding()]
param(
    [string]$InstallDir = "C:\HrPlatform",
    [string]$SiteName   = "HRPlatform",
    [int]   $Port       = 80,
    [string]$AppVersion = "0.0.0",
    [string]$AppPoolName = "HRPlatformPool",
    [string]$PrereqDir  = (Join-Path $PSScriptRoot "prereq"),
    [string]$AppFilesDir = (Join-Path $PSScriptRoot "app")
)

$ErrorActionPreference = "Stop"
function Info($m)  { Write-Host "  >> $m" -ForegroundColor Cyan }
function OK($m)    { Write-Host "  ✔ $m" -ForegroundColor Green }
function Die($m)   { Write-Host "  ✖ $m" -ForegroundColor Red; exit 1 }

# ---------- ۰) پیش‌نیازها ----------
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
      ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Die "این اسکریپت باید با Run as Administrator اجرا شود"
}
if (-not (Test-Path (Join-Path $AppFilesDir "Platform.Web.dll"))) {
    Die "پوشهٔ فایل‌های برنامه یافت نشد: $AppFilesDir (payload را با installer/build-installer.sh بسازید)"
}

# ---------- ۱) قابلیت‌های IIS ----------
Info "بررسی/فعال‌سازی قابلیت‌های IIS ..."
$feats = @(
    "IIS-WebServerRole","IIS-WebServer","IIS-CommonHttpFeatures","IIS-StaticContent",
    "IIS-DefaultDocument","IIS-HttpErrors","IIS-ApplicationDevelopment","IIS-HealthAndDiagnostics",
    "IIS-HttpLogging","IIS-Security","IIS-RequestFiltering","IIS-Performance",
    "IIS-HttpCompressionStatic","IIS-WebServerManagementTools","IIS-ManagementConsole",
    "IIS-WebSockets"
)
foreach ($f in $feats) {
    $state = (Get-WindowsOptionalFeature -Online -FeatureName $f -ErrorAction SilentlyContinue).State
    if ($state -ne "Enabled") {
        Info "فعال‌سازی $f"
        Enable-WindowsOptionalFeature -Online -FeatureName $f -All -NoRestart | Out-Null
    }
}
OK "IIS آماده است"

# ---------- ۲) NET 8. Hosting Bundle ----------
Info "بررسی NET 8. Hosting Bundle ..."
$hasHosting = $false
try {
    $dotnetPath = "$env:ProgramFiles\dotnet\dotnet.exe"
    if (Test-Path $dotnetPath) {
        $ver = & $dotnetPath --list-runtimes 2>$null | Select-String "Microsoft.AspNetCore.App 8\."
        if ($ver) { $hasHosting = $true }
    }
} catch {}
if (-not $hasHosting) {
    $bundle = Get-ChildItem -Path $PrereqDir -Filter "dotnet-hosting-8*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($bundle) {
        Info "نصب Hosting Bundle از $($bundle.FullName)"
        Start-Process -Wait -FilePath $bundle.FullName -ArgumentList "/quiet","/norestart"
    } else {
        Info "دانلود NET 8 Hosting Bundle. از مایکروسافت ..."
        $url = "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/8.0.11/dotnet-hosting-8.0.11-win.exe"
        $tmp = Join-Path $env:TEMP "dotnet-hosting-8-win.exe"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing
        Start-Process -Wait -FilePath $tmp -ArgumentList "/quiet","/norestart"
    }
    OK "NET 8 Hosting Bundle نصب شد (اگر Restart خواست، پس از راه‌اندازی دوباره اسکریپت را اجرا کنید)"
} else { OK "NET 8 از قبل نصب است" }

Import-Module WebAdministration -ErrorAction SilentlyContinue
Import-Module IISAdministration -ErrorAction SilentlyContinue

# ---------- ۳) فایل‌ها و دسترسی‌ها ----------
Info "کپی فایل‌های برنامه به $InstallDir ..."
if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Recurse -Force -Path "$AppFilesDir\*" -Destination $InstallDir
foreach ($sub in @("App_Data","App_Data\updates","App_Data\backups","App_Data\uploads","logs")) {
    New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir $sub) | Out-Null
}
& icacls $InstallDir /grant "IIS_IUSRS:(OI)(CI)M" /T | Out-Null
OK "فایل‌ها و دسترسی‌ها آماده شد"

# ---------- ۴) App Pool و Website ----------
Info "ساخت Application Pool و Website ..."
if (Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue) { Remove-IISAppPool -Name $AppPoolName }
New-IISAppPool -Name $AppPoolName
$p = Get-IISAppPool -Name $AppPoolName
$p.ManagedRuntimeVersion = ""        # No Managed Code — ANCM خودش dotnet را اجرا می‌کند
$p.ProcessModel.IdentityType = "ApplicationPoolIdentity"
$p.CommitChanges()
if (Get-IISSite -Name $SiteName -ErrorAction SilentlyContinue) { Remove-IISSite -Name $SiteName -Confirm:$false }
New-IISSite -Name $SiteName -PhysicalPath $InstallDir -BindingInformation "*:${Port}:" -Protocol http
Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationDefaults.preloadEnabled -Value $true
OK "سایت «$SiteName» روی پورت $Port ساخته شد"

# فایروال (اگر فعال است)
try {
    New-NetFirewallRule -DisplayName "HR Platform $Port" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -ErrorAction SilentlyContinue | Out-Null
} catch {}

# ---------- ۵) وظیفهٔ به‌روزرسان ----------
Info "ثبت وظیفهٔ زمان‌بندی‌شدهٔ به‌روزرسان ..."
$runner = Join-Path $InstallDir "update-runner.ps1"
Copy-Item -Force -Path (Join-Path $PSScriptRoot "update-runner.ps1") -Destination $runner -ErrorAction SilentlyContinue
$act  = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$runner`" -InstallDir `"$InstallDir`""
$trig = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 5) -RepetitionDuration (New-TimeSpan -Days 3650)
Register-ScheduledTask -TaskName "HR Platform Updater" -Action $act -Trigger $trig -RunLevel Highest -Force | Out-Null
OK "وظیفهٔ «HR Platform Updater» ثبت شد (هر ۵ دقیقه — نصب بسته‌های در صف)"

# ---------- ۶) اجرا ----------
Info "راه‌اندازی سایت ..."
Start-IISSite -Name $SiteName
$health = "http://localhost:$Port/health"
$ok200 = $false
for ($i = 0; $i -lt 30; $i++) {
    try { if ((Invoke-WebRequest -UseBasicParsing -Uri $health -TimeoutSec 3).StatusCode -eq 200) { $ok200 = $true; break } }
    catch { Start-Sleep -Seconds 2 }
}
if ($ok200) { OK "سامانه بالا آمد: $health" }
else { Write-Host "  ! سایت ساخته شد اما /health هنوز 200 نمی‌دهد — Event Viewer / logs را ببینید" -ForegroundColor Yellow }

Write-Host ""
Write-Host "════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  نصب کامل شد!" -ForegroundColor Green
Write-Host "  آدرس سامانه : http://localhost:$Port/" -ForegroundColor Green
Write-Host "  نسخه        : $AppVersion"
Write-Host "  مسیر نصب    : $InstallDir"
Write-Host "  قدم بعدی    : ویزارد راه‌اندازی اولیه در مرورگر باز می‌شود — بانک داده، زبان و فایل مجوز را همان‌جا تنظیم کنید."
Write-Host "════════════════════════════════════════════════" -ForegroundColor Green
try { Start-Process "http://localhost:$Port/" } catch {}
