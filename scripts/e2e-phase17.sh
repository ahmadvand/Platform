#!/usr/bin/env bash
# ============================================================
# فاز ۱۷ — کنترل دسترسی پیشرفته
# آنتی‌پس‌بک، برنامه زمانی گیت، گروه دسترسی، شمارش اضطراری (muster)
# اجرا: bash scripts/e2e-phase17.sh
# ============================================================
set -u
cd "$(dirname "$0")/.."
BASE="http://localhost:3000"
PASS=0; FAIL=0
ck() { if [ "$1" = "1" ]; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }

CJ=/tmp/p17.txt; rm -f "$CJ"
GET()  { curl -s -b "$CJ" -c "$CJ" "$BASE$1" --max-time 20; }
POST() { local url="$1"; shift; curl -s -b "$CJ" -c "$CJ" -X POST "$BASE$url" "$@" --max-time 20 -o /dev/null; }
TOKEN() { GET "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
curl -s -c "$CJ" -X POST "$BASE/api/auth/login" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' > /dev/null

echo "── داده پایه"
ZONES=$(python3 scripts/dbq.py --json "SELECT Id, Name, Code FROM Zones ORDER BY Code" 2>/dev/null)
Z1=$(echo "$ZONES" | python3 -c "import sys,json;d=json.load(sys.stdin);print(d[0]['Id'])")
GATES=$(python3 scripts/dbq.py --json "SELECT Id, Name, Code, ZoneId FROM Gates ORDER BY Code" 2>/dev/null)
G1=$(echo "$GATES" | python3 -c "import sys,json;d=json.load(sys.stdin);print([g for g in d if g['ZoneId']=='$Z1'][0]['Id'])")
# پرسنلی که مجوز فردی به Z1 ندارد
E2=$(python3 scripts/dbq.py --json "SELECT Id, PersonnelCode FROM Employees WHERE Status='فعال' AND Id NOT IN (SELECT EmployeeId FROM AccessPermissions WHERE ZoneId='$Z1') ORDER BY PersonnelCode LIMIT 1" 2>/dev/null | python3 -c "import sys,json;d=json.load(sys.stdin);print(d[0]['Id'] if d else '')")
E1C=$(python3 scripts/dbq.py --json "SELECT PersonnelCode FROM Employees WHERE Id='$E2'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['PersonnelCode'])")
echo "منطقه: $Z1 | گیت: $G1 | پرسنل بدون مجوز: $E1C"

echo "── ۱) گروه دسترسی"
code=$(curl -s -b "$CJ" -o /dev/null -w "%{http_code}" "$BASE/Access?tab=groups")
ck "$([ "$code" = "200" ] && echo 1 || echo 0)" "GET /Access?tab=groups → 200"
tk=$(TOKEN "/Access?tab=groups")
POST /Access/CreateGroup -d "__RequestVerificationToken=$tk&name=نگهبان‌های شیفت A&note=e2e-p17"
GID=$(python3 scripts/dbq.py --json "SELECT Id FROM AccessGroups WHERE Name='نگهبان‌های شیفت A' LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
ck "$([ -n "$GID" ] && echo 1 || echo 0)" "گروه «نگهبان‌های شیفت A» در دیتابیس"
tk=$(TOKEN "/Access?tab=groups")
POST /Access/AddGroupMember -d "__RequestVerificationToken=$tk&groupId=$GID&employeeId=$E2"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AccessGroupMembers WHERE GroupId='$GID' AND EmployeeId='$E2'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "1" ] && echo 1 || echo 0)" "عضویت پرسنل در گروه"
tk=$(TOKEN "/Access?tab=groups")
POST /Access/SetGroupGate -d "__RequestVerificationToken=$tk&groupId=$GID&gateId=$G1&days=&timeFrom=&timeUntil="
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AccessGroupGates WHERE GroupId='$GID' AND GateId='$G1'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "1" ] && echo 1 || echo 0)" "گیت مجاز گروه (بدون محدودیت زمانی)"

echo "── ۲) اسکن قبل/بعد از گروه"
tk=$(TOKEN "/Access?tab=zones")
POST /Access/Scan --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "personnelCode=$E1C" --data-urlencode "gateId=$G1" --data-urlencode "direction=ورود" --data-urlencode "method=اثر انگشت"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AccessEvents WHERE EmployeeId='$E2' AND GateId='$G1' AND Result='مجاز' AND Direction='ورود'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "اسکن ورود با مجوز گروهی → مجاز"

