#!/usr/bin/env bash
# ======================================================================
# E2E فاز ۶۳ — ماژول دروازهٔ دستگاه‌ها (جایگزین سرویس ویندوزی)
# ----------------------------------------------------------------------
# بوت یک‌نوبته + تست کامل:
#   ۱) بوت ماژول (لاگ فعال‌شدن) + endpointهای پل (ADMS/ANPR) بدون احراز
#   ۲) پنل /DeviceGateway (رندر داشبورد + لاگ زنده) با احراز + قفل بی‌احراز
#   ۳) poll TCP واقعی: شبیه‌ساز دستگاه → ورود پانچ + آنلاین‌شدن
#   ۴) دستور unlock از صف → اجرای TCP → ACK → رکورد جدید
#   ۵) مسیر ADMS: handshake → تحویل دستور متنی → ACK → وضعیت acked
#   ۶) پل ANPR: خوانش پلاک مجاز → رویداد پارکینگ + barrier open
# سپس بازگردانی کانفیگ دستگاه به حالت اصلی (صفر رد در دادهٔ نمایشی)
# ======================================================================
set -u
BASE=http://localhost:3000
DEV_SERIAL="ZK-U2000-0142"
DEV_ORIG_IP="192.168.10.11"
ADMS_SERIAL="AV-W1-0455"
CAM_SERIAL="ANPR-PG-IN1-1024"
CAM_OUT="ANPR-PG-OUT1-2048"
PLATE='23ی100 ایران 22'
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

echo "== 0) بوت اپ (یک نوبت) =="
pkill -f "HrAttendance.Web.dll" 2>/dev/null; sleep 2
cd /home/z/my-project
export PATH="/home/z/.dotnet:$PATH"
setsid nohup bash scripts/start-web.sh </dev/null >/dev/null 2>&1 &
disown
UP=0
for i in $(seq 1 30); do
  c=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "$BASE/health" 2>/dev/null)
  [ "$c" = "200" ] && { UP=1; break; }
  sleep 3
done
[ "$UP" = "1" ] && ok "boot: /health 200" || { echo "FAIL: boot نشد"; exit 1; }
sleep 4

echo "== 1) بوت ماژول دروازه =="
grep -q "=== دروازهٔ دستگاه‌ها (فاز ۶۳) شروع شد" /home/z/my-project/dev.log \
  && ok "worker: لاگ فعال‌شدن ماژول" || bad "worker: لاگ فعال‌شدن"

T=$(curl -s --max-time 5 "$BASE/iclock/cdata?SN=UNKNOWN-SN" | head -1)
[ "$T" = "OK" ] && ok "adms: SN ناشناس → OK" || bad "adms SN ناشناس: '$T'"

C=$(curl -s -o /tmp/p63_cam.json -w "%{http_code}" --max-time 5 -X POST \
  "$BASE/anpr/plate?SN=NO-SUCH-CAM" -H 'Content-Type: application/json' \
  -d '{"plate":"12ب345","direction":"in"}')
[ "$C" = "404" ] && ok "anpr: دوربین ناشناس → 404" || bad "anpr دوربین ناشناس: $C $(cat /tmp/p63_cam.json 2>/dev/null)"

echo "== 2) پنل /DeviceGateway =="
JAR=/tmp/p63_jar; rm -f "$JAR"
curl -s -c "$JAR" "$BASE/Auth/Login" -o /tmp/p63_login.html
TOK=$(sed -n 's/.*name="__RequestVerificationToken"[^>]*value="\([^"]*\)".*/\1/p' /tmp/p63_login.html | head -1)
CODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" \
  -d "identifier=admin" -d "secret=Admin@1234" \
  -d "__RequestVerificationToken=$TOK" "$BASE/Auth/Login")
[ "$CODE" = "302" ] && ok "login admin" || bad "login: $CODE"

# توکن آنتی‌فورجری صفحات — برای POSTهای دستور
curl -s -b "$JAR" "$BASE/Devices" -o /tmp/p63_dev.html
FTOK=$(sed -n 's/.*name="__RequestVerificationToken"[^>]*value="\([^"]*\)".*/\1/p' /tmp/p63_dev.html | head -1)
[ -n "$FTOK" ] && ok "antiforgery token از /Devices" || bad "antiforgery token"

