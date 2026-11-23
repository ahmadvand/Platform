#!/usr/bin/env bash
# ============================================================
# تسک ۲۳ — ادغام پرتال خودخدمتی (ESS) در پنل یکپارچه
# ورود کارکنان از صفحه ورود اصلی → نشست JWT با نقش «پرسنل» (self.*)
# پرتال من: خانه/درخواست‌ها/کارکرد/فیش — همه دسترسی‌محور + AJAX
# اجرا: bash scripts/e2e-self-portal.sh
# ============================================================
set -u
cd "$(dirname "$0")/.."
BASE="http://localhost:3000"
PASS=0; FAIL=0
ck() { if [ "$1" = "1" ]; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }
TSMARK=$(date +%s)

# ---------- کمکی‌ها ----------
CJ=/tmp/self-cookies.txt; rm -f "$CJ"          # نشست کارمند
CJA=/tmp/self-admin-cookies.txt; rm -f "$CJA"  # نشست مدیر
GET()  { curl -s -b "$CJ" -c "$CJ" "$BASE$1" --max-time 15; }
POST() { local url="$1"; shift; curl -s -b "$CJ" -c "$CJ" -X POST "$BASE$url" "$@" --max-time 15; }
GETA() { curl -s -b "$CJA" -c "$CJA" "$BASE$1" --max-time 15; }
POSTA(){ local url="$1"; shift; curl -s -b "$CJA" -c "$CJA" -X POST "$BASE$url" "$@" --max-time 15; }
TOKEN()  { GET "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
TOKENA() { GETA "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
XHR="X-Requested-With: XMLHttpRequest"
jmsg() { python3 -c "import sys,json;d=json.load(sys.stdin);print(d.get('$1',''))" 2>/dev/null; }

# ---------- داده پایه ----------
EMP=$(python3 scripts/dbq.py --json "SELECT Id, PersonnelCode, NationalId FROM Employees WHERE Status='فعال' ORDER BY PersonnelCode LIMIT 1" --json 2>/dev/null)
EID=$(echo "$EMP" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
CODE=$(echo "$EMP" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['PersonnelCode'])")
NID=$(echo "$EMP" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['NationalId'])")
echo "پرسنل نمونه: $CODE / دسترسی‌های self سید شدند"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM Permissions WHERE Key LIKE 'self.%'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "3" ] && echo 1 || echo 0)" "سه دسترسی self.* در کاتالوگ (یافت: $n)"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM RolePermissions rp JOIN Roles r ON r.Id=rp.RoleId JOIN Permissions p ON p.Id=rp.PermissionId WHERE r.Name='employee'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "3" ] && echo 1 || echo 0)" "نقش «پرسنل» با ۳ دسترسی self (یافت: $n)"

echo "── ۱) صفحه ورود یکپارچه (یک فرم برای همه — تسک ۲۵)"
P=$(curl -s "$BASE/Auth/Login")
echo "$P" | grep -q 'name="identifier"' && echo "$P" | grep -q 'name="secret"' && ck 1 "فیلدهای یکپارچه identifier/secret" || ck 0 "فیلدهای یکپارچه"
n=$(echo "$P" | grep -c '<form method="post"')
ck "$([ "$n" = "1" ] && echo 1 || echo 0)" "دقیقاً یک فرم روی صفحه (یافت: $n)"
echo "$P" | grep -q 'کد پرسنلی' && echo "$P" | grep -q 'کد ملی' && ck 1 "راهنمای ورود کارکنان روی همان فرم" || ck 0 "راهنمای کارکنان"
echo "$P" | grep -q 'LoginEmployee' && ck 0 "فرم جداگانه کارکنان باید حذف شده باشد" || ck 1 "فرم جداگانه کارکنان حذف شد"

echo "── ۲) ورود کارمند از فرم واحد"
tk=$(TOKEN /Auth/Login)
r=$(POST /Auth/Login --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "identifier=$CODE" --data-urlencode "secret=0000000000")
echo "$r" | grep -q "اطلاعات ورود نادرست" && ck 1 "رد اطلاعات نادرست + شمارنده تلاش" || ck 0 "رد اطلاعات نادرست"
tk=$(TOKEN /Auth/Login)
code=$(curl -s -o /dev/null -w "%{http_code}" -b "$CJ" -c "$CJ" -X POST "$BASE/Auth/Login" \
  --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "identifier=$CODE" --data-urlencode "secret=$NID" --max-time 15)
ck "$([ "$code" = "302" ] && echo 1 || echo 0)" "ورود موفق از فرم واحد → 302 به پرتال من"
P=$(GET /Self)
echo "$P" | grep -q "اطلاعات من" && ck 1 "خانه پرتال من (KPI + اطلاعات من)" || ck 0 "خانه پرتال من"
echo "$P" | grep -q "پرتال من" && echo "$P" | grep -q "درخواست‌های من" && ck 1 "منوی «پرتال من» دسترسی‌محور" || ck 0 "منوی پرتال من"
echo "$P" | grep -q "کاربران و نقش‌ها" && ck 0 "کارمند نباید منوی مدیریت سیستم را ببیند" || ck 1 "منوهای مدیریتی پنهان (بدون users.view)"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM Users WHERE Username='emp:$CODE'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "1" ] && echo 1 || echo 0)" "کاربر خودکار emp:$CODE ساخته شد" || true
code=$(curl -s -o /dev/null -w "%{http_code}" -b "$CJ" "$BASE/Users" --max-time 15)
ck "$([ "$code" = "302" ] && echo 1 || echo 0)" "/Users بدون دسترسی → 302 Denied"
loc=$(curl -s -o /dev/null -w "%{redirect_url}" -b "$CJ" "$BASE/" --max-time 15)
echo "$loc" | grep -q "/Self" && ck 1 "داشبورد برای کارمند فقط-پرتالی → /Self" || ck 0 "ریدایرکت داشبورد (یافت: $loc)"

