#!/usr/bin/env bash
# ======================================================================
# فاز ۹۲ — تست E2E کامل: سوییچ زبان ویزارد، اعلان‌ها، به‌روزرسانی، آیکن‌ها، کنسول مرورگر
# یک نمونهٔ تازه (پورت ۳998) با App_Data خالی — دیتابیس نمونهٔ اصلی دست نمی‌خورد.
# ======================================================================
set -u
export PATH="$HOME/.dotnet:$PATH:$PATH"
FRESH=/home/z/my-project/wiztest92
T=/home/z/my-project/wiztest-tmp
SRC=/home/z/my-project/aspnet/src/Platform.Web
mkdir -p $T
pkill -f "Platform.Web.dll --urls=http://127.0.0.1:39" 2>/dev/null
sleep 1
rm -rf $FRESH; mkdir -p $FRESH/App_Data
cp -r $SRC/bin/Debug/net8.0 $FRESH/bin
cp -r $SRC/wwwroot $FRESH/wwwroot
cp $SRC/appsettings.json $FRESH/
echo "== fresh instance at $FRESH =="

cd $FRESH
setsid dotnet bin/Platform.Web.dll --urls=http://127.0.0.1:3998 > $FRESH/server.log 2>&1 &
for i in $(seq 1 60); do curl -s -m 2 -o /dev/null http://127.0.0.1:3998/health && break; sleep 1; done
echo "health: $(curl -s -m 3 -o /dev/null -w '%{http_code}' http://127.0.0.1:3998/health)"

echo "== T1) setup page + whitelist /Ui/Lang + سوییچ زبان (FIX 1) =="
JAR=$T/cookies92.txt; rm -f $JAR
curl -s -c $JAR -o $T/setup92.html -w "GET /Setup → %{http_code}\n" http://127.0.0.1:3998/Setup
grep -c 'lang="fa"' $T/setup92.html | xargs echo "  setup fa html count:"
curl -s -b $JAR -c $JAR -o /dev/null -w "GET /Ui/Lang?l=en&r=/Setup → %{http_code} → %{redirect_url}\n" "http://127.0.0.1:3998/Ui/Lang?l=en&r=/Setup"
curl -s -b $JAR -c $JAR -o $T/setup92-en.html http://127.0.0.1:3998/Setup
grep -o 'lang="en" dir="ltr"' $T/setup92-en.html | head -1 | xargs echo "  en mode marker:"
grep -c 'wizPane1' $T/setup92-en.html | xargs echo "  en wizard pane1:"
curl -s -b $JAR -c $JAR -o /dev/null "http://127.0.0.1:3998/Ui/Lang?l=fa&r=/Setup"
curl -s -b $JAR -c $JAR -o $T/setup92-fa.html http://127.0.0.1:3998/Setup
grep -o 'lang="fa" dir="rtl"' $T/setup92-fa.html | head -1 | xargs echo "  back to fa marker:"

echo "== T2) تکمیل ویزارد (SQLite + مجوز + seedDemo) =="
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' $T/setup92-fa.html | head -1 | sed 's/.*value="//;s/"$//')
MACHINE=$(grep -o 'id="machineId">[^<"]*' $T/setup92-fa.html | head -1 | sed 's/.*>//')
[ -z "$MACHINE" ] && MACHINE=$(grep -oE '[A-Z0-9]{5}(-[A-Z0-9]{5}){4}' $T/setup92-fa.html | head -1)
echo "  token len ${#TOKEN}, machine $MACHINE"
curl -s -b $JAR -c $JAR -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" \
  http://127.0.0.1:3998/Setup/GenerateKeyPair > $T/keypair92.json
python3 -c "import json;d=json.load(open('$T/keypair92.json'));assert d.get('ok'),d;open('$T/priv92.pem','w').write(d['privatePem'])" && echo "  keypair ok"
dotnet /home/z/my-project/tools/LicenseGen/bin/Debug/net8.0/LicenseGen.dll issue \
  --key $T/priv92.pem --licensee "Phase92 Test" --machine "$MACHINE" --expires 2027-01-01 \
  --out $T/lic92.dat > /dev/null 2>&1 && echo "  license issued"
LIC=$(cat $T/lic92.dat)
curl -s -b $JAR -c $JAR -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" \
  --data-urlencode "provider=Sqlite" --data-urlencode "dbPath=hr.db" \
  --data-urlencode "seedDemo=true" --data-urlencode "uiLanguage=fa" \
  --data-urlencode "licenseContent=$LIC" \
  http://127.0.0.1:3998/Setup/Save > $T/save92.json
python3 -c "import json;d=json.load(open('$T/save92.json'));print('  save ok:', d.get('ok'))"
for i in $(seq 1 30); do curl -s -m 2 -o /dev/null http://127.0.0.1:3998/health && break; sleep 1; done

echo "== T3) ورود ادمین =="
curl -s -c $JAR -o $T/login92.html http://127.0.0.1:3998/Auth/Login
LT=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' $T/login92.html | head -1 | sed 's/.*value="//;s/"$//')
curl -s -b $JAR -c $JAR -o /dev/null -w "  POST /Auth/Login → %{http_code} → %{redirect_url}\n" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$LT" \
  --data-urlencode "username=admin" --data-urlencode "password=Admin@1234" \
  http://127.0.0.1:3998/Auth/Login
curl -s -b $JAR -c $JAR -o $T/home92.html -w "  GET / → %{http_code}\n" http://127.0.0.1:3998/

