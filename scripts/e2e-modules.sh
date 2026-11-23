#!/usr/bin/env bash
# ======================================================================
# فاز ۵۴ — E2E رجیستری ماژول‌ها
# بخش A) toggle ماژول: خاموش/روشن payroll → منو/مسیر/پرتال من/حسابرسی
# بخش B) سقف مجوز: مجوز با فهرست ماژول (بدون parking) → قفل + رفع قفل
# پاکسازی کامل در پایان (بازگشت به حالت آزمایشی)
# ======================================================================
set -u
BASE="${BASE:-http://127.0.0.1:3000}"
J=/tmp/p54
mkdir -p "$J"; rm -f "$J"/*.txt "$J"/*.pem "$J"/*.dat "$J"/*.hrreq 2>/dev/null
APPD=/home/z/my-project/aspnet/src/HrAttendance.Web/App_Data

say() { printf '%s\n' "$*"; }
fail() { say "✗ $1"; exit 1; }

# ---------- پاکسازی اولیه: از اجرای قبلی مجوز/کلید/وضعیت ماژولی نماند ----------
rm -f "$APPD/modules.json"
rm -rf "$APPD/license-keys"
rm -f "$APPD/license.dat"

# ---------- ورود ----------
AF=$(curl -s -c "$J/c.txt" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF" ] || fail "توکن ورود پیدا نشد"
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$CODE" = "302" ] || fail "ورود ناموفق ($CODE)"
say "✓ LOGIN 302"

# توکن آنتی‌فورجری پس از ورود — از خود صفحهٔ /Settings (توکن صفحهٔ ورود با تغییر هویت می‌میرد)
AF=$(curl -s -b "$J/c.txt" "$BASE/Settings" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF" ] || fail "توکن آنتی‌فورجری /Settings پیدا نشد"

# ---------- بخش A: کارت ماژول‌ها ----------
PAGE=$(curl -s -b "$J/c.txt" "$BASE/Settings")
echo "$PAGE" | grep -q "ماژول‌های سامانه" || fail "کارت ماژول‌ها روی /Settings نیست"
echo "$PAGE" | grep -q "همیشه فعال" || fail "نشان «همیشه فعال» پایه‌ها نیست"
echo "$PAGE" | grep -q "modTable" || fail "جدول modTable نیست"
say "✓ MODULES-CARD (۸ ماژول — پایه‌ها «همیشه فعال»)"

TOGGLE() { # TOGGLE <code> <enabled> → خروجی JSON
  curl -s -b "$J/c.txt" -X POST "$BASE/Settings/ToggleModule" \
    --data-urlencode "code=$1" --data-urlencode "enabled=$2" \
    --data-urlencode "__RequestVerificationToken=$AF"
}

# ---------- گارد: پایه خاموش نمی‌شود ----------
R=$(TOGGLE core false)
echo "$R" | grep -q '"ok":false' || fail "گارد پایه کار نکرد: $R"
echo "$R" | grep -q "خاموش‌کردن نیستند\|قابل خاموش" || say "  (پیام گارد: $R)"
say "✓ GUARD-CORE (پایه خاموش‌نشدنی)"

# ---------- گارد: کد نامعلوم ----------
R=$(TOGGLE bogus false)
echo "$R" | grep -q '"ok":false' || fail "گارد کد نامعلوم کار نکرد: $R"
say "✓ GUARD-UNKNOWN-CODE"

# ---------- خاموش‌کردن payroll ----------
R=$(TOGGLE payroll false)
echo "$R" | grep -q '"ok":true' || fail "خاموش‌کردن payroll ناموفق: $R"
say "✓ TOGGLE-OFF payroll"

C=$(curl -s -o /dev/null -w "%{http_code}|%{redirect_url}" -b "$J/c.txt" "$BASE/Payroll")
echo "$C" | grep -q "302" || fail "/Payroll بعد از خاموشی 302 نشد ($C)"
echo "$C" | grep -q "ModuleDisabled?code=payroll" || fail "مقصد ریدایرکت درست نیست ($C)"
say "✓ GATE-PAGE /Payroll → ModuleDisabled"

H=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Home/ModuleDisabled?code=payroll")
[ "$H" = "200" ] || fail "صفحهٔ دوستانه ماژول 200 نشد ($H)"
PAGE=$(curl -s -b "$J/c.txt" "$BASE/Home/ModuleDisabled?code=payroll")
echo "$PAGE" | grep -q "غیرفعال است" || fail "متن صفحهٔ «غیرفعال است» نیست"
say "✓ FRIENDLY-PAGE 200"

HOME=$(curl -s -b "$J/c.txt" "$BASE/")
echo "$HOME" | grep -q 'href="/Payroll' && fail "لینک منوی payroll هنوز در سایدبار است"
echo "$HOME" | grep -q "فیش حقوقی من" && fail "«فیش حقوقی من» هنوز در پرتال من است"
echo "$HOME" | grep -q 'href="/Attendance"' || fail "آیتم حضور و غیاب نباید حذف می‌شد"
say "✓ SIDEBAR (payroll حذف شد — بقیه سالم)"

S=$(curl -s -o /dev/null -w "%{http_code}|%{redirect_url}" -b "$J/c.txt" "$BASE/Self/Payslips")
echo "$S" | grep -q "ModuleDisabled?code=payroll" || fail "/Self/Payslips مسدود نشد ($S)"
say "✓ GATE-SELF /Self/Payslips → غیرفعال"

P=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Reports/Parking")
[ "$P" = "200" ] || fail "/Reports/Parking نباید مسدود می‌شد ($P)"
say "✓ UNAFFECTED /Reports/Parking 200"

# ---------- حسابرسی ----------
AUD=$(curl -s -b "$J/c.txt" "$BASE/Audit")
echo "$AUD" | grep -q "module_disable" || fail "رکورد حسابرسی module_disable نیست"
say "✓ AUDIT module_disable"

# ---------- روشن‌کردن دوباره ----------
R=$(TOGGLE payroll true)
echo "$R" | grep -q '"ok":true' || fail "روشن‌کردن payroll ناموفق: $R"
C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Payroll")
[ "$C" = "200" ] || fail "/Payroll بعد از روشن‌شدن 200 نشد ($C)"
HOME=$(curl -s -b "$J/c.txt" "$BASE/")
echo "$HOME" | grep -q "فیش حقوقی من" || fail "«فیش حقوقی من» برنگشت"
say "✓ TOGGLE-ON payroll → همه‌چیز برگشت"

# ======================================================================
# بخش B — سقف مجوز نصب (مجوز بدون parking)
# ======================================================================
rm -rf "$APPD/license-keys"; rm -f "$APPD/license.dat"
PAGE=$(curl -s -b "$J/c.txt" "$BASE/License")
AFL=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AFL" ] || fail "توکن /License پیدا نشد"
G=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/GenerateKey" --data-urlencode "__RequestVerificationToken=$AFL")
[ "$G" = "302" ] || fail "GenerateKey ناموفق ($G)"
H=$(curl -s -o "$J/private.pem" -w "%{http_code}" -b "$J/c.txt" "$BASE/License/PrivateKey")
[ "$H" = "200" ] || fail "دانلود private.pem ناموفق ($H)"
curl -s -b "$J/c.txt" -o "$J/req.hrreq" "$BASE/License/RequestFile"
head -c 40 "$J/req.hrreq" | grep -q "{" || fail "فایل درخواست (.hrreq) سالم نیست — احتمالاً بدون نشست دانلود شده"
say "✓ LICENSE-KEY آماده"

export PATH="/home/z/.dotnet:$PATH" DOTNET_ROOT=/home/z/.dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1
cd /home/z/my-project
# نکته: dotnet run در سندباکس گاهی روی پیکربندی first-run گیر می‌کند — DLL کامپیل‌شده مستقیم اجرا می‌شود
if [ ! -f tools/LicenseGen/bin/Debug/net8.0/LicenseGen.dll ]; then
  dotnet build tools/LicenseGen/LicenseGen.csproj -v q --nologo > "$J/build.log" 2>&1 || { tail -5 "$J/build.log"; fail "بیلد LicenseGen ناموفق"; }
fi
LOG="$J/gen.log"
dotnet tools/LicenseGen/bin/Debug/net8.0/LicenseGen.dll issue --key "$J/private.pem" --req "$J/req.hrreq" \
  --expires 2027-10-20 --notes "تست فاز ۵۴ — بدون پارکینگ" \
  --modules core,org,devices,attendance,access,visitors,payroll --out "$J/lic.dat" > "$LOG" 2>&1 \
  || { tail -5 "$LOG"; fail "LicenseGen issue (--modules) ناموفق"; }
[ -s "$J/lic.dat" ] || fail "فایل مجوز ساخته نشد"
say "✓ ISSUE --modules (بدون parking)"

I=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/Import" \
  --data-urlencode "__RequestVerificationToken=$AFL" --data-urlencode "text=$(cat "$J/lic.dat")")
[ "$I" = "302" ] || fail "Import ناموفق ($I)"
PAGE=$(curl -s -b "$J/c.txt" "$BASE/License")
echo "$PAGE" | grep -q "فعال و معتبر" || fail "مجوز فعال نشد"
echo "$PAGE" | grep -q "ماژول‌های مجاز" || fail "سطر «ماژول‌های مجاز» روی صفحهٔ مجوز نیست"
echo "$PAGE" | grep -q "۷ ماژول" || fail "شمارش «۷ ماژول» در سطر مجاز نیست (باید parking غایب باشد)"
say "✓ IMPORT → فعال + سطر «ماژول‌های مجاز»"

# parking حالا قفل مجوز است → مسیر بسته
C=$(curl -s -o /dev/null -w "%{http_code}|%{redirect_url}" -b "$J/c.txt" "$BASE/Parking")
echo "$C" | grep -q "ModuleDisabled?code=parking" || fail "/Parking بعد از مجوز بدون parking باید بسته می‌شد ($C)"
say "✓ LICENSE-CEILING /Parking → غیرفعال"

SETTINGS=$(curl -s -b "$J/c.txt" "$BASE/Settings")
echo "$SETTINGS" | grep -q "قفل با مجوز" || fail "نشان «قفل با مجوز» در تنظیمات نیست"
R=$(TOGGLE parking true)
echo "$R" | grep -q '"ok":false' || fail "روشن‌کردن ماژول قفل‌شده باید رد می‌شد: $R"
echo "$R" | python3 -c "import sys,json; d=json.load(sys.stdin); sys.exit(0 if 'مجوز' in (d.get('message') or '') else 1)" \
  || fail "پیام رد باید به مجوز اشاره کند: $R"
say "✓ TOGGLE-LOCKED رد شد + پیام مجوز"

# ماژول‌های داخل فهرست سالم‌اند
C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Payroll")
[ "$C" = "200" ] || fail "/Payroll با مجوز فهرست‌دار باید باز باشد ($C)"
HOME=$(curl -s -b "$J/c.txt" "$BASE/")
echo "$HOME" | grep -q 'href="/Payroll' || fail "منوی payroll برنگشت"
say "✓ IN-LIST /Payroll 200"

# ---------- مجوز کامل (با parking) → رفع قفل ----------
dotnet tools/LicenseGen/bin/Debug/net8.0/LicenseGen.dll issue --key "$J/private.pem" --req "$J/req.hrreq" \
  --expires 2027-10-20 --notes "تست فاز ۵۴ — کامل" \
  --modules core,org,devices,attendance,access,visitors,payroll,parking --out "$J/lic2.dat" > "$LOG" 2>&1 \
  || { tail -5 "$LOG"; fail "LicenseGen issue (کامل) ناموفق"; }
curl -s -o /dev/null -b "$J/c.txt" -X POST "$BASE/License/Import" \
  --data-urlencode "__RequestVerificationToken=$AFL" --data-urlencode "text=$(cat "$J/lic2.dat")"
C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Parking")
[ "$C" = "200" ] || fail "/Parking بعد از مجوز کامل باز نشد ($C)"
say "✓ UNLOCK /Parking 200"

# ======================================================================
# بخش C (فاز ۵۵) — راه‌اندازی فقط با ماژول‌های پایه:
# خاموش‌کردن هر ۵ ماژول سیستم → تمام مسیرهای پایه باید باز بمانند و
# تمام مسیرهای سیستمی بسته؛ سپس روشن‌کردن مجدد و بازگشت کامل.
# ======================================================================
for m in attendance access visitors payroll parking; do
  R=$(TOGGLE "$m" false)
  echo "$R" | grep -q '"ok":true' || fail "خاموش‌کردن $m ناموفق: $R"
done
say "✓ ALL-SYSTEM-OFF (۵ ماژول سیستم خاموش شد)"

SETTINGS=$(curl -s -b "$J/c.txt" "$BASE/Settings")
for p in / /Employees /Org /Holidays /Devices /Biometrics /Gateway /Companies \
         /Users /Notifications /Audit /License /Settings /Reports /Self; do
  C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE$p")
  [ "$C" = "200" ] || fail "مسیر پایهٔ $p با ماژول‌های خاموش باید 200 باشد ($C)"
done
say "✓ BASE-ONLY (۱۵ مسیر پایه 200 — core/org/devices سالم)"

for p in /Attendance /Shifts /Requests /Access /Cards /Visitors /Payroll /Parking \
         /Self/Attendance /Self/Requests /Self/Payslips /Reports/Parking; do
  C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE$p")
  echo "$C" | grep -q "302" || fail "مسیر سیستمی $p باید 302 به ModuleDisabled باشد ($C)"
done
say "✓ SYSTEM-BLOCKED (۱۲ مسیر سیستمی 302)"

echo "$SETTINGS" | grep -q 'href="/Employees"' || fail "منوی پرسنل (پایه) باید باقی می‌ماند"
echo "$SETTINGS" | grep -q 'href="/Devices"' || fail "منوی دستگاه‌ها (پایه) باید باقی می‌ماند"
echo "$SETTINGS" | grep -q 'href="/Attendance"' && fail "منوی حضور (سیستمی) باید حذف شده باشد"
echo "$SETTINGS" | grep -q 'href="/Parking"' && fail "منوی پارکینگ (سیستمی) باید حذف شده باشد"
echo "$SETTINGS" | grep -q 'href="/Payroll"' && fail "منوی حقوق (سیستمی) باید حذف شده باشد"
say "✓ BASE-MENU (منو = فقط پایه‌ها + گروه‌های خالی حذف)"

for m in attendance access visitors payroll parking; do
  R=$(TOGGLE "$m" true)
  echo "$R" | grep -q '"ok":true' || fail "روشن‌کردن $m ناموفق: $R"
done
for p in /Attendance /Payroll /Parking /Access /Visitors; do
  C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE$p")
  [ "$C" = "200" ] || fail "$p بعد از روشن‌سازی 200 نشد ($C)"
done
rm -f "$APPD/modules.json"
say "✓ RE-ENABLE (هر ۵ ماژول برگشتند — داده‌ها دست‌نخورده)"

# ---------- پاکسازی کامل ----------
curl -s -o /dev/null -b "$J/c.txt" -X POST "$BASE/License/RemoveKey" --data-urlencode "__RequestVerificationToken=$AFL"
rm -rf "$APPD/license-keys"; rm -f "$APPD/license.dat"; rm -f "$APPD/modules.json"
PAGE=$(curl -s -b "$J/c.txt" "$BASE/License")
echo "$PAGE" | grep -q "ساخته نشده" || fail "پاکسازی کلید ناقص است"
PAGE=$(curl -s -b "$J/c.txt" "$BASE/Settings")
echo "$PAGE" | grep -q "قفل با مجوز" && fail "پس از پاکسازی هنوز قفل مجوز هست"
say "✓ CLEANUP (بازگشت به حالت آزمایشی — همهٔ ماژول‌ها آزاد)"

AUD=$(curl -s -b "$J/c.txt" "$BASE/Audit")
for act in module_disable module_enable license-activated; do
  echo "$AUD" | grep -q "$act" || fail "رکورد حسابرسی $act نیست"
done
say "✓ AUDIT (module_* + license-activated)"

say "===== P54 MODULES E2E: ALL OK ====="
