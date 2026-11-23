#!/bin/bash
# فاز ۱۰۷ — راستی‌آزمایی زنده: درون‌ریزی بستهٔ فصلی v2 + انتخاب دستی فصل + ترمیم رمز
set -uo pipefail
BASE=http://localhost:3000
JAR=/tmp/i107-cookies.txt
rm -f "$JAR"

tok() { # استخراج توکن CSRF از HTML ورودی
  python3 - "$1" <<'PY'
import re, sys
html = open(sys.argv[1], encoding='utf-8').read()
m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html) or \
    re.search(r'value="([^"]+)"[^>]*name="__RequestVerificationToken"', html)
print(m.group(1) if m else '')
PY
}

echo "== 1) login admin =="
curl -s -c "$JAR" "$BASE/Auth/Login" -o /tmp/login.html
T=$(tok /tmp/login.html)
curl -s -b "$JAR" -c "$JAR" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$T" -o /tmp/login-post.html -w "login http=%{http_code}\n"

echo "== 1b) license gate ack =="
curl -s -b "$JAR" -c "$JAR" "$BASE/License/Notice" -o /tmp/lic.html -w "notice http=%{http_code}\n"
T=$(tok /tmp/lic.html)
curl -s -b "$JAR" -c "$JAR" -X POST "$BASE/License/Ack" \
  --data-urlencode "__RequestVerificationToken=$T" -o /dev/null -w "ack http=%{http_code}\n"

echo "== 2) settings page + token =="
curl -s -b "$JAR" -c "$JAR" "$BASE/Settings" -o /tmp/settings.html -w "settings http=%{http_code}\n"
T=$(tok /tmp/settings.html)
echo "token len=${#T}"

echo "== 3) import seasons package v2 =="
curl -s -b "$JAR" -c "$JAR" -X POST "$BASE/Settings/ImportScreensaverPackage" \
  -F "file=@/home/z/my-project/scripts/i106-seasons/out/hr-screensaver-package-seasons-2.0.0.json;type=application/json" \
  -F "applySettings=true" \
  -F "__RequestVerificationToken=$T" \
  -o /tmp/import.json -w "import http=%{http_code}\n"

echo "== 4) delete old 1.0.0 rows (query DB for their ids) =="
OLD_IDS=$(python3 -c "
import sqlite3
db = sqlite3.connect('/home/z/my-project/aspnet/sc-run/hr.db')
ids = [str(r[0]) for r in db.execute(\"SELECT Id FROM SsPackages WHERE Version='1.0.0'\").fetchall()]
print(' '.join(ids))")
echo "old ids: $OLD_IDS"
for id in $OLD_IDS; do
  curl -s -b "$JAR" -c "$JAR" -X POST "$BASE/Settings/DeleteScreensaverPackage" \
    --data-urlencode "id=$id" --data-urlencode "__RequestVerificationToken=$T" \
    -o /tmp/del-$id.txt -w "del $id http=%{http_code}\n"
done

echo "== 5) SaveGeneral with ssSeason=winter (تمام فیلدهای اجباری اعتبارسنجی) =="
curl -s -b "$JAR" -c "$JAR" -X POST "$BASE/Settings/SaveGeneral" \
  --data-urlencode "companyName=شرکت پتروشیمی پارس آسیا" \
  --data-urlencode "calendar=jalali" --data-urlencode "currency=IRR" \
  --data-urlencode "currencyRate=1" --data-urlencode "hijriAdjust=-1" \
  --data-urlencode "ssEnabled=true" --data-urlencode "ssMinutes=1" \
  --data-urlencode "ssSeconds=15" --data-urlencode "ssRequirePassword=true" \
  --data-urlencode "ssSeason=winter" \
  --data-urlencode "__RequestVerificationToken=$T" \
  -o /tmp/save.json -w "save http=%{http_code}\n"

echo "== 6) runtime check: season + new code =="
curl -s -b "$JAR" "$BASE/Settings/ScreensaverRuntime" -o /tmp/runtime.html -w "runtime http=%{http_code}\n"
rg -o '"season":"[a-z]*"' /tmp/runtime.html | head -2
rg -c "drawSnowman|drawHare|drawRobin|drawFlock|drawRainbow|hr-ss-unlock" /tmp/runtime.html
rg -o "آدم‌برفی" /tmp/runtime.html | head -1

echo "== 7) new host js served =="
curl -s "$BASE/js/hr-screensaver.js" -o /tmp/ssjs.js
rg -c "hr-ss-unlock|WAKE_GRACE_MS|PASS_CFG" /tmp/ssjs.js

echo "== 8) export screensaver settings shows season =="
curl -s -b "$JAR" "$BASE/Settings/ExportScreensaver" | rg -o '"season": "[a-z]*"'