UN=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/DeviceGateway")
[ "$UN" = "302" ] && ok "بی‌احراز → ریدایرکت ورود" || bad "بی‌احراز: $UN"

curl -s -b "$JAR" "$BASE/DeviceGateway" -o /tmp/p63_page.html
grep -q "ماتریس اتصال دستگاه‌ها" /tmp/p63_page.html \
  && grep -q "لاگ فعالیت زنده" /tmp/p63_page.html \
  && grep -q "دوربین‌های پلاک‌خوان" /tmp/p63_page.html \
  && ok "داشبورد ماژول: ماتریس + لاگ + دوربین‌ها" || bad "رندر داشبورد ماژول"

# در منو حاضر است؟ (فاز ۵۴ — nav)
curl -s -b "$JAR" "$BASE/Employees" -o /tmp/p63_nav.html
grep -q 'href="/DeviceGateway"' /tmp/p63_nav.html && ok "nav: آیتم دروازهٔ دستگاه‌ها" || bad "nav آیتم ماژول"

echo "== 3) مسیر ADMS — handshake + تحویل + ACK =="
ADMS_ID=$(q "SELECT Id FROM Devices WHERE Serial='$ADMS_SERIAL'")
# بهداشت تست: دستورهای معلق/تحویل‌شدهٔ قدیمی صف را خالی کن — وگرنه handshake چندخطی می‌شود
# و ACK تک‌خطی با assert «آخرین ردیف» ناسازگار می‌افتد (تجربهٔ اجرای 2026-10-27)
sq "$DB" "DELETE FROM DeviceCommands WHERE DeviceId='$ADMS_ID' AND Status IN ('pending','delivered')"
curl -s -b "$JAR" -o /dev/null -d "deviceId=$ADMS_ID" -d "type=sync_time" \
  -d "__RequestVerificationToken=$FTOK" \
  -H "X-Requested-With: XMLHttpRequest" "$BASE/Devices/Command"
HS=$(curl -s --max-time 5 "$BASE/iclock/cdata?SN=$ADMS_SERIAL")
echo "$HS" | grep -q "C:.*:SET OPTIONS DateTime=" && ok "adms: دستور SET OPTIONS تحویل شد" || bad "adms handshake: '$HS'"
SHORT=$(echo "$HS" | sed -n 's/^C:\([0-9a-f]\{8\}\):.*/\1/p' | head -1)
if [ -n "$SHORT" ]; then
  curl -s --max-time 5 -X POST "$BASE/iclock/cdata?SN=$ADMS_SERIAL" --data-binary "C:$SHORT:    OK" > /dev/null
  # assert روی «همان» دستور تحویل‌شده — شناسهٔ کوتاه، ۸ کاراکتر آخرِ Id کامل است
  AST=$(q "SELECT Status FROM DeviceCommands WHERE Id LIKE '%$SHORT'")
  [ "$AST" = "acked" ] && ok "adms: ACK دستگاه → acked" || bad "adms ack: $AST"
else
  bad "adms: شناسهٔ کوتاه دستور استخراج نشد"
fi

echo "== 4) poll TCP واقعی =="
BASE_PUNCH=$(q "SELECT COUNT(*) FROM DevicePunchLogs")
DEV_ID=$(q "SELECT Id FROM Devices WHERE Serial='$DEV_SERIAL'")
sq "$DB" "UPDATE Devices SET Transport='tcp', Ip='127.0.0.1', DevicePort=14370, PollingInterval=15 WHERE Serial='$DEV_SERIAL'"
node /home/z/my-project/device-service-csharp/scripts/fake-tcp-device.mjs 14370 "$DEV_SERIAL" > /tmp/p63_fake.log 2>&1 &
FAKE_PID=$!
echo "   … منتظر supervisor (کانفیگ) + poll اول"
sleep 45
NOW_PUNCH=$(q "SELECT COUNT(*) FROM DevicePunchLogs")
MODE=$(q "SELECT Mode FROM Devices WHERE Serial='$DEV_SERIAL'")
[ "$NOW_PUNCH" -gt "$BASE_PUNCH" ] && ok "tcp: پانچ دستگاه ورود شد ($BASE_PUNCH→$NOW_PUNCH)" || bad "tcp پانچ ($BASE_PUNCH→$NOW_PUNCH)"
[ "$MODE" = "آنلاین" ] && ok "tcp: دستگاه آنلاین شد" || bad "tcp وضعیت: $MODE"

