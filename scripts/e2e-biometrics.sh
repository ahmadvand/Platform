#!/bin/bash
# ============================================================
# E2E داده‌های هویتی (بایومتریک):
#  وب ASP.NET Core (3000) + دو دستگاه شبیه‌سازی TCP (4370/4371) + گیت‌وی C#
#  ۱) رندر صفحه /Biometrics با سیدر
#  ۲) شبیه‌ساز: دریافت پرسنل dev5 → ساخت پرسنل جدید در دیتابیس + دریافت هویتی
#  ۳) گیت‌وی واقعی: get_users + get_user_bios از دستگاه ۱ → ذخیره در DB
#  ۴) انتقال: push_user_bios پرسنل ۱۰۱۲ به دستگاه ۲ → ثبت قالب‌ها روی دستگاه
# ============================================================
export PATH=/home/z/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
export Database__Provider=Sqlite TZ=Asia/Tehran ASPNETCORE_URLS=http://0.0.0.0:3000
WEB=/home/z/my-project/aspnet/src/HrAttendance.Web
GW=/home/z/my-project/device-service-csharp/HrDeviceGateway
FAKE=/home/z/my-project/device-service-csharp/scripts/fake-tcp-device.mjs

pkill -f HrAttendance.Web 2>/dev/null; pkill -f HrDeviceGateway 2>/dev/null; pkill -f fake-tcp-device 2>/dev/null
sleep 1
# فاز ۲۸ — ⚠️ حذف hr.db ممنوع است (سابقاً rm -f "$WEB/hr.db" کل سامانه را پاک می‌کرد!)
# اسکیما/سیدر خودترمیم است — PatchSchema ستون‌های جدید را خودش می‌افزاید

cd "$WEB"
setsid dotnet run --no-build --no-launch-profile > /tmp/web.log 2>&1 &
cd "$GW"
setsid dotnet bin/Debug/net8.0/HrDeviceGateway.dll \
  "--Gateway:Devices:0:Ip=127.0.0.1" "--Gateway:Devices:0:PollIntervalSec=15" \
  "--Gateway:Devices:1:Transport=tcp" "--Gateway:Devices:1:Serial=ZK-FD7-0311" \
  "--Gateway:Devices:1:Ip=127.0.0.1" "--Gateway:Devices:1:Port=4371" \
  "--Gateway:Devices:1:PollIntervalSec=15" \
  > /tmp/gw.log 2>&1 &
setsid node "$FAKE" 4370 ZK-U2000-0142 > /tmp/fake1.log 2>&1 &
setsid node "$FAKE" 4371 ZK-FD7-0311 > /tmp/fake2.log 2>&1 &

for i in $(seq 1 40); do curl -s -o /dev/null http://localhost:3000/ 2>/dev/null && break; sleep 1; done
echo "== web up =="

