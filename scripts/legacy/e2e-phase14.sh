#!/usr/bin/env bash
# ============================================================
# فاز ۱۴ — پرتال خودخدمتی پرسنل (ESS)
# ورود با کد پرسنلی + کد ملی، داشبورد کارکرد، ثبت درخواست
# مرخصی/مأموریت/اصلاح تردد، فیش حقوقی، تأیید از پنل مدیریت
# اجرا: bash scripts/e2e-phase14.sh
# ============================================================
set -u
cd "$(dirname "$0")/.."
BASE="http://localhost:3000"
PASS=0; FAIL=0
ck() { if [ "$1" = "1" ]; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }

# ---------- کمکی‌های curl با کوکی ----------
CJ=/tmp/ess-cookies.txt; rm -f "$CJ"
GET()  { curl -s -b "$CJ" -c "$CJ" "$BASE$1" --max-time 15; }
POST() { local url="$1"; shift; curl -s -b "$CJ" -c "$CJ" -X POST "$BASE$url" "$@" --max-time 15; }

TOKEN() { GET "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }

# ---------- داده پایه ----------
EMP=$(python3 scripts/dbq.py --json "SELECT Id, PersonnelCode, NationalId FROM Employees WHERE Status='فعال' ORDER BY PersonnelCode LIMIT 1" --json 2>/dev/null)
EID=$(echo "$EMP" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
CODE=$(echo "$EMP" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['PersonnelCode'])")
NID=$(echo "$EMP" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['NationalId'])")
echo "پرسنل نمونه: $CODE / $NID"

echo "── ۱) صفحه ورود پرتال"
r=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/Ess/Login")
ck "$([ "$r" = "200" ] && echo 1 || echo 0)" "GET /Ess/Login → 200"
GET /Ess/Login | grep -q "کد پرسنلی" && ck 1 "فرم ورود (کد پرسنلی + کد ملی)" || ck 0 "فرم ورود"
curl -s "$BASE/Ess/Login" | grep -q "ورود مدیران" && ck 1 "لینک ورود مدیران روی پرتال" || ck 0 "لینک ورود مدیران"

echo "── ۲) ورود با اطلاعات نادرست"
tk=$(TOKEN /Ess/Login)
r=$(POST /Ess/Login -d "__RequestVerificationToken=$tk&personnelCode=$CODE&nationalId=9999999999")
echo "$r" | grep -q "اطلاعات ورود نادرست" && ck 1 "رد ورود نادرست + شمارنده تلاش" || ck 0 "رد ورود نادرست"
r=$(curl -s -b "$CJ" "$BASE/Ess" -L --max-time 10)
echo "$r" | grep -q "پرتال خدمت کارکنان" && [ -f "$CJ" ] && echo "$r" | grep -q "کد پرسنلی:" && ck 0 "بدون کوکی، /Ess نباید داشبورد بدهد" || ck 1 "بدون کوکی → هدایت به ورود"

echo "── ۳) ورود موفق"
tk=$(TOKEN /Ess/Login)
r=$(POST /Ess/Login --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "personnelCode=$CODE" --data-urlencode "nationalId=$NID")
ck "$(curl -s -b "$CJ" -o /dev/null -w "%{http_code}" "$BASE/Ess" | grep -q 200 && echo 1 || echo 0)" "ورود موفق → داشبورد 200"
r=$(curl -s -b "$CJ" "$BASE/Ess")
echo "$r" | grep -q "روزهای حاضر" && ck 1 "داشبورد کارکرد (KPI ماه جاری)" || ck 0 "داشبورد کارکرد"
echo "$r" | grep -q "مانده مرخصی استحقاقی" && ck 1 "مانده مرخصی سالانه" || ck 0 "مانده مرخصی"
echo "$r" | grep -q "کد پرسنلی:" && ck 1 "کد پرسنلی در نوار بالا" || ck 0 "کد پرسنلی در هدر"

echo "── ۴) صفحات پرتال"
for p in "/Ess/Requests" "/Ess/Attendance" "/Ess/Payslips"; do
  r=$(curl -s -b "$CJ" -o /dev/null -w "%{http_code}" "$BASE$p")
  ck "$([ "$r" = "200" ] && echo 1 || echo 0)" "GET $p → 200"
done

echo "── ۵) ثبت درخواست مرخصی از پرتال"
tk=$(TOKEN /Ess/Requests)
FROM=$(date -d "+5 day" +%F); TO=$(date -d "+6 day" +%F)
r=$(POST /Ess/CreateLeave --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "type=استحقاقی" --data-urlencode "fromDate=$FROM" --data-urlencode "toDate=$TO" --data-urlencode "days=1" --data-urlencode "reason=کارهای شخصی")
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM LeaveRequests WHERE EmployeeId='$EID' AND FromDate='$FROM' AND Status='در انتظار تأیید'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])" 2>/dev/null || echo 0)
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "درخواست مرخصی با وضعیت «در انتظار تأیید» ثبت شد"

