#!/bin/bash
# فاز ۱۰۸ — تست جامع زنده: ۵ بستهٔ محافظ صفحه + فونت وزیر (اتصال حروف) + opts (تنظیمات کاربر) + فصل + ترجمه‌ها + قفل/رمز
set -uo pipefail
R=/home/z/my-project
T=/tmp/i107t; rm -rf "$T"; mkdir -p "$T"
B=http://localhost:3000
J="$T/jar"
PKGS="$R/download/packages"

TOK=$(curl -s -c "$J" "$B/Auth/Login" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOK")
echo "login POST -> $CODE"; [ "$CODE" = "302" ] || { echo LOGIN_FAILED; exit 1; }

REDIR=$(curl -s -b "$J" -o /dev/null -w "%{redirect_url}" "$B/Settings")
if [[ "$REDIR" == *"/License/Notice"* ]]; then
  ATOK=$(curl -s -b "$J" -c "$J" "$B/License/Notice" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
  curl -s -b "$J" -c "$J" -o /dev/null -X POST "$B/License/Ack" --data-urlencode "__RequestVerificationToken=$ATOK"
  echo "license ack done"
fi

echo "== 1) تنظیمات: تب فصل + ویرایشگر opts + نسخهٔ ۲.۷.۱۹ =="
curl -s -b "$J" -c "$J" "$B/Settings" -o "$T/settings.html"
for pat in "فصل صحنهٔ محافظ چهارفصل" 'name="ssSeason"' "باغ سرسبز میوه‌دار" "۲.۷.۱۹" "تنظیمات اختصاصی بستهٔ فعال (اختیاری)" 'name="optionsJson"' 'ssPkgOptsForm'; do
  if grep -q "$pat" "$T/settings.html"; then echo "  OK: $pat"; else echo "  MISSING: $pat"; fi
done

echo "== 2) درون‌ریزی/بررسی/حذف هر ۵ بسته + فونت + opts (آخرین: چهارفصل = فعال می‌ماند) =="
get_tok() { curl -s -b "$J" -c "$J" "$B/Settings" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
del_pkgs() {
  for PID in $(curl -s -b "$J" "$B/Settings" | grep -o 'ssPkgAct_[0-9]*' | grep -o '[0-9]*' | sort -u); do
    curl -s -b "$J" -c "$J" -o /dev/null -X POST "$B/Settings/DeleteScreensaverPackage" \
      --data-urlencode "id=$PID" --data-urlencode "__RequestVerificationToken=$(get_tok)"
  done
}
for P in Fireworks NightSky PoetryRain Aquarium FourSeasons; do
  del_pkgs
  F="$PKGS/HR-Screensaver-$P-2.7.19.json"
  CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/ImportScreensaverPackage" \
    -F "file=@$F;type=application/json" -F "applySettings=true" \
    -F "__RequestVerificationToken=$(get_tok)")
  RT=$(curl -s -b "$J" "$B/Settings/ScreensaverRuntime")
  TITLE=$(printf '%s' "$RT" | grep -o '<title>[^<]*</title>' | head -1)
  FONTOK=$(printf '%s' "$RT" | grep -c "data:font/woff2;base64")
  SEAS=$(printf '%s' "$RT" | grep -o '"season":"[^"]*"' | head -1)
  OPTS=$(printf '%s' "$RT" | grep -o '"opts":{[^}]*}' | head -1)
  echo "  [$P] import=$CODE | $TITLE | font=$FONTOK | $SEAS | $OPTS"
done
curl -s -b "$J" "$B/Settings" | grep -q "محافظ صفحهٔ چهارفصل" && echo "  OK: four-season active row" || echo "  MISSING row"

echo "== 2ب) چرخهٔ تنظیمات کاربر (options) روی بستهٔ فعال =="
APID=$(curl -s -b "$J" "$B/Settings" | python3 -c "
import re, sys
h = sys.stdin.read()
m = re.search(r'<form id=\"ssPkgOptsForm\".*?name=\"id\" value=\"(\d+)\"', h, re.S)
print(m.group(1) if m else '')")
TK=$(get_tok)
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/SaveScreensaverPackageOptions" \
  --data-urlencode "id=$APID" --data-urlencode 'optionsJson={"density":1.5,"pile":false}' \
  --data-urlencode "__RequestVerificationToken=$TK")
echo "  save opts -> $CODE"
curl -s -b "$J" "$B/Settings/ScreensaverRuntime" | grep -o '"opts":{[^}]*}' | head -1
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/SaveScreensaverPackageOptions" \
  --data-urlencode "id=$APID" --data-urlencode 'optionsJson={"bad' \
  --data-urlencode "__RequestVerificationToken=$TK")
echo "  invalid JSON -> $CODE (باید 302 و ردّ باشد)"
RT=$(curl -s -b "$J" "$B/Settings/ScreensaverRuntime")
printf '%s' "$RT" | grep -o '"opts":{[^}]*}' | head -1 || echo "  OK: JSON نامعتبر ذخیره نشد (opts قبلی باقی است یا null)"
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/SaveScreensaverPackageOptions" \
  --data-urlencode "id=$APID" --data-urlencode "optionsJson=" \
  --data-urlencode "__RequestVerificationToken=$TK")
echo "  clear opts -> $CODE"
curl -s -b "$J" "$B/Settings/ScreensaverRuntime" | grep -o '"opts":null' | head -1 && echo "  OK: بازگشت به پیش‌فرض بسته"

echo "== 3) انتخاب دستی فصل: زمستان از SaveGeneral (فرم کامل) =="
python3 - "$T/settings.html" "$T/general.post" <<'PY'
import io, re, sys, urllib.parse
h = io.open(sys.argv[1], encoding='utf-8').read()
def inp(name):
    m = re.search(r'name="' + name + r'"[^>]*value="([^"]*)"', h)
    return m.group(1) if m else ''
def sel(name):
    m = re.search(r'<select name="' + name + r'"[^>]*>(.*?)</select>', h, re.S)
    if not m: return ''
    o = re.findall(r'<option value="([^"]*)"[^>]*selected="selected"', m.group(1))
    return o[0] if o else ''
fields = {}
for k in ('companyName','companyLegalName','companyNationalId','companyPhone','companyEmail','companyWebsite','companyAddress','description'):
    v = inp(k)
    if v: fields[k] = v
for k in ('lang','calendar','currency','hijriAdjust','ssEnabled','ssRequirePassword'):
    v = sel(k) or inp(k)
    if v: fields[k] = v
for k in ('currencyRate','ssMinutes','ssSeconds'):
    v = inp(k)
    if v: fields[k] = v
fields['ssSeason'] = 'winter'
tok = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', h)
fields['__RequestVerificationToken'] = tok.group(1)
open(sys.argv[2],'w',encoding='utf-8').write(urllib.parse.urlencode(fields))
assert fields.get('ssMinutes','').isdigit(), 'numeric fields must parse'
print('  fields ok:', len(fields))
PY
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/SaveGeneral" --data "@$T/general.post")
echo "  SaveGeneral(winter) -> $CODE"
curl -s -b "$J" "$B/Settings/ScreensaverRuntime" | grep -o '"season":"winter"' | head -1
sed 's/ssSeason=winter/ssSeason=auto/' "$T/general.post" > "$T/general2.post"
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/SaveGeneral" --data "@$T/general2.post")
echo "  SaveGeneral(auto) -> $CODE"
curl -s -b "$J" "$B/" -o "$T/home.html" && grep -o 'data-ss-season="auto"' "$T/home.html" | head -1 && grep -o 'data-ss-ticket="[^"]*"' "$T/home.html" | head -c 60; echo

echo "== 4) ترجمه‌های رسمی: درون‌ریزی en/ru/ar از مسیر رسمی /Langs/Import =="
for L in en ru ar; do
  CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Langs/Import" \
    -F "file=@$R/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-$L.json;type=application/json" \
    -F "__RequestVerificationToken=$(get_tok)")
  echo "  langpack $L import -> $CODE"
done
curl -s -b "$J" "$B/Langs/Export?code=en" -o "$T/en-export.json"
python3 - "$T/en-export.json" <<'PY'
import json, sys
d = json.load(open(sys.argv[1], encoding='utf-8'))
e = d.get('entries', d if isinstance(d, dict) else {})
hit = [k for k in e if 'فصل صحنه' in k]
print('  en season keys:', len(hit), '→', (e.get(hit[0]) if hit else 'MISSING')[:50] if hit else '')
PY

echo "== 5) چرخهٔ قفل/423/رمز =="
TOKL=$(get_tok)
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Auth/ScreensaverLock" \
  --data-urlencode "__RequestVerificationToken=$TOKL" -H "X-Requested-With: XMLHttpRequest")
echo "  ScreensaverLock -> $CODE"
C423=$(curl -s -b "$J" -o /dev/null -w "%{http_code}" "$B/Settings" -H "X-Requested-With: XMLHttpRequest")
echo "  AJAX during lock -> $C423 (must be 423)"
R2=$(curl -s -b "$J" -X POST "$B/Auth/VerifyWakePassword" \
  --data-urlencode "password=Admin@1234" --data-urlencode "__RequestVerificationToken=$TOKL" -H "X-Requested-With: XMLHttpRequest")
echo "  right pass -> $R2"
C200=$(curl -s -b "$J" -o /dev/null -w "%{http_code}" "$B/Settings" -H "X-Requested-With: XMLHttpRequest")
echo "  AJAX after unlock -> $C200 (must be 200)"

echo "== 6) برون‌بری تنظیمات محافظ — فیلد فصل =="
curl -s -b "$J" "$B/Settings/ExportScreensaver" | python3 -c "
import json,sys
s=json.load(sys.stdin)['settings']
print('  export settings:', s)
assert s.get('season') in ('auto','spring','summer','autumn','winter')
print('  OK: season carried')"

echo "== DONE =="