curl -s -c /tmp/acj.txt -X POST http://localhost:3000/api/auth/login \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' -o /dev/null
AT=$(curl -s -D - -o /dev/null -X POST http://localhost:3000/api/auth/login \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' | grep -io "set-cookie: hr_at=[^;]*" | head -1 | sed 's/[Ss]et-[Cc]ookie: hr_at=//')
CSRF=$(curl -s -b /tmp/acj.txt -c /tmp/acj.txt http://localhost:3000/Biometrics | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')

echo "== ۱) صفحه داده‌های هویتی =="
CODE=$(curl -s -o /tmp/bio1.html -w "%{http_code}" -b /tmp/acj.txt http://localhost:3000/Biometrics)
echo "GET /Biometrics → $CODE"
grep -o "داده‌های هویتی دستگاه‌ها" /tmp/bio1.html | head -1
grep -oE "قالب‌های هویتی ذخیره‌شده: <b>[^<]*" /tmp/bio1.html | head -1
grep -oE "پرسنل روی دستگاه‌ها: <b>[^<]*" /tmp/bio1.html | head -1

DEVID() { curl -s "http://localhost:3000/api/devices" -H "Authorization: Bearer $AT" | python3 -c "import sys,json;d=json.load(sys.stdin)['devices'];print([x for x in d if x['serial']=='$1'][0]['id'])"; }
DEV1=$(DEVID ZK-U2000-0142)
DEV2=$(DEVID ZK-FD7-0311)
DEV5=$(DEVID TM-A5-0773)
echo "DEV1=$DEV1 DEV2=$DEV2 DEV5=$DEV5"

# فاز ۲۱: کانفیگ پویا جای bootstrap خط فرمان می‌نشیند — پس دستگاه‌های شبیه‌ساز باید
# از مسیر رسمی (ویرایش دستگاه) به TCP + 127.0.0.1:437x برسند؛ همین Edit نسخه کانفیگ
# گیت‌وی را بالا می‌برد (BumpGatewayConfigAsync) و پولرها تا ~۳۰ ثانیه استارت می‌خورند.
devedit() { # $1=id $2=serial $3=port $4=name $5=kind $6=purpose [$7=ip]
  # توکن تازه + ذخیره کوکی چرخیده (-c) — وگرنه POST دوم antiforgery را رد می‌کند
  local TK=$(curl -s -b /tmp/acj.txt -c /tmp/acj.txt http://localhost:3000/Devices | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
  local IP=${7:-127.0.0.1}   # IP یکتا برای هر دستگاه (قاعده یکتایی IP فرم دستگاه)
  curl -s -b /tmp/acj.txt -c /tmp/acj.txt -o /dev/null -w "  edit $2 → %{http_code}\n" -X POST "http://localhost:3000/Devices/Edit/$1" \
    -H "RequestVerificationToken: $TK" \
    --data-urlencode "__RequestVerificationToken=$TK" \
    -d "name=$4&kind=$5&brand=ZKTeco&serial=$2&ip=$IP&location=سندباکس&purpose=$6&mode=آنلاین&pollingInterval=15&transport=tcp&devicePort=$3&commKey=0&active=true"
}
echo "-- تنظیم TCP شبیه‌سازها از مسیر رسمی (باعث bump کانفیگ گیت‌وی) --"
devedit "$DEV1" ZK-U2000-0142 4370 "ترمینال ورودی اصلی" "ترمینال اثر انگشت" "حضور و غیاب" 127.0.0.1
devedit "$DEV2" ZK-FD7-0311 4371 "تشخیص چهره گیت شرقی" "ترمینال تشخیص چهره" "کنترل تردد" 127.0.0.2
sleep 3

echo "== ۲) شبیه‌ساز: دریافت پرسنل کارگاه فنی (ساخت پرسنل جدید در دیتابیس) =="
curl -s -b /tmp/acj.txt -o /dev/null -w "simulate users dev5: %{http_code}\n" -X POST http://localhost:3000/Biometrics/SimulateFetch \
  -d "deviceId=$DEV5&mode=users&__RequestVerificationToken=$CSRF"
curl -s -b /tmp/acj.txt -o /tmp/bio2.html http://localhost:3000/Biometrics
grep -c "9901" /tmp/bio2.html | xargs echo "occurrences of 9901:"
grep -oE "ناصر رضایی" /tmp/bio2.html | head -1

echo "== شبیه‌ساز: دریافت اطلاعات هویتی کارگاه فنی =="
curl -s -b /tmp/acj.txt -o /dev/null -w "simulate bios dev5: %{http_code}\n" -X POST http://localhost:3000/Biometrics/SimulateFetch \
  -d "deviceId=$DEV5&mode=bios&__RequestVerificationToken=$CSRF"
curl -s -b /tmp/acj.txt -o /tmp/bio3.html http://localhost:3000/Biometrics
grep -oE "قالب‌های هویتی ذخیره‌شده: <b>[^<]*" /tmp/bio3.html | head -1
grep -oE "پرسنل دارای قالب: <b>[^<]*" /tmp/bio3.html | head -1

echo "== ۳) گیت‌وی واقعی: دریافت پرسنل + هویتی از دستگاه ۱ (TCP) =="
curl -s -b /tmp/acj.txt -o /dev/null -w "fetch users dev1: %{http_code}\n" -X POST http://localhost:3000/Biometrics/FetchUsers \
  -d "deviceId=$DEV1&__RequestVerificationToken=$CSRF"
curl -s -b /tmp/acj.txt -o /dev/null -w "fetch bios dev1: %{http_code}\n" -X POST http://localhost:3000/Biometrics/FetchBios \
  -d "deviceId=$DEV1&__RequestVerificationToken=$CSRF"
echo "انتظار ۵۰ ثانیه برای دریافت کانفیگ پویا + چرخه poll گیت‌وی…"
sleep 50
grep -E "get_users|get_user_bios|USERTEMP|کاربر|قالب" /tmp/gw.log | head -12
curl -s -b /tmp/acj.txt -o /tmp/bio4.html http://localhost:3000/Biometrics
echo "-- پرسنل جدید از دستگاه واقعی (9901 ناصر رضایی):"
grep -c "9901" /tmp/bio4.html | xargs echo "occurrences of 9901:"
grep -oE "قالب‌های هویتی ذخیره‌شده: <b>[^<]*" /tmp/bio4.html | head -1

echo "== ۴) انتقال پرسنل ۱۰۱۲ + قالب‌هایش به دستگاه ۲ =="
EMP_ID=$(python3 - <<'EOF'
import re, html
txt = html.unescape(open('/tmp/bio4.html', encoding='utf-8').read())
m = re.search(r'<option value="([a-f0-9]{32})">\s*۱۰۱۲\s*—\s*امیر محمدی', txt)
print(m.group(1) if m else "")
EOF
)
echo "EMP(1012 امیر محمدی)=$EMP_ID"
curl -s -b /tmp/acj.txt -o /dev/null -w "push to dev2: %{http_code}\n" -X POST http://localhost:3000/Biometrics/PushToDevices \
  -d "employeeIds=$EMP_ID&toDeviceIds=$DEV2&includeBios=true&__RequestVerificationToken=$CSRF"
echo "انتظار ۴۵ ثانیه برای اجرای push_user_bios روی دستگاه ۲…"
sleep 45
echo "-- لاگ دستگاه ۲ (باید USER_WRQ + USERTEMP_WRQ داشته باشد):"
grep -E "USER_WRQ|USERTEMP_WRQ" /tmp/fake2.log | head -8
echo "-- نتیجه دستور در پنل:"
curl -s -b /tmp/acj.txt -o /tmp/bio5.html http://localhost:3000/Biometrics
python3 - <<'EOF'
import re, html
txt = html.unescape(open('/tmp/bio5.html', encoding='utf-8').read())
hits = re.findall(r'[^<>]*قالب هویتی روی دستگاه ثبت شد', txt)
print("ACK:", hits[0].strip() if hits else "NOT FOUND")
hits2 = re.findall(r'کاربر 1012[^<>]*', txt)
print("PUSH_USER:", hits2[0].strip() if hits2 else "-")
EOF

pkill -f HrDeviceGateway 2>/dev/null; pkill -f fake-tcp-device 2>/dev/null
# فاز ۲۸ — وب‌سرور زنده نگه داشته می‌شود (قبلاً pkill -f HrAttendance.Web سامانه را می‌کشت)
echo "== done =="
