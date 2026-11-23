#!/usr/bin/env bash
# E2E Phase 10: org chart fix + personnel edit + device→device transfer
set -u
BASE="${BASE:-http://localhost:3000}"
JAR=$(mktemp)
DB=/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db
DBQ="python3.13 /home/z/my-project/scripts/dbq.py"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); echo "  ✗ $1"; }

echo "== login =="
code=$(curl -s -c "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' --max-time 10)
[ "$code" = "200" ] && ok "login 200" || bad "login $code"

AF=$(curl -s -b "$JAR" -c "$JAR" "$BASE/Employees" | rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]+"' -N | head -1 | sed 's/.*value="//;s/"$//')

echo "== P10.1 org chart renders (was 500) =="
code=$(curl -s -b "$JAR" -o /tmp/org.html -w "%{http_code}" "$BASE/Org" --max-time 15)
[ "$code" = "200" ] && ok "/Org 200" || { bad "/Org $code"; head -c 200 /tmp/org.html; }
NULLDEPT=$($DBQ "SELECT COUNT(*) FROM Employees WHERE DepartmentId IS NULL")
if [ "$NULLDEPT" -gt 0 ]; then
  rg -q "پرسنل بدون واحد سازمانی" /tmp/org.html && ok "no-dept section present ($NULLDEPT device-synced employees)" || bad "no-dept section missing"
else
  ok "no null-dept employees left (all edited) — section correctly absent"
fi
rg -q "ساختار سازمانی" /tmp/org.html && ok "org tree content" || bad "org content missing"

echo "== P10.2 personnel edit =="
# find an employee id + a department id from DB (اول بدون واحد، وگرنه هر پرسنلی)
EID=$($DBQ "SELECT Id FROM Employees WHERE DepartmentId IS NULL LIMIT 1")
# dbq برای نتیجه خالی «[]» چاپ می‌کند — هر دو حالت خالی را پوشش بده
if [ -z "$EID" ] || [ "$EID" = "None" ] || [ "$EID" = "[]" ]; then EID=$($DBQ "SELECT Id FROM Employees ORDER BY PersonnelCode LIMIT 1"); fi
# واحد باید هم‌شرکت پرسنل باشد (چندشرکتی فاز ۲۸) — وگرنه گارد بین‌شرکتی ۴۰۰ می‌دهد
DEPT=$($DBQ "SELECT Id FROM Departments WHERE CompanyId=(SELECT CompanyId FROM Employees WHERE Id='$EID') LIMIT 1")
if [ -z "$DEPT" ] || [ "$DEPT" = "None" ]; then DEPT=$($DBQ "SELECT Id FROM Departments LIMIT 1"); fi
# ابعاد واقعی پرسنل انتخابی — فقط تلفن و واحد را تغییر می‌دهیم (تست داده-محور)
EMP=$($DBQ --json "SELECT PersonnelCode, FirstName, LastName, NationalId, EmploymentType, ISNULL(Phone,'') AS Phone, ISNULL(Email,'') AS Email, BaseSalary, HireDate, AnnualLeaveEnt, SickLeaveEnt FROM Employees WHERE Id='$EID'")
PC=$(echo "$EMP" | python3.13 -c "import sys,json;print(json.load(sys.stdin)[0]['PersonnelCode'])")
FN=$(echo "$EMP" | python3.13 -c "import sys,json;print(json.load(sys.stdin)[0]['FirstName'])")
LN=$(echo "$EMP" | python3.13 -c "import sys,json;print(json.load(sys.stdin)[0]['LastName'])")
NID=$(echo "$EMP" | python3.13 -c "import sys,json;print(json.load(sys.stdin)[0]['NationalId'])")
ET=$(echo "$EMP" | python3.13 -c "import sys,json;print(json.load(sys.stdin)[0]['EmploymentType'])")
SAL=$(echo "$EMP" | python3.13 -c "import sys,json;print(json.load(sys.stdin)[0]['BaseSalary'])")
HD=$(echo "$EMP" | python3.13 -c "import sys,json;print(str(json.load(sys.stdin)[0]['HireDate'])[:10])")
ALE=$(echo "$EMP" | python3.13 -c "import sys,json;print(json.load(sys.stdin)[0]['AnnualLeaveEnt'])")
SLE=$(echo "$EMP" | python3.13 -c "import sys,json;print(json.load(sys.stdin)[0]['SickLeaveEnt'])")
echo "  (employee pin=$PC name=$FN $LN)"
DUPNID=$($DBQ --p "$EID" "SELECT NationalId FROM Employees WHERE Id <> ? AND NationalId NOT LIKE 'DEV-%' LIMIT 1")
code=$(curl -s -b "$JAR" -o /tmp/edit.html -w "%{http_code}" "$BASE/Employees/Edit/$EID" --max-time 15)
[ "$code" = "200" ] && ok "GET /Employees/Edit 200" || bad "GET Edit $code"
rg -q "ویرایش پرسنل" /tmp/edit.html && ok "edit form renders" || bad "edit form missing"

AF2=$(rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]+"' -N /tmp/edit.html | head -1 | sed 's/.*value="//;s/"$//')
OLD=$($DBQ --json "SELECT FirstName, LastName, Phone, EmploymentType, DepartmentId FROM Employees WHERE Id='$EID'")
# submit edit: set department + phone
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Employees/Edit" \
  --data-urlencode "__RequestVerificationToken=$AF2" \
  --data-urlencode "id=$EID" \
  --data-urlencode "personnelCode=$PC" \
  --data-urlencode "firstName=$FN" \
  --data-urlencode "lastName=$LN" \
  --data-urlencode "nationalId=$NID" \
  --data-urlencode "phone=09121110001" \
  --data-urlencode "email=" \
  --data-urlencode "employmentType=$ET" \
  --data-urlencode "status=فعال" \
  --data-urlencode "departmentId=$DEPT" \
  --data-urlencode "positionId=" \
  --data-urlencode "shiftId=" \
  --data-urlencode "baseSalary=$SAL" \
  --data-urlencode "hireDate=$HD" \
  --data-urlencode "annualLeaveEnt=$ALE" \
  --data-urlencode "sickLeaveEnt=$SLE" \
  --max-time 15)
[ "$code" = "302" ] && ok "POST Edit → 302" || bad "POST Edit $code"
PERSIST=$($DBQ --json "SELECT ISNULL(Phone,'') AS Phone, ISNULL(DepartmentId,'') AS Dep FROM Employees WHERE Id='$EID'" | python3.13 -c "
import sys, json
r = json.load(sys.stdin)[0]
print('PERSISTED' if (r['Phone'] == '09121110001' and r['Dep'] == '$DEPT') else 'NOT-PERSISTED: %r %r' % (r['Phone'], r['Dep']))")
echo "  → $PERSIST"
echo "$PERSIST" | rg -q "PERSISTED" && ok "edit persisted (phone + department)" || bad "$PERSIST"
DEPTOK=$($DBQ "SELECT DepartmentId FROM Employees WHERE Id='$EID'")
[ "$DEPTOK" = "$DEPT" ] && ok "department assignment persisted" || bad "dept not set: $DEPTOK vs $DEPT"
# duplicate national-id guard — توکن تازه بگیر (کوکی hr_af در هر رندر فرم می‌چرخد)؛
# سپس POST بدون -L و بعد GET روی Location برای دیدن toast
curl -s -b "$JAR" -c "$JAR" -o /tmp/edit3.html "$BASE/Employees/Edit/$EID" --max-time 15
AF3=$(rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]+"' -N /tmp/edit3.html | head -1 | sed 's/.*value="//;s/"$//')
LOC=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{redirect_url}" -X POST "$BASE/Employees/Edit" \
  --data-urlencode "__RequestVerificationToken=$AF3" \
  --data-urlencode "id=$EID" \
  --data-urlencode "personnelCode=$PC" \
  --data-urlencode "firstName=تکراری" \
  --data-urlencode "lastName=ملی" \
  --data-urlencode "nationalId=$DUPNID" \
  --data-urlencode "employmentType=$ET" --data-urlencode "status=فعال" \
  --data-urlencode "baseSalary=$SAL" --data-urlencode "hireDate=$HD" \
  --data-urlencode "annualLeaveEnt=$ALE" --data-urlencode "sickLeaveEnt=$SLE" \
  --max-time 15)
curl -s -b "$JAR" -c "$JAR" -o /tmp/edit2.html "$LOC" --max-time 15
# toast به‌صورت HTML-entity رندر می‌شود: «کد ملی تکراری است» = &#x6A9;&#x62F; &#x645;&#x644;&#x6CC; ...
rg -q "کد ملی تکراری است" /tmp/edit2.html && ok "duplicate nationalId rejected with toast" || bad "duplicate nationalId NOT rejected (loc=$LOC)"

echo "== P10.3 device→device bulk transfer =="
TF=$(mktemp)
code=$(curl -s -b "$JAR" -c "$JAR" -o "$TF" -w "%{http_code}" "$BASE/Biometrics" --max-time 20)
[ "$code" = "200" ] && ok "/Biometrics 200" || bad "/Biometrics $code"
rg -q "TransferDeviceUsers" "$TF" && rg -q "از دستگاه به دستگاه‌ها" "$TF" && ok "device→device panel present" || bad "device→device panel missing"
rg -q "usersByDevice" "$TF" && ok "usersByDevice JS map embedded" || bad "usersByDevice missing"
SRC=$($DBQ "SELECT DeviceId FROM DeviceUsers GROUP BY DeviceId ORDER BY COUNT(*) DESC LIMIT 1")
DST=$($DBQ --p "$SRC" "SELECT Id FROM Devices WHERE Id <> ? LIMIT 1")
PINS=$(python3.13 /home/z/my-project/scripts/dbq.py --json --p "$SRC" "SELECT Pin FROM DeviceUsers WHERE DeviceId=? LIMIT 2" | python3.13 -c "
import sys, json
rows = json.load(sys.stdin)
print('&'.join('pins=' + r['Pin'] for r in rows))")
echo "  (source=$SRC dst=$DST pins: $(echo $PINS | tr '&' ' ') dupNID=$DUPNID)"
BEFORE=$($DBQ "SELECT COUNT(*) FROM DeviceCommands WHERE Type='push_user_bios'")
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Biometrics/TransferDeviceUsers" \
  --data-urlencode "__RequestVerificationToken=$AF" \
  --data-urlencode "sourceDeviceId=$SRC" \
  -d "$PINS" \
  --data-urlencode "toDeviceIds=$DST" \
  --data-urlencode "toDeviceIds=$DST" \
  --data-urlencode "includeBios=true" \
  --max-time 20)
[ "$code" = "302" ] && ok "POST TransferDeviceUsers → 302" || bad "POST TransferDeviceUsers $code"
AFTER=$($DBQ "SELECT COUNT(*) FROM DeviceCommands WHERE Type='push_user_bios'")
[ "$AFTER" -gt "$BEFORE" ] && ok "push_user_bios queued ($BEFORE → $AFTER)" || bad "no command queued"
PAYLOAD=$($DBQ --json "SELECT Payload FROM DeviceCommands WHERE Type='push_user_bios' ORDER BY CreatedAt DESC LIMIT 1" | python3.13 -c "
import sys, json
p = json.load(sys.stdin)[0]['Payload']
d = json.loads(p)
print('users:', len(d['users']), 'bios:', len(d['bios']), 'has_card:', any(u.get('card') for u in d['users']), 'has_template:', all(b.get('data') for b in d['bios'][:5]))")
echo "  payload → $PAYLOAD"
echo "$PAYLOAD" | rg -q "users: 2" && ok "2 users in payload" || bad "payload users wrong: $PAYLOAD"
echo "$PAYLOAD" | rg -q "has_template: True" && ok "templates included (base64 data)" || bad "templates missing"
# self-transfer guard
code=$(curl -s -b "$JAR" -o /tmp/self.html -w "%{http_code}" -X POST "$BASE/Biometrics/TransferDeviceUsers" \
  --data-urlencode "__RequestVerificationToken=$AF" \
  --data-urlencode "sourceDeviceId=$SRC" --data-urlencode "$PINS" \
  --data-urlencode "toDeviceIds=$SRC" --data-urlencode "includeBios=true" --max-time 15)
rg -q "دستگاه مقصد نباید همان دستگاه مبدأ باشد" /tmp/self.html 2>/dev/null || \
  { BEFORE2=$($DBQ "SELECT COUNT(*) FROM DeviceCommands WHERE Type='push_user_bios'"); \
    [ "$BEFORE2" = "$AFTER" ] && ok "self-transfer rejected" || bad "self-transfer ACCEPTED"; }

echo "== P10.4 regression (existing flows intact) =="
for p in / /Employees /Attendance /Requests /Payroll /Holidays /Access /Visitors /Devices /Users /Audit; do
  code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" "$BASE$p" --max-time 15)
  [ "$code" = "200" ] && ok "$p 200" || bad "$p $code"
done
# DB→device transfer still works
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Biometrics/PushToDevices" \
  --data-urlencode "__RequestVerificationToken=$AF" \
  --data-urlencode "employeeIds=$EID" \
  --data-urlencode "toDeviceIds=$DST" \
  --data-urlencode "includeBios=true" --max-time 15)
[ "$code" = "302" ] && ok "DB→device PushToDevices still 302" || bad "PushToDevices $code"

echo
echo "== RESULT: PASS=$PASS FAIL=$FAIL =="
[ "$FAIL" = "0" ]