echo "── ۳) پنجره زمانی گروه خارج از ساعت فعلی"
tk=$(TOKEN "/Access?tab=groups")
POST /Access/SetGroupGate -d "__RequestVerificationToken=$tk&groupId=$GID&gateId=$G1&days=&timeFrom=03%3A00&timeUntil=04%3A00"
tk=$(TOKEN "/Access?tab=zones")
POST /Access/Scan --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "personnelCode=$E1C" --data-urlencode "gateId=$G1" --data-urlencode "direction=ورود" --data-urlencode "method=اثر انگشت"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AccessEvents WHERE EmployeeId='$E2' AND GateId='$G1' AND Result='غیرمجاز' AND (Reason LIKE '%بازه زمانی%' OR Reason LIKE '%پنجره زمانی%')" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "اسکن خارج از پنجره گروه → رد («خارج از بازه زمانی»)"
# بازگرداندن پنجره آزاد برای تست‌های بعدی
tk=$(TOKEN "/Access?tab=groups")
POST /Access/SetGroupGate -d "__RequestVerificationToken=$tk&groupId=$GID&gateId=$G1&days=&timeFrom=&timeUntil="

echo "── ۴) آنتی‌پس‌بک"
apb=$(python3 scripts/dbq.py --json "SELECT AntiPassback AS v FROM Zones WHERE Id='$Z1'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['v'])")
if [ "$apb" != "True" ] && [ "$apb" != "1" ]; then
  tk=$(TOKEN "/Access?tab=zones")
  POST /Access/ToggleAntiPassback -d "__RequestVerificationToken=$tk&id=$Z1"
fi
n=$(python3 scripts/dbq.py --json "SELECT AntiPassback AS n FROM Zones WHERE Id='$Z1'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "True" ] || [ "$n" = "1" ] && echo 1 || echo 0)" "آنت‌ی‌پس‌بک منطقه فعال شد"
tk=$(TOKEN "/Access?tab=zones")
# آخرین وضعیت پرسنل در این منطقه: اگر داخل است اول خروج بدهیم
POST /Access/Scan --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "personnelCode=$E1C" --data-urlencode "gateId=$G1" --data-urlencode "direction=خروج" --data-urlencode "method=اثر انگشت"
POST /Access/Scan --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "personnelCode=$E1C" --data-urlencode "gateId=$G1" --data-urlencode "direction=ورود" --data-urlencode "method=اثر انگشت"
APB_BEFORE=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AccessEvents WHERE EmployeeId='$E2' AND GateId='$G1' AND Result='غیرمجاز' AND Reason LIKE '%آنت‌ی‌پس‌بک%'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
POST /Access/Scan --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "personnelCode=$E1C" --data-urlencode "gateId=$G1" --data-urlencode "direction=ورود" --data-urlencode "method=اثر انگشت"
APB_AFTER=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AccessEvents WHERE EmployeeId='$E2' AND GateId='$G1' AND Result='غیرمجاز' AND Reason LIKE '%آنت‌ی‌پس‌بک%'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$APB_AFTER" -gt "$APB_BEFORE" ] && echo 1 || echo 0)" "ورود دوباره بدون خروج → رد آنتی‌پس‌بک (دلتا $((APB_AFTER-APB_BEFORE)))"

echo "── ۵) برنامه زمانی گیت (بازه بسته)"
tk=$(TOKEN "/Access?tab=zones")
POST /Access/SetGateSchedule -d "__RequestVerificationToken=$tk&id=$G1&days=&from=03%3A00&to=04%3A00"
POST /Access/Scan --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "personnelCode=$E1C" --data-urlencode "gateId=$G1" --data-urlencode "direction=خروج" --data-urlencode "method=اثر انگشت"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AccessEvents WHERE GateId='$G1' AND Result='غیرمجاز' AND Reason='گیت خارج از بازه زمانی مجاز است'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "اسکن خارج از بازه گیت → رد («گیت خارج از بازه زمانی»)"
# پاک کردن برنامه گیت
tk=$(TOKEN "/Access?tab=zones")
POST /Access/SetGateSchedule -d "__RequestVerificationToken=$tk&id=$G1&days=&from=&to="

echo "── ۶) شمارش اضطراری (muster)"
r=$(GET /Access/Muster)
echo "$r" | grep -q "شمارش اضطراری" && ck 1 "صفحه muster" || ck 0 "صفحه muster"
echo "$r" | grep -q "نفر داخل" && ck 1 "شمارش مناطق" || ck 0 "شمارش مناطق"

echo "── ۷) حسابرسی"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AuditLogs WHERE Action='update' AND Entity IN ('zone','gate','access_group_gate')" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" -ge 3 ] && echo 1 || echo 0)" "رویدادهای حسابرسی APB/برنامه/گروه ($n)"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM SecurityAlerts WHERE Message LIKE '%آنت‌ی‌پس‌بک%'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "هشدار امنیتی آنتی‌پس‌بک"

echo "── ۸) پاک‌سازی"
tk=$(TOKEN "/Access?tab=groups")
POST /Access/DeleteGroup -d "__RequestVerificationToken=$tk&id=$GID"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AccessGroups WHERE Id='$GID'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "0" ] && echo 1 || echo 0)" "حذف گروه (اعضای و گیت‌های گروه هم پاک شد)"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AccessGroupMembers WHERE GroupId='$GID'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "0" ] && echo 1 || echo 0)" "بدون عضو یتیم"

echo ""
echo "═══════ نتیجه فاز ۱۷: $PASS سبز، $FAIL قرمز ═══════"
[ "$FAIL" = "0" ]
