#!/bin/bash
# ============================================================
# E2E یکپارچگی کامل:
#  وب ASP.NET Core (3000) + دستگاه شبیه‌سازی TCP (4370) + سرویس ویندوزی C# گیت‌وی
# ============================================================
export PATH=/home/z/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
export Database__Provider=Sqlite TZ=Asia/Tehran ASPNETCORE_URLS=http://0.0.0.0:3000
WEB=/home/z/my-project/aspnet/src/HrAttendance.Web
GW=/home/z/my-project/device-service-csharp/HrDeviceGateway
FAKE=/home/z/my-project/device-service-csharp/scripts/fake-tcp-device.mjs

cd "$WEB"
setsid dotnet run --no-build --no-launch-profile > /tmp/web.log 2>&1 &
cd "$GW"
setsid dotnet bin/Debug/net8.0/HrDeviceGateway.dll \
  "--Gateway:Devices:0:Ip=127.0.0.1" "--Gateway:Devices:0:PollIntervalSec=15" \
  > /tmp/gw.log 2>&1 &
setsid node "$FAKE" 4370 ZK-U2000-0142 > /tmp/fake.log 2>&1 &

# wait for web
for i in $(seq 1 40); do curl -s -o /dev/null http://localhost:3000/ 2>/dev/null && break; sleep 1; done
echo "== web up =="

# admin session
curl -s -c /tmp/acj.txt -X POST http://localhost:3000/api/auth/login \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' -o /dev/null
AT=$(curl -s -D - -o /dev/null -X POST http://localhost:3000/api/auth/login \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' | grep -io "set-cookie: hr_at=[^;]*" | head -1 | sed 's/[Ss]et-[Cc]ookie: hr_at=//')

echo "== صف دستور unlock از پنل وب (فرم MVC) =="
DEV_ID=$(curl -s http://localhost:3000/api/devices -H "Authorization: Bearer $AT" | python3 -c "import sys,json;d=json.load(sys.stdin)['devices'];print([x for x in d if x['serial']=='ZK-U2000-0142'][0]['id'])")
CSRF=$(curl -s -b /tmp/acj.txt -c /tmp/acj.txt http://localhost:3000/Devices | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
curl -s -b /tmp/acj.txt -X POST http://localhost:3000/Devices/Command \
  -d "deviceId=$DEV_ID&type=unlock&seconds=5&__RequestVerificationToken=$CSRF" -o /dev/null -w "queue unlock: %{http_code}\n"

echo "== انتظار ۳۰ ثانیه برای چرخه poll گیت‌وی (اتصال TCP → دستورات → رکوردها → وضعیت) =="
sleep 30

echo "== لاگ گیت‌وی =="
grep -E "✓|✗|دستور|رکورد|ورود|آفلاین|unlock|کاربر" /tmp/gw.log | head -16

echo "== وضعیت دستگاه از سرور (باید آنلاین + firmware باشد) =="
curl -s http://localhost:3000/api/devices -H "Authorization: Bearer $AT" | python3 -m json.tool | grep -A6 "ZK-U2000-0142" | head -8

echo "== وضعیت دستور unlock در لاگ سرور (باید acked/اجرا شد باشد) =="
curl -s -b /tmp/acj.txt http://localhost:3000/Devices | grep -o 'اجرا شد (TCP)' | head -1
curl -s -b /tmp/acj.txt http://localhost:3000/Devices | grep -o 'درب به مدت ۵ ثانیه باز شد' | head -1

echo "== لاگ وب: درخواست‌های webhook دریافتی =="
grep -E "webhook|devices" /tmp/web.log | head -4

# فاز ۲۸ — وب‌سرور زنده نگه داشته می‌شود؛ فقط گیت‌وی و دستگاه‌های شبیه‌سازی متوقف می‌شوند
pkill -f HrDeviceGateway 2>/dev/null; pkill -f fake-tcp-device 2>/dev/null
echo "== done =="
