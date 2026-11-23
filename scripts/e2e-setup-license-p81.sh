#!/bin/bash
# e2e فاز ۸۱ — خوداتکای مجوز در ویزارد راه‌اندازی اولیه (پورت ۳۹۹۹)
# شروع نمونهٔ ایزوله → GET /Setup → GenerateKeyPair → IssueLicense → راستی‌آزمایی رمزنگارانه → خاموشی
set -u
export PATH="/home/z/.dotnet:$PATH"
APP=/home/z/my-project/tmp-p81/app
WORK=/home/z/my-project/tmp-p81
LOG=$WORK/instance.log
cd "$APP"
export ASPNETCORE_URLS=http://127.0.0.1:3999 ASPNETCORE_ENVIRONMENT=Production Database__Provider=Sqlite Setup__Configured=false TZ=Asia/Tehran
rm -f "$WORK/cookies.txt" "$WORK/setup.html" "$WORK/genkey.json" "$WORK/issue.json" "$WORK/license-issued.dat"
dotnet HrAttendance.Web.dll > "$LOG" 2>&1 &
APP_PID=$!
trap 'kill $APP_PID 2>/dev/null' EXIT

# انتظار برای سلامت
code=000
for i in $(seq 1 30); do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 http://127.0.0.1:3999/health 2>/dev/null)
  [ "$code" = "200" ] && break
  sleep 2
done
echo "health=$code"
[ "$code" != "200" ] && { echo "FAIL: instance not healthy"; tail -5 "$LOG"; exit 1; }

# ۱) صفحهٔ راه‌اندازی + توکن ضدجعل
http=$(curl -s --max-time 30 -c "$WORK/cookies.txt" http://127.0.0.1:3999/Setup -o "$WORK/setup.html" -w "%{http_code}")
echo "setup_page=$http  new_block=$(grep -c 'صدور مجوز در همین لحظه' "$WORK/setup.html")"
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' "$WORK/setup.html" | head -1 | sed 's/.*value="//; s/"$//')
echo "token_len=${#TOKEN}"

# ۲) ساخت جفت‌کلید
http=$(curl -s --max-time 90 -b "$WORK/cookies.txt" -H "RequestVerificationToken: $TOKEN" \
  -H "X-Requested-With: XMLHttpRequest" -X POST http://127.0.0.1:3999/Setup/GenerateKeyPair \
  -o "$WORK/genkey.json" -w "%{http_code}")
echo "genkey_http=$http"
python3 - <<'PY'
import json
d = json.load(open('/home/z/my-project/tmp-p81/genkey.json'))
print('genkey ok:', d.get('ok'), '| fp:', d.get('fingerprint'))
pem = d.get('privatePem','')
print('privatePem head:', pem[:30] if pem else None)
open('/home/z/my-project/tmp-p81/private.pem','w').write(pem)
PY
[ "$(python3 -c "import json;print(json.load(open('$WORK/genkey.json'))['ok'])")" != "True" ] && { echo FAIL-GENKEY; exit 1; }

# ۳) صدور و فعال‌سازی مجوز
http=$(curl -s --max-time 90 -b "$WORK/cookies.txt" -H "RequestVerificationToken: $TOKEN" \
  -H "X-Requested-With: XMLHttpRequest" -X POST http://127.0.0.1:3999/Setup/IssueLicense \
  --data-urlencode "licensee=شرکت آزمایشی فاز ۸۱" --data-urlencode "validDays=365" \
  --data-urlencode "maxEmployees=50" -o "$WORK/issue.json" -w "%{http_code}")
echo "issue_http=$http"
python3 - <<'PY'
import json, base64
d = json.load(open('/home/z/my-project/tmp-p81/issue.json'))
print('issue ok:', d.get('ok'))
print('issue message:', d.get('message'))
c = d.get('licenseContent','')
open('/home/z/my-project/tmp-p81/license-issued.dat','w').write(c)
body, sig = c.split('.', 1)
payload = json.loads(base64.b64decode(body))
print('payload.Licensee:', payload.get('licensee') or payload.get('Licensee'))
print('payload.Product:', payload.get('product') or payload.get('Product'))
print('payload.ExpiresAtUtc:', payload.get('expiresAtUtc') or payload.get('ExpiresAtUtc'))
print('payload.MaxEmployees:', payload.get('maxEmployees') or payload.get('MaxEmployees'))
print('payload.Machine set?', bool(payload.get('machine') or payload.get('Machine')))
PY
[ "$(python3 -c "import json;print(json.load(open('$WORK/issue.json'))['ok'])")" != "True" ] && { echo FAIL-ISSUE; exit 1; }

# ۴) فایل‌های سمت سرور
echo "files: $(ls -la "$APP/App_Data/license-keys/" 2>/dev/null | grep -c pem) pem files; license.dat: $(test -f "$APP/App_Data/license.dat" && echo EXISTS || echo MISSING)"

# ۵) راستی‌آزمایی رمزنگارانه: امضا با کلید عمومیِ همین نصب
python3 - <<'PY'
import base64, json, subprocess
c = open('/home/z/my-project/tmp-p81/license-issued.dat').read()
body, sig = c.split('.', 1)
open('/home/z/my-project/tmp-p81/payload.bin','wb').write(base64.b64decode(body))
open('/home/z/my-project/tmp-p81/sig.bin','wb').write(base64.b64decode(sig))
# کلید عمومی نصب (SPKI PEM) سمت سرور ذخیره شده
pub = open('/home/z/my-project/tmp-p81/app/App_Data/license-keys/public.pem').read()
open('/home/z/my-project/tmp-p81/public.pem','w').write(pub)
r = subprocess.run(['openssl','dgst','-sha256','-verify','/home/z/my-project/tmp-p81/public.pem',
                    '-signature','/home/z/my-project/tmp-p81/sig.bin','/home/z/my-project/tmp-p81/payload.bin'],
                   capture_output=True, text=True)
print('openssl verify:', r.stdout.strip() or r.stderr.strip())
PY

# ۶) خوانش مجوز توسط همان سرویس (Evaluate از طریق صفحهٔ status داخلی نیست؛ لاگ را چک می‌کنیم)
grep -o "مجوز خوداتکا[^\"*]*" "$LOG" | tail -2
grep -o "مجوز نصب ذخیره شد[^\"*]*" "$LOG" | tail -2

echo "E2E-DONE"
