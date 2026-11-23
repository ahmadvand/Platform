#!/bin/bash
# مکمل e2e فاز ۷۵ (ب) — کارت ریز/والد + تفکیک تأیید مرخصی/مأموریت + کلیدهای کاتالوگ
B=http://localhost:3000
AJ=/tmp/p75b-admin.jar
DB="python3.13 /home/z/my-project/scripts/dbq.py"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "PASS $1"; }
bad() { FAIL=$((FAIL+1)); echo "FAIL $1"; }
jq_row() { python3 -c "import sys,json;print(json.load(sys.stdin)[0]['$1'])" 2>/dev/null; }

code=$(curl -s -c "$AJ" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && ok "login admin" || { echo "FAIL login ($code)"; exit 1; }
token() { curl -s -b "$AJ" -c "$AJ" "$B$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
utoken() { curl -s -b "/tmp/$1.jar" -c "/tmp/$1.jar" "$B$2" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
T=$(token /Users)

# پاکسازی idempotent (تک-statement)
for RN in p75b_card p75b_leaf; do
  RID=$($DB --json "SELECT Id FROM Roles WHERE Name='$RN'" 2>/dev/null | jq_row Id)
  if [ -n "$RID" ]; then
    curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" --data-urlencode "id=$RID" "$B/Users/RoleDelete" >/dev/null
    $DB --exec "DELETE FROM RoleScopes WHERE RoleId=$RID" >/dev/null 2>&1
    $DB --exec "DELETE FROM RolePermissions WHERE RoleId=$RID" >/dev/null 2>&1
    $DB --exec "DELETE FROM Roles WHERE Id=$RID" >/dev/null 2>&1
  fi
done
for UN in e2e75b2 e2e75c2 e2e75d2; do
  UID_=$($DB --json "SELECT Id FROM Users WHERE Username='$UN'" 2>/dev/null | jq_row Id)
  if [ -n "$UID_" ]; then
    $DB --exec "DELETE FROM UserRoles WHERE UserId=$UID_" >/dev/null 2>&1
    $DB --exec "DELETE FROM PasswordHistories WHERE UserId=$UID_" >/dev/null 2>&1
    $DB --exec "DELETE FROM RefreshTokens WHERE UserId=$UID_" >/dev/null 2>&1
    $DB --exec "DELETE FROM Users WHERE Id=$UID_" >/dev/null 2>&1
  fi
done
$DB --exec "DELETE FROM Cards WHERE CardNo LIKE 'E2E75B-%'" >/dev/null 2>&1
echo "PASS cleanup"

mk_role() {
  curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" \
    --data-urlencode "name=$1" --data-urlencode "title=$2" --data-urlencode "description=$3" \
    --data-urlencode "scopeKind=all" "$B/Users/RoleCreate" | grep -q '"ok":true' || { echo "FAIL RoleCreate $1"; exit 1; }
  local RID; RID=$($DB --json "SELECT Id FROM Roles WHERE Name='$1'" 2>/dev/null | jq_row Id)
  curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" \
    --data-urlencode "roleId=$RID" --data-urlencode "permKeys=$4" "$B/Users/SetRolePermissions" | grep -q '"ok":true' || { echo "FAIL perms $1"; exit 1; }
  echo "$RID"
}
mk_user() {
  curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" \
    --data-urlencode "username=$1" --data-urlencode "fullName=$1 تست" --data-urlencode "password=$2" \
    --data-urlencode "password2=$2" --data-urlencode "roleIds=$3" --data-urlencode "isActive=true" \
    "$B/Users/Create" | grep -q '"ok":true' || { echo "FAIL user $1"; exit 1; }
}
login() { curl -s -c "/tmp/$1.jar" -H "Content-Type: application/json" -d "{\"username\":\"$1\",\"password\":\"$2\"}" "$B/api/auth/login" -o /dev/null -w "%{http_code}"; }

# ---- ۱) کارت: نقش ریز issue فقط ----
RA=$(mk_role p75b_card "کارت ریز p75b" "issue only" "dashboard.view,cards.view,cards.issue")
mk_user e2e75b2 Card@1234 "$RA"
[ "$(login e2e75b2 Card@1234)" = "200" ] && ok "e2e75b2 login" || bad "e2e75b2 login"
R=$(curl -s -b /tmp/e2e75b2.jar -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$(utoken e2e75b2 /Cards)" \
  --data-urlencode "cardNo=E2E75B-1" --data-urlencode "type=mifare" "$B/Cards/Create")
echo "$R" | grep -q '"ok":true' && ok "fine cards.issue → Create allowed" || bad "Create → $R"
CID=$($DB --json "SELECT Id FROM Cards WHERE CardNo='E2E75B-1'" 2>/dev/null | jq_row Id)
code=$(curl -s -b /tmp/e2e75b2.jar -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$(utoken e2e75b2 /Cards)" --data-urlencode "id=$CID" \
  -o /dev/null -w "%{http_code}" "$B/Cards/MarkLost")
[ "$code" = "302" ] && ok "fine role MarkLost denied (cards.state ∉) → 302" || bad "MarkLost=$code"
# والد درشت: نقش با cards.manage همان MarkLost را پاس می‌کند (ادمین = superadmin؛ درشت را با نقش دوم می‌سازیم)
RB=$(mk_role p75b_coarse "درشت p75b" "coarse manage" "dashboard.view,cards.view,cards.manage")
mk_user e2e75c2 Coarse@1234 "$RB"
[ "$(login e2e75c2 Coarse@1234)" = "200" ] && ok "e2e75c2 login" || bad "e2e75c2 login"
R=$(curl -s -b /tmp/e2e75c2.jar -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$(utoken e2e75c2 /Cards)" --data-urlencode "id=$CID" "$B/Cards/MarkLost")
echo "$R" | grep -q '"ok":true' && ok "coarse cards.manage → cards.state via parent" || bad "coarse MarkLost → $R"

# ---- ۲) تفکیک تأیید: leaves ∈ / missions ∉ ----
EMP=$($DB --json "SELECT Id FROM Employees WHERE PersonnelCode='E2E-1001'" 2>/dev/null | jq_row Id)
TD=$(date +%Y-%m-%d)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "employeeId=$EMP" --data-urlencode "type=استحقاقی" --data-urlencode "fromDate=$TD" \
  --data-urlencode "toDate=$TD" --data-urlencode "days=1" --data-urlencode "reason=e2e75b" "$B/Requests/CreateLeave")
echo "$R" | grep -q '"ok":true' && ok "admin CreateLeave" || bad "CreateLeave → $R"
LID=$($DB --json "SELECT Id FROM LeaveRequests WHERE EmployeeId='$EMP' AND Reason='e2e75b' ORDER BY CreatedAt DESC LIMIT 1" 2>/dev/null | jq_row Id)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "employeeId=$EMP" --data-urlencode "type=مأموریت اداری" --data-urlencode "destination=کرج" \
  --data-urlencode "fromDate=$TD" --data-urlencode "toDate=$TD" --data-urlencode "purpose=e2e75b" "$B/Requests/CreateMission")
echo "$R" | grep -q '"ok":true' && ok "admin CreateMission" || bad "CreateMission → $R"
MID=$($DB --json "SELECT Id FROM Missions WHERE EmployeeId='$EMP' AND Purpose='e2e75b' ORDER BY CreatedAt DESC LIMIT 1" 2>/dev/null | jq_row Id)
RD=$(mk_role p75b_leaf "مرخصی ریز p75b" "leaves only" "dashboard.view,attendance.view,attendance.leaves.approve")
mk_user e2e75d2 Leaf@1234 "$RD"
[ "$(login e2e75d2 Leaf@1234)" = "200" ] && ok "e2e75d2 login" || bad "e2e75d2 login"
UTD=$(utoken e2e75d2 /Requests)
R=$(curl -s -w "|HTTP=%{http_code}" -b /tmp/e2e75d2.jar -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$UTD" --data-urlencode "id=$LID" \
  --data-urlencode "approve=true" "$B/Requests/DecideLeave")
echo "$R" | grep -q '"ok":true' && ok "leaves.approve → DecideLeave allowed" || bad "DecideLeave → $R (UTlen=${#UTD}, LID=$LID, MID=$MID)"
code=$(curl -s -b /tmp/e2e75d2.jar -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$(utoken e2e75d2 /Requests)" --data-urlencode "id=$MID" \
  --data-urlencode "approve=true" -o /dev/null -w "%{http_code}" "$B/Requests/DecideMission")
[ "$code" = "302" ] && ok "missions.approve ∉ → DecideMission 302" || bad "DecideMission=$code"

# ---- ۳) سیم‌کشی‌های ریز دیگر: صفحات سالم ----
for P in /Shifts /Holidays /Org /Devices /Biometrics /Access /Visitors /Payroll/Settings /Payroll/Items /Payroll/Params; do
  code=$(curl -s -b "$AJ" -o /dev/null -w "%{http_code}" "$B$P")
  [ "$code" = "200" ] && ok "page $P 200" || bad "page $P ($code)"
done

# ---- پاکسازی ----
for RN in p75b_card p75b_coarse p75b_leaf; do
  RID=$($DB --json "SELECT Id FROM Roles WHERE Name='$RN'" 2>/dev/null | jq_row Id)
  if [ -n "$RID" ]; then
    curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$(token /Users)" --data-urlencode "id=$RID" "$B/Users/RoleDelete" >/dev/null
    $DB --exec "DELETE FROM RoleScopes WHERE RoleId=$RID" >/dev/null 2>&1
    $DB --exec "DELETE FROM RolePermissions WHERE RoleId=$RID" >/dev/null 2>&1
    $DB --exec "DELETE FROM Roles WHERE Id=$RID" >/dev/null 2>&1
  fi
done
for UN in e2e75b2 e2e75c2 e2e75d2; do
  UID_=$($DB --json "SELECT Id FROM Users WHERE Username='$UN'" 2>/dev/null | jq_row Id)
  if [ -n "$UID_" ]; then
    $DB --exec "DELETE FROM UserRoles WHERE UserId=$UID_" >/dev/null 2>&1
    $DB --exec "DELETE FROM PasswordHistories WHERE UserId=$UID_" >/dev/null 2>&1
    $DB --exec "DELETE FROM RefreshTokens WHERE UserId=$UID_" >/dev/null 2>&1
    $DB --exec "DELETE FROM Users WHERE Id=$UID_" >/dev/null 2>&1
  fi
done
$DB --exec "DELETE FROM LeaveRequests WHERE Reason='e2e75b'" >/dev/null 2>&1
$DB --exec "DELETE FROM Missions WHERE Purpose='e2e75b'" >/dev/null 2>&1
$DB --exec "DELETE FROM Cards WHERE CardNo LIKE 'E2E75B-%'" >/dev/null 2>&1
echo "PASS cleanup"
echo "=============================="
echo "P75B RESULT: PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ] && echo "ALL GREEN ✅" || echo "SOME FAILED ❌"
exit "$FAIL"
