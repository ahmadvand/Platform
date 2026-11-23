#!/bin/bash
# ============================================================
# E2E فاز ۷۴ — RBAC ریز + نقش‌ساز + دامنهٔ رکوردی (Row-Level)
#  ۱) نقش‌ساز: RoleCreate/RoleEdit/RoleDelete + ماتریس + برچسب دامنه
#  ۲) دامنهٔ واحد: /Employees /Attendance /Requests /Payroll /Reports/Custom
#  ۳) اعمال فوری دسترسی بدون ورود مجدد (RbacRefresh + RbacService.Bump)
# ============================================================
B=http://localhost:3000
AJ=/tmp/p74-admin.jar
UJ=/tmp/p74-user.jar
rm -f "$AJ" "$UJ"
DB="python3.13 /home/z/my-project/scripts/dbq.py"
jq_row() { python3 -c "import sys,json;print(json.load(sys.stdin)[0]['$1'])" 2>/dev/null; }
dec() { python3 -c "import sys,json;print(json.loads(sys.stdin.read()).get('message',''))" 2>/dev/null; }

code=$(curl -s -c "$AJ" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { echo "FAIL login admin ($code)"; exit 1; }
echo "PASS login admin"

token() { curl -s -b "$AJ" -c "$AJ" "$B$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }

# ---------- ۰) پاکسازی اجرای قبلی (idempotent) ----------
OLD_ROLE=$($DB --json "SELECT Id FROM Roles WHERE Name='scope_e2e'" 2>/dev/null | jq_row Id)
OLD_USER=$($DB --json "SELECT Id FROM Users WHERE Username='e2e_scope'" 2>/dev/null | jq_row Id)
if [ -n "$OLD_USER" ]; then
  $DB --exec "DELETE FROM UserRoles WHERE UserId=$OLD_USER" >/dev/null 2>&1
  $DB --exec "DELETE FROM PasswordHistories WHERE UserId=$OLD_USER" >/dev/null 2>&1
  $DB --exec "DELETE FROM RefreshTokens WHERE UserId=$OLD_USER" >/dev/null 2>&1
  $DB --exec "DELETE FROM Users WHERE Id=$OLD_USER" >/dev/null 2>&1
fi
if [ -n "$OLD_ROLE" ]; then
  T0=$(token /Users)
  curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T0" --data-urlencode "id=$OLD_ROLE" "$B/Users/RoleDelete" >/dev/null
  $DB --exec "DELETE FROM RoleScopes WHERE RoleId=$OLD_ROLE" >/dev/null 2>&1
  $DB --exec "DELETE FROM RolePermissions WHERE RoleId=$OLD_ROLE" >/dev/null 2>&1
  $DB --exec "DELETE FROM Roles WHERE Id=$OLD_ROLE" >/dev/null 2>&1
fi
[ -z "$OLD_ROLE$OLD_USER" ] || echo "PASS cleanup previous run"

# ---------- ۱) صفحه کاربران + نقش‌ساز ----------
USERS=$(curl -s -b "$AJ" "$B/Users")
echo "$USERS" | grep -q 'data-modal="/Users/RoleForm"' && echo "PASS /Users has role-create button" || echo "FAIL role-create button"
CO=$($DB --json "SELECT Id FROM Companies LIMIT 1" 2>/dev/null | jq_row Id)
IT=$($DB --json "SELECT Id FROM Departments WHERE Code='E2E-IT'" 2>/dev/null | jq_row Id)
FIN=$($DB --json "SELECT Id FROM Departments WHERE Code='E2E-FIN'" 2>/dev/null | jq_row Id)
[ -n "$CO" ] && [ -n "$IT" ] && [ -n "$FIN" ] && echo "PASS demo data present (co/depts)" || { echo "FAIL demo data ($CO/$IT/$FIN)"; exit 1; }

T=$(token /Users)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "name=scope_e2e" \
  --data-urlencode "title=دامنهٔ آزمون E2E" \
  --data-urlencode "description=نقش آزمون فاز ۷۴" \
  --data-urlencode "scopeKind=dept" \
  --data-urlencode "scopeRefs=$IT,$FIN" \
  "$B/Users/RoleCreate")
echo "$R" | grep -q '"ok":true' && echo "PASS RoleCreate ajax (dept scope ×2)" || echo "FAIL RoleCreate → $R"

ROLE=$($DB --json "SELECT Id FROM Roles WHERE Name='scope_e2e'" 2>/dev/null | jq_row Id)
NS=$($DB --json "SELECT COUNT(*) n FROM RoleScopes WHERE RoleId=$ROLE" 2>/dev/null | jq_row n)
[ "$NS" = "2" ] && echo "PASS two RoleScope rows saved" || echo "FAIL scope rows ($NS != 2)"

# صفحه کاربران با نقشِ چند-ردیفی نباید کرش کند (GroupBy fix)
code=$(curl -s -b "$AJ" -o /dev/null -w "%{http_code}" "$B/Users")
[ "$code" = "200" ] && echo "PASS /Users 200 with multi-row scope role" || echo "FAIL /Users crash ($code)"
curl -s -b "$AJ" "$B/Users" | grep -q 'دامنهٔ آزمون E2E' && echo "PASS new role listed" || echo "FAIL role not listed"

FRM=$(curl -s -b "$AJ" "$B/Users/RoleForm/$ROLE")
echo "$FRM" | grep -q 'name="scopeKind"' && echo "$FRM" | grep -q 'name="scopeRefs"' && echo "PASS role form fields" || echo "FAIL role form fields"

# ---------- ۲) دسترسی‌های نقش آزمون + کاربر محدودشده ----------
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "roleId=$ROLE" \
  --data-urlencode "permKeys=dashboard.view,employees.view,attendance.view,reports.view,payroll.view" \
  "$B/Users/SetRolePermissions")
