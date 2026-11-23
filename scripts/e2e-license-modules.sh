#!/usr/bin/env bash
# ======================================================================
# فاز ۷۲ — E2E «سقف ماژول‌ها در مجوز نصب» (درخواست کاربر)
# «هنگام تولید لایسنس، ماژول‌های خریداری‌شده انتخاب شود؛ حتی اگر مشتری DLL
#  یک ماژول را داشته باشد، اگر در لایسنس نباشد نه در منو بیاید و نه قابل استفاده باشد»
# ----------------------------------------------------------------------
# ۱) ورود ادمین  ۲) ساخت جفت‌کلید روی صفحهٔ /License + دانلود private.pem
# ۳) صدور مجوز «محدود» با موتور خود GUI (هارنس LicenseCoreTest --issue --modules)
# ۴) ورود مجوز → اثر فوری: منو/داشبورد/صفحه‌ها/API/تنظیمات
# ۵) صدور مجوز «کامل» (بدون فهرست) → همه‌چیز برگردد
# ۶) پاکسازی کامل (بازگشت به دورهٔ آزمایشی)
# ======================================================================
set -u
BASE="${BASE:-http://127.0.0.1:3000}"
ROOT=/home/z/my-project
J=/tmp/p72
APPD="$ROOT/aspnet/src/Platform.Web/App_Data"
mkdir -p "$J"; rm -f "$J"/*.txt "$J"/*.pem "$J"/*.dat "$J"/*.hrreq "$J"/*.json 2>/dev/null

say() { printf '%s\n' "$*"; }
fail() { say "✗ $1"; exit 1; }
# هارنس = همان موتور GUI استودیو (بیلد یک‌بار، اجرای مستقیم DLL — بدون ابهام ارسال آرگومان dotnet run)
export PATH="/home/z/.dotnet:$PATH"
dotnet build "$ROOT/scripts/LicenseCoreTest" -v q --nologo > /dev/null 2>&1 || fail "بیلد هارنس ناموفق"
HARNESS="dotnet $ROOT/scripts/LicenseCoreTest/bin/Debug/net8.0/LicenseCoreTest.dll"

# ---------- پاکسازی اولیه: از اجرای قبلی کلید/مجوزی نماند ----------
rm -rf "$APPD/license-keys"
rm -f "$APPD/license.dat"

# ---------- ۱) ورود ----------
AF=$(curl -s -c "$J/c.txt" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF" ] || fail "توکن آنتی‌فورجری صفحهٔ ورود پیدا نشد"
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$CODE" = "302" ] || fail "ورود ناموفق (کد $CODE)"
say "✓ LOGIN 302"

# ---------- ۲) ساخت جفت‌کلید در همین نصب + دانلود کلید خصوصی ----------
PAGE=$(curl -s -b "$J/c.txt" "$BASE/License")
AF2=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF2" ] || fail "توکن /License پیدا نشد"
G=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/GenerateKey" --data-urlencode "__RequestVerificationToken=$AF2")
[ "$G" = "302" ] || fail "GenerateKey ناموفق (کد $G)"
HTTP=$(curl -s -o "$J/private.pem" -w "%{http_code}" -b "$J/c.txt" "$BASE/License/PrivateKey")
[ "$HTTP" = "200" ] && head -1 "$J/private.pem" | grep -q "BEGIN PRIVATE KEY" || fail "دانلود private.pem ناموفق"
say "✓ KEY-PAIR ساخته و دانلود شد"

# امضای سرور — از فایل درخواست (بدون نشست؛ همان مسیر مشتری واقعی)
curl -s -o "$J/req.hrreq" "$BASE/License/RequestFile"
MACHINE=$(python3 -c "import json;print(json.load(open('$J/req.hrreq'))['machine'])")
[ "${#MACHINE}" = "29" ] || fail "امضای سرور از .hrreq خوانده نشد ($MACHINE)"
say "✓ MACHINE $MACHINE"

# ---------- ۳) صدور مجوز «محدود» با موتور GUI — فقط org + attendance + access ----------
$HARNESS --issue --keys "$J" --machine "$MACHINE" \
  --licensee "شرکت تست فاز۷۲" --expires-jalali 1410/01/01 \
  --max-employees 500 --notes "تست سقف ماژول" --modules "org,attendance,access" \
  --out "$J/restricted.dat" > /dev/null 2>&1 || fail "صدور مجوز محدود با هارنس ناموفق"
grep -q "\." "$J/restricted.dat" || fail "محتوای مجوز محدود نامعتبر"
say "✓ ISSUE-RESTRICTED (org, attendance, access)"

# ---------- ۴) ورود مجوز محدود ----------
PAGE=$(curl -s -b "$J/c.txt" "$BASE/License")
AF3=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
IMP=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/Import" \
  -F "file=@$J/restricted.dat" -F "__RequestVerificationToken=$AF3")
[ "$IMP" = "302" ] || fail "Import مجوز محدود ناموفق (کد $IMP)"
say "✓ IMPORT-RESTRICTED 302"

# ---------- ۴الف) منو: فقط ماژول‌های مجاز ----------
HOME_HTML=$(curl -s -b "$J/c.txt" "$BASE/")
echo "$HOME_HTML" | grep -q 'href="/Attendance"' || fail "منو: /Attendance باید باشد"
echo "$HOME_HTML" | grep -q 'href="/Employees"' || fail "منو: /Employees (org) باید باشد"
echo "$HOME_HTML" | grep -q 'href="/Access"' || fail "منو: /Access باید باشد"
for m in Parking Visitors Payroll Devices; do
  echo "$HOME_HTML" | grep -q "href=\"/$m\"" && fail "منو: /$m نباید باشد (خارج از مجوز)"
done
say "✓ MENU — ۳ ماژول مجاز در منو، ۴ ماژول خارج از مجوز غایب"

# ---------- ۴ب) داشبورد: ویجت پارکینگ غایب ----------
echo "$HOME_HTML" | grep -q 'href="/Parking' && fail "داشبورد: لینک پارکینگ نباید باشد"
say "✓ DASHBOARD — بدون ویجت/لینک ماژول‌های خارج از مجوز"

# ---------- ۴ج) صفحه‌های قفل‌شده → صفحهٔ دوستانهٔ «در مجوز فعال نشده» ----------
for m in Parking Visitors Payroll Devices; do
  R=$(curl -s -o /dev/null -w "%{http_code} %{redirect_url}" -b "$J/c.txt" "$BASE/$m")
  echo "$R" | grep -q "^302" || fail "/$m باید 302 باشد — الان: $R"
  echo "$R" | grep -q "ModuleDisabled?code=" || fail "/$m باید به ModuleDisabled برود — الان: $R"
  CODE_Q=$(echo "$R" | sed 's/.*code=//')
  FR=$(curl -s -b "$J/c.txt" "$BASE/Home/ModuleDisabled?code=$CODE_Q")
  echo "$FR" | grep -q "در مجوز نصب فعال نشده است" || fail "صفحهٔ دوستانهٔ /$m پیام قفل مجوز ندارد"
  echo "$FR" | grep -q "توسط مدیر سامانه خاموش شده" && fail "پیام «خاموشِ مدیر» نباید برای قفل مجوز نمایش داده شود"
done
say "✓ GATE-PAGE — هر ۴ ماژول خارج از مجوز → پیام «در مجوز نصب فعال نشده است»"

# ---------- ۴د) صفحهٔ مجاز همچنان ۲۰۰ ----------
C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Attendance")
[ "$C" = "200" ] || fail "/Attendance باید 200 باشد (الان $C)"
say "✓ ALLOWED — /Attendance 200"

# ---------- ۴ه) AJAX → 403 با پیام قفل مجوز ----------
XJ=$(curl -s -w "|%{http_code}" -H "X-Requested-With: XMLHttpRequest" -b "$J/c.txt" "$BASE/Payroll")
echo "$XJ" | grep -q "|403" || fail "AJAX /Payroll باید 403 باشد: $XJ"
echo "$XJ" | grep -q "در مجوز نصب فعال نشده است" || fail "پیام 403 باید قفل مجوز باشد"
say "✓ AJAX — 403 + پیام قفل مجوز"

# ---------- ۴و) صفحهٔ /License: نمایش ماژول‌های مجاز ----------
LP=$(curl -s -b "$J/c.txt" "$BASE/License")
echo "$LP" | grep -q "ماژول‌های مجاز" || fail "سطر «ماژول‌های مجاز» در /License نیست"
echo "$LP" | grep -q "سازمان و پرسنل" || fail "عنوان فارسی ماژول org در /License نیست"
echo "$LP" | grep -q "حضور و غیاب" || fail "عنوان فارسی attendance در /License نیست"
say "✓ LICENSE-PAGE — فهرست ماژول‌های مجاز با عنوان فارسی"

# ---------- ۴ز) تنظیمات: نشان «قفل با مجوز» + رد فعال‌سازی دستی ----------
SP=$(curl -s -b "$J/c.txt" "$BASE/Settings")
echo "$SP" | grep -q "قفل با مجوز" || fail "نشان «قفل با مجوز» در تنظیمات نیست"
AF4=$(echo "$SP" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
TOG=$(curl -s -H "X-Requested-With: XMLHttpRequest" -b "$J/c.txt" -X POST "$BASE/Settings/ToggleModule" \
  --data-urlencode "code=parking" --data-urlencode "enabled=true" \
  --data-urlencode "__RequestVerificationToken=$AF4")
echo "$TOG" | grep -q '"ok":false' || fail "فعال‌سازی دستی ماژول خارج از مجوز باید رد شود: $TOG"
echo "$TOG" | python3 -c "import sys,json;print(json.load(sys.stdin)['message'])" | grep -q "در مجوز نصب فعال نشده است" || fail "پیام رد فعال‌سازی باید قفل مجوز باشد"
say "✓ SETTINGS — نشان «قفل با مجوز» + رد فعال‌سازی دستی"

# ---------- ۵) صدور و ورود مجوز «کامل» (بدون فهرست ماژول) → همه‌چیز برگردد ----------
$HARNESS --issue --keys "$J" --machine "$MACHINE" \
  --licensee "شرکت تست فاز۷۲ — کامل" --expires-jalali 1410/01/01 \
  --out "$J/full.dat" > /dev/null 2>&1 || fail "صدور مجوز کامل ناموفق"
AF5=$(curl -s -b "$J/c.txt" "$BASE/License" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
IMP2=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/Import" \
  -F "file=@$J/full.dat" -F "__RequestVerificationToken=$AF5")
[ "$IMP2" = "302" ] || fail "Import مجوز کامل ناموفق (کد $IMP2)"

H2=$(curl -s -b "$J/c.txt" "$BASE/")
echo "$H2" | grep -q 'href="/Parking"' || fail "بعد از مجوز کامل: /Parking باید به منو برگردد"
echo "$H2" | grep -q 'href="/Visitors"' || fail "بعد از مجوز کامل: /Visitors باید برگردد"
echo "$H2" | grep -q 'href="/Payroll"' || fail "بعد از مجوز کامل: /Payroll باید برگردد"
C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Parking")
[ "$C" = "200" ] || fail "بعد از مجوز کامل: /Parking باید 200 باشد (الان $C)"
SP2=$(curl -s -b "$J/c.txt" "$BASE/Settings")
echo "$SP2" | grep -q "قفل با مجوز" && fail "بعد از مجوز کامل: نشان «قفل با مجوز» نباید باشد"
say "✓ FULL-LICENSE — منو/صفحه‌ها برگشت؛ بدون قفل مجوز"

# ---------- ۶) پاکسازی: بازگشت به دورهٔ آزمایشی ----------
rm -rf "$APPD/license-keys"
rm -f "$APPD/license.dat"
sleep 21  # انقضای کش ۲۰ ثانیه‌ای وضعیت مجوز
C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" "$BASE/Parking")
[ "$C" = "200" ] || fail "پاکسازی: /Parking باید 200 بماند (آزمایشی = همهٔ ماژول‌ها)"
say "✓ CLEANUP — بازگشت به دورهٔ آزمایشی (همهٔ ماژول‌ها آزاد)"

echo
echo "همهٔ چک‌های فاز ۷۲ سبز ✓ — سقف ماژول‌ها در مجوز به‌صورت زنده اعمال شد"
