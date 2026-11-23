#!/bin/bash
# رگرسیون کامل پس از حذف X-Frame-Options
B=http://localhost:3000
J=/tmp/reg.txt
UA="Mozilla/5.0"
rm -f $J
PASS=0; FAIL=0
ok(){ if [ "$1" = "$2" ]; then PASS=$((PASS+1)); echo "  ✓ $3"; else FAIL=$((FAIL+1)); echo "  ✗ $3 (انتظار $2، گرفتم $1)"; fi; }

echo "== 1) هدرها =="
H=$(curl -s -D - -o /dev/null "$B/Auth/Login")
echo "$H" | grep -qi "X-Frame-Options" && { FAIL=$((FAIL+1)); echo "  ✗ هنوز XFO هست"; } || { PASS=$((PASS+1)); echo "  ✓ X-Frame-Options حذف شد"; }

echo "== 2) ورود و صفحات =="
C=$(curl -s -o /dev/null -w "%{http_code}" -c $J -b $J -X POST "$B/api/auth/login" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}')
ok "$C" "200" "login API"
for p in "" "Employees" "Devices" "Biometrics" "Payroll" "Users" "Access" "Visitors"; do
  C=$(curl -s -o /dev/null -w "%{http_code}" -b $J -c $J -A "$UA" "$B/$p")
  ok "$C" "200" "/$p"
done

echo "== 3) فرم‌ها (Antiforgery سالم؟) =="
PAGE=$(curl -s -b $J -c $J -A "$UA" "$B/Biometrics")
TOKEN=$(echo "$PAGE" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')
[ -n "$TOKEN" ] && { PASS=$((PASS+1)); echo "  ✓ توکن آنتی‌فورجری تولید می‌شود"; } || { FAIL=$((FAIL+1)); echo "  ✗ توکن نیست"; }
DEV=$(python3 -c "import sqlite3; print(sqlite3.connect('/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db').execute('SELECT Id FROM Devices LIMIT 1').fetchone()[0])")
C=$(curl -s -o /dev/null -w "%{http_code}" -b $J -c $J -A "$UA" -X POST "$B/Biometrics/SimulateFetch" -d "deviceId=$DEV&mode=users&__RequestVerificationToken=$TOKEN")
ok "$C" "302" "POST فرم شبیه‌ساز (302=موفق PRG)"

echo "== 4) پنل داده‌های هویتی =="
curl -s -b $J -c $J -A "$UA" "$B/Biometrics" -o /tmp/fin.html
python3 -c "
import html
t=html.unescape(open('/tmp/fin.html').read())
print('  ✓ منوی داده‌های هویتی' if 'داده\u200cهای هویتی' in t else '  ✗ منو نیست')
"
echo; echo "نتیجه: $PASS موفق / $FAIL ناموفق"