echo "== 5) دستور unlock از صف → TCP → ACK =="
curl -s -b "$JAR" -o /dev/null -d "deviceId=$DEV_ID" -d "type=unlock" -d "seconds=5" \
  -d "__RequestVerificationToken=$FTOK" \
  -H "X-Requested-With: XMLHttpRequest" "$BASE/Devices/Command"
sleep 22   # poll بعدی → اجرای دستور + رکورد 1012 پس از بازشدن درب
CMD_ID=$(q "SELECT Id FROM DeviceCommands WHERE DeviceId='$DEV_ID' AND Type='unlock' ORDER BY CreatedAt DESC LIMIT 1")
ACK=$(q "SELECT Status FROM DeviceCommands WHERE Id='$CMD_ID'")
RESULT=$(q "SELECT Result FROM DeviceCommands WHERE Id='$CMD_ID'")
[ "$ACK" = "acked" ] && ok "unlock: acked («$RESULT»)" || bad "unlock وضعیت: $ACK ($RESULT)"
P2=$(q "SELECT COUNT(*) FROM DevicePunchLogs")
[ "$P2" -gt "$NOW_PUNCH" ] && ok "unlock: رکورد پس از بازشدن درب رسید ($NOW_PUNCH→$P2)" || bad "رکورد پس از unlock ($NOW_PUNCH→$P2)"
grep -q "UNLOCK" /tmp/p63_fake.log && ok "شبیه‌ساز: UNLOCK دریافت شد" || bad "شبیه‌ساز UNLOCK"


echo "== 6) پل ANPR — پلاک مجاز =="
# موتور پارکینگ حالت‌پذیر است (آنتی‌پس‌بک) — خروج فقط از گیت خروجی (جهت = گیت دوربین)
curl -s --max-time 8 -X POST "$BASE/anpr/plate?SN=$CAM_OUT" \
  -H 'Content-Type: application/json' \
  -d "{\"plate\":\"$PLATE\",\"direction\":\"out\",\"confidence\":92}" -o /tmp/p63_exit.json
sleep 1
curl -s --max-time 8 -X POST "$BASE/anpr/plate?SN=$CAM_SERIAL" \
  -H 'Content-Type: application/json' \
  -d "{\"plate\":\"$PLATE\",\"direction\":\"in\",\"confidence\":92}" -o /tmp/p63_plate.json
grep -q '"ok" *: *true' /tmp/p63_plate.json && grep -q '"barrier" *: *"open"' /tmp/p63_plate.json \
  && ok "anpr: پلاک مجاز → barrier open ($(cat /tmp/p63_plate.json | head -c 120))" || bad "anpr پلاک: $(cat /tmp/p63_plate.json)"
curl -s --max-time 8 -X POST "$BASE/anpr/plate?SN=$CAM_SERIAL" \
  -H 'Content-Type: application/json' \
  -d '{"plate":"99ن999 ایران 99","direction":"in","confidence":92}' -o /tmp/p63_plate2.json
grep -q '"barrier" *: *"closed"' /tmp/p63_plate2.json \
  && ok "anpr: پلاک ناشناس → closed" || bad "anpr ناشناس: $(cat /tmp/p63_plate2.json)"

echo "== 7) بازگردانی کانفیگ دستگاه =="
sq "$DB" "UPDATE Devices SET Transport='adms', Ip='$DEV_ORIG_IP', DevicePort=NULL, PollingInterval=60 WHERE Serial='$DEV_SERIAL'"
kill $FAKE_PID 2>/dev/null
ok "بازگردانی transport=adms برای $DEV_SERIAL"

echo
echo "RESULT: PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ]