echo "$R" | grep -q '"ok":true' && echo "PASS SetRolePermissions (5 keys)" || echo "FAIL SetRolePermissions → $R"

R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "username=e2e_scope" \
  --data-urlencode "fullName=کاربر دامنه‌دار E2E" \
  --data-urlencode "password=Scope@1234" \
  --data-urlencode "password2=Scope@1234" \
  --data-urlencode "roleIds=$ROLE" \
  --data-urlencode "isActive=true" \
  "$B/Users/Create")
echo "$R" | grep -q '"ok":true' && echo "PASS scoped user created" || echo "FAIL user create → $R"

code=$(curl -s -c "$UJ" -H "Content-Type: application/json" -d '{"username":"e2e_scope","password":"Scope@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && echo "PASS scoped user login" || echo "FAIL scoped login ($code)"

# ---------- ۳) دامنهٔ رکوردی در فهرست‌ها ----------
OUT_EMP=$($DB --json "SELECT FirstName || ' ' || LastName AS n FROM Employees WHERE DepartmentId IS NULL OR DepartmentId NOT IN ('$IT','$FIN') ORDER BY Id LIMIT 1" 2>/dev/null | jq_row n)
IN_EMP="علی رضایی"
[ -n "$OUT_EMP" ] && echo "PASS out-of-scope sample employee: $OUT_EMP" || echo "WARN no out-of-scope employee found"
EMP=$(curl -s -b "$UJ" "$B/Employees")
code=$(curl -s -b "$UJ" -o /dev/null -w "%{http_code}" "$B/Employees")
[ "$code" = "200" ] && echo "PASS /Employees 200" || echo "FAIL /Employees ($code)"
echo "$EMP" | grep -qF "$IN_EMP" && echo "PASS scoped employee visible ($IN_EMP)" || echo "FAIL scoped employee missing"
if [ -n "$OUT_EMP" ]; then echo "$EMP" | grep -qF "$OUT_EMP" && echo "FAIL out-of-scope employee leaked ($OUT_EMP)" || echo "PASS out-of-scope employee hidden"; fi

ATT=$(curl -s -b "$UJ" "$B/Attendance")
code=$(curl -s -b "$UJ" -o /dev/null -w "%{http_code}" "$B/Attendance")
[ "$code" = "200" ] && echo "PASS /Attendance 200" || echo "FAIL /Attendance ($code)"
echo "$ATT" | grep -qF "$IN_EMP" && echo "PASS scoped attendance row visible ($IN_EMP)" || echo "FAIL scoped attendance missing"
if [ -n "$OUT_EMP" ]; then echo "$ATT" | grep -qF "$OUT_EMP" && echo "FAIL attendance leak ($OUT_EMP)" || echo "PASS attendance out-of-scope hidden"; fi

REQ=$(curl -s -b "$UJ" "$B/Requests")
code=$(curl -s -b "$UJ" -o /dev/null -w "%{http_code}" "$B/Requests")
[ "$code" = "200" ] && echo "PASS /Requests 200" || echo "FAIL /Requests ($code)"

PAY=$(curl -s -b "$UJ" "$B/Payroll")
code=$(curl -s -b "$UJ" -o /dev/null -w "%{http_code}" "$B/Payroll")
[ "$code" = "200" ] && echo "PASS /Payroll 200" || echo "FAIL /Payroll ($code)"
echo "$PAY" | grep -q 'لیست حقوق' && echo "PASS payroll page renders" || echo "FAIL payroll content"

REP=$(curl -s -b "$UJ" "$B/Reports/Custom")
code=$(curl -s -b "$UJ" -o /dev/null -w "%{http_code}" "$B/Reports/Custom")
[ "$code" = "200" ] && echo "PASS /Reports/Custom 200" || echo "FAIL /Reports/Custom ($code)"
echo "$REP" | grep -qF "$OUT_EMP" && echo "FAIL report leaked out-of-scope employee" || echo "PASS report rows scoped"

# ---------- ۴) اعمال فوری: سلب و اعطای دسترسی بدون ورود مجدد ----------
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "roleId=$ROLE" \
  --data-urlencode "permKeys=dashboard.view,employees.view,attendance.view,payroll.view" \
  "$B/Users/SetRolePermissions")
