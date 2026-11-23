#!/usr/bin/env bash
# E2E Phase 12: device definition with type-based defaults + biometrics UI tabs
set -u
BASE="${BASE:-http://localhost:3000}"
JAR=$(mktemp)
DB=/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db
DBQ="python3.13 /home/z/my-project/scripts/dbq.py"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); echo "  ✗ $1"; }
q() { $DBQ "$1"; }

TEST_SERIAL="ZK-E2E-1290"
TEST_IP="192.168.10.199"

# --- پاک‌سازی قبلی (idempotent) — روی دیتابیسی که واقعاً در حال استفاده است ---
$DBQ --exec "DELETE FROM DeviceCommands WHERE DeviceId IN (SELECT Id FROM Devices WHERE Serial='$TEST_SERIAL')" >/dev/null
$DBQ --exec "DELETE FROM Devices WHERE Serial='$TEST_SERIAL'" >/dev/null

echo "== login =="
code=$(curl -s -c "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' --max-time 10)
[ "$code" = "200" ] && ok "login 200" || bad "login $code"

AF=$(curl -s -b "$JAR" -c "$JAR" "$BASE/Devices" | rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]+"' -N | head -1 | sed 's/.*value="//;s/"$//')
[ -n "$AF" ] && ok "antiforgery token" || bad "no antiforgery token"

echo "== P12.1 device create form (type defaults) =="
code=$(curl -s -b "$JAR" -o /tmp/devcreate.html -w "%{http_code}" "$BASE/Devices/Create")
[ "$code" = "200" ] && ok "GET /Devices/Create 200" || bad "GET /Devices/Create $code"
rg -q "تعریف دستگاه جدید" /tmp/devcreate.html && ok "form title" || bad "form title missing"
for kind in "ترمینال اثر انگشت" "ترمینال تشخیص چهره" "کنترلر دسترسی"; do
  if rg -q "$kind" /tmp/devcreate.html; then ok "kind option: $kind"; else bad "kind option missing: $kind"; fi
done
rg -q "CATALOG" /tmp/devcreate.html && ok "JS catalog injected" || bad "JS catalog missing"
rg -q "192\.168\.10\." /tmp/devcreate.html && ok "suggested IP present" || bad "suggested IP missing"
# قاعده پیش‌فرض: کنترلر دسترسی → کارکرد کنترل تردد + پایش ۳۰ ثانیه
rg -q "pollingInterval" /tmp/devcreate.html && ok "polling field present" || bad "polling field missing"

echo "== P12.2 create access controller (server-side defaults normalization) =="
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code} %{redirect_url}" -X POST "$BASE/Devices/Create" \
  --data-urlencode "name=ترمینال تست E2E فاز ۱۲" \
  --data-urlencode "kind=کنترلر دسترسی" \
  --data-urlencode "brand=Virdi" \
  --data-urlencode "serial=$TEST_SERIAL" \
  --data-urlencode "ip=$TEST_IP" \
  --data-urlencode "location=انبار تست" \
  --data-urlencode "purpose=کنترل تردد" \
  --data-urlencode "mode=آنلاین" \
  --data-urlencode "pollingInterval=30" \
  --data-urlencode "active=true" \
  --data-urlencode "__RequestVerificationToken=$AF")
