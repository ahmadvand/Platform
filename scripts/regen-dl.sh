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
RUN=$ROOT/aspnet/sc-run
VER=2.7.38
LOG=$ROOT/scripts/regen-dl.log
FAST=0
[ "${1:-}" = "--fast" ] && FAST=1
mkdir -p "$DLDIR"

# گارد شبح‌ها — رندر مرکز/زیپ سورس از درخت آلوده ممنوع (دستور کاربر ۲۰۲۶-۱۱-۲۱)
bash scripts/ghost-guard.sh || exit 1
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
# قانون همیشگی کاربر: نام زیپ سورس حتماً تاریخ + ساعت داشته باشد (تیمزون تهران)
SRC_TS=$(TZ=Asia/Tehran date '+%Y-%m-%d-%H%M')
SRCZIP="$DLDIR/HR-Platform-Source-$VER-$SRC_TS.zip"
git -C "$ROOT" archive --format=zip -o "$SRCZIP" HEAD \
  && echo "source.zip OK -> $SRCZIP" || echo "source.zip FAILED"

# ---------- قانون همیشگی کاربر (دستور صریح ۲۰۲۶-۱۱-۱۰): فقط آخرین نسخهٔ سورس در مرکز دانلود ----------
# هر زیپ سورس قدیمی‌تر از تازه‌ساخته‌شده حذف می‌شود (شامل هم‌نام‌های زمان‌دار قبلی)؛
# بسته‌های مشتری/سورس نسخه‌های قدیمی هم پاک می‌شوند تا مرکز فقط نسخهٔ جاری + ابزارها + اسناد را نشان دهد.
ls -1t "$DLDIR"/HR-Platform-Source-*.zip 2>/dev/null | tail -n +2 | while read -r old; do
  rm -f "$old"; echo "removed old source zip: $(basename "$old")"
done
# فاز ۱۰۶ — استثنای زیپ سورس تازه: نام «HR-Platform-Source-$VER-*» با «HR-Platform-$VER-*» مچ
# نمی‌شود؛ بدون این استثنا، قانون پاک‌سازی زیپ سورسِ همین‌لحظه ساخته‌شده را هم می‌کشت.
find "$DLDIR" -maxdepth 1 -type f -name '*.zip' \
  ! -name "HR-Platform-$VER-*" \
  ! -name "HR-Platform-Source-$VER-*" \
  ! -name "HR-Platform-Windows-Tools-*" \
  ! -name "LicenseStudio.zip" ! -name "TranslationStudio.zip" \
  ! -name "*-Docs-FA.zip" -print -delete | sed 's/^/removed old-version zip: /'

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
# درس فاز ۱۲۹: کرش پایتون قورت داده نمی‌شود — شکست رندر یعنی شکست کل regen
# (وگرنه dlc بدون index.html می‌ماند و مرکز 404 می‌دهد با گزارش «OK»).
python3 "$ROOT/scripts/make-dl-center.py" \
  && echo "dl-center rendered OK" \
  || { echo "dl-center render FAILED — aborting regen"; exit 1; }
[ -f "$ROOT/aspnet/src/Platform.Web/wwwroot/dlc/index.html" ] \
  || { echo "dl-center index.html missing — aborting regen"; exit 1; }

# ---------- ۵) سینک مرکز رندرشده به اپ زندهٔ sc-run (ترمیم 2.7.19) ----------
# make-dl-center داخل src/wwwroot/dlc می‌نویسد و تا deploy بعدی منتقل نمی‌شد؛
# اکنون بلافاصله به sc-run/wwwroot/dlc هم کپی می‌شود تا مرکز بدون deploy تازه شود.
rsync -a --delete "$ROOT/aspnet/src/Platform.Web/wwwroot/dlc/" "$RUN/wwwroot/dlc/" \
  && echo "dl-center synced -> sc-run OK" || echo "dl-center sync FAILED"
echo "=== regen-dl done $(date '+%F %T') ==="
