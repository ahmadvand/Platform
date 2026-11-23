#!/bin/bash
# ======================================================================
# خودترمیمی مرکز دانلود — پس از ری‌بیلد سندباکس همهٔ خروجی‌های
# gitignore-شده (download/ و wwwroot/dl/) از بین می‌روند؛ این اسکریپت
# هر ۳ بسته را دوباره می‌سازد و مرکز دانلود /dl را رندر می‌کند.
# مصرف:  bash scripts/regen-dl.sh          (همهٔ بسته‌ها)
#        bash scripts/regen-dl.sh --fast   (فقط source.zip + مرکز؛ zips موجود را دست نمی‌زند)
# ======================================================================
set -uo pipefail
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
ROOT=/home/z/my-project
DLDIR=$ROOT/download
PUB=$ROOT/aspnet/publish/customer
VER=2.8.0
LOG=$ROOT/scripts/regen-dl.log
FAST=0
[ "${1:-}" = "--fast" ] && FAST=1
mkdir -p "$DLDIR"
exec >>"$LOG" 2>&1
echo "=== regen-dl start $(date '+%F %T') args=$* fast=$FAST ==="

zipdir() {  # zipdir <out.zip> — بستن محتوای پوشهٔ publish
  local out=$1
  rm -f "$out"
  ( cd "$PUB" && python3 - "$out" <<'PY'
import os, sys, zipfile
out = sys.argv[1]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for base, _, files in os.walk("."):
        for f in files:
            p = os.path.join(base, f)
            z.write(p, os.path.relpath(p, "."))
print("zipped ->", out)
PY
  )
  ls -la "$out"
}

# ---------- ۱) source.zip از git archive (سریع — همیشه) ----------
git -C "$ROOT" archive --format=zip -o "$DLDIR/HR-Platform-$VER-source.zip" HEAD \
  && echo "source.zip OK" || echo "source.zip FAILED"

# ---------- گیت حافظه: اگر قرار است بیلد سنگین انجام شود، اول منتظر سلامت اپ بمان ----------
# علت ریشه‌ای مرگ اپ در بوت ۲۰۲۶-۱۰-۲۶: بیلد هم‌زمان regen-dl با اپ روی سندباکس ۴GB
# → OOM اپ را قربانی کرد. حالا بیلدها تا سالم شدن اپ صبر می‌کنند و start-web.sh
# هم سوپروایزر ری‌اسپان دارد؛ رقابت حافظه‌ای دیگر اپ را نمی‌کشد.
# فاز ۶۲ — بوت فقط --fast صدا می‌زند؛ بیلد سنگین ۱۰دقیقه بعد از بوت (خارج از
# پنجرهٔ سلامت ۱۲۰ثانیه‌ای لبه برای ثبت مسیر preview) اجرا می‌شود.
FULL="$DLDIR/HR-Platform-$VER-customer-FULL.zip"
MIN="$DLDIR/HR-Platform-$VER-customer-MINIMAL-base-only.zip"
if [ "$FAST" -eq 1 ]; then
  echo ">> fast mode — skip heavy builds (source.zip + center only)"
else
if [ ! -f "$FULL" ] || [ ! -f "$MIN" ]; then
  echo ">> heavy build pending — waiting for app health first (max 6min)..."
  code=000
  for i in $(seq 1 72); do
    code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 http://localhost:3000/health 2>/dev/null || true)
    [ "$code" = "200" ] && { echo "app healthy (200) after ~$((i*5))s — proceeding with heavy builds"; break; }
    sleep 5
  done
  [ "$code" != "200" ] && echo "app not healthy within 6min — proceeding anyway (respawn supervisor will self-heal)"
fi

# ---------- ۲) بستهٔ کامل مشتری (هر ۵ ماژول + دروازه) ----------
if [ ! -f "$FULL" ]; then
  echo ">> building FULL package..."
  bash "$ROOT/scripts/build-customer.sh" "" "$VER" && zipdir "$FULL" && echo "FULL OK" || echo "FULL FAILED"
else
  echo "FULL exists, skip"
fi

# ---------- ۳) بستهٔ حداقلی (بدون هیچ DLL ماژول — شامل دروازه) ----------
if [ ! -f "$MIN" ]; then
  echo ">> building MINIMAL package..."
  bash "$ROOT/scripts/build-customer.sh" parking,attendance,access,visitors,payroll,gateway "$VER" && zipdir "$MIN" && echo "MINIMAL OK" || echo "MINIMAL FAILED"
else
  echo "MINIMAL exists, skip"
fi
fi   # end non-fast branch

# ---------- ۴) رندر مرکز دانلود داخل اپ ----------
python3 "$ROOT/scripts/make-dl-center.py" && echo "dl-center rendered OK"
echo "=== regen-dl done $(date '+%F %T') ==="
