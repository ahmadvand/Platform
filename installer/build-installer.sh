#!/usr/bin/env bash
# ======================================================================
# فاز ۹۲ — ساخت payload نصب‌کنندهٔ ویندوز (تجربهٔ MSI مانند)
# خروجی: installer/HR-Platform-Setup-payload/  (آماده برای ISCC.exe روی ویندوز)
#   app/     ← خروجی dotnet publish win-x64 (فایل‌های قابل اجرا روی IIS)
#   prereq/  ← (اختیاری) dotnet-hosting-8*.exe را خودتان اینجا بگذارید تا نصب آفلاین شود
# سپس روی ویندوز با Inno Setup 6:
#   ISCC.exe installer\HR-Platform-Setup.iss   →  dist\HR-Platform-Setup-<ver>.exe
# ======================================================================
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/aspnet"
VER="${1:-2.7.5}"
export PATH="$HOME/.dotnet:$PATH"

echo ">> dotnet publish win-x64 (Release, AppVersion=$VER)"
OUT="$ROOT/installer/HR-Platform-Setup-payload/app"
rm -rf "$OUT"; mkdir -p "$OUT" "$ROOT/installer/HR-Platform-Setup-payload/prereq"
dotnet build "$SRC/Platform.sln" -c Release --nologo -v q -p:WarningLevel=0
dotnet publish "$SRC/src/Platform.Web/Platform.Web.csproj" -c Release -o "$OUT" \
  -r win-x64 --self-contained false -p:WarningLevel=0 -p:AppVersion="$VER" -p:VendorName="HR Platform"
# بسته‌های دانلودی جزو کد نیستند
rm -rf "$OUT/wwwroot/dl" 2>/dev/null || true

echo "✅ payload آماده شد: $OUT"
echo "   (اختیاری) dotnet-hosting-8.0.x-win.exe را در installer/HR-Platform-Setup-payload/prereq/ بگذارید"
echo "   روی ویندوز:  ISCC.exe installer\\HR-Platform-Setup.iss  (متغیر محیطی HRP_VERSION=$VER)"
