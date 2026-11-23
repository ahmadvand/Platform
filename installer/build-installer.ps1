# ======================================================================
# فاز ۹۴ — ساخت payload نصب‌کنندهٔ ویندوز (تجربهٔ MSI مانند) — نسخهٔ ویندوزی build-installer.sh
#   usage:  powershell -ExecutionPolicy Bypass -File build-installer.ps1 [-Version 2.7.5]
#           یا دابل‌کلیک روی build-installer.bat (نسخه را می‌پرسد)
# خروجی:  installer\HR-Platform-Setup-payload\app   (+ پوشهٔ prereq برای نصب آفلاین)
# گام بعد (روی همین ویندوز، اگر Inno Setup 6 نصب باشد خودکار کامپایل می‌شود):
#   ISCC.exe installer\HR-Platform-Setup.iss   →  dist\HR-Platform-Setup-<ver>.exe
# ======================================================================
param(
    [string]$Version = "2.7.10",
    [string]$IsccPath = ""
)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Root   = Split-Path -Parent $PSScriptRoot          # ریشهٔ ریپو (والد installer/)
$Src    = Join-Path $Root "aspnet"
$Out    = Join-Path $PSScriptRoot "HR-Platform-Setup-payload\app"
$Prereq = Join-Path $PSScriptRoot "HR-Platform-Setup-payload\prereq"

Write-Host ">> dotnet publish win-x64 (Release, AppVersion=$Version)"
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Out, $Prereq | Out-Null

dotnet build "$Src\Platform.sln" -c Release --nologo -v q -p:WarningLevel=0
if ($LASTEXITCODE -ne 0) { throw "dotnet build ناموفق بود" }
dotnet publish "$Src\src\Platform.Web\Platform.Web.csproj" -c Release -o $Out `
    -r win-x64 --self-contained false -p:WarningLevel=0 -p:AppVersion=$Version -p:VendorName="HR Platform"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ناموفق بود" }
# بسته‌های دانلودی جزو کد نیستند
if (Test-Path "$Out\wwwroot\dl") { Remove-Item "$Out\wwwroot\dl" -Recurse -Force }

Write-Host ""
Write-Host "✅ payload آماده شد: $Out" -ForegroundColor Green
Write-Host "   (اختیاری برای نصب آفلاین) dotnet-hosting-8.0.x-win.exe را در این پوشه بگذارید:" -NoNewline
Write-Host " $Prereq"

# ---------- گام بعد: کامپایل Setup.exe با Inno Setup 6 (در صورت نصب بودن — خودکار) ----------
if (-not $IsccPath -or $IsccPath -eq "") {
    foreach ($c in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe")) {
        if (Test-Path $c) { $IsccPath = $c; break }
    }
}
if ($IsccPath -and (Test-Path $IsccPath)) {
    $env:HRP_VERSION = $Version
    Write-Host ">> ISCC.exe پیدا شد — کامپایل خودکار HR-Platform-Setup-$Version.exe ..."
    & $IsccPath (Join-Path $PSScriptRoot "HR-Platform-Setup.iss")
    if ($LASTEXITCODE -ne 0) { throw "کامپایل Inno Setup ناموفق بود" }
    $setupExe = Join-Path $Root ("dist\HR-Platform-Setup-" + $Version + ".exe")
    Write-Host "✅ نصب‌کنندهٔ تک‌فایلی ساخته شد: $setupExe" -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "   گام بعدی (نیازمند Inno Setup 6 — رایگان: https://jrsoftware.org/isdl.php):" 
    Write-Host "     ۱) دانلود و نصب Inno Setup 6"
    Write-Host "     ۲) در همان پنجرهٔ CMD:  set HRP_VERSION=$Version"
    Write-Host "     ۳)  `"%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe`"  installer\HR-Platform-Setup.iss"
    Write-Host "   خروجی: dist\HR-Platform-Setup-$Version.exe — اجرای آن روی سرور مقصد با حساب Administrator"
    Write-Host "   (یا بدون Inno Setup:  .\installer\Install-HRPlatform.ps1 -AppVersion `"$Version`" -Port 80)"
}