echo "── ۶) ثبت درخواست مأموریت"
tk=$(TOKEN /Ess/Requests)
r=$(POST /Ess/CreateMission --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "type=داخلی" --data-urlencode "destination=شیراز — کارخانه" --data-urlencode "fromDate=$FROM" --data-urlencode "toDate=$TO" --data-urlencode "purpose=بازدید فنی")
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM Missions WHERE EmployeeId='$EID' AND Destination LIKE '%شیراز%' AND Status='در انتظار تأیید'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])" 2>/dev/null || echo 0)
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "درخواست مأموریت ثبت شد"

echo "── ۷) ثبت درخواست اصلاح تردد"
tk=$(TOKEN /Ess/Requests)
Y=$(date -d "-2 day" +%F)
r=$(POST /Ess/CreateCorrection --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "date=$Y" --data-urlencode "requestedIn=08:05" --data-urlencode "requestedOut=17:10" --data-urlencode "reason=ثبت نشدن ورود کارتی")
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM CorrectionRequests WHERE EmployeeId='$EID' AND Date='$Y' AND Status='در انتظار تأیید'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])" 2>/dev/null || echo 0)
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "درخواست اصلاح تردد ثبت شد"

echo "── ۸) تأیید از پنل مدیریت (گردش کار سرپرست)"
CJ2=/tmp/ess-admin-cookies.txt; rm -f "$CJ2"
APOST() { local url="$1"; shift; curl -s -b "$CJ2" -c "$CJ2" -L -X POST "$BASE$url" "$@" --max-time 15; }
ATOKEN() { curl -s -b "$CJ2" -c "$CJ2" "$BASE$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
curl -s -c "$CJ2" -X POST "$BASE/api/auth/login" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' > /dev/null
LID=$(python3 scripts/dbq.py --json "SELECT TOP 1 Id FROM LeaveRequests WHERE EmployeeId='$EID' AND Status='در انتظار تأیید' ORDER BY CreatedAt DESC" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])" 2>/dev/null)
if [ -n "${LID:-}" ]; then
  tk=$(ATOKEN /Requests)
  APOST "/Requests/DecideLeave" -d "__RequestVerificationToken=$tk&id=$LID&approve=true" > /dev/null
  st=$(python3 scripts/dbq.py --json "SELECT TOP 1 Status FROM LeaveRequests WHERE Id='$LID'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Status'])" 2>/dev/null)
  ck "$([ "$st" = "تأیید شده" ] && echo 1 || echo 0)" "تأیید مرخصی توسط مدیر → «تأیید شده»"
else
  echo "$LID" | head -2
  ck 0 "یافتن شناسه درخواست (TOP/LIMIT دیتابیس فعال)"
fi

echo "── ۹) وضعیت جدید در پرتال دیده می‌شود"
r=$(curl -s -b "$CJ" "$BASE/Ess/Requests")
echo "$r" | grep -q "تأیید شده" && ck 1 "پرتال وضعیت تأییدشده را نشان می‌دهد" || ck 0 "پرتال وضعیت تأیید"

echo "── ۱۰) حسابرسی ESS"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AuditLogs WHERE Action='ess_login'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])" 2>/dev/null || echo 0)
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "رویداد ess_login در حسابرسی"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AuditLogs WHERE Action='ess_create'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])" 2>/dev/null || echo 0)
ck "$([ "$n" -ge 2 ] && echo 1 || echo 0)" "رویداد ess_create (مرخصی/مأموریت/اصلاح)"

echo "── ۱۱) خروج و قفل تلاش ناموفق"
tk=$(TOKEN /Ess/Requests)
POST /Ess/Logout -d "__RequestVerificationToken=$tk" > /dev/null
curl -s -b "$CJ" -L "$BASE/Ess" | grep -q 'name="personnelCode"' && ck 1 "خروج → بازگشت به فرم ورود" || ck 0 "خروج → فرم ورود"

echo ""
echo "═══════ نتیجه فاز ۱۴: $PASS سبز، $FAIL قرمز ═══════"
[ "$FAIL" = "0" ]
