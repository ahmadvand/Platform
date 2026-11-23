#!/bin/bash
# فاز ۱۰۶ — راستی‌آزمایی زنده: انتخابگر فصل + درون‌ریزی بستهٔ چهارفصل + payload + باگ رمز تکراری
set -euo pipefail
R=/home/z/my-project
T=/tmp/i106e2e; rm -rf "$T"; mkdir -p "$T"
B=http://localhost:3000
PKG=$R/download/HR-Screensaver-FourSeasons-Package-1.0.0.json

echo "== 1) login"
TOK=$(curl -s -c "$T/jar" "$B/Auth/Login" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
CODE=$(curl -s -b "$T/jar" -c "$T/jar" -o /dev/null -w "%{http_code}" -X POST "$B/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOK")
echo "login -> $CODE"; [ "$CODE" = "302" ]

REDIR=$(curl -s -b "$T/jar" -o /dev/null -w "%{redirect_url}" "$B/Settings")
if [[ "$REDIR" == *"/License/Notice"* ]]; then
  ATOK=$(curl -s -b "$T/jar" -c "$T/jar" "$B/License/Notice" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
  curl -s -b "$T/jar" -c "$T/jar" -o /dev/null -X POST "$B/License/Ack" \
    --data-urlencode "__RequestVerificationToken=$ATOK"
  echo "license ack done"
fi

echo "== 2) settings UI: season selector present"
curl -s -b "$T/jar" -c "$T/jar" "$B/Settings" -o "$T/settings.html"
python3 - "$T/settings.html" <<'PY'
import sys, io, re
h = io.open(sys.argv[1], encoding='utf-8').read()
assert 'name="ssSeason"' in h, 'ssSeason select missing'
for v in ('auto', 'spring', 'summer', 'autumn', 'winter'):
    assert f'value="{v}"' in h, 'option missing: ' + v
m = re.search(r'name="ssSeason"', h)
print('ssSeason selector OK (5 options)')
PY

echo "== 3) save manual season = winter"
python3 "$R/scripts/i100-parse-form.py" "$T/settings.html" > "$T/body.txt"
python3 - "$T/body.txt" <<'PY'
import sys, io, urllib.parse
pairs = urllib.parse.parse_qsl(io.open(sys.argv[1], encoding='utf-8').read(), keep_blank_values=True)
out = [(k, 'winter' if k == 'ssSeason' else v) for k, v in pairs]
io.open(sys.argv[1], 'w', encoding='utf-8').write(urllib.parse.urlencode(out, encoding='utf-8'))
print('ssSeason patched -> winter')
PY
CODE=$(curl -s -b "$T/jar" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/SaveGeneral" \
  -H "Content-Type: application/x-www-form-urlencoded" --data-binary @"$T/body.txt")
echo "SaveGeneral -> $CODE"; [ "$CODE" = "302" ]

echo "== 4) DB check: ScreenSaverSeason persisted"
python3 - <<'PY'
import sqlite3
con = sqlite3.connect('/home/z/my-project/aspnet/sc-run/hr.db')
v = con.execute("SELECT ScreenSaverSeason FROM AppSettings WHERE Id='default'").fetchone()[0]
assert v == 'winter', 'season not persisted: ' + str(v)
print('DB ScreenSaverSeason =', v)
PY

echo "== 5) import four-season package (applySettings)"
CTOK=$(curl -s -b "$T/jar" -c "$T/jar" "$B/Settings" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
CODE=$(curl -s -b "$T/jar" -o "$T/imp.html" -w "%{http_code}" -X POST "$B/Settings/ImportScreensaverPackage" \
  -F "file=@$PKG;type=application/json" -F "applySettings=true" \
  -F "__RequestVerificationToken=$CTOK")
echo "import -> $CODE"; [ "$CODE" = "302" ]
grep -o 'ToastOk[^<]*' "$T/imp.html" | head -2 || true

echo "== 6) DB check: package active + settings applied"
python3 - <<'PY'
import sqlite3
con = sqlite3.connect('/home/z/my-project/aspnet/sc-run/hr.db')
row = con.execute("SELECT Name, Version, IsActive, SizeBytes, substr(Sha256,1,12) FROM SsPackages WHERE IsActive=1").fetchone()
assert row, 'no active package'
print('active package:', row)
se = con.execute("SELECT ScreenSaverSeason, ScreenSaverMinutes, ScreenSaverSeconds, ScreenSaverRequirePassword FROM AppSettings").fetchone()
print('app settings after import (season/min/sec/pass):', se)
assert row[0].startswith('چهارفصل'), 'wrong package name'
PY

echo "== 7) manual season (winter) after import -> runtime payload + CSP"
python3 "$R/scripts/i100-parse-form.py" "$T/settings.html" > "$T/body3.txt"
python3 - "$T/body3.txt" <<'PY'
import sys, io, urllib.parse
pairs = urllib.parse.parse_qsl(io.open(sys.argv[1], encoding='utf-8').read(), keep_blank_values=True)
out = [(k, 'winter' if k == 'ssSeason' else v) for k, v in pairs]
io.open(sys.argv[1], 'w', encoding='utf-8').write(urllib.parse.urlencode(out, encoding='utf-8'))
PY
CODE=$(curl -s -b "$T/jar" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/SaveGeneral" \
  -H "Content-Type: application/x-www-form-urlencoded" --data-binary @"$T/body3.txt")
echo "SaveGeneral(winter) -> $CODE"; [ "$CODE" = "302" ]
curl -s -b "$T/jar" -D "$T/rt.h" "$B/Settings/ScreensaverRuntime" -o "$T/rt.html"
python3 - "$T/rt.html" "$T/rt.h" <<'PY'
import sys, io, re, json
h = io.open(sys.argv[1], encoding='utf-8').read()
hd = io.open(sys.argv[2], encoding='utf-8').read()
m = re.search(r'<script id="hr-ss-data" type="application/json">(.*?)</script>', h)
assert m, 'payload missing'
p = json.loads(m.group(1))
print('payload:', {k: p[k] for k in ('lang', 'season', 'cal')})
assert p['season'] == 'winter', 'season not in payload'
assert p['cal'] == 'jalali', 'cal missing'
assert 'چهارفصل' in p['name'], 'name missing'
assert 'sandbox allow-scripts' in hd, 'CSP sandbox header missing'
assert 'data:image/jpeg;base64,' in h, 'embedded photos missing'
print('runtime payload + CSP OK (winter forced)')
PY

echo "== 8) phase-106 fix present in deployed js"
grep -c "hr-ss-unlock-signal" "$R/aspnet/sc-run/wwwroot/js/hr-screensaver.js" | xargs echo "unlock-signal occurrences in sc-run js:"

echo "== 9) season back to auto"
python3 "$R/scripts/i100-parse-form.py" "$T/settings.html" > "$T/body2.txt"
python3 - "$T/body2.txt" <<'PY'
import sys, io, urllib.parse
pairs = urllib.parse.parse_qsl(io.open(sys.argv[1], encoding='utf-8').read(), keep_blank_values=True)
out = [(k, 'auto' if k == 'ssSeason' else v) for k, v in pairs]
io.open(sys.argv[1], 'w', encoding='utf-8').write(urllib.parse.urlencode(out, encoding='utf-8'))
PY
CODE=$(curl -s -b "$T/jar" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/SaveGeneral" \
  -H "Content-Type: application/x-www-form-urlencoded" --data-binary @"$T/body2.txt")
echo "SaveGeneral(auto) -> $CODE"; [ "$CODE" = "302" ]
curl -s -b "$T/jar" "$B/Settings/ScreensaverRuntime" | python3 -c "
import re, sys, json
h = sys.stdin.read()
p = json.loads(re.search(r'id=\"hr-ss-data\" type=\"application/json\">(.*?)</script>', h).group(1))
print('payload season now:', p['season'])
assert p['season'] == 'auto', 'season restore failed'
print('AUTO RESTORE OK')"
echo "E2E-PART1-OK"
