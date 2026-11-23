#!/bin/bash
# ======================================================================
# گارد شبح‌ها — دستور صریح کاربر (۲۰۲۶-۱۱-۲۱): پروژه‌های ممنوعه هرگز نباید برگردند.
# در سرِ scripts/i136-deploy.sh و scripts/regen-dl.sh صدا زده می‌شود؛ اگر هر مسیر
# ممنوعه‌ای روی دیسک باشد، دیپلوی/رندر مرکز دانلود با پیام بلند متوقف می‌شود تا
# هیچ خروجی (زیپ سورس/بستهٔ مشتری/مرکز /dlc) هرگز آلوده نشود.
# مصرف مستقیم: bash scripts/ghost-guard.sh [ROOT]
# ======================================================================
set -uo pipefail
ROOT=${1:-/home/z/my-project}
GHOSTS=(
  "aspnet/src/HrAttendance.Web"
  "aspnet/src/HrPlatform.Contracts"
  "aspnet/src/HrPlatform.Core"
  "aspnet/src/modules/HrModules.Parking"
  "aspnet/src/modules/HrModules.Visitors"
  "aspnet/HrAttendance.sln",
  "scripts/finalverify"
)
BAD=0
for g in "${GHOSTS[@]}"; do
  if [ -e "$ROOT/$g" ]; then
    echo "!! GHOST برگشته: $g — با «git rm -r + rm -rf» حذفش کن؛ ساخت خروجی متوقف شد"
    BAD=1
  fi
done
for d in "HrAttendance.Web" "HrModules.Parking" "HrModules.Visitors"; do
  if [ -d "$ROOT/aspnet/sc-run/wwwroot/dlc/source/aspnet/src/$d" ]; then
    echo "!! GHOST در snapshot مرکز دانلود: dlc/source/aspnet/src/$d — bash scripts/regen-dl.sh --fast را دوباره اجرا کن"
    BAD=1
  fi
done
if [ "$BAD" = "1" ]; then
  echo ">> ghost-guard: ناموفق — ادامه (دیپلوی/regen) مجاز نیست"
  exit 1
fi
echo ">> ghost-guard: تمیز — هیچ پروژهٔ ممنوعه‌ای نیست (فهرست مجاز: Platform.Contracts/Core/Web + ۸ ماژول)"
