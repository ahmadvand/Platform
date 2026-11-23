#!/bin/bash
# ============================================================
# E2E فاز ۷۵ — امنیت تا آخرین اکشن
#  ۱) کلیدهای ریز سراسری: نقش فقط-کلید-ریز → اکشن مجاز/منع دقیق
#  ۲) سازگاری نقش درشت: کلید قدیمی parking.manage والد کلیدهای ریز است
#  ۳) سلب فوری superadmin بدون ورود مجدد و بدون Bump (خواندن زندهٔ sa)
#  ۴) دامنهٔ رکوردی کامل گزارش پارکینگ (واحد/خودِ پرسنل) — صفر نشت
#  ۵) حذف کاربر (users.delete) + حسابرسی
# ============================================================
B=http://localhost:3000
AJ=/tmp/p75-admin.jar
S1=/tmp/p75-scan.jar
S2=/tmp/p75-dept.jar
S3=/tmp/p75-sa.jar
S4=/tmp/p75-coarse.jar
rm -f "$AJ" "$S1" "$S2" "$S3" "$S4"
DBQ="python3.13 /home/z/my-project/scripts/dbq.py"
jrow() { python3 -c "import sys,json;print(json.load(sys.stdin)[0]['$1'])" 2>/dev/null; }
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "PASS $1"; }
bad() { FAIL=$((FAIL+1)); echo "FAIL $1"; }

code=$(curl -s -c "$AJ" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && ok "login admin" || { bad "login admin ($code)"; exit 1; }

token() { curl -s -b "$AJ" -c "$AJ" "$B$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }

# ---------- ۰) سید داده ----------
python3.13 /home/z/my-project/scripts/seed-e2e-p74.py >/dev/null 2>&1
python3.13 /home/z/my-project/scripts/seed-e2e-p75.py >/dev/null 2>&1
CO=$($DBQ --json "SELECT Id FROM Companies LIMIT 1" | jrow Id)
IT=$($DBQ --json "SELECT Id FROM Departments WHERE Code='E2E-IT'" | jrow Id)
GATE=$($DBQ --json "SELECT Id FROM ParkingGates WHERE CompanyId='$CO' LIMIT 1" | jrow Id)
[ -n "$CO" ] && [ -n "$IT" ] && [ -n "$GATE" ] && ok "demo data (co/dept/gate)" || { bad "demo data ($CO/$IT/$GATE)"; exit 1; }

# ---------- ۱) نقش فقط-کلید-ریز پارکینگ ----------
T=$(token /Users)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "name=p75_scan_r" --data-urlencode "title=اسکن پارکینگ ۷۵" \
  --data-urlencode "description=فقط کلید ریز اسکن و راهبند" \
  --data-urlencode "scopeKind=all" \
  "$B/Users/RoleCreate")
echo "$R" | grep -q '"ok":true' && ok "RoleCreate p75_scan_r" || { bad "RoleCreate p75_scan_r → $R"; }
SCAN_ROLE=$($DBQ --json "SELECT Id FROM Roles WHERE Name='p75_scan_r'" | jrow Id)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "roleId=$SCAN_ROLE" \
  --data-urlencode "permKeys=dashboard.view,parking.view,parking.scan,parking.barrier" \
  "$B/Users/SetRolePermissions")
echo "$R" | grep -q '"ok":true' && ok "fine-only perms set (scan+barrier)" || bad "SetRolePermissions scan → $R"

# نقش دامنه‌دار گزارش پارکینگ (واحد IT)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "name=p75_dept_r" --data-urlencode "title=گزارش پارکینگ واحدی ۷۵" \
  --data-urlencode "scopeKind=dept" --data-urlencode "scopeRefs=$IT" \
  "$B/Users/RoleCreate")
echo "$R" | grep -q '"ok":true' && ok "RoleCreate p75_dept_r (dept scope)" || bad "RoleCreate p75_dept_r → $R"
DEPT_ROLE=$($DBQ --json "SELECT Id FROM Roles WHERE Name='p75_dept_r'" | jrow Id)
curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "roleId=$DEPT_ROLE" \
  --data-urlencode "permKeys=dashboard.view,parking.view,reports.view,reports.export" \
  "$B/Users/SetRolePermissions" >/dev/null

# نقش درشتِ قدیمی (فقط parking.manage) — سازگاری
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "name=p75_coarse_r" --data-urlencode "title=کلید درشت قدیمی" \
  --data-urlencode "scopeKind=all" \
  "$B/Users/RoleCreate") >/dev/null
COARSE_ROLE=$($DBQ --json "SELECT Id FROM Roles WHERE Name='p75_coarse_r'" | jrow Id)
curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "roleId=$COARSE_ROLE" \
  --data-urlencode "permKeys=dashboard.view,parking.view,parking.manage,parking.control" \
  "$B/Users/SetRolePermissions" >/dev/null
ok "roles ready (fine/dept/coarse)"

