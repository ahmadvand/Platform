#!/usr/bin/env bash
# ======================================================================
# فاز ۵۷ — ساخت بستهٔ مشتری با انتخاب ماژول‌ها
# ----------------------------------------------------------------------
# از فاز ۵۷ هر ماژول سیستمی یک پروژهٔ جداست (aspnet/src/modules/HrModules.*)
# و می‌تواند کلاً از بستهٔ مشتری حذف شود — یعنی DLL ماژول در خروجی publish
# وجود ندارد (نه فقط خاموش). ماژول‌های پایه (core/org/devices) همیشه همراه
# سامانه‌اند و حذف‌شدنی نیستند.
#
# مصرف:
#   ./build-customer.sh                          ← همهٔ ماژول‌ها (بستهٔ کامل)
#   ./build-customer.sh parking                  ← بدون پارکینگ
#   ./build-customer.sh parking,payroll          ← بدون پارکینگ و حقوق (آینده)
#
# خروجی:  aspnet/publish/customer/  (dotnet publish -c Release)
# نسخه/برند:
#   ./build-customer.sh parking 2.4.0 "نام تولیدکننده"
# ======================================================================
set -euo pipefail
ROOT=/home/z/my-project/aspnet
EXCLUDE="${1:-}"
APPVER="${2:-}"
VENDOR="${3:-}"

# ---- فهرست ماژول‌های پروژهٔ جدا + پرچم MSBuild متناظر (فاز ۵۸: هر ۵ ماژول سیستم، فاز ۶۳: + دروازه)
declare -A FLAG=(
  [parking]=HrModuleParking
  [attendance]=HrModuleAttendance
  [access]=HrModuleAccess
  [visitors]=HrModuleVisitors
  [payroll]=HrModulePayroll
  [gateway]=HrModuleGateway
)

IFS=',' read -ra MODS <<< "$EXCLUDE"
for m in "${MODS[@]:-}"; do
  [ -z "$m" ] && continue
  f="${FLAG[$m]:-}"
  [ -z "$f" ] && { echo "ماژول ناشناخته یا غیرقابل‌حذف: $m (قابل‌حذف: ${!FLAG[*]})" >&2; exit 2; }
  say="1"
done

# فاز ۶۹ — همهٔ پروژه‌ها (شامل پلاگین‌های HrModules.*) در Release بیلد شوند؛
# تارگت کپی هاست خروجی Release ماژول‌ها را کنار اپ می‌گذارد (قرارداد کشف رانتایم).
echo ">> dotnet build sln (Release — همهٔ پلاگین‌ها)"
dotnet build "$ROOT/Platform.sln" -c Release --nologo -v q -p:WarningLevel=0

ARGS=(publish "$ROOT/src/Platform.Web/Platform.Web.csproj" -c Release -o "$ROOT/publish/customer" -p:WarningLevel=0)
[ -n "$APPVER" ] && ARGS+=(-p:AppVersion="$APPVER")
[ -n "$VENDOR" ] && ARGS+=(-p:VendorName="$VENDOR")

echo ">> dotnet ${ARGS[*]}"
dotnet "${ARGS[@]}"

# فاز ۶۹ — حذف فیزیکی DLL ماژول‌های حذف‌شده (UI + لایهٔ دیتا؛ چیدمان تخت و modules/) —
# ملاک «نصب» در رانتایم = وجود فایل HrModules.*.dll کنار اپ.
if [ -n "${say:-}" ]; then
  for m in "${MODS[@]}"; do
    [ -z "$m" ] && continue
    # فاز ۶۹ — نگاشت کد ماژول به الگوی DLL (gateway → DeviceGateway)
    case "$m" in
      gateway) Pat="HrModules.DeviceGateway*.dll" ;;
      parking) Pat="Platform.Modules.Parking*.dll" ;;
      visitors) Pat="Platform.Modules.Visitors*.dll" ;;
      *)       Cap="$(tr '[:lower:]' '[:upper:]' <<< "${m:0:1}")${m:1}"; Pat="HrModules.$Cap*.dll" ;;
    esac
    rm -f "$ROOT/publish/customer"/$Pat "$ROOT/publish/customer"/modules/$Pat 2>/dev/null || true
    echo "  ✓ بدون ماژول $m (DLL حذف شد)"
  done
fi

echo
echo "== بسته آماده: $ROOT/publish/customer ="
if [ -n "${say:-}" ]; then
  echo "ماژول‌های حذف‌شده: $EXCLUDE"
  for m in "${MODS[@]}"; do
    [ -z "$m" ] && continue
    case "$m" in
      gateway) Pat="HrModules.DeviceGateway*.dll" ;;
      parking) Pat="Platform.Modules.Parking*.dll" ;;
      visitors) Pat="Platform.Modules.Visitors*.dll" ;;
      *)       Cap="$(tr '[:lower:]' '[:upper:]' <<< "${m:0:1}")${m:1}"; Pat="HrModules.$Cap*.dll" ;;
    esac
    if ls "$ROOT/publish/customer"/$Pat 2>/dev/null | grep -q .; then
      echo "  ✗ خطا: DLL ماژول $m هنوز در خروجی است!"; exit 3
    else
      echo "  ✓ بدون ماژول $m (DLL حذف شد)"
    fi
  done
else
  echo "بستهٔ کامل — همهٔ ماژول‌ها:"
  # فاز ۸۷ — پلاگین‌ها با هر دو پیشوند (HrModules.* و Platform.Modules.*)
  ls "$ROOT/publish/customer"/HrModules.*.dll "$ROOT/publish/customer"/Platform.Modules.*.dll 2>/dev/null | xargs -n1 basename || true
  N=$(ls "$ROOT/publish/customer"/HrModules.*.dll "$ROOT/publish/customer"/Platform.Modules.*.dll 2>/dev/null | wc -l)
  [ "$N" -ge 13 ] || { echo "  ✗ خطا: پلاگین‌های ماژول ناقص ($N از 13) — بیلد Release ماژول‌ها را چک کنید"; exit 4; }
fi
