#!/usr/bin/env bash
# ======================================================================
# E2E فاز ۶۴ — پورت اختصاصی ADMS (relay شفاف) + سرویس خارجی (استقرار ترکیبی)
# ----------------------------------------------------------------------
# بوت یک‌نوبته + تست کامل:
#   ۱) relay فعال روی ListenPort (۸۰۸۲) — ss + handshake بی‌احراز از 8082
#   ۲) شفافیت relay: تحویل دستور sync_time از مسیر 8082 (عین مسیر 3000)
#   ۳) داشبورد ماژول: کارت پورت اختصاصی + بخش سرویس خارجی
#   ۴) سرویس خارجی: ExternalStatus با سرویس تقلبی (reachable + پارس runtime)
#      و سپس خارج از دسترس (پیام دوستانه بدون 500)
#   ۵) جابه‌جایی پورت از پنل (مسیر کاربر): 8082 → 3000 (relay خاموش) → 8082
#      بدون ری‌استارت اپ (supervisor هر ConfigRefreshSec هم‌تراز می‌کند)
#   ۶) قطعیت تحویل ADMS: ۳ بار POST→handshake فوری — همه باید همان لحظه
#      C:{id}:… بدهند (رگرسیون رَیس چندنمونه‌ای) + DB صفر stuck pending
# ======================================================================
set -u
BASE=http://localhost:3000
RELAY=http://127.0.0.1:8082
ADMS_SERIAL="AV-W1-0455"
DB=/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db
PASS=0; FAIL=0
ok(){ echo "PASS: $1"; PASS=$((PASS+1)); }
bad(){ echo "FAIL: $1"; FAIL=$((FAIL+1)); }
q(){ python3.13 /home/z/my-project/scripts/dbq.py "$1" 2>/dev/null; }
sq(){ python3 - "$1" "$2" <<'PYEOF'
import sqlite3, sys
con = sqlite3.connect(sys.argv[1]); con.execute(sys.argv[2]); con.commit()
PYEOF
}

echo "== 0) بوت اپ با سرویس خارجی تقلبی (یک نوبت) =="
pkill -f "HrAttendance.Web.dll" 2>/dev/null; sleep 2
# سرویس تقلبی گیت‌وی خارجی روی 8099 — /health با runtime پارس‌شدنی
python3 - > /tmp/p64_ext.log 2>&1 <<'PYEOF' &
import http.server, json
class H(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        body = json.dumps({"status":"healthy","runtime":{"enabled":True,"configVersion":7,"pollers":2,"restartRequired":False}}).encode()
        self.send_response(200); self.send_header("Content-Type","application/json")
        self.send_header("Content-Length",str(len(body))); self.end_headers(); self.wfile.write(body)
    def log_message(self, *a): pass
http.server.HTTPServer(("127.0.0.1",8099), H).serve_forever()
PYEOF
EXT_PID=$!
cd /home/z/my-project
export PATH="/home/z/.dotnet:$PATH"
export Gateway__ExternalService__Url="http://127.0.0.1:8099"
export Gateway__ExternalService__TimeoutSec="3"
setsid nohup bash scripts/start-web.sh </dev/null >/dev/null 2>&1 &
disown
UP=0
for i in $(seq 1 30); do
  c=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "$BASE/health" 2>/dev/null)
  [ "$c" = "200" ] && { UP=1; break; }
  sleep 3
done
[ "$UP" = "1" ] && ok "boot: /health 200" || { echo "FAIL: boot نشد"; kill $EXT_PID 2>/dev/null; exit 1; }
sleep 3   # فرصت اولین ApplyConfig برای supervisor دروازه

echo "== 1) relay فعال روی 8082 =="
LISTEN=0
for i in $(seq 1 8); do
  LISTEN=$(ss -tln 2>/dev/null | rg -c "0.0.0.0:8082" || true)
  [ "${LISTEN:-0}" -ge 1 ] && break
  sleep 2
done
[ "${LISTEN:-0}" -ge 1 ] && ok "relay: گوش دادن روی 8082" || bad "relay روی 8082 گوش نمی‌دهد"
T=""
for i in $(seq 1 8); do
  T=$(curl -s --max-time 6 "$RELAY/iclock/cdata?SN=UNKNOWN-SN" | head -1)
  [ "$T" = "OK" ] && break
  sleep 2
done
[ "$T" = "OK" ] && ok "relay: handshake بی‌احراز از 8082 → OK" || bad "relay handshake: '$T'"

echo "== 2) شفافیت relay — تحویل دستور از 8082 =="
JAR=/tmp/p64_jar; rm -f "$JAR"
curl -s -c "$JAR" "$BASE/Auth/Login" -o /tmp/p64_login.html
TOK=$(sed -n 's/.*name="__RequestVerificationToken"[^>]*value="\([^"]*\)".*/\1/p' /tmp/p64_login.html | head -1)
CODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" \
  -d "identifier=admin" -d "secret=Admin@1234" \
  -d "__RequestVerificationToken=$TOK" "$BASE/Auth/Login")