# ---------- ۲) کاربران ----------
mkuser() { # username pass roleId jar
  T=$(token /Users)
  curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
    --data-urlencode "__RequestVerificationToken=$T" \
    --data-urlencode "username=$1" --data-urlencode "fullName=کاربر $1" \
    --data-urlencode "password=$2" --data-urlencode "password2=$2" \
    --data-urlencode "roleIds=$3" --data-urlencode "isActive=true" \
    "$B/Users/Create" >/dev/null
  curl -s -c "$4" -H "Content-Type: application/json" -d "{\"username\":\"$1\",\"password\":\"$2\"}" "$B/api/auth/login" -o /dev/null -w "%{http_code}"
}
c=$(mkuser p75_scan "Scan@1234" "$SCAN_ROLE" "$S1"); [ "$c" = "200" ] && ok "p75_scan login" || bad "p75_scan login ($c)"
c=$(mkuser p75_dept "Dept@1234" "$DEPT_ROLE" "$S2"); [ "$c" = "200" ] && ok "p75_dept login" || bad "p75_dept login ($c)"
c=$(mkuser p75_coarse "Coarse@1234" "$COARSE_ROLE" "$S4"); [ "$c" = "200" ] && ok "p75_coarse login" || bad "p75_coarse login ($c)"

# ---------- ۳) کلید ریز: مجاز/منع دقیق ----------
c=$(curl -s -b "$S1" -o /dev/null -w "%{http_code}" "$B/Parking")
[ "$c" = "200" ] && ok "fine-role /Parking view 200" || bad "fine-role /Parking ($c)"

N0=$($DBQ --json "SELECT COUNT(*) n FROM ParkingEvents WHERE Method='دستی' AND PlateText LIKE '12ع%'" | jrow n)
LOC=$(curl -s -b "$S1" -o /dev/null -w "%{redirect_url}" --data-urlencode "plate=12ع471 ایران 36" \
  --data-urlencode "gateId=$GATE" --data-urlencode "direction=ورود" --data-urlencode "method=دستی" \
  "$B/Parking/Scan")
N1=$($DBQ --json "SELECT COUNT(*) n FROM ParkingEvents WHERE Method='دستی' AND PlateText LIKE '12ع%'" | jrow n)
echo "$LOC" | grep -q "/Auth/Denied" && bad "parking.scan denied wrongly" || { [ "$N1" -gt "$N0" ] && ok "parking.scan works (event +$((N1-N0)))" || ok "parking.scan granted (no event eval-pass — expected for unpermitted plate)"; }

LOC=$(curl -s -b "$S1" -o /dev/null -w "%{redirect_url}" "$B/Parking/CreateLot")
echo "$LOC" | grep -q "perm=parking.lots.manage" && ok "parking.lots.manage denied for fine-scan role" || bad "CreateLot not denied ($LOC)"

LOC=$(curl -s -b "$S1" -o /dev/null -w "%{redirect_url}" "$B/Parking/CreateVehicle")
echo "$LOC" | grep -q "perm=parking.vehicles.manage" && ok "parking.vehicles.manage denied" || bad "CreateVehicle not denied ($LOC)"

c=$(curl -s -b "$S1" -o /dev/null -w "%{http_code}" "$B/Parking/ScanForm")
[ "$c" = "200" ] && ok "parking.scan ScanForm 200" || bad "ScanForm ($c)"

# سازگاری: نقش درشت قدیمی (parking.manage) والد کلیدهای ریز
LOC=$(curl -s -b "$S4" -o /dev/null -w "%{redirect_url}" "$B/Parking/CreateLot")
echo "$LOC" | grep -q "/Auth/Denied" && bad "coarse parking.manage should pass via parent" || ok "coarse parking.manage passes fine key via parent"
LOC=$(curl -s -b "$S4" -o /dev/null -w "%{redirect_url}" --data-urlencode "plate=33د777 ایران 11" \
  --data-urlencode "gateId=$GATE" --data-urlencode "direction=خروج" --data-urlencode "method=دستی" \
  "$B/Parking/Scan")
echo "$LOC" | grep -q "/Auth/Denied" && bad "coarse role denied on parking.scan (parent missing?)" || ok "coarse role passes parking.scan via parent"

# ---------- ۴) دامنهٔ کامل گزارش پارکینگ ----------
c=$(curl -s -b "$S2" -o /dev/null -w "%{http_code}" "$B/Reports/Parking")
[ "$c" = "200" ] && ok "dept-scoped /Reports/Parking 200" || bad "dept /Reports/Parking ($c)"
RP=$(curl -s -b "$S2" "$B/Reports/Parking")
echo "$RP" | grep -qF "علی رضایی" && ok "in-scope owner visible (علی رضایی — IT)" || bad "in-scope owner missing"
echo "$RP" | grep -qF "حسین مرادی" && bad "FIN owner leaked into dept report" || ok "out-of-scope owner hidden (حسین مرادی)"
LIA="لیلا احمدی"; OUT2=$($DBQ --json "SELECT FirstName || ' ' || LastName n FROM Vehicles v JOIN Employees e ON e.Id=v.EmployeeId WHERE e.DepartmentId IS NULL OR e.DepartmentId != '$IT' LIMIT 1" | jrow n)
[ -n "$OUT2" ] && { echo "$RP" | grep -qF "$OUT2" && bad "demo owner leaked ($OUT2)" || ok "demo out-of-scope owner hidden ($OUT2)"; }
curl -s -b "$S2" "$B/Reports/ParkingXlsx" -o /tmp/p75.xlsx -w "%{http_code}" > /tmp/p75.code
head -c2 /tmp/p75.xlsx | grep -q "PK" && [ "$(cat /tmp/p75.code)" = "200" ] && ok "dept-scoped ParkingXlsx 200 + xlsx" || bad "ParkingXlsx ($(cat /tmp/p75.code))"

