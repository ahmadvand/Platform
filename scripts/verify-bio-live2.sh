#!/bin/bash
# تست درست چرخه هویتی با فرم‌های MVC + antiforgery
B=http://localhost:3000
J=/tmp/hr-cookies.txt
rm -f $J
UA="Mozilla/5.0"

curl -s -c $J -b $J -X POST "$B/api/auth/login" -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@1234"}' -o /dev/null

PAGE=$(curl -s -b $J -c $J -A "$UA" "$B/Biometrics")
TOKEN=$(echo "$PAGE" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')
echo "token: ${TOKEN:0:20}..."

echo "== 1) شبیه‌ساز دریافت پرسنل+هویتی از dev4 (خالی) =="
curl -s -b $J -c $J -A "$UA" -X POST "$B/Biometrics/SimulateFetch" \
  -d "deviceId=dev4&mode=both&__RequestVerificationToken=$TOKEN" -o /tmp/sim.html -w "HTTP %{http_code}\n"
python3 -c "
import html,re
t=html.unescape(open('/tmp/sim.html').read())
m=re.findall(r'(۹۹۰[۰-۹]|990[0-9]|شبیه|موفق|با موفقیت|قالب)', t)
print('  نشانه‌ها:', sorted(set(m))[:10])
print('  9901 در صفحه:', '9901' in t or '۹۹۰۱' in t)
"

echo "== 2) شمار پرسنل per-device بعد از دریافت =="
python3 -c "
import re
t=open('/tmp/sim.html').read()
print('  data-pin rows:', len(re.findall(r'data-pin=\"', t)))
print('  select employee ids:', len(set(re.findall(r'name=\"employeeIds\"[^>]*value=\"([^\"]+)\"', t))) or len(set(re.findall(r'value=\"([0-9a-f-]{20,})\"', t))))
"

echo "== 3) انتقال: دو پرسنل → dev5 =="
EIDS=$(python3 -c "
import re,html
t=html.unescape(open('/tmp/sim.html').read())
ids=re.findall(r'name=\"employeeIds\"[^>]*value=\"([^\"]+)\"',t) or re.findall(r'data-employee-id=\"([^\"]+)\"',t)
print(' '.join(ids[:2]))")
echo "  employeeIds: $EIDS"
ARGS=""
for e in $EIDS; do ARGS="$ARGS -d employeeIds=$e"; done
curl -s -b $J -c $J -A "$UA" -X POST "$B/Biometrics/PushToDevices" \
  $ARGS -d "toDeviceIds=dev5&includeBios=true&__RequestVerificationToken=$TOKEN" \
  -o /tmp/push.html -w "HTTP %{http_code}\n"
python3 -c "
import html,re
t=html.unescape(open('/tmp/push.html').read())
m=[x for x in re.findall(r'(انتقال|قالب|انجام شد|موفق|خطا)', t)]
print('  نشانه‌ها:', sorted(set(m)))
"