echo "$code" | rg -q "^302" && ok "POST Create → 302 redirect" || bad "POST Create → $code"
ROW=$($DBQ --json "SELECT Kind, Purpose, PollingInterval, Ip, Brand FROM Devices WHERE Serial='$TEST_SERIAL'" | python3.13 -c "
import sys, json
r = json.load(sys.stdin)
if not r: print('MISSING')
else: print('|'.join(str(x) for x in (r[0]['Kind'], r[0]['Purpose'], r[0]['PollingInterval'], r[0]['Ip'], r[0]['Brand'])))
")
[ "$ROW" = "کنترلر دسترسی|کنترل تردد|30|$TEST_IP|Virdi" ] && ok "device row correct (kind/purpose/polling/ip/brand)" || bad "device row wrong: $ROW"
DEVID=$(q "SELECT Id FROM Devices WHERE Serial='$TEST_SERIAL'")

echo "== P12.3 duplicate serial rejected =="
code=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code} %{redirect_url}" -X POST "$BASE/Devices/Create" \
  --data-urlencode "name=تست تکراری" \
  --data-urlencode "kind=ترمینال اثر انگشت" \
  --data-urlencode "brand=ZKTeco" \
  --data-urlencode "serial=$TEST_SERIAL" \
  --data-urlencode "purpose=حضور و غیاب" \
  --data-urlencode "mode=آنلاین" \
  --data-urlencode "pollingInterval=60" \
  --data-urlencode "__RequestVerificationToken=$AF")
echo "$code" | rg -q "^302 .*/Devices/Create" && ok "duplicate serial → back to Create" || bad "duplicate serial → $code"
curl -s -b "$JAR" -c "$JAR" -o /tmp/devdup.html "$BASE/Devices/Create"
rg -q "قبلاً برای دستگاه دیگری ثبت شده" /tmp/devdup.html && ok "duplicate error toast shown" || bad "duplicate error missing"

echo "== P12.4 invalid kind rejected =="
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code} %{redirect_url}" -X POST "$BASE/Devices/Create" \
  --data-urlencode "name=دستگاه نامعتبر" \
  --data-urlencode "kind=دستگاه فضایی" \
  --data-urlencode "brand=ZKTeco" \
  --data-urlencode "serial=XX-INVALID-01" \
  --data-urlencode "purpose=حضور و غیاب" \
  --data-urlencode "mode=آنلاین" \
  --data-urlencode "pollingInterval=60" \
  --data-urlencode "__RequestVerificationToken=$AF")
echo "$code" | rg -q "^302 .*/Devices/Create" && ok "invalid kind → back to Create" || bad "invalid kind → $code"

echo "== P12.5 device edit + toggle =="
code=$(curl -s -b "$JAR" -o /tmp/devedit.html -w "%{http_code}" "$BASE/Devices/Edit/$DEVID")
[ "$code" = "200" ] && ok "GET Edit 200" || bad "GET Edit $code"
rg -q "ویرایش دستگاه" /tmp/devedit.html && ok "edit form title" || bad "edit title missing"
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Devices/Edit/$DEVID" \
  --data-urlencode "name=ترمینال تست E2E ویرایش‌شده" \
  --data-urlencode "kind=کنترلر دسترسی" \
  --data-urlencode "brand=Virdi" \
  --data-urlencode "serial=$TEST_SERIAL" \
  --data-urlencode "ip=$TEST_IP" \
  --data-urlencode "location=محل جدید تست" \
  --data-urlencode "purpose=کنترل تردد" \
  --data-urlencode "mode=آنلاین" \
  --data-urlencode "pollingInterval=45" \
  --data-urlencode "active=true" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "POST Edit 302" || bad "POST Edit $code"
V=$(q "SELECT CONCAT(PollingInterval, '|', Location) FROM Devices WHERE Id='$DEVID'")
[ "$V" = "45|محل جدید تست" ] && ok "edit persisted (polling=45, location)" || bad "edit not persisted: $V"
curl -s -b "$JAR" -o /dev/null -X POST "$BASE/Devices/ToggleActive" --data-urlencode "deviceId=$DEVID" --data-urlencode "__RequestVerificationToken=$AF"
A=$(q "SELECT CASE WHEN Active=1 THEN '1' ELSE '0' END FROM Devices WHERE Id='$DEVID'")
[ "$A" = "0" ] && ok "toggle → inactive" || bad "toggle inactive failed: $A"
curl -s -b "$JAR" -o /dev/null -X POST "$BASE/Devices/ToggleActive" --data-urlencode "deviceId=$DEVID" --data-urlencode "__RequestVerificationToken=$AF"
A=$(q "SELECT CASE WHEN Active=1 THEN '1' ELSE '0' END FROM Devices WHERE Id='$DEVID'")
[ "$A" = "1" ] && ok "toggle → active again" || bad "toggle active failed: $A"

