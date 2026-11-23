#!/bin/bash
# E2E verification: ASP.NET Core web app + HrDeviceGateway contract + fake TCP device
export PATH=/home/z/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
export Database__Provider=Sqlite TZ=Asia/Tehran ASPNETCORE_URLS=http://0.0.0.0:3000
WEB=/home/z/my-project/aspnet/src/HrAttendance.Web
LOG=/home/z/my-project/aspnet/e2e.log

cd "$WEB"
setsid dotnet run --no-build --no-launch-profile > "$LOG" 2>&1 &
SRV=$!

# wait for port
for i in $(seq 1 40); do
  curl -s -o /dev/null http://localhost:3000/ 2>/dev/null && break
  sleep 1
done
echo "== server up =="
curl -s -o /dev/null -w "root unauth: %{http_code} → %{redirect_url}\n" http://localhost:3000/

echo "== 1) API login contract (svc_bridge) =="
curl -s -D /tmp/lh.txt -X POST http://localhost:3000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"svc_bridge","password":"Bridge@1234"}' | head -c 260
echo ""
grep -io "set-cookie: hr_at=[^;]\{25\}" /tmp/lh.txt | head -1
BT=$(grep -io "set-cookie: hr_at=[^;]*" /tmp/lh.txt | head -1 | sed 's/[Ss]et-[Cc]ookie: hr_at=//')

echo "== 2) wrong password → expect 401 =="
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:3000/api/auth/login \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"nope"}'

echo "== 3) GET /api/devices (Bearer) =="
curl -s http://localhost:3000/api/devices -H "Authorization: Bearer $BT" | head -c 400
echo ""

echo "== 4) webhook GET handshake (queue empty → ok) =="
curl -s "http://localhost:3000/api/devices/webhook?SN=ZK-U2000-0142" -H "Authorization: Bearer $BT" | head -c 200
echo ""

echo "== 5) webhook POST records (2 punches, one unknown pin) =="
curl -s -X POST http://localhost:3000/api/devices/webhook \
  -H "Authorization: Bearer $BT" -H "Content-Type: application/json" \
  -d '{"SN":"ZK-U2000-0142","event":"records","data":{"punches":[{"pin":"1001","timestamp":"2026-10-18 07:58:12","status":"in"},{"pin":"1028","timestamp":"2026-10-18 08:21:40","status":"in"},{"pin":"9999","timestamp":"2026-10-18 08:22:00","status":"in"}]}}'
echo ""

echo "== 6) webhook POST status =="
curl -s -X POST http://localhost:3000/api/devices/webhook \
  -H "Authorization: Bearer $BT" -H "Content-Type: application/json" \
  -d '{"SN":"ZK-U2000-0142","event":"status","data":{"firmware":"ver8.0.4.1-20260101","userCount":28,"online":true}}'
echo ""

echo "== 7) queue unlock command (admin) =="
curl -s -D /tmp/ah.txt -X POST http://localhost:3000/api/auth/login \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' > /dev/null
AT=$(grep -io "set-cookie: hr_at=[^;]*" /tmp/ah.txt | head -1 | sed 's/[Ss]et-[Cc]ookie: hr_at=//')
DEV_ID=$(curl -s http://localhost:3000/api/devices -H "Authorization: Bearer $AT" | python3 -c "import sys,json;d=json.load(sys.stdin)['devices'];print([x for x in d if x['serial']=='ZK-U2000-0142'][0]['id'])")
echo "device id: $DEV_ID"
# MVC form: queue unlock via API? — commands are queued via MVC; emulate via admin session cookie instead:
curl -s -c /tmp/acj.txt -X POST http://localhost:3000/api/auth/login -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' -o /dev/null
CSRF=$(curl -s -b /tmp/acj.txt -c /tmp/acj.txt http://localhost:3000/Devices | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
curl -s -b /tmp/acj.txt -X POST http://localhost:3000/Devices/Command \
  -d "deviceId=$DEV_ID&type=unlock&seconds=5&__RequestVerificationToken=$CSRF" -o /dev/null -w "queue unlock: %{http_code}\n"

echo "== 8) handshake now delivers pending command =="
CMDS=$(curl -s "http://localhost:3000/api/devices/webhook?SN=ZK-U2000-0142" -H "Authorization: Bearer $BT")
echo "$CMDS" | head -c 400
echo ""
CMD_ID=$(echo "$CMDS" | python3 -c "import sys,json;c=json.load(sys.stdin)['commands'];print(c[0]['id'] if c else '')")
if [ -n "$CMD_ID" ]; then
  echo "== 9) device ACKs command ($CMD_ID) =="
  curl -s -X POST http://localhost:3000/api/devices/webhook \
    -H "Authorization: Bearer $BT" -H "Content-Type: application/json" \
    -d "{\"SN\":\"ZK-U2000-0142\",\"event\":\"ack\",\"data\":{\"commandId\":\"$CMD_ID\",\"ok\":true,\"result\":\"درب به مدت 5 ثانیه باز شد\"}}"
  echo ""
fi

echo "== 10) pages render (admin cookie) =="
for p in / /Employees /Org /Attendance /Requests /Devices /Access /Visitors /Payroll /Holidays /Users /Audit; do
  code=$(curl -s -b /tmp/acj.txt -o /dev/null -w "%{http_code}" "http://localhost:3000$p")
  echo "$p → $code"
done

echo "== 11) permission enforcement (viewer on /Users → denied) =="
curl -s -c /tmp/vcj.txt -X POST http://localhost:3000/api/auth/login -H "Content-Type: application/json" -d '{"username":"viewer","password":"View@1234"}' -o /dev/null
curl -s -b /tmp/vcj.txt -o /dev/null -w "/Users(viewer): %{http_code} → %{redirect_url}\n" http://localhost:3000/Users
curl -s -b /tmp/vcj.txt -o /dev/null -w "/Employees(viewer): %{http_code}\n" http://localhost:3000/Employees

echo "== 12) excel export =="
curl -s -b /tmp/acj.txt -o /tmp/emp.xlsx -w "employees.xlsx: %{http_code} %{size_download}B\n" http://localhost:3000/Export/Employees
file /tmp/emp.xlsx | head -1

echo "== 13) punch via MVC =="
curl -s -b /tmp/acj.txt -c /tmp/acj.txt http://localhost:3000/Attendance | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 > /tmp/tok.txt
CSRF2=$(sed 's/.*value="//;s/"$//' /tmp/tok.txt)
curl -s -b /tmp/acj.txt -c /tmp/acj.txt -X POST http://localhost:3000/Attendance/Punch \
  -d "personnelCode=1003&kind=in&__RequestVerificationToken=$CSRF2" -o /dev/null -w "punch: %{http_code}\n"
curl -s -b /tmp/acj.txt "http://localhost:3000/Attendance" | grep -c "1003" | sed 's/^/punch visible for 1003: /'

echo "== server log tail =="
grep -iE "warn|error|fail" "$LOG" | grep -v "No XML encryptor" | head -5
# فاز ۲۸ — نمونهٔ وب خودِ سوییت زنده نگه داشته می‌شود (پورت ۳۰۰۰ فعال می‌ماند)
echo "== done =="