echo "── ۳) فرم‌های AJAX پرتال من"
P=$(GET /Self/Requests)
echo "$P" | grep -q "data-refresh-host" && ck 1 "host تعویض درجا" || ck 0 "host تعویض درجا"
n=$(echo "$P" | grep -o 'data-ajax' | wc -l)
ck "$([ "$n" -ge 3 ] && echo 1 || echo 0)" "سه فرم data-ajax (یافت: $n)"
tk=$(TOKEN /Self/Requests)
LM="self-leave-$TSMARK"
r=$(POST /Self/CreateLeave -H "$XHR" --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "type=استحقاقی" --data-urlencode "fromDate=2026-11-01" \
  --data-urlencode "toDate=2026-11-03" --data-urlencode "days=1.5" --data-urlencode "reason=$LM")
echo "$r" | grep -q '"ok":true' && ck 1 "مرخصی AJAX ok:true" || ck 0 "مرخصی AJAX (یافت: $r)"
m=$(echo "$r" | jmsg message); echo "$m" | grep -q "در انتظار تأیید" && ck 1 "پیام فارسی toast" || ck 0 "پیام toast (یافت: $m)"
tk=$(TOKEN /Self/Requests)
MM="self-mission-$TSMARK"
r=$(POST /Self/CreateMission -H "$XHR" --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "type=داخلی" --data-urlencode "destination=$MM" \
  --data-urlencode "fromDate=2026-11-05" --data-urlencode "toDate=2026-11-06" --data-urlencode "purpose=e2e")
echo "$r" | grep -q '"ok":true' && GET /Self/Requests | grep -q "$MM" && ck 1 "مأموریت AJAX + درجای لیست" || ck 0 "مأموریت AJAX"
tk=$(TOKEN /Self/Requests)
CM="self-corr-$TSMARK"
r=$(POST /Self/CreateCorrection -H "$XHR" --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "date=2026-09-01" --data-urlencode "requestedIn=08:30" \
  --data-urlencode "requestedOut=17:45" --data-urlencode "reason=$CM")