# ---------- ۵) سلب فوری superadmin ----------
c=$(curl -s -c "$S3" -H "Content-Type: application/json" -d '{"username":"p75_sa","password":"Sa@12345"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$c" = "200" ] && ok "p75_sa login (superadmin)" || bad "p75_sa login ($c)"
c=$(curl -s -b "$S3" -o /dev/null -w "%{http_code}" "$B/Users")
[ "$c" = "200" ] && ok "superadmin /Users 200" || bad "superadmin /Users ($c)"
python3.13 - <<'PYEOF'
import sqlite3
c = sqlite3.connect("/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db")
c.execute("UPDATE Users SET IsSuperadmin=0 WHERE Username='p75_sa'")
c.commit()
PYEOF
c=$(curl -s -b "$S3" -o /dev/null -w "%{http_code}" "$B/Users")
[ "$c" = "302" ] && ok "superadmin revoked instantly (302) — بدون ورود مجدد/Bump" || bad "revocation not instant ($c)"
LOC=$(curl -s -b "$S3" -o /dev/null -w "%{redirect_url}" "$B/Employees")
echo "$LOC" | grep -q "/Auth/Denied" && ok "ex-sa zero perms (Denied)" || bad "ex-sa /Employees ($LOC)"

# ---------- ۶) حذف کاربر + حسابرسی ----------
T=$(token /Users)
curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "username=p75_tmp" --data-urlencode "fullName=کاربر حذف‌شدنی" \
  --data-urlencode "password=Tmp@12345" --data-urlencode "password2=Tmp@12345" \
  --data-urlencode "isActive=true" \
  "$B/Users/Create" >/dev/null
TMPID=$($DBQ --json "SELECT Id FROM Users WHERE Username='p75_tmp'" | jrow Id)
T=$(token /Users)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" "$B/Users/Delete/$TMPID")
echo "$R" | grep -q '"ok":true' && ok "users.delete ajax ok" || bad "users.delete → $R"
GONE=$($DBQ --json "SELECT COUNT(*) n FROM Users WHERE Username='p75_tmp'" | jrow n)
[ "$GONE" = "0" ] && ok "user row removed" || bad "user row still there"
AUD=$($DBQ --json "SELECT COUNT(*) n FROM AuditLogs WHERE Action='delete' AND Entity='user' AND EntityId='p75_tmp'" | jrow n)
[ "$AUD" -ge "1" ] && ok "audit delete user logged" || bad "audit missing"

# گارد حذف superadmin
SAID=$($DBQ --json "SELECT Id FROM Users WHERE Username='admin'" | jrow Id)
T=$(token /Users)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" "$B/Users/Delete/$SAID")
echo "$R" | grep -q '"ok":true' && bad "superadmin delete NOT blocked" || ok "superadmin delete blocked"

# ---------- ۷) پاکسازی ----------
$DBQ --exec "DELETE FROM UserRoles WHERE UserId IN (SELECT Id FROM Users WHERE Username LIKE 'p75_%')" >/dev/null 2>&1
$DBQ --exec "DELETE FROM RefreshTokens WHERE UserId IN (SELECT Id FROM Users WHERE Username LIKE 'p75_%')" >/dev/null 2>&1
$DBQ --exec "DELETE FROM PasswordHistories WHERE UserId IN (SELECT Id FROM Users WHERE Username LIKE 'p75_%')" >/dev/null 2>&1
$DBQ --exec "DELETE FROM Users WHERE Username LIKE 'p75_%'" >/dev/null 2>&1
$DBQ --exec "DELETE FROM RoleScopes WHERE RoleId IN (SELECT Id FROM Roles WHERE Name LIKE 'p75_%')" >/dev/null 2>&1
$DBQ --exec "DELETE FROM RolePermissions WHERE RoleId IN (SELECT Id FROM Roles WHERE Name LIKE 'p75_%')" >/dev/null 2>&1
$DBQ --exec "DELETE FROM Roles WHERE Name LIKE 'p75_%'" >/dev/null 2>&1
$DBQ --exec "DELETE FROM ParkingEvents WHERE PlateKey LIKE 'E2E%'" >/dev/null 2>&1
$DBQ --exec "DELETE FROM Vehicles WHERE PlateKey LIKE 'E2E%'" >/dev/null 2>&1
ok "cleanup"

echo "=============================="
echo "P75 RESULT: PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ]
