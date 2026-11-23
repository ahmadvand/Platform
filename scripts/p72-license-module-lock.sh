#!/usr/bin/env bash
# ======================================================================
# فاز ۷۲ — E2E قفل ماژول‌ها با «مجوز نصب» (درخواست کاربر)
# سناریو: مشتری DLL همهٔ ماژول‌ها را دارد، اما مجوز فقط بخشی از ماژول‌ها را
# شامل می‌شود → ماژول‌های خارج از مجوز باید:
#   ۱) از منو حذف شوند   ۲) مسیرهایشان به صفحهٔ «در مجوز نصب فعال نشده» برود
#   ۳) AJAX/API شان بسته شود   ۴) فعال‌سازی دستی در تنظیمات رد شود
# سپس با مجوز کامل، همه‌چیز بدون تغییر داده‌ها برگردد.
# پاکسازی کامل در پایان (بازگشت به حالت آزمایشی).
# ======================================================================
set -u
BASE="${BASE:-http://127.0.0.1:3000}"
J=/tmp/p72; mkdir -p "$J"; rm -f "$J"/* 2>/dev/null
APPD=/home/z/my-project/aspnet/src/Platform.Web/App_Data
BIN=/home/z/my-project/aspnet/src/Platform.Web/bin/Debug/net8.0

say() { printf '%s\n' "$*"; }
fail() { say "✗ $1"; exit 1; }
poll_page() { # poll_page <url> <pattern> <tries>
  local url="$1" pat="$2" n="${3:-30}" i=0 body=""
  while [ $i -lt $n ]; do
    body=$(curl -s -b "$J/c.txt" "$BASE$url")
    echo "$body" | grep -q "$pat" && { echo "$body"; return 0; }
    i=$((i+1)); sleep 1
  done
  echo "$body"; return 1
}

# ---------- پاکسازی اولیه: از اجرای قبلی کلید/مجوز نماند ----------
rm -rf "$APPD/license-keys"; rm -f "$APPD/license.dat"

# ---------- پیش‌شرط: DLL ماژول‌ها واقعاً در بسته حاضرند (سناریوی «DLL هست، مجوز نیست») ----------
ls "$BIN"/HrModules.Parking*.dll >/dev/null 2>&1 || fail "DLL پارکینگ در بسته نیست — سناریوی تست معنا ندارد"
ls "$BIN"/HrModules.Visitors*.dll >/dev/null 2>&1 || fail "DLL بازدیدکنندگان در بسته نیست — سناریوی تست معنا ندارد"
say "✓ DLLS-PRESENT (پارکینگ + بازدیدکنندگان در bin حاضرند)"

# ---------- ورود ----------
AF=$(curl -s -c "$J/c.txt" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF" ] || fail "توکن آنتی‌فورجری پیدا نشد"
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$CODE" = "302" ] || fail "ورود ناموفق (کد $CODE)"
say "✓ LOGIN 302"

# ---------- مبنا (قبل از مجوز): منو شامل پارکینگ/بازدیدکنندگان است (آزمایشی = همه مجاز) ----------
HOME0=$(curl -s -b "$J/c.txt" "$BASE/")
echo "$HOME0" | grep -q 'href="/Parking"'  || fail "مبنای تست: لینک /Parking در منو نیست"
echo "$HOME0" | grep -q 'href="/Visitors"' || fail "مبنای تست: لینک /Visitors در منو نیست"
say "✓ BASELINE (منو شامل پارکینگ و بازدیدکنندگان)"

# ---------- ساخت جفت‌کلید نصب + دانلود private.pem ----------
PAGE=$(curl -s -b "$J/c.txt" "$BASE/License")
AF2=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF2" ] || fail "توکن صفحهٔ مجوز پیدا نشد"
G=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/GenerateKey" --data-urlencode "__RequestVerificationToken=$AF2")
[ "$G" = "302" ] || fail "GenerateKey ناموفق (کد $G)"
HTTP=$(curl -s -o "$J/private.pem" -w "%{http_code}" -b "$J/c.txt" "$BASE/License/PrivateKey")
[ "$HTTP" = "200" ] || fail "دانلود private.pem ناموفق (کد $HTTP)"
say "✓ KEYPAIR + private.pem"

# ---------- صدور مجوز ناقص: بدون parking و visitors ----------
curl -s -o "$J/req.hrreq" "$BASE/License/RequestFile"
cd /home/z/my-project && export PATH="/home/z/.dotnet:$PATH"
dotnet run --project tools/LicenseGen -- issue --key "$J/private.pem" --req "$J/req.hrreq" \
  --expires 2027-10-20 --notes "تست فاز ۷۲ — بدون پارکینگ/مهمان" \
  --modules org,attendance,access,devices,payroll,gateway \
  --out "$J/lic-partial.dat" >/dev/null 2>&1 || fail "صدور مجوز ناقص ناموفق"
[ -s "$J/lic-partial.dat" ] || fail "فایل مجوز ناقص ساخته نشد"
I=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/Import" \
  --data-urlencode "__RequestVerificationToken=$AF2" --data-urlencode "text=$(cat "$J/lic-partial.dat")")
[ "$I" = "302" ] || fail "ورود مجوز ناقص ناموفق (کد $I)"
LICPAGE=$(poll_page "/License" "فعال و معتبر" 15) || fail "مجوز ناقص فعال نشد"
echo "$LICPAGE" | grep -q "ماژول‌های مجاز" || fail "سطر «ماژول‌های مجاز» روی صفحه نیست"
echo "$LICPAGE" | grep -q "۶ ماژول" || fail "شمارش ۶ ماژول روی صفحه نیست"
say "✓ PARTIAL-LICENSE (فعال — ۶ ماژول، بدون parking/visitors)"

# ---------- ۱) منو: ماژول‌های خارج از مجوز حذف، بقیه برجا ----------
sleep 1
HOME1=$(curl -s -b "$J/c.txt" "$BASE/")
echo "$HOME1" | grep -q 'href="/Parking"'  && fail "لینک /Parking با وجود غیبت در مجوز هنوز در منوست!"
echo "$HOME1" | grep -q 'href="/Visitors"' && fail "لینک /Visitors با وجود غیبت در مجوز هنوز در منوست!"
echo "$HOME1" | grep -q 'href="/Access"'     || fail "لینک /Access نباید حذف می‌شد"
echo "$HOME1" | grep -q 'href="/Attendance"' || fail "لینک /Attendance نباید حذف می‌شد"
say "✓ MENU (پارکینگ/مهمان حذف — بقیه برجا) — با DLL حاضر"

# ---------- ۲) مسیرها: ریدایرکت به صفحهٔ دوستانهٔ «در مجوز فعال نشده» ----------
R1=$(curl -s -o /dev/null -w "%{http_code} %{redirect_url}" -b "$J/c.txt" "$BASE/Parking")
echo "$R1" | grep -q "302" || fail "مسیر /Parking بسته نشد (کد $R1)"
D1=$(curl -s -b "$J/c.txt" "$BASE/Home/ModuleDisabled?code=parking")
echo "$D1" | grep -q "در مجوز نصب فعال نشده است" || fail "صفحهٔ دوستانهٔ پارکینگ پیام مجوز ندارد"
R2=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Visitors")
echo "$R2" | grep -q "302" || fail "مسیر /Visitors بسته نشد (کد $R2)"
D2=$(curl -s -b "$J/c.txt" "$BASE/Home/ModuleDisabled?code=visitors")
echo "$D2" | grep -q "در مجوز نصب فعال نشده است" || fail "صفحهٔ دوستانهٔ مهمان پیام مجوز ندارد"
say "✓ ROUTES (/Parking و /Visitors → صفحهٔ «در مجوز نصب فعال نشده») — با DLL حاضر"

# ---------- ۳) AJAX ماژول قفل‌شده → ۴۰۳ با پیام مجوز ----------
AJ=$(curl -s -w "|%{http_code}" -H "X-Requested-With: XMLHttpRequest" -b "$J/c.txt" "$BASE/Parking")
echo "$AJ" | grep -q "|403" || fail "AJAX پارکینگ ۴۰۳ نشد ($AJ)"
echo "$AJ" | grep -q "در مجوز نصب فعال نشده است" || fail "پیام ۴۰۳ AJAX به مجوز اشاره ندارد"
say "✓ AJAX 403 (پیام مجوز)"

# ---------- ۴) فعال‌سازی دستی در تنظیمات رد شود ----------
SETAF=$(curl -s -b "$J/c.txt" "$BASE/Settings" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$SETAF" ] || fail "توکن صفحهٔ تنظیمات پیدا نشد"
TG=$(curl -s -b "$J/c.txt" -X POST "$BASE/Settings/ToggleModule" \
  --data-urlencode "__RequestVerificationToken=$SETAF" \
  --data-urlencode "code=parking" --data-urlencode "enabled=true")
# نکته: AjaxResult متن فارسی را \uXXXX می‌کند — با پارس JSON قضاوت می‌کنیم، نه grep خام
TOG=$(echo "$TG" | python3 -c 'import sys,json
d=json.load(sys.stdin)
print("OK" if d.get("ok") is False and "\u0645\u062c\u0648\u0632" in d.get("message","") else "BAD")' 2>/dev/null)
[ "$TOG" = "OK" ] || fail "تاگل پارکینگ باید با پیام مجوز رد می‌شد: $TG"
say "✓ TOGGLE-REJECTED (فعال‌سازی دستی ماژولِ خارج از مجوز رد شد)"

# ---------- ۵) داشبورد: هیچ ویجت پارکینگ/مهمان ----------
DASH=$(curl -s -b "$J/c.txt" "$BASE/")
echo "$DASH" | grep -q 'href="/Parking"'  && fail "داشبورد لینک پارکینگ دارد"
echo "$DASH" | grep -q 'href="/Visitors"' && fail "داشبورد لینک مهمان دارد"
say "✓ DASHBOARD (بدون ردپای ماژول‌های قفل‌شده)"

# ---------- ۶) مجوز کامل → همه‌چیز بدون تغییر داده‌ها برمی‌گردد ----------
dotnet run --project tools/LicenseGen -- issue --key "$J/private.pem" --req "$J/req.hrreq" \
  --expires 2027-10-20 --notes "تست فاز ۷۲ — مجوز کامل" \
  --out "$J/lic-full.dat" >/dev/null 2>&1 || fail "صدور مجوز کامل ناموفق"
I2=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/Import" \
  --data-urlencode "__RequestVerificationToken=$AF2" --data-urlencode "text=$(cat "$J/lic-full.dat")")
[ "$I2" = "302" ] || fail "ورود مجوز کامل ناموفق (کد $I2)"
HOME2=$(curl -s -b "$J/c.txt" "$BASE/")
echo "$HOME2" | grep -q 'href="/Parking"'  || fail "پس از مجوز کامل، لینک /Parking برنگشت"
echo "$HOME2" | grep -q 'href="/Visitors"' || fail "پس از مجوز کامل، لینک /Visitors برنگشت"
P200=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Parking")
[ "$P200" = "200" ] || fail "صفحهٔ /Parking پس از مجوز کامل ۲۰۰ نشد (کد $P200)"
V200=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Visitors")
[ "$V200" = "200" ] || fail "صفحهٔ /Visitors پس از مجوز کامل ۲۰۰ نشد (کد $V200)"
say "✓ RESTORE (مجوز کامل → منو و مسیرها برگشتند؛ /Parking و /Visitors = 200)"

# ---------- پاکسازی: حذف کلید و مجوز → بازگشت به آزمایشی ----------
curl -s -o /dev/null -b "$J/c.txt" -X POST "$BASE/License/RemoveKey" --data-urlencode "__RequestVerificationToken=$AF2"
rm -f "$APPD/license.dat"; rm -rf "$APPD/license-keys"
poll_page "/License" "دورهٔ آزمایشی" 35 >/dev/null || fail "بازگشت به حالت آزمایشی ناموفق"
say "✓ CLEANUP (کلید و مجوز حذف — دورهٔ آزمایشی)"

say "===== P72 LICENSE-MODULE-LOCK E2E: ALL OK ====="