echo "== T4) صفحهٔ به‌روزرسانی (FIX 2) =="
curl -s -b $JAR -c $JAR -o $T/upd92.html -w "  GET /Updates → %{http_code}\n" http://127.0.0.1:3998/Updates
grep -c "به‌روزرسانی سامانه" $T/upd92.html | xargs echo "  page title count:"
grep -o 'نسخهٔ فعلی' $T/upd92.html | head -1 | xargs echo "  version chip:"

echo "== T5) بارگذاری + نصب بستهٔ به‌روزرسانی =="
python3 - <<'EOF'
import zipfile, json
with zipfile.ZipFile('/home/z/my-project/wiztest-tmp/upd92-pkg.zip', 'w') as z:
    z.writestr('manifest.json', json.dumps({
        "format": "hr-update-pack", "version": "9.9.9",
        "notes": {"fa": "بستهٔ آزمایشی فاز ۹۲", "en": "Phase92 test package"}}, ensure_ascii=False))
    z.writestr('wwwroot/test-update-marker.txt', 'phase92-marker')
    z.writestr('bin/test-update-dummy.bin', 'dummy')
print('package built')
EOF
UT=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' $T/upd92.html | head -1 | sed 's/.*value="//;s/"$//')
curl -s -b $JAR -c $JAR -o /dev/null -w "  POST /Updates/Upload → %{http_code} → %{redirect_url}\n" \
  -F "package=@$T/upd92-pkg.zip;type=application/zip" \
  -F "__RequestVerificationToken=$UT" \
  http://127.0.0.1:3998/Updates/Upload
curl -s -b $JAR -c $JAR -o $T/upd92b.html http://127.0.0.1:3998/Updates
grep -c "بستهٔ در صف نصب" $T/upd92b.html | xargs echo "  pending panel count:"
grep -c "۹.۹.۹" $T/upd92b.html | xargs echo "  version 9.9.9 (fa digits) count:"
curl -s -b $JAR -c $JAR -o /dev/null -w "  POST /Updates/Apply → %{http_code} → %{redirect_url}\n" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$UT" \
  http://127.0.0.1:3998/Updates/Apply
[ -f $FRESH/test-update-marker.txt ] && echo "  ✔ marker installed: $(cat $FRESH/test-update-marker.txt)" || echo "  ✖ marker NOT installed"
ls $FRESH/App_Data/backups/backup-*.zip > /dev/null 2>&1 && echo "  ✔ auto-backup created: $(ls $FRESH/App_Data/backups/ | head -1)" || echo "  ✖ no backup"
curl -s -b $JAR -c $JAR -o $T/upd92c.html http://127.0.0.1:3998/Updates
grep -c "پشتیبان‌های نصب" $T/upd92c.html | xargs echo "  backups section count:"
[ -f $FRESH/test-update-marker.txt ] && rm -f $FRESH/test-update-marker.txt

echo "== T6) اعلان‌ها (FIX 4) =="
AID=$(grep -o 'data-alert-id="[^"]*"' $T/home92.html | head -1 | sed 's/.*="//;s/"$//')
echo "  open alert id on dashboard: ${AID:-NONE}"
if [ -n "$AID" ]; then
  curl -s -b $JAR -c $JAR -H "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "__RequestVerificationToken=$UT" --data-urlencode "id=$AID" \
    http://127.0.0.1:3998/Notifications/ReadAlert > $T/read92.json
  cat $T/read92.json; echo
fi
curl -s -b $JAR -c $JAR -o $T/notif92.html -w "  GET /Notifications → %{http_code}\n" http://127.0.0.1:3998/Notifications
grep -c "هشدارهای باز" $T/notif92.html | xargs echo "  open-alerts card count:"
grep -c "لاگ اعلان‌ها" $T/notif92.html | xargs echo "  logs card count:"
grep -c "href=\"/Parking\"\|href=\"/Devices\"\|href=\"/Access?tab=alerts\"" $T/notif92.html | xargs echo "  alert links count:"

echo "== T7) زبان انگلیسی در پنل (en) =="
curl -s -b $JAR -c $JAR -o /dev/null "http://127.0.0.1:3998/Ui/Lang?l=en&r=/"
curl -s -b $JAR -c $JAR -o $T/home92en.html http://127.0.0.1:3998/
grep -o '<html lang="en" dir="ltr"' $T/home92en.html | head -1 | xargs echo "  en html:"
grep -c "__hrL10n" $T/home92en.html | xargs echo "  hrL10n dict count:"
grep -o '"Confirm action"' $T/home92en.html | head -1 | xargs echo "  JS dict translated sample:"
curl -s -b $JAR -c $JAR -o /dev/null "http://127.0.0.1:3998/Ui/Lang?l=fa&r=/"

echo "== T8) فایل‌های استاتیک — بدون 404 =="
python3 - <<'EOF'
import re, urllib.request, http.cookiejar
html = open('/home/z/my-project/wiztest-tmp/home92.html', encoding='utf-8').read()
urls = set(re.findall(r'(?:src|href)="(/[^"]+\.(?:css|js|woff2?|ico|png|svg))"', html))
urls.add('/favicon.ico')
bad = []
for u in sorted(urls):
    try:
        r = urllib.request.urlopen('http://127.0.0.1:3998' + u, timeout=5)
        print(('OK ' if r.status == 200 else f'{r.status} '), u)
        if r.status != 200: bad.append(u)
    except Exception as e:
        print('ERR', u, e); bad.append(u)
print('BAD-ASSETS:', bad if bad else 'none')
EOF
echo "== T8b) syntax JS =="
node --check $FRESH/wwwroot/js/site.js && echo "  site.js OK"
node --check $FRESH/wwwroot/js/formula-builder.js && echo "  formula-builder.js OK"

echo "== DONE (server still up for browser console check) =="
