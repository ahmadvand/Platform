#!/usr/bin/env bash
# ======================================================================
# فاز ۵۳ — E2E مدیریت کلید امضای مجوز از صفحهٔ /License
# ۱) ورود ادمین  ۲) کارت کلید  ۳) ساخت جفت‌کلید  ۴) دانلود private.pem
# ۵) صدور مجوز با LicenseGen + کلید دانلودشده  ۶) ورود مجوز → فعال
# ۷) امضای جعلی رد شود  ۸) پاکسازی کامل (بازگشت به آزمایشی)
# ======================================================================
set -u
BASE="${BASE:-http://127.0.0.1:3000}"
J=/tmp/p53
mkdir -p "$J"; rm -f "$J"/*.txt "$J"/*.pem "$J"/*.dat "$J"/*.hrreq 2>/dev/null

say() { printf '%s\n' "$*"; }
fail() { say "✗ $1"; exit 1; }

# ---------- پاکسازی اولیه: از اجرای قبلی کلید/مجوزی نماند ----------
rm -rf /home/z/my-project/aspnet/src/HrAttendance.Web/App_Data/license-keys
rm -f /home/z/my-project/aspnet/src/HrAttendance.Web/App_Data/license.dat

# ---------- ورود ----------
AF=$(curl -s -c "$J/c.txt" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF" ] || fail "توکن آنتی‌فورجری صفحهٔ ورود پیدا نشد"
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$CODE" = "302" ] || fail "ورود ناموفق (کد $CODE)"
say "✓ LOGIN 302"

# ---------- کارت کلید روی صفحه ----------
PAGE=$(curl -s -b "$J/c.txt" "$BASE/License")
echo "$PAGE" | grep -q "کلید امضای مجوز" || fail "کارت کلید روی صفحه نیست"
echo "$PAGE" | grep -q "ساخت جفت‌کلید جدید" || fail "دکمهٔ ساخت جفت‌کلید نیست"
echo "$PAGE" | grep -q "ساخته نشده" || fail "وضعیت اولیهٔ «ساخته نشده» نیست"
EMB=$(echo "$PAGE" | grep -o "کلید مرجع تولیدکننده" | head -1)
[ -n "$EMB" ] || fail "سطر کلید مرجع نیست"
say "✓ KEY-CARD (بدون کلید — «ساخته نشده»)"

# ---------- ساخت جفت‌کلید ----------
AF2=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF2" ] || fail "توکن آنتی‌فورجری /License پیدا نشد"
G=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/GenerateKey" --data-urlencode "__RequestVerificationToken=$AF2")
[ "$G" = "302" ] || fail "GenerateKey ناموفق (کد $G)"
say "✓ GENERATE 302"

PAGE2=$(curl -s -b "$J/c.txt" "$BASE/License")
# اثر انگشت «کلید همین نصب» — سطر دوم کارت (نه کلید مرجع جاسازی‌شده)
FPR=$(echo "$PAGE2" | grep -A3 "کلید همین نصب" | grep -oE '[0-9A-F]{8}-[0-9A-F]{8}-[0-9A-F]{4}' | head -1)
[ -n "$FPR" ] || fail "اثر انگشت کلید ساختگی روی صفحه نیست"
echo "$PAGE2" | grep -q "دانلود کلید خصوصی" || fail "دکمهٔ دانلود بعد از ساخت ظاهر نشد"
say "✓ FINGERPRINT $FPR"

# ---------- دانلود private.pem ----------
HTTP=$(curl -s -o "$J/private.pem" -w "%{http_code}" -b "$J/c.txt" "$BASE/License/PrivateKey")
[ "$HTTP" = "200" ] || fail "دانلود کلید خصوصی ناموفق (کد $HTTP)"
head -1 "$J/private.pem" | grep -q "BEGIN PRIVATE KEY" || fail "فایل دانلودشده PKCS#8 PEM نیست"
openssl pkey -in "$J/private.pem" -noout -text 2>/dev/null | head -1 | grep -q "2048" || fail "کلید RSA-2048 نیست"
say "✓ PRIVATE-PEM (PKCS#8 / RSA-2048)"

# ---------- محافظت روی دیسک ----------
APPD=/home/z/my-project/aspnet/src/HrAttendance.Web/App_Data/license-keys
grep -q "^PROTECTED1:" "$APPD/private.pem" || fail "کلید خصوصی روی دیسک رمز نشده است"
say "✓ AT-REST (فایل دیسک PROTECTED1)"

# ---------- بدون نشست → دانلود نباید باشد ----------
NOSESS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/License/PrivateKey")
echo "$NOSESS" | grep -qE "302" || fail "دانلود بدون نشست مسدود نشد (کد $NOSESS)"
say "✓ GUARD (بدون نشست → ریدایرکت ورود)"

# ---------- صدور مجوز با کلید دانلودشده ----------
curl -s -o "$J/req.hrreq" "$BASE/License/RequestFile"
GEN=/tmp/p53-gen; rm -rf "$GEN"
cd /home/z/my-project && export PATH="/home/z/.dotnet:$PATH"
dotnet run --project tools/LicenseGen -- issue --key "$J/private.pem" --req "$J/req.hrreq" \
  --expires 2027-10-20 --notes "تست فاز ۵۳" --out "$J/lic.dat" >/dev/null 2>&1 || fail "LicenseGen issue ناموفق"
[ -s "$J/lic.dat" ] || fail "فایل مجوز ساخته نشد"
say "✓ ISSUE (با private.pem دانلودشده)"

# ---------- ورود مجوز → فعال ----------
I=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/Import" \
  --data-urlencode "__RequestVerificationToken=$AF2" --data-urlencode "text=$(cat "$J/lic.dat")")
[ "$I" = "302" ] || fail "Import ناموفق (کد $I)"
PAGE3=$(curl -s -b "$J/c.txt" "$BASE/License")
echo "$PAGE3" | grep -q "فعال و معتبر" || fail "مجوز فعال نشد"
say "✓ IMPORT → فعال و معتبر"

# ---------- امضای جعلی (کلید دیگر) رد شود ----------
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$J/other.pem" 2>/dev/null
dotnet run --project tools/LicenseGen -- issue --key "$J/other.pem" --req "$J/req.hrreq" \
  --expires 2027-10-20 --out "$J/fake.dat" >/dev/null 2>&1
[ -s "$J/fake.dat" ] || fail "ساخت مجوز با کلید جعلی شکست خورد (هارنس)"
# نکته: از -L استفاده نمی‌کنیم — curl 8 روی 302 «Stick to POST» می‌کند و POST /License → 405؛
# به‌جایش کوکی TempData با -c ذخیره و صفحه در GET جداگانه خوانده می‌شود.
curl -s -o /dev/null -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/License/Import" \
  --data-urlencode "__RequestVerificationToken=$AF2" --data-urlencode "text=$(cat "$J/fake.dat")"
F=$(curl -s -b "$J/c.txt" "$BASE/License")
echo "$F" | grep -q "امضای دیجیتال تأیید نشد" || fail "مجوز جعلی پذیرفته شد!"
say "✓ FORGERY-REJECTED"

# ---------- حذف جفت‌کلید (پاکسازی) + حذف مجوز تست ----------
R=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -X POST "$BASE/License/RemoveKey" --data-urlencode "__RequestVerificationToken=$AF2")
[ "$R" = "302" ] || fail "RemoveKey ناموفق (کد $R)"
PAGE4=$(curl -s -b "$J/c.txt" "$BASE/License")
echo "$PAGE4" | grep -q "ساخته نشده" || fail "پس از حذف، وضعیت «ساخته نشده» نیست"
# مجوزِ فعالِ قبلی (امضاشده با کلید حذف‌شده) باید نامعتبر شود — طلوع ابطال کلید
PAGE4b=$(curl -s -b "$J/c.txt" "$BASE/License")
echo "$PAGE4b" | grep -q "نامعتبر" || fail "پس از حذف کلید، مجوز قبلی همچنان معتبر ماند"
say "✓ REVOKE (مجوز امضاشده با کلید حذف‌شده → نامعتبر)"
rm -rf "$APPD"
rm -f /home/z/my-project/aspnet/src/HrAttendance.Web/App_Data/license.dat
say "✓ REMOVE + CLEANUP"

# ---------- حسابرسی ----------
AUD=$(curl -s -b "$J/c.txt" "$BASE/Audit" )
for act in license-key-generated license-key-downloaded license-key-removed license-activated license-rejected; do
  echo "$AUD" | grep -q "$act" || fail "رکورد حسابرسی $act نیست"
done
say "✓ AUDIT (۵ رخداد)"

say "===== P53 LICENSE-KEY E2E: ALL OK ====="