echo "$R" | grep -q '"ok":true' && echo "PASS reports.view revoked from role" || echo "FAIL revoke → $R"
code=$(curl -s -b "$UJ" -o /dev/null -w "%{http_code}" "$B/Reports")
[ "$code" = "302" ] && echo "PASS instant revoke: /Reports → 302 without re-login" || echo "FAIL instant revoke ($code)"
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "roleId=$ROLE" \
  --data-urlencode "permKeys=dashboard.view,employees.view,attendance.view,reports.view,payroll.view" \
  "$B/Users/SetRolePermissions")
echo "$R" | grep -q '"ok":true' && echo "PASS reports.view granted back" || echo "FAIL grant → $R"
code=$(curl -s -b "$UJ" -o /dev/null -w "%{http_code}" "$B/Reports")
[ "$code" = "200" ] && echo "PASS instant grant: /Reports → 200 without re-login" || echo "FAIL instant grant ($code)"

# ---------- ۵) منفی: صفحه کاربران برای کاربر محدودشده ----------
code=$(curl -s -b "$UJ" -o /dev/null -w "%{http_code}" "$B/Users")
[ "$code" = "302" ] && echo "PASS /Users denied for scoped user ($code)" || echo "FAIL /Users guard ($code)"

# ---------- ۶) پاکسازی ----------
TUID=$($DB --json "SELECT Id FROM Users WHERE Username='e2e_scope'" 2>/dev/null | jq_row Id)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "id=$TUID" --data-urlencode "roleIds=" \
  "$B/Users/SetRoles")
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "id=$ROLE" "$B/Users/RoleDelete")
echo "$R" | grep -q '"ok":true' && echo "PASS RoleDelete" || echo "FAIL RoleDelete → $R"
$DB --exec "DELETE FROM UserRoles WHERE UserId=$TUID" >/dev/null 2>&1
$DB --exec "DELETE FROM PasswordHistories WHERE UserId=$TUID" >/dev/null 2>&1
$DB --exec "DELETE FROM RefreshTokens WHERE UserId=$TUID" >/dev/null 2>&1
$DB --exec "DELETE FROM Users WHERE Id=$TUID" >/dev/null 2>&1
LEFT=$($DB --json "SELECT COUNT(*) n FROM Roles WHERE Name='scope_e2e'" 2>/dev/null | jq_row n)
[ "$LEFT" = "0" ] && echo "PASS cleanup (role+user removed)" || echo "WARN cleanup left rows"
echo "DONE"
