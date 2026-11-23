#!/bin/bash
# ======================================================================
# make-studio.sh — انتشار LicenseStudio (win-x64، تک‌فایل self-contained)
# خروجی: download/LicenseStudio.zip (برای مرکز دانلود /dl)
# بازسازی فاز ۷۴ — نسخهٔ فاز ۷۳ توسط جریان موازی از بین رفته بود
# ======================================================================
set -euo pipefail
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
ROOT=/home/z/my-project
STUDIO=$ROOT/tools/LicenseStudio
OUT=$ROOT/download/LicenseStudio.zip
VER=$(rg -o '<Version>([^<]+)</Version>' "$STUDIO/LicenseStudio.csproj" -r '$1' | head -1)
echo "== make-studio: version $VER =="

PUB=$ROOT/aspnet/publish/studio
rm -rf "$PUB"
cd "$STUDIO"
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o "$PUB" -v q --nologo

EXE=$(ls "$PUB"/LicenseStudio.exe 2>/dev/null || true)
[ -n "$EXE" ] || { echo "publish output missing"; exit 1; }
ls -la "$PUB"

mkdir -p "$ROOT/download"
rm -f "$OUT"
( cd "$PUB" && python3 - "$OUT" <<'PY'
import os, sys, zipfile
out = sys.argv[1]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for base, _, files in os.walk("."):
        for f in files:
            p = os.path.join(base, f)
            z.write(p, os.path.relpath(p, "."))
print("zipped ->", out)
PY
)
# راستی‌آزمایی نسخهٔ داخل بسته (utf16le در exe)
python3 - "$OUT" "$VER" <<'PY'
import sys, zipfile
data = zipfile.ZipFile(sys.argv[1]).read([n for n in zipfile.ZipFile(sys.argv[1]).namelist() if n.endswith('.exe')][0])
ver = sys.argv[2].encode('utf-16-le')
print("version-string in exe:", "OK" if ver in data else "MISSING")
assert ver in data
PY
ls -la "$OUT"
echo "== make-studio done =="
