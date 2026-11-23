#!/bin/bash
# End-to-end test of the /Setup wizard Save flow (فاز ۸۸ regression test)
# فاز ۹۰ — + تست ورود کلید خصوصی (ImportKeyPair) + بررسی ساختار ویزارد دومرحله‌ای
set -e
BASE=http://127.0.0.1:3997
JAR=/home/z/my-project/wiztest-tmp/cookies.txt
rm -f $JAR

echo "=== 1) GET /Setup ==="
curl -s -c $JAR -o /home/z/my-project/wiztest-tmp/setup.html -w "%{http_code}\n" $BASE/Setup
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' /home/z/my-project/wiztest-tmp/setup.html | head -1 | sed 's/.*value="//;s/"$//')
MACHINE=$(grep -o 'id="machineId">[^<"]*' /home/z/my-project/wiztest-tmp/setup.html | head -1 | sed 's/.*>//')
echo "token len: ${#TOKEN}  machine: $MACHINE"
if [ -z "$TOKEN" ]; then echo "NO ANTIFORGERY TOKEN FOUND"; grep -o 'RequestVerificationToken[^>]*' /home/z/my-project/wiztest-tmp/setup.html | head -3; exit 1; fi

echo "=== 1b) ساختار ویزارد دومرحله‌ای + پورت پیش‌فرض + کادر ورود کلید ==="
grep -c 'id="wizPane1"' /home/z/my-project/wiztest-tmp/setup.html | xargs echo "wizPane1 count:"
grep -c 'id="wizPane2"' /home/z/my-project/wiztest-tmp/setup.html | xargs echo "wizPane2 count:"
grep -c 'id="keyImportFile"' /home/z/my-project/wiztest-tmp/setup.html | xargs echo "keyImportFile count:"
grep -o 'name="port" value="[^"]*"' /home/z/my-project/wiztest-tmp/setup.html | head -1
grep -c 'btnToStep2\|btnBackStep1' /home/z/my-project/wiztest-tmp/setup.html | xargs echo "wiz buttons:"

echo "=== 2) POST /Setup/GenerateKeyPair ==="
curl -s -b $JAR -c $JAR -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" \
  $BASE/Setup/GenerateKeyPair > /home/z/my-project/wiztest-tmp/keypair.json
python3 - <<'EOF'
import json
d = json.load(open('/home/z/my-project/wiztest-tmp/keypair.json'))
assert d.get('ok'), d
pem = d['privatePem']
open('/home/z/my-project/wiztest-tmp/private.pem','w').write(pem)
print('keypair ok, fingerprint:', d.get('fingerprint','')[:40])
EOF

echo "=== 2b) POST /Setup/ImportKeyPair (private.pem ساخته‌شده در جای دیگر) ==="
curl -s -b $JAR -c $JAR -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" \
  --data-urlencode "privatePem@/home/z/my-project/wiztest-tmp/private.pem" \
  $BASE/Setup/ImportKeyPair > /home/z/my-project/wiztest-tmp/import.json
python3 - <<'EOF'
import json
d = json.load(open('/home/z/my-project/wiztest-tmp/import.json'))
print('import:', d)
assert d.get('ok'), d
assert d.get('fingerprint'), d
print('import ok, fingerprint:', d['fingerprint'])
EOF

echo "=== 3) LicenseGen issue ==="
MACHINE=$(grep -o 'id="machineId">[^<"]*' /home/z/my-project/wiztest-tmp/setup.html | head -1 | sed 's/.*>//')
if [ -z "$MACHINE" ]; then
  # fallback: machine id format may differ; try to find it in the page near "اثر انگشت"
  MACHINE=$(grep -oE '[A-Z0-9]{5}(-[A-Z0-9]{5}){4}' /home/z/my-project/wiztest-tmp/setup.html | head -1)
fi
echo "machine id for license: $MACHINE"
/home/z/.dotnet/dotnet /home/z/my-project/tools/LicenseGen/bin/Debug/net8.0/LicenseGen.dll issue \
  --key /home/z/my-project/wiztest-tmp/private.pem --licensee "Test Corp" --machine "$MACHINE" \
  --expires 2027-01-01 --out /home/z/my-project/wiztest-tmp/license.dat 2>&1
head -c 80 /home/z/my-project/wiztest-tmp/license.dat; echo

echo "=== 4) POST /Setup/Save (SQLite + license) ==="
LIC=$(cat /home/z/my-project/wiztest-tmp/license.dat)
curl -s -b $JAR -c $JAR -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" \
  --data-urlencode "provider=Sqlite" \
  --data-urlencode "dbPath=hr.db" \
  --data-urlencode "seedDemo=true" \
  --data-urlencode "uiLanguage=fa" \
  --data-urlencode "licenseContent=$LIC" \
  $BASE/Setup/Save > /home/z/my-project/wiztest-tmp/save.json
cat /home/z/my-project/wiztest-tmp/save.json; echo
