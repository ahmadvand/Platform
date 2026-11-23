#!/usr/bin/env bash
# E2E Phase 21: تنظیمات گیت‌وی C# از پنل ادمین + API کانفیگ پویا + فیلدهای اتصال دستگاه
set -u
BASE="${BASE:-http://localhost:3000}"
JAR=$(mktemp)
DBQ="python3.13 /home/z/my-project/scripts/dbq.py"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); echo "  ✗ $1"; }
q() { $DBQ "$1"; }

echo "== login =="
code=$(curl -s -c "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' --max-time 10)
[ "$code" = "200" ] && ok "login admin 200" || bad "login $code"

AF=$(curl -s -b "$JAR" -c "$JAR" "$BASE/Gateway" | rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]+"' -N | head -1 | sed 's/.*value="//;s/"$//')
[ -n "$AF" ] && ok "antiforgery token" || bad "no antiforgery token"

echo "== P21.1 پنل گیت‌وی =="
code=$(curl -s -b "$JAR" -o /tmp/gw.html -w "%{http_code}" "$BASE/Gateway" --max-time 15)
[ "$code" = "200" ] && ok "GET /Gateway 200" || bad "GET /Gateway $code"
for t in "سرویس گیت‌وی دستگاه‌های تردد" "ویرایش تنظیمات" "پورت پل ADMS" "دستگاه‌های تحت مدیریت گیت‌وی" "نسخه کانفیگ" "api/gateway/config"; do
  rg -q "$t" /tmp/gw.html && ok "panel section: $t" || bad "section missing: $t"
done
rg -q 'تنظیمات گیت‌وی' /tmp/gw.html && ok "gateway menu link present (title)" || bad "menu/title missing"

echo "== P21.2 ذخیره تنظیمات =="
V0=$(q "SELECT ConfigVersion FROM GatewaySettings WHERE Id='default'")
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Gateway/Save" \
  --data-urlencode "Enabled=true" \
  --data-urlencode "ListenPort=8082" \
  --data-urlencode "TcpTimeoutMs=7000" \
  --data-urlencode "PollIntervalSec=45" \
  --data-urlencode "SweeperIntervalSec=90" \
  --data-urlencode "ConfigRefreshSec=20" \
  --data-urlencode "LogLevel=Information" \
  --data-urlencode "DefaultDevicePort=4370" \
  --data-urlencode "DefaultCommKey=0" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "POST /Gateway/Save 302" || bad "POST Save $code"
V1=$(q "SELECT ConfigVersion FROM GatewaySettings WHERE Id='default'")
[ "$V1" != "$V0" ] && ok "ConfigVersion bumped ($V0 → $V1)" || bad "ConfigVersion unchanged ($V1)"
LP=$(q "SELECT ListenPort FROM GatewaySettings WHERE Id='default'")
[ "$LP" = "8082" ] && ok "ListenPort persisted (8082)" || bad "ListenPort=$LP"
PI=$(q "SELECT PollIntervalSec FROM GatewaySettings WHERE Id='default'")
[ "$PI" = "45" ] && ok "PollIntervalSec persisted (45)" || bad "PollIntervalSec=$PI"

echo "== P21.3 اعتبارسنجی بازه‌ها =="
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Gateway/Save" \
  --data-urlencode "Enabled=true" \
  --data-urlencode "ListenPort=99999" \
  --data-urlencode "TcpTimeoutMs=7000" \
  --data-urlencode "PollIntervalSec=45" \
  --data-urlencode "SweeperIntervalSec=90" \
  --data-urlencode "ConfigRefreshSec=20" \
  --data-urlencode "LogLevel=Information" \
  --data-urlencode "DefaultDevicePort=4370" \
  --data-urlencode "DefaultCommKey=0" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "invalid port → 302 with error toast" || bad "invalid port $code"
LP2=$(q "SELECT ListenPort FROM GatewaySettings WHERE Id='default'")
[ "$LP2" = "8082" ] && ok "invalid value not persisted (still 8082)" || bad "invalid persisted: $LP2"

echo "== P21.4 API کانفیگ پویا با حساب svc_bridge =="
BJAR=$(mktemp)
code=$(curl -s -c "$BJAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" -d '{"username":"svc_bridge","password":"Bridge@1234"}' --max-time 10)
[ "$code" = "200" ] && ok "svc_bridge login 200" || bad "svc_bridge login $code"
code=$(curl -s -b "$BJAR" -o /tmp/gwcfg.json -w "%{http_code}" "$BASE/api/gateway/config" --max-time 15)
[ "$code" = "200" ] && ok "GET /api/gateway/config 200" || bad "GET config $code"
for k in '"version"' '"settings"' '"devices"' '"enabled"' '"listenPort"' '"tcpTimeoutMs"' '"pollIntervalSec"' '"sweeperIntervalSec"' '"configRefreshSec"' '"defaultDevicePort"' '"serial"'; do
  rg -q -- "$k" /tmp/gwcfg.json && ok "config field: $k" || bad "config field missing: $k"
done
rg -q '"listenPort":\s*8082' /tmp/gwcfg.json && ok "API returns saved ListenPort 8082" || bad "API ListenPort mismatch"
LF=$(q "SELECT LastFetchedAt FROM GatewaySettings WHERE Id='default'")
[ "$LF" != "None" ] && [ -n "$LF" ] && ok "LastFetchedAt updated by gateway fetch" || bad "LastFetchedAt=$LF"
# بدون توکن باید ۴۰۱ باشد
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/gateway/config" --max-time 10)
[ "$code" = "401" ] && ok "config without token → 401" || bad "unauth config $code"

echo "== P21.5 فیلدهای اتصال دستگاه در فرم =="
code=$(curl -s -b "$JAR" -o /tmp/devform.html -w "%{http_code}" "$BASE/Devices/Create" --max-time 15)
[ "$code" = "200" ] && ok "GET /Devices/Create 200" || bad "GET Create $code"
for t in 'name="transport"' 'name="devicePort"' 'name="commKey"' 'ADMS / PUSH' 'TCP — پول مستقیم'; do
  rg -q "$t" /tmp/devform.html && ok "device form field: $t" || bad "form field missing: $t"
done
# ثبت دستگاه کنترلر دسترسی با TCP + پورت/CommKey
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Devices/Create" \
  --data-urlencode "name=کنترلر گیت تست P21" \
  --data-urlencode "kind=کنترلر دسترسی" \
  --data-urlencode "brand=Virdi" \
  --data-urlencode "serial=VR-AC7-P21" \
  --data-urlencode "ip=192.168.10.66" \
  --data-urlencode "location=تست E2E" \
  --data-urlencode "purpose=کنترل تردد" \
  --data-urlencode "mode=آنلاین" \
  --data-urlencode "pollingInterval=30" \
  --data-urlencode "transport=tcp" \
  --data-urlencode "devicePort=5150" \
  --data-urlencode "commKey=7" \
  --data-urlencode "active=true" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "POST Create device (tcp) 302" || bad "POST Create $code"
TR=$(q "SELECT Transport FROM Devices WHERE Serial='VR-AC7-P21'")
DP=$(q "SELECT DevicePort FROM Devices WHERE Serial='VR-AC7-P21'")
CK=$(q "SELECT CommKey FROM Devices WHERE Serial='VR-AC7-P21'")
[ "$TR" = "tcp" ] && ok "device Transport=tcp persisted" || bad "Transport=$TR"
[ "$DP" = "5150" ] && ok "device DevicePort=5150 persisted" || bad "DevicePort=$DP"
[ "$CK" = "7" ] && ok "device CommKey=7 persisted" || bad "CommKey=$CK"
# پاک‌سازی دستگاه تست
DID=$(q "SELECT Id FROM Devices WHERE Serial='VR-AC7-P21'")
# دریافت مجدد کانفیگ پس از ثبت دستگاه — دستگاه جدید باید در خروجی باشد
curl -s -b "$BJAR" -o /tmp/gwcfg2.json "$BASE/api/gateway/config" --max-time 15
rg -q 'VR-AC7-P21' /tmp/gwcfg2.json && ok "new device visible in gateway config API" || bad "device not in config API"
rg -q '"ip":\s*"192.168.10.66"' /tmp/gwcfg2.json && ok "device IP in config API" || bad "IP missing in API"
rg -q '"port":\s*5150' /tmp/gwcfg2.json && ok "device port in config API" || bad "port missing in API"
q "DELETE FROM Devices WHERE Serial='VR-AC7-P21'" >/dev/null 2>&1 || true
$DBQ --exec "DELETE FROM Devices WHERE Serial='VR-AC7-P21'" >/dev/null
ok "test device cleaned up ($DID)"

echo "== P21.6 منو و صفحه‌ها سالم =="
for path in "/Devices" "/Biometrics" "/Users" "/Audit" "/Notifications" "/Attendance" "/Employees" "/Org" "/Access" "/Visitors" "/Holidays" "/Requests" "/Payroll" "/"; do
  code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" "$BASE$path" --max-time 15)
  [ "$code" = "200" ] && ok "GET $path 200" || bad "GET $path $code"
done

echo "== P21.7 حسابرسی ثبت شده =="
AU=$(q "SELECT COUNT(*) FROM AuditLogs WHERE Action='edit_gateway_settings'")
[ "$AU" != "0" ] && ok "audit edit_gateway_settings logged" || bad "audit missing"

echo ""
echo "RESULT: PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ]
