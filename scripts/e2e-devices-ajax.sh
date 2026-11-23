#!/bin/bash
# ============================================================
# E2E: زنجیره AJAX دستگاه‌ها — Task 19
# 1) GET /Devices/Live (JSON)           5) webhook pull+ack → Live acked
# 2) bump کانفیگ گیت‌وی پس از ویرایش    6) ToggleActive/TransferUser AJAX
# 3) Command AJAX → JSON + صف DB        7) Biometrics/Gateway AJAX
# 4) Create AJAX + خطای اعتبارسنجی      8) مارک‌های فرانت (data-ajax/live)
# ============================================================
B=http://localhost:3000
J=/tmp/dev-ajax.jar
DB=/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db
SER="E2E-AJAX-$(date +%H%M%S)"
PASS=0; FAIL=0
ok(){ echo "PASS $1"; PASS=$((PASS+1)); }
no(){ echo "FAIL $1"; FAIL=$((FAIL+1)); }

rm -f "$J"
code=$(curl -s -c "$J" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && ok "login" || { echo "LOGIN FAILED ($code)"; exit 1; }

token() {
  curl -s -b "$J" -c "$J" "$B/Devices" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'
}
T=$(token)
[ -n "$T" ] || { echo "NO ANTIFORGERY TOKEN"; exit 1; }

ajax() { # $1=path, data در $2... — پاسخ JSON با هدر AJAX
  curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" \
    -H "RequestVerificationToken: $T" \
    --data-urlencode "__RequestVerificationToken=$T" "${@:2}" "$B$1"
}

# ---------- 0) پاکسازی اجرای قبلی (غیرفعال‌سازی دستگاه‌های E2E قبلی) ----------
python3 - <<'PYEOF' > /tmp/e2e-old-dev.txt
import sqlite3
c = sqlite3.connect("/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db")
rows = c.execute("SELECT Id, Serial FROM Devices WHERE Serial LIKE 'E2E-AJAX-%'").fetchall()
for r in rows: print(r[0], r[1])
PYEOF
while read -r OID OSER; do
  ajax /Devices/ToggleActive -d "deviceId=$OID" > /dev/null
done < /tmp/e2e-old-dev.txt

# ---------- 1) Live JSON ----------
LIVE=$(curl -s -b "$J" "$B/Devices/Live")
echo "$LIVE" | python3 -c "import sys,json; j=json.load(sys.stdin); assert 'devices' in j and 'commands' in j and 'chips' in j" 2>/dev/null \
  && ok "GET /Devices/Live JSON shape" || no "GET /Devices/Live JSON shape"

# ---------- 2) Create AJAX ----------
RES=$(ajax /Devices/Create -d "name=تست-E2E-AJAX&kind=ترمینال اثر انگشت&brand=ZKTeco&serial=$SER&ip=&location=سندباکس&purpose=حضور و غیاب&mode=آنلاین&pollingInterval=45&transport=adms&active=true")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==True and j.get('redirect')=='/Devices'" 2>/dev/null \
  && ok "Create AJAX → JSON ok+redirect" || { no "Create AJAX"; echo "  got: $RES"; }
DEV=$(python3 -c "
import sqlite3
c = sqlite3.connect('$DB')
r = c.execute('SELECT Id, WebhookToken FROM Devices WHERE Serial=?', ('$SER',)).fetchone()
print(r[0], r[1])")
DID=${DEV%% *}; DTOK=${DEV##* }
[ -n "$DID" ] && ok "device row created in DB" || { no "device row"; exit 1; }

# ---------- 3) خطای اعتبارسنجی AJAX (سریال تکراری) ----------
RES=$(ajax /Devices/Create -d "name=تست-تکراری&kind=ترمینال اثر انگشت&brand=ZKTeco&serial=$SER&purpose=حضور و غیاب&mode=آنلاین&pollingInterval=45")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==False and 'قبلاً' in j['message']" 2>/dev/null \
  && ok "Create duplicate serial → JSON err" || { no "Create dup-serial"; echo "  got: $RES"; }

# ---------- 4) bump کانفیگ گیت‌وی پس از ویرایش ----------
V1=$(curl -s -b "$J" "$B/api/gateway/config" | python3 -c "import sys,json; print(json.load(sys.stdin)['version'])")
ajax "/Devices/Edit/$DID" -d "name=تست-E2E-AJAX-۲&kind=ترمینال اثر انگشت&brand=ZKTeco&serial=$SER&ip=192.168.10.99&location=سندباکس&purpose=حضور و غیاب&mode=آنلاین&pollingInterval=50&transport=tcp&devicePort=4370&commKey=0&active=true" > /dev/null
V2=$(curl -s -b "$J" "$B/api/gateway/config" | python3 -c "import sys,json; print(json.load(sys.stdin)['version'])")
[ "$V2" -gt "$V1" ] && ok "Edit device bumps gateway config version ($V1→$V2)" || no "config version bump ($V1→$V2)"
RES=$(ajax "/Devices/Edit/$DID" -d "name=تست-E2E-AJAX&kind=ترمینال اثر انگشت&brand=ZKTeco&serial=$SER&ip=&location=سندباکس&purpose=حضور و غیاب&mode=آنلاین&pollingInterval=45&transport=adms&active=true" > /dev/null)

# ---------- 5) Command AJAX + صف ----------
RES=$(ajax /Devices/Command -d "deviceId=$DID&type=unlock&seconds=3")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==True and j.get('cmdId')" 2>/dev/null \
  && ok "Command AJAX → JSON ok+cmdId" || { no "Command AJAX"; echo "  got: $RES"; }
CMD=$(echo "$RES" | python3 -c "import sys,json; print(json.load(sys.stdin)['cmdId'])")
ST=$(python3 -c "
import sqlite3
c = sqlite3.connect('$DB')
print(c.execute('SELECT Status FROM DeviceCommands WHERE Id=?', ('$CMD',)).fetchone()[0])")
[ "$ST" = "pending" ] && ok "command queued as pending in DB" || no "command status=$ST"

# ---------- 6) webhook: pull → ack → Live acked ----------
PULL=$(curl -s -H "X-Device-Token: $DTOK" "$B/api/devices/webhook?SN=$SER")
echo "$PULL" | python3 -c "
import sys, json
j = json.load(sys.stdin)
ids = [c['id'] for c in j.get('commands', [])]
assert '$CMD' in ids, ids" 2>/dev/null \
  && ok "webhook GET delivers queued command" || { no "webhook pull"; echo "  got: $PULL"; }
ACK=$(curl -s -H "X-Device-Token: $DTOK" -H "Content-Type: application/json" \
  -d "{\"SN\":\"$SER\",\"event\":\"ack\",\"data\":{\"commandId\":\"$CMD\",\"ok\":true,\"result\":\"درب ۳ ثانیه باز شد (تست E2E)\"}}" \
  "$B/api/devices/webhook")
echo "$ACK" | grep -q '"ok":true' && ok "webhook POST ack accepted" || { no "webhook ack"; echo "  got: $ACK"; }
LIVE=$(curl -s -b "$J" "$B/Devices/Live")
echo "$LIVE" | python3 -c "
import sys, json
j = json.load(sys.stdin)
c = next(c for c in j['commands'] if c['id'] == '$CMD')
assert c['status'] == 'acked' and c['statusText'] == 'اجرا شد' and 'E2E' in c['result'], c" 2>/dev/null \
  && ok "Live shows acked status + result" || no "Live acked state"

# ---------- 7) ToggleActive AJAX ----------
RES=$(ajax /Devices/ToggleActive -d "deviceId=$DID")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==True and j['active']==False" 2>/dev/null \
  && ok "ToggleActive AJAX → inactive" || { no "ToggleActive"; echo "  got: $RES"; }
V3=$(curl -s -b "$J" "$B/api/gateway/config" | python3 -c "import sys,json; print(json.load(sys.stdin)['version'])")
[ "$V3" -gt "$V2" ] && ok "Toggle bumps config version ($V2→$V3)" || no "toggle bump ($V2→$V3)"
ajax /Devices/ToggleActive -d "deviceId=$DID" > /dev/null   # برگرداندن به فعال

# ---------- 8) TransferUser AJAX ----------
EMP=$(python3 -c "
import sqlite3
c = sqlite3.connect('$DB')
print(c.execute(\"SELECT Id FROM Employees WHERE Status='فعال' LIMIT 1\").fetchone()[0])")
RES=$(ajax /Devices/TransferUser -d "employeeId=$EMP&toDeviceIds=$DID&fromDeviceIds=")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==True and 'صف' in j['message']" 2>/dev/null \
  && ok "TransferUser AJAX → JSON ok" || { no "TransferUser AJAX"; echo "  got: $RES"; }

# ---------- 9) Biometrics AJAX ----------
RES=$(ajax /Biometrics/FetchUsers -d "deviceId=$DID")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==True" 2>/dev/null \
  && ok "Biometrics FetchUsers AJAX" || { no "Biometrics AJAX"; echo "  got: $RES"; }

# ---------- 10) Gateway Save AJAX ----------
GS=$(curl -s -b "$J" "$B/Gateway" | grep -o 'name="__[A-Za-z]*"' | head -1)
ENABLED=$(curl -s -b "$J" "$B/api/gateway/config" | python3 -c "import sys,json; print('true' if json.load(sys.stdin)['settings']['enabled'] else 'false')")
RES=$(ajax /Gateway/Save -d "Enabled=true&ListenPort=8085&TcpTimeoutMs=6000&PollIntervalSec=60&SweeperIntervalSec=60&ConfigRefreshSec=30&LogLevel=Information&DefaultDevicePort=4370&DefaultCommKey=0")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==True and j['configVersion']>0" 2>/dev/null \
  && ok "Gateway Save AJAX → JSON ok+version" || { no "Gateway Save AJAX"; echo "  got: $RES"; }
curl -s -b "$J" -H "RequestVerificationToken: $T" --data-urlencode "__RequestVerificationToken=$T" \
  -d "Enabled=true&ListenPort=8085&TcpTimeoutMs=6000&PollIntervalSec=60&SweeperIntervalSec=60&ConfigRefreshSec=30&LogLevel=Information&DefaultDevicePort=4370&DefaultCommKey=0" \
  "$B/Gateway/Save" -o /dev/null   # بازگرداندن پورت به 8085 همان مقدار است — بی‌اثر

# ---------- 11) مارک‌های فرانت ----------
HTML=$(curl -s -b "$J" "$B/Devices")
echo "$HTML" | grep -q 'data-ajax' && ok "Devices view: data-ajax forms" || no "Devices data-ajax"
echo "$HTML" | grep -q 'id="cmdlog"' && ok "Devices view: live log tbody" || no "Devices cmdlog"
echo "$HTML" | grep -q 'liveCommands' && ok "Devices view: liveCommands poller" || no "Devices liveCommands"
echo "$HTML" | grep -q 'onsubmit="return confirm' && no "browser confirm leaked" || ok "no browser confirm"
HTML=$(curl -s -b "$J" "$B/Biometrics")
echo "$HTML" | grep -q 'id="biolog"' && ok "Biometrics view: live log tbody" || no "Biometrics biolog"
echo "$HTML" | grep -q 'data-ajax' && ok "Biometrics view: data-ajax forms" || no "Biometrics data-ajax"
HTML=$(curl -s -b "$J" "$B/Gateway")
echo "$HTML" | grep -q 'data-ajax' && ok "Gateway view: data-ajax form" || no "Gateway data-ajax"
HTML=$(curl -s -b "$J" "$B/Devices/Create")
echo "$HTML" | grep -q 'data-ajax-redirect="/Devices"' && ok "Form view: ajax redirect" || no "Form data-ajax-redirect"

# ---------- 12) پاکسازی: دستگاه تستی غیرفعال شود (حفظ برای idempotency) ----------
ajax /Devices/ToggleActive -d "deviceId=$DID" > /dev/null

echo "=============================="
echo "PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ] && echo "ALL GREEN ✓" || exit 1
