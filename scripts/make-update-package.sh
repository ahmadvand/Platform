#!/usr/bin/env bash
# ======================================================================
# فاز ۹۲ — ساخت «بستهٔ به‌روزرسانی» سامانه (درخواست کاربر: مکانیسم به‌روزرسانی ماژول‌ها)
#   usage: ./scripts/make-update-package.sh <new-version> [--notes-fa "..."] [--notes-en "..."]
#          [--min-version 2.7.5] [--appsettings] [--out dist]
# خروجی: dist/hr-update-<ver>.zip — شامل manifest.json + bin/ + wwwroot/ (بدون dl) + web.config
# نصب: پنل ادمین ← «به‌روزرسانی سامانه» ← بارگذاری zip ← نصب (پشتیبان خودکار).
# قالب کامل manifest در صفحهٔ به‌روزرسانی پنل ادمین مستند است.
# ======================================================================
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/aspnet"

VER=""
NOTES_FA="" NOTES_EN="" MINVER="" APPSETTINGS=0 OUT="$ROOT/dist" FROM=""
while [ $# -gt 0 ]; do
  case "$1" in
    --notes-fa) NOTES_FA="$2"; shift 2;;
    --notes-en) NOTES_EN="$2"; shift 2;;
    --min-version) MINVER="$2"; shift 2;;
    --appsettings) APPSETTINGS=1; shift;;
    --out) OUT="$2"; shift 2;;
    --from-publish) FROM="$2"; shift 2;;   # انتشار آماده (مثلاً publish/customer) — بدون بیلد
    -h|--help) sed -n '2,8p' "$0"; exit 0;;
    *) if [ -z "$VER" ]; then VER="$1"; shift; else echo "?? آرگومان ناشناخته: $1" >&2; exit 1; fi;;
  esac
done
[ -z "$VER" ] && { echo "usage: $0 <new-version> [--notes-fa ...] [--notes-en ...]"; exit 1; }
echo "$VER" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+([-._].+)?$' || { echo "?? نسخه باید سه‌جزئی باشد مثل 2.7.6"; exit 1; }

PUB="$FROM"
if [ -z "$PUB" ]; then
  export PATH="$HOME/.dotnet:$PATH"
  echo ">> dotnet publish (Release, AppVersion=$VER)"
  PUB="$ROOT/dist/publish-update-$VER"
  rm -rf "$PUB"; mkdir -p "$(dirname "$PUB")"
  dotnet build "$SRC/Platform.sln" -c Release --nologo -v q -p:WarningLevel=0
  dotnet publish "$SRC/src/Platform.Web/Platform.Web.csproj" -c Release -o "$PUB" \
    -p:WarningLevel=0 -p:AppVersion="$VER" -p:VendorName="HR Platform"
fi
[ -f "$PUB/Platform.Web.dll" ] || [ -f "$PUB/bin/Platform.Web.dll" ] || {
  echo "?? خروجی publish معتبر نیست: $PUB"; exit 1; }

STAGE="$ROOT/dist/stage-update-$VER"
rm -rf "$STAGE"; mkdir -p "$STAGE/bin" "$STAGE/wwwroot"
# چیدمان publish ماژول‌ها: bin کنار اپ (فاز ۶۹ — تخت) + wwwroot
if [ -d "$PUB/bin" ]; then
  cp -r "$PUB/bin/." "$STAGE/bin/"
  [ -d "$PUB/wwwroot" ] && cp -r "$PUB/wwwroot/." "$STAGE/wwwroot/"
  cp -f "$PUB"/*.dll "$PUB"/*.deps.json "$STAGE/bin/" 2>/dev/null || true
  for f in web.config appsettings.json; do [ -f "$PUB/$f" ] && cp -f "$PUB/$f" "$STAGE/$f"; done
else
  # publish تخت: فایل‌های اجرایی در bin سمت مقصد باید براقن — ساخت bin از ریشهٔ publish
  cp -r "$PUB/." "$STAGE/bin/"
  [ -d "$STAGE/bin/wwwroot" ] && { rm -rf "$STAGE/wwwroot"; mv "$STAGE/bin/wwwroot" "$STAGE/wwwroot"; }
fi
# بسته‌های دانلودی جزو کد نیستند
rm -rf "$STAGE/wwwroot/dl" 2>/dev/null || true
if [ "$APPSETTINGS" = "0" ]; then rm -f "$STAGE/appsettings.json" "$STAGE/bin/appsettings.json" 2>/dev/null || true; fi

python3 - "$STAGE/manifest.json" <<PY
import json, sys
m = {"format": "hr-update-pack", "version": "$VER",
     "notes": {"fa": r'''$NOTES_FA''', "en": r'''$NOTES_EN'''}}
if "$MINVER": m["minVersion"] = "$MINVER"
if $APPSETTINGS: m["includeAppSettings"] = True
json.dump(m, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY

mkdir -p "$OUT"
ZIP="$OUT/hr-update-$VER.zip"
rm -f "$ZIP"
( cd "$STAGE" && python3 -c "
import zipfile, os, sys
root = '.'
with zipfile.ZipFile('$ZIP', 'w', zipfile.ZIP_DEFLATED) as z:
    for r, d, fs in os.walk(root):
        for f in fs:
            p = os.path.join(r, f)
            z.write(p, os.path.relpath(p, root))
print('zip ok')
" )
rm -rf "$STAGE" "$ROOT/dist/publish-update-$VER" 2>/dev/null || true
echo "✅ بستهٔ به‌روزرسانی ساخته شد: $ZIP ($(du -h "$ZIP" | cut -f1))"
echo "   نصب: پنل ادمین ← به‌روزرسانی سامانه ← بارگذاری ← نصب"
