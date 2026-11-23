#!/usr/bin/env bash
# ======================================================================
# فاز ۵۸ — اثبات زمان اجرای «بستهٔ حداقلی» (فقط پایه‌ها)
# اپ باید با بیلد حذف‌شدهٔ هر ۵ ماژول سیستم بالا آمده باشد:
#   -p:HrModuleParking=false -p:HrModuleAttendance=false -p:HrModuleAccess=false \
#   -p:HrModuleVisitors=false -p:HrModulePayroll=false
# توقع: هیچ ماژولی DLL ندارد؛ سامانه سالم بالا می‌آید، پایه‌ها کامل کار
# می‌کنند، مسیرهای سیستم‌ها صفحهٔ دوستانهٔ «نصب نشده» می‌دهند (نه کرش/۴۰۴ خام)
# ======================================================================
set -u
# فاز ۶۵ — BASE/DLLDIR قابل بازنویسی: اثبات فیزیکی باید روی «پابلیش حداقلی»
# (build-customer) اجرا شود نه حذف DLL از bin دیباگ (Web به نوع‌های ماژول TypeRef دارد).
BASE="${BASE:-http://127.0.0.1:3000}"
DLLDIR="${DLLDIR:-/home/z/my-project/aspnet/src/HrAttendance.Web/bin/Debug/net8.0}"
J=/tmp/p58; mkdir -p "$J"; rm -f "$J"/*.txt
P=0; F=0
ok()  { P=$((P+1)); printf '✓ %s\n' "$1"; }
bad() { F=$((F+1)); printf '✗ %s\n' "$1"; }

# ---- DLLهای ماژول واقعاً نباشند (شرط ورود به تست)
for d in Parking Attendance Access Visitors Payroll DeviceGateway; do
  [ -f "$DLLDIR/HrModules.$d.dll" ] \
    && bad "DLL-CHECK: HrModules.$d.dll هنوز در bin است!" \
    || ok "DLL-CHECK: HrModules.$d.dll حذف شده"
done

# ---- ورود
AF=$(curl -s -c "$J/c.txt" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF" ] || { bad "توکن ورود"; exit 1; }
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$CODE" = "302" ] && ok "LOGIN 302" || { bad "LOGIN=$CODE"; exit 1; }

# ---- هر ۸ مسیر سیستم‌ها → ریدایرکت دوستانه با کد ماژول درست
for pair in "/Attendance attendance" "/Shifts attendance" "/Requests attendance" \
            "/Access access" "/Cards access" "/Visitors visitors" \
            "/Payroll payroll" "/Parking parking" "/DeviceGateway gateway"; do
  set -- $pair
  R=$(curl -s -b "$J/c.txt" -o /dev/null -w "%{http_code} %{redirect_url}" "$BASE$1")
  echo "$R" | grep -q "^302 .*ModuleDisabled?code=$2$" && ok "GATE $1 → code=$2" || bad "GATE $1 → $R"
done

# ---- صفحهٔ دوستانه: واریانت «نصب نشده» برای هر ۵ کد
for c in attendance access visitors payroll parking; do
  PG=$(curl -s -b "$J/c.txt" "$BASE/Home/ModuleDisabled?code=$c")
  echo "$PG" | grep -q "نصب نشده است" && ok "PAGE $c: «در این نسخه نصب نشده است»" || bad "PAGE $c: واریانت اشتباه"
done

# ---- منو: هیچ href سیستمی نمانده؛ پایه‌ها سر جایشان
S=$(curl -s -b "$J/c.txt" "$BASE/Settings")
MISS=0
for h in /Attendance /Shifts /Requests /Access /Cards /Visitors /Payroll /Parking; do
  if echo "$S" | grep -q "href=\"$h\""; then
    MISS=1; bad "MENU: $h هنوز هست"
  fi
done
[ "$MISS" = "0" ] && ok "MENU: هر ۸ لینک سیستمی حذف شد"
HIT=0
for h in /Employees /Org /Holidays /Devices /Gateway /Biometrics /Reports /Settings /Audit; do
  if echo "$S" | grep -q "href=\"$h\""; then
    :
  else
    HIT=1; bad "MENU: پایهٔ $h گم شد"
  fi
done
[ "$HIT" = "0" ] && ok "MENU: همهٔ لینک‌های پایه سر جایشان"

# ---- نشان تنظیمات: ۵ × «نصب نشده در این نسخه»
N=$(echo "$S" | grep -o "نصب نشده در این نسخه" | wc -l)
[ "$N" -ge 5 ] && ok "SETTINGS: نشان نصب‌نشده ×$N" || bad "SETTINGS: نشان نصب‌نشده فقط ×$N"

# ---- پایه‌ها کامل 200
for r in / /Employees /Org /Holidays /Devices /Devices/LiveView /Gateway /Biometrics /Reports /Users /Companies /Notifications /Audit /Settings /License /Self; do
  c=$(curl -s -b "$J/c.txt" -o /dev/null -w "%{http_code}" "$BASE$r")
  [ "$c" = "200" ] && ok "BASE $r 200" || bad "BASE $r=$c"
done

# ---- بخش‌های چند-ماژولیِ هاست: بسته‌های ماژولی 302، بقیه 200
for pair in "/Self/Requests attendance" "/Self/Attendance attendance" "/Self/Payslips payroll" \
            "/Ess/Requests attendance" "/Ess/Payslips payroll" \
            "/Reports/Parking parking" "/Export/Attendance attendance"; do
  set -- $pair
  R=$(curl -s -b "$J/c.txt" -o /dev/null -w "%{http_code} %{redirect_url}" "$BASE$1")
  echo "$R" | grep -q "^302 .*code=$2$" && ok "HOST-SECTION $1 → $2 بسته" || bad "HOST-SECTION $1 → $R"
done
for r in /Export/Employees /Reports; do
  c=$(curl -s -b "$J/c.txt" -o /dev/null -w "%{http_code}" "$BASE$r")
  [ "$c" = "200" ] && ok "HOST-SECTION $r 200 (باز)" || bad "HOST-SECTION $r=$c"
done

# ---- XHR → 403 JSON واریانت نصب‌نشده
X=$(curl -s -H "X-Requested-With: XMLHttpRequest" -b "$J/c.txt" -w "|%{http_code}" "$BASE/Attendance")
echo "$X" | grep -q "|403$" && ok "XHR /Attendance → 403" || bad "XHR=$(echo "$X" | tail -c 8)"
python3 -c "
import json,sys
d=json.loads('''$(echo "$X" | sed 's/|[0-9]*$//')'''.strip().split(chr(10))[-1])
t=d.get('message','')
sys.exit(0 if ('نصب نشده' in t) else 1)
" && ok "XHR-MSG: واریانت «نصب نشده»" || bad "XHR-MSG اشتباه"

# ---- وب‌هوک ANPR غایب = 404 واقعی (قرارداد پل)
C=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/anpr/webhook?SN=TEST")
[ "$C" = "404" ] && ok "ANPR-WEBHOOK 404" || bad "ANPR-WEBHOOK=$C"

# ---- toggle ماژول نصب‌نشده رد می‌شود (JSON یونیکد-اسکیپ → پایتون)
AF2=$(echo "$S" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
T=$(curl -s -b "$J/c.txt" -H "X-Requested-With: XMLHttpRequest" -X POST "$BASE/Settings/ToggleModule" \
  --data-urlencode "code=attendance" --data-urlencode "enabled=true" \
  --data-urlencode "__RequestVerificationToken=$AF2")
echo "$T" | grep -q '"ok":false' && ok "TOGGLE رد شد" || bad "TOGGLE=$(echo "$T" | head -c 120)"
python3 -c "
import json,sys
d=json.loads('''$T''')
sys.exit(0 if ('نصب' in d.get('message','') and 'وجود ندارد' in d.get('message','')) else 1)
" && ok "TOGGLE-MSG: «در این نسخهٔ نصب‌شده وجود ندارد»" || bad "TOGGLE-MSG اشتباه"

# ---- فایل‌های استاتیک هاست (بی‌تأثیر از حذف ماژول‌ها)
for r in /js/site.js /css/site.css /js/chart.umd.js; do
  c=$(curl -s -o /dev/null -w "%{http_code}" "$BASE$r")
  [ "$c" = "200" ] && ok "STATIC $r 200" || bad "STATIC $r=$c"
done

printf -- "-----------------------------\n"
printf "P58-MINIMAL: PASS=%s FAIL=%s\n" "$P" "$F"
[ "$F" = "0" ]
