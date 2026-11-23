#!/bin/bash
# ============================================================
# E2E فاز ۵۱ — جریان «هشدار به‌جای قطع» مجوز روی نمونهٔ ایزوله (پورت 3200)
# License:Mode=Strict + دورهٔ آزمایشی منقضی → هشدار /License/Notice → تایید → ادامه
# ============================================================
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WEB=/home/z/my-project/aspnet/src/HrAttendance.Web
cd "$WEB" || exit 1

E2E=/home/z/my-project/scripts/tmp-e2e
mkdir -p "$E2E"
rm -f "$E2E/e2e.db" "$E2E/e2e.db-"* 2>/dev/null

# دورهٔ آزمایشی منقضی (۴۰ روز پیش) — فایل امضاشدهٔ HMAC همان‌طور که LicenseService می‌نویسد.
# خودترمیم: بعد از ری‌بیلد سندباکس این فایل از بین می‌رود؛ اینجا خودکار ساخته می‌شود.
python3 - "$E2E/trial.json" <<'PY'
import hashlib, hmac, base64, json, datetime, sys
machine_id = open("/etc/machine-id").read().strip()
key = hashlib.sha256((machine_id + "|trial|HrAttendance|Licensing|v1").encode()).digest()
first = datetime.datetime.now(datetime.UTC).replace(tzinfo=None) - datetime.timedelta(days=40)
iso = first.strftime("%Y-%m-%dT%H:%M:%S.") + f"{first.microsecond * 10:07d}Z"
mac = base64.b64encode(hmac.new(key, iso.encode(), hashlib.sha256).digest()).decode()
open(sys.argv[1], "w").write(json.dumps({"First": iso, "Mac": mac}))
PY

setsid env \
  ASPNETCORE_URLS=http://127.0.0.1:3200 \
  ASPNETCORE_ENVIRONMENT=Production \
  Database__Provider=Sqlite \
  ConnectionStrings__Sqlite="Data Source=/home/z/my-project/scripts/tmp-e2e/e2e.db" \
  Setup__Configured=true \
  License__Mode=Strict \
  License__TrialDays=30 \
  License__TrialFile=/home/z/my-project/scripts/tmp-e2e/trial.json \
  /home/z/.dotnet/dotnet bin/Debug/net8.0/HrAttendance.Web.dll > "$E2E/e2e.log" 2>&1 &

for i in $(seq 1 60); do
  code=$(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:3200/Auth/Login --max-time 2)
  if [ "$code" = "200" ]; then echo "E2E instance UP (port 3200)"; break; fi
  sleep 1
done

J=/home/z/my-project/scripts/tmp-e2e
rm -f "$J/cookies.txt" "$J/body.html"

echo "=== ۱) ورود ادمین ==="
AF=$(curl -s -c "$J/cookies.txt" http://127.0.0.1:3200/Auth/Login | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//')
LOGIN=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/cookies.txt" -c "$J/cookies.txt" -X POST http://127.0.0.1:3200/Auth/Login \
  -d "identifier=admin" -d "secret=Admin@1234" \
  -d "__RequestVerificationToken=$AF")
echo "login POST → $LOGIN"

echo "=== ۲) ناوبری صفحهٔ کامل با مجوز قفل → باید به /License/Notice ریدایرکت شود ==="
RED=$(curl -s -o /dev/null -w "%{redirect_url}" -b "$J/cookies.txt" http://127.0.0.1:3200/)
echo "GET / → redirect: $RED"
NOTICE=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/cookies.txt" "$RED")
echo "GET $RED → $NOTICE (باید 200 باشد)"
curl -s -b "$J/cookies.txt" "$RED" | grep -o "فهمیدم — ادامه به سامانه" | head -1

echo "=== ۳) API حتی در حالت قفل باید زنده باشد ==="
APIC=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/cookies.txt" http://127.0.0.1:3200/api/anpr/webhook 2>/dev/null)
APIC2=$(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:3200/health)
echo "GET /api/... → $APIC | GET /health → $APIC2"

echo "=== ۴) تایید هشدار → ادامه به سامانه ==="
AF2=$(curl -s -b "$J/cookies.txt" "$RED" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
ACK=$(curl -s -o /dev/null -w "%{http_code}|%{redirect_url}" -b "$J/cookies.txt" -c "$J/cookies.txt" -X POST http://127.0.0.1:3200/License/Ack \
  -H "RequestVerificationToken: $AF2" \
  --data-urlencode "__RequestVerificationToken=$AF2")
echo "POST /License/Ack → $ACK"
HOME_C=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/cookies.txt" http://127.0.0.1:3200/)
echo "GET / پس از تایید → $HOME_C (باید 200 باشد)"

echo "=== ۵) ورود دوباره → کوکی تایید پاک می‌شود → هشدار دوباره نمایش داده شود ==="
rm -f "$J/cookies2.txt"
AF3=$(curl -s -b "$J/cookies2.txt" -c "$J/cookies2.txt" http://127.0.0.1:3200/Auth/Login | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//')
curl -s -o /dev/null -b "$J/cookies2.txt" -c "$J/cookies2.txt" -X POST http://127.0.0.1:3200/Auth/Login \
  -d "identifier=admin" -d "secret=Admin@1234" \
  -d "__RequestVerificationToken=$AF3"
RED2=$(curl -s -o /dev/null -w "%{redirect_url}" -b "$J/cookies2.txt" http://127.0.0.1:3200/)
echo "GET / پس از ورود دوباره → redirect: $RED2 (باید Notice باشد)"

echo "=== ۶) بنر هشدار بالای صفحه‌ها ==="
curl -s -b "$J/cookies.txt" http://127.0.0.1:3200/Traffic | grep -o 'lic-banner\|دورهٔ آزمایشی به پایان رسیده' | sort -u | head -3

echo "=== ۷) هشدار روی صفحهٔ ورود ==="
curl -s http://127.0.0.1:3200/Auth/Login | grep -o 'کارکرد سامانه قطع نشده' | head -1

echo "=== پاکسازی ==="
pkill -f "3200" 2>/dev/null
