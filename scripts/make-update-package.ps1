# ======================================================================
# فاز ۹۴ — ساخت «بستهٔ به‌روزرسانی» سامانه — نسخهٔ ویندوزی make-update-package.sh
#   usage (PowerShell):
#     powershell -ExecutionPolicy Bypass -File make-update-package.ps1 -Version 2.7.6 `
#                -NotesFa "رفع اشکال دستگاه‌ها" -NotesEn "Device fixes"
#   سوییچ‌ها:  -MinVersion 2.7.5    -IncludeAppSettings    -Out D:\dist
#              -FromPublish "D:\publish\customer"   (انتشار آماده — بدون بیلد)
# خروجی: dist\hr-update-<ver>.zip  — شامل manifest.json + bin\ + wwwroot\ (بدون dl) + web.config
# نصب: پنل ادمین ← «به‌روزرسانی سامانه» ← بارگذاری zip ← نصب (پشتیبان خودکار).
# ======================================================================
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Version,
    [string]$NotesFa = "",
    [string]$NotesEn = "",
    [string]$MinVersion = "",
    [switch]$IncludeAppSettings,
    [string]$Out = "",
    [string]$FromPublish = ""
)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Root = Split-Path -Parent $PSScriptRoot          # ریشهٔ ریپو (والد scripts/)
$Src  = Join-Path $Root "aspnet"
if (-not $Out -or $Out -eq "") { $Out = Join-Path $Root "dist" }

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+([-._].+)?$') {
    Write-Host "?? نسخه باید سه‌جزئی باشد مثل 2.7.6" -ForegroundColor Red
    exit 1
}

$Pub = $FromPublish
if (-not $Pub -or $Pub -eq "") {
    Write-Host ">> dotnet publish (Release, AppVersion=$Version)"
    $Pub = Join-Path $Root "dist\publish-update-$Version"
    if (Test-Path $Pub) { Remove-Item $Pub -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $Pub | Out-Null
    dotnet build "$Src\Platform.sln" -c Release --nologo -v q -p:WarningLevel=0
    if ($LASTEXITCODE -ne 0) { throw "dotnet build ناموفق بود" }
    dotnet publish "$Src\src\Platform.Web\Platform.Web.csproj" -c Release -o $Pub `
        -p:WarningLevel=0 -p:AppVersion=$Version -p:VendorName="HR Platform"
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ناموفق بود" }
}
if (-not (Test-Path "$Pub\Platform.Web.dll") -and -not (Test-Path "$Pub\bin\Platform.Web.dll")) {
    Write-Host "?? خروجی publish معتبر نیست: $Pub" -ForegroundColor Red
    exit 1
}

$Stage = Join-Path $Root "dist\stage-update-$Version"
if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path "$Stage\bin" "$Stage\wwwroot" | Out-Null

# چیدمان publish ماژول‌ها: bin کنار اپ (فاز ۶۹ — تخت) + wwwroot
if (Test-Path "$Pub\bin") {
    Copy-Item "$Pub\bin\*" "$Stage\bin\" -Recurse -Force
    if (Test-Path "$Pub\wwwroot") { Copy-Item "$Pub\wwwroot\*" "$Stage\wwwroot\" -Recurse -Force }
    Get-ChildItem $Pub -File | Where-Object { $_.Extension -eq ".dll" -or $_.Name -like "*.deps.json" } | Copy-Item -Destination "$Stage\bin\" -Force
    foreach ($f in @("web.config", "appsettings.json")) {
        if (Test-Path "$Pub\$f") { Copy-Item "$Pub\$f" "$Stage\$f" -Force }
    }
}
else {
    # publish تخت: فایل‌های اجرایی در bin سمت مقصد باید براقن — ساخت bin از ریشهٔ publish
    Copy-Item "$Pub\*" "$Stage\bin\" -Recurse -Force
    if (Test-Path "$Stage\bin\wwwroot") {
        Remove-Item "$Stage\wwwroot" -Recurse -Force
        Move-Item "$Stage\bin\wwwroot" "$Stage\wwwroot"
    }
}
# بسته‌های دانلودی جزو کد نیستند
if (Test-Path "$Stage\wwwroot\dl") { Remove-Item "$Stage\wwwroot\dl" -Recurse -Force }
if (-not $IncludeAppSettings) {
    foreach ($p in @("$Stage\appsettings.json", "$Stage\bin\appsettings.json")) {
        if (Test-Path $p) { Remove-Item $p -Force }
    }
}

# manifest.json
$m = [ordered]@{ format = "hr-update-pack"; version = $Version; notes = [ordered]@{ fa = $NotesFa; en = $NotesEn } }
if ($MinVersion -and $MinVersion -ne "") { $m["minVersion"] = $MinVersion }
if ($IncludeAppSettings) { $m["includeAppSettings"] = $true }
$m | ConvertTo-Json -Depth 5 | Out-File "$Stage\manifest.json" -Encoding utf8

# zip
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Zip = Join-Path $Out "hr-update-$Version.zip"
if (Test-Path $Zip) { Remove-Item $Zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($Stage, $Zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item $Stage -Recurse -Force
if (-not ($FromPublish -and $FromPublish -ne "")) { Remove-Item (Join-Path $Root "dist\publish-update-$Version") -Recurse -Force -ErrorAction SilentlyContinue }

$sz = "{0:N1} MB" -f ((Get-Item $Zip).Length / 1MB)
Write-Host ""
Write-Host "✅ بستهٔ به‌روزرسانی ساخته شد: $Zip ($sz)" -ForegroundColor Green
Write-Host "   نصب: پنل ادمین ← به‌روزرسانی سامانه ← بارگذاری ← نصب (پشتیبان خودکار گرفته می‌شود)"
Write-Host "   سپس سرویس را ری‌استارت کنید (Stop-Website/Start-Website) تا نسخهٔ جدید قطعاً فعال شود."