[ "$CODE" = "302" ] && ok "login admin" || bad "login: $CODE"
ADMS_ID=$(q "SELECT Id FROM Devices WHERE Serial='$ADMS_SERIAL'")
curl -s -b "$JAR" "$BASE/Devices" -o /tmp/p64_dev.html
FTOK=$(sed -n 's/.*name="__RequestVerificationToken"[^>]*value="\([^"]*\)".*/\1/p' /tmp/p64_dev.html | head -1)
sq "$DB" "DELETE FROM DeviceCommands WHERE DeviceId='$ADMS_ID' AND Status='pending'"
curl -s -b "$JAR" -o /dev/null -d "deviceId=$ADMS_ID" -d "type=sync_time" \
  -d "__RequestVerificationToken=$FTOK" \
  -H "X-Requested-With: XMLHttpRequest" "$BASE/Devices/Command"
HS2=$(curl -s --max-time 6 "$RELAY/iclock/cdata?SN=$ADMS_SERIAL" | head -1)
echo "$HS2" | grep -q "C:.*:SET OPTIONS DateTime=" && ok "relay: دستور از 8082 تحویل شد ($HS2)" || bad "relay تحویل: '$HS2'"

echo "== 3) داشبورد ماژول =="
curl -s -b "$JAR" "$BASE/DeviceGateway" -o /tmp/p64_dash.html
grep -q "پورت اختصاصی ADMS" /tmp/p64_dash.html && grep -q "ExternalStatus" /tmp/p64_dash.html \
  && ok "داشبورد: کارت پورت اختصاصی + بخش سرویس خارجی" || bad "داشبورد ماژول (کارت‌های فاز ۶۴)"

echo "== 4) سرویس خارجی — در دسترس و خارج از دسترس =="
EX=$(curl -s -b "$JAR" --max-time 8 "$BASE/DeviceGateway/ExternalStatus")
echo "$EX" | grep -q '"reachable":true' && echo "$EX" | grep -q '"configVersion":7' \
  && echo "$EX" | grep -q '"pollers":2' \
  && ok "سرویس خارجی: reachable + پارس runtime (v7, pollers=2)" || bad "ExternalStatus reachable: $EX"
kill $EXT_PID 2>/dev/null
sleep 1
EX2=$(curl -s -b "$JAR" --max-time 8 "$BASE/DeviceGateway/ExternalStatus")
echo "$EX2" | grep -q '"reachable":false' && echo "$EX2" | grep -q '"ok":true' \
  && ok "سرویس خارجی: قطع → پیام دوستانه (بدون 500)" || bad "ExternalStatus offline: $EX2"

echo "== 5) جابه‌جایی پورت از پنل (بدون ری‌استارت) =="
# 8082 → 3000 (relay خاموش: پورت همان اپ)
curl -s -b "$JAR" -o /dev/null -d "Enabled=true" -d "ListenPort=3000" -d "TcpTimeoutMs=7000" \
  -d "PollIntervalSec=45" -d "SweeperIntervalSec=90" -d "ConfigRefreshSec=20" \
  -d "LogLevel=Information" -d "DefaultDevicePort=4370" -d "DefaultCommKey=0" \
  -d "__RequestVerificationToken=$FTOK" "$BASE/Gateway/Save"
MOVED=0
for i in $(seq 1 10); do
  sleep 4
  if ! ss -tln 2>/dev/null | rg -q "0.0.0.0:8082"; then MOVED=1; break; fi
done
[ "$MOVED" = "1" ] && ok "جابه‌جایی: relay روی 8082 بسته شد (پورت = پورت اپ)" || bad "relay بعد از تغییر کانفیگ باز ماند"
# 3000 → 8082 (relay دوباره)
curl -s -b "$JAR" -o /dev/null -d "Enabled=true" -d "ListenPort=8082" -d "TcpTimeoutMs=7000" \
  -d "PollIntervalSec=45" -d "SweeperIntervalSec=90" -d "ConfigRefreshSec=20" \
  -d "LogLevel=Information" -d "DefaultDevicePort=4370" -d "DefaultCommKey=0" \
  -d "__RequestVerificationToken=$FTOK" "$BASE/Gateway/Save"
BACK=0
for i in $(seq 1 10); do
  sleep 4
  T3=$(curl -s --max-time 4 "$RELAY/iclock/cdata?SN=UNKNOWN-SN" | head -1)
  [ "$T3" = "OK" ] && { BACK=1; break; }
done
[ "$BACK" = "1" ] && ok "بازگشت: relay دوباره روی 8082 سرویس می‌دهد" || bad "relay بازگشتی: '$T3'"

echo "== 6) قطعیت تحویل ADMS (رگرسیون رَیس) =="
ALLDEL=1
for n in 1 2 3; do
  curl -s -b "$JAR" -o /dev/null -d "deviceId=$ADMS_ID" -d "type=sync_time" \
    -d "__RequestVerificationToken=$FTOK" \
    -H "X-Requested-With: XMLHttpRequest" "$BASE/Devices/Command"
  HS=$(curl -s --max-time 6 "$BASE/iclock/cdata?SN=$ADMS_SERIAL" | head -1)
  echo "$HS" | grep -q "C:.*:SET OPTIONS DateTime=" || { ALLDEL=0; bad "تحویل فوری نوبت $n: '$HS'"; }
done
[ "$ALLDEL" = "1" ] && ok "قطعیت تحویل: ۳/۳ handshake فوری دستور را داد"
STUCK=$(q "SELECT COUNT(*) FROM DeviceCommands WHERE DeviceId='$ADMS_ID' AND Status='pending'")
[ "${STUCK:-1}" = "0" ] && ok "صف تمیز: صفر دستور stuck pending" || bad "stuck pending: $STUCK"

echo
echo "RESULT: PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ]