echo "$r" | grep -q '"ok":true' && ck 1 "اصلاح تردد AJAX ok:true" || ck 0 "اصلاح تردد AJAX (یافت: $r)"
CID=$(python3 scripts/dbq.py --json "SELECT Id FROM CorrectionRequests WHERE Reason='$CM' LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
tk=$(TOKEN /Self/Requests)
r=$(POST /Self/CreateLeave -H "$XHR" --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "type=استحقاقی" --data-urlencode "fromDate=2026-11-01" \
  --data-urlencode "toDate=2026-11-02" --data-urlencode "days=0" --data-urlencode "reason=x")
echo "$r" | grep -q '"ok":false' && ck 1 "اعتبارسنجی ok:false" || ck 0 "اعتبارسنجی (یافت: $r)"

echo "── ۴) fallback بدون JS + آنتی‌فورجری"
tk=$(TOKEN /Self/Requests)
code=$(curl -s -o /dev/null -w "%{http_code}" -b "$CJ" -c "$CJ" -X POST "$BASE/Self/CreateMission" \
  --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "type=داخلی" \
  --data-urlencode "destination=self-fb-$TSMARK" --data-urlencode "fromDate=2026-11-07" \
  --data-urlencode "toDate=2026-11-08" --max-time 15)
ck "$([ "$code" = "302" ] && echo 1 || echo 0)" "بدون هدر AJAX → 302"
GET /Self/Requests | grep -q "toast toast-ok" && ck 1 "بنر TempData در fallback سالم" || ck 0 "بنر fallback"
tk=$(TOKEN /Self/Requests)
code=$(curl -s -o /dev/null -w "%{http_code}" -b "$CJ" -X POST "$BASE/Self/CreateLeave" -H "$XHR" \
  --data-urlencode "type=استحقاقی" --data-urlencode "fromDate=2026-11-01" \
  --data-urlencode "toDate=2026-11-02" --data-urlencode "days=1" --max-time 15)
ck "$([ "$code" = "400" ] && echo 1 || echo 0)" "بدون توکن → 400"

echo "── ۵) کارکرد و فیش"
GET /Self/Attendance | grep -q "کارکرد ماه" && ck 1 "کارکرد من" || ck 0 "کارکرد من"
GET /Self/Payslips | grep -q "فیش‌های حقوقی" && ck 1 "فیش‌های من" || ck 0 "فیش‌های من"
code=$(curl -s -o /dev/null -w "%{http_code}" -b "$CJ" "$BASE/Self/Payslip/NOT-EXISTS" --max-time 15)
ck "$([ "$code" = "404" ] && echo 1 || echo 0)" "فیش متعلق به دیگران/ناموجود → 404"

echo "── ۶) سازگاری بوکمارک‌های ESS قدیمی"
loc=$(curl -s -o /dev/null -w "%{redirect_url}" "$BASE/Ess/Requests" --max-time 15)
echo "$loc" | grep -q "/Self/Requests" && ck 1 "/Ess/Requests → /Self/Requests" || ck 0 "ریدایرکت ESS (یافت: $loc)"
loc=$(curl -s -o /dev/null -w "%{redirect_url}" "$BASE/Ess/Login" --max-time 15)
echo "$loc" | grep -q "/Auth/Login" && ck 1 "/Ess/Login → /Auth/Login" || ck 0 "ریدایرکت ورود (یافت: $loc)"

echo "── ۷) چرخه کامل: مدیر تأیید می‌کند → رکورد تردد بازنویسی می‌شود"
tk=$(TOKENA /Auth/Login)
POSTA /Auth/Login --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" > /dev/null
GETA /Self | grep -q "اطلاعات من\|متصل نیست" && ck 1 "مدیر هم /Self را می‌بیند (NoLink/داده)" || ck 0 "مدیر /Self"
tk=$(TOKENA /Requests)
POSTA /Requests/DecideCorrection --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "id=$CID" --data-urlencode "approve=true" \
  -H "$XHR" --max-time 15 > /dev/null
ST=$(python3 scripts/dbq.py --json "SELECT Status FROM CorrectionRequests WHERE Id='$CID'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Status'])")
ck "$([ "$ST" = "تأیید شده" ] && echo 1 || echo 0)" "وضعیت درخواست: تأیید شده (یافت: $ST)"
REC=$(python3 scripts/dbq.py --json "SELECT Note FROM AttendanceRecords WHERE EmployeeId='$EID' AND Date='2026-09-01'" 2>/dev/null | python3 -c "import sys,json;d=json.load(sys.stdin);print(d[0]['Note'] if d else '')")
echo "$REC" | grep -q "اصلاح‌شده" && ck 1 "رکورد تردد واقعی بازنویسی شد" || ck 0 "رکورد تردد (یافت: $REC)"

echo "── ۸) گارد حساب اختصاصی متصل"
tk=$(TOKENA /Users/Create)
POSTA /Users/Create --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "username=e2elink$TSMARK" --data-urlencode "fullName=تست اتصال" \
  --data-urlencode "password=Link@12345" --data-urlencode "password2=Link@12345" \
  --data-urlencode "employeeId=$EID" --data-urlencode "isActive=true" --max-time 15 > /dev/null
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM Users WHERE EmployeeId='$EID' AND Username LIKE 'e2elink%'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "1" ] && echo 1 || echo 0)" "کاربر متصل به پرسنل ساخته شد"
CJ3=/tmp/self-link-cookies.txt; rm -f "$CJ3"
tk=$(curl -s -b "$CJ3" -c "$CJ3" "$BASE/Auth/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
r=$(curl -s -b "$CJ3" -c "$CJ3" -X POST "$BASE/Auth/Login" \
  --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "identifier=$CODE" --data-urlencode "secret=$NID" --max-time 15)
echo "$r" | grep -q "حساب کاربری اختصاصی دارد" && ck 1 "ورود کد-پرسنلی برای حساب متصل مسدود شد" || ck 0 "گارد حساب متصل (یافت: $r)"

echo "── پاکسازی"
python3 scripts/dbq.py --exec "DELETE FROM LeaveRequests WHERE Reason LIKE 'self-leave-%'" > /dev/null 2>&1
python3 scripts/dbq.py --exec "DELETE FROM Missions WHERE Destination LIKE 'self-mission-%' OR Destination LIKE 'self-fb-%'" > /dev/null 2>&1
python3 scripts/dbq.py --exec "DELETE FROM CorrectionRequests WHERE Reason LIKE 'self-corr-%'" > /dev/null 2>&1
python3 scripts/dbq.py --exec "DELETE FROM AttendanceRecords WHERE EmployeeId='$EID' AND Date='2026-09-01' AND Note LIKE 'اصلاح‌شده%'" > /dev/null 2>&1
python3 scripts/dbq.py --exec "DELETE FROM Users WHERE Username LIKE 'e2elink%'" > /dev/null 2>&1
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM Missions WHERE Destination LIKE 'self-%'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "0" ] && echo 1 || echo 0)" "پاکسازی کامل"

echo "════════════════════════════"
echo "PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ] && echo "DONE ✓" || echo "DONE ✗"
exit $([ "$FAIL" = "0" ] && echo 0 || echo 1)