echo "== P12.6 devices index shows kind + create button =="
curl -s -b "$JAR" -o /tmp/devindex.html "$BASE/Devices"
rg -q "تعریف دستگاه جدید" /tmp/devindex.html && ok "create button on index" || bad "create button missing"
rg -q "کنترلر دسترسی" /tmp/devindex.html && ok "kind shown on cards" || bad "kind not shown"
rg -q "/Devices/Edit/" /tmp/devindex.html && ok "edit button on cards" || bad "edit button missing"

echo "== P12.7 biometrics tabbed UI =="
code=$(curl -s -b "$JAR" -o /tmp/bio.html -w "%{http_code}" "$BASE/Biometrics")
[ "$code" = "200" ] && ok "GET /Biometrics 200" || { bad "GET /Biometrics $code"; head -c 300 /tmp/bio.html; }
rg -q 'id="bioTabs"' /tmp/bio.html && ok "tab bar present" || bad "tab bar missing"
for tab in "نمای کلی" "پرسنل دستگاه‌ها" "انتقال پرسنل" "قالب‌های مرجع" "لاگ دستورها"; do
  if rg -q "$tab" /tmp/bio.html; then ok "tab: $tab"; else bad "tab missing: $tab"; fi
done
rg -q 'class="stat-grid"' /tmp/bio.html && ok "stat grid (overview)" || bad "stat grid missing"
rg -q 'class="ov-grid"' /tmp/bio.html && ok "per-device overview cards" || bad "overview cards missing"
rg -q 'data-panel="users"' /tmp/bio.html && ok "users panel" || bad "users panel missing"
rg -q 'data-panel="transfer"' /tmp/bio.html && ok "transfer panel" || bad "transfer panel missing"
rg -q "شبیه‌ساز" /tmp/bio.html && ok "simulator buttons kept" || bad "simulator buttons lost"
rg -q "TransferDeviceUsers" /tmp/bio.html && ok "device→device form kept" || bad "device→device form lost"
rg -q "activateTab" /tmp/bio.html && ok "tab JS present" || bad "tab JS missing"

echo "== P12.8 regression — pages 200 =="
for p in "/" "/Employees" "/Org" "/Attendance" "/Requests" "/Holidays" "/Access" "/Visitors" "/Devices" "/Biometrics" "/Payroll" "/Users" "/Audit"; do
  code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" "$BASE$p" --max-time 15)
  [ "$code" = "200" ] && ok "$p 200" || bad "$p → $code"
done

echo "== P12.9 regression — DB→device transfer still works =="
EMPID=$(q "SELECT Id FROM Employees WHERE Status='فعال' ORDER BY PersonnelCode LIMIT 1")
TGT=$(q "SELECT Id FROM Devices ORDER BY Name LIMIT 1")
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code} %{redirect_url}" -X POST "$BASE/Biometrics/PushToDevices" \
  --data-urlencode "employeeIds=$EMPID" \
  --data-urlencode "toDeviceIds=$TGT" \
  --data-urlencode "includeBios=true" \
  --data-urlencode "__RequestVerificationToken=$AF")
echo "$code" | rg -q "^302" && ok "PushToDevices 302" || bad "PushToDevices → $code"
C=$(q "SELECT COUNT(*) FROM DeviceCommands WHERE Type='push_user_bios' AND Status IN ('pending','delivered')")
[ "$C" -ge 1 ] && ok "push_user_bios queued ($C)" || bad "no queued command"

echo ""
echo "RESULT: $PASS passed, $FAIL failed"
# --- پاک‌سازی دستگاه تست ---
$DBQ --exec "DELETE FROM DeviceCommands WHERE DeviceId='$DEVID'" >/dev/null
$DBQ --exec "DELETE FROM Devices WHERE Id='$DEVID'" >/dev/null
echo "test device cleaned up"
[ "$FAIL" = "0" ]
