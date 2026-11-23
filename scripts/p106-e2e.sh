#!/bin/bash
# ======================================================================
# فاز ۱۰۶ — E2E زندهٔ بستهٔ محافظ صفحهٔ ترمیم‌شده:
#   ورود → درون‌ریزی hr-ss-poem-rain.json (بدون اعمال تنظیمات) → ردیف بسته
#   → سرو Runtime (CSP سندباکس + تزریق فونت وزیرمتن میزبان + کل عبارت با یک
#   fillText + settings در hr-ss-data) → بازگشت به محافظ داخلی → حذف همهٔ
#   بسته‌های تستی (پاک‌سازی وضعیت)
# ======================================================================
set -u
R=/home/z/my-project
T=/tmp/p106-e2e
B=http://localhost:3000
PKG=$R/assets/screensaver-packages/hr-ss-poem-rain.json
rm -rf "$T"; mkdir -p "$T"
JAR=$T/jar

fail() { echo "E2E FAILED: $1"; exit 1; }

# ---------- ورود ----------
TOK=$(curl -s -c "$JAR" "$B/Auth/Login" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1) if m else '')")
[ -n "$TOK" ] || fail "no login token"
CODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" -X POST "$B/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOK")
[ "$CODE" = "302" ] || fail "login POST -> $CODE"

# ---------- گیت لایسنس (در صورت نمایش) ----------
REDIR=$(curl -s -b "$JAR" -o /dev/null -w "%{redirect_url}" "$B/Settings")
if [[ "$REDIR" == *"/License/Notice"* ]]; then
  ATOK=$(curl -s -b "$JAR" -c "$JAR" "$B/License/Notice" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1) if m else '')")
  curl -s -b "$JAR" -c "$JAR" -o /dev/null -X POST "$B/License/Ack" \
    --data-urlencode "__RequestVerificationToken=$ATOK"
  echo "license ack done"
fi

# ---------- توکن آنتی‌فرجری از خود صفحهٔ تنظیمات ----------
STOK=$(curl -s -b "$JAR" -c "$JAR" "$B/Settings" | python3 -c "
import re,sys; h=sys.stdin.read(); m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',h); print(m.group(1) if m else '')")
[ -n "$STOK" ] || fail "no settings token"

# ---------- درون‌ریزی بستهٔ ترمیم‌شده (بدون اعمال تنظیمات — تنظیمات کاربر دست‌نخورده) ----------
RCODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/ImportScreensaverPackage" \
  -F "file=@$PKG;type=application/json" -F "applySettings=false" \
  -F "__RequestVerificationToken=$STOK")
[ "$RCODE" = "302" ] || fail "import -> $RCODE"
echo "import -> 302 OK"

# ---------- ردیف بسته در تب محافظ + استخراج id (فیلد مخفی name="id") ----------
curl -s -b "$JAR" "$B/Settings" -o "$T/settings.html"
grep -q "باران شعر" "$T/settings.html" || fail "package name not rendered in Settings"
PID=$(python3 -c "
import re
h = open('$T/settings.html', encoding='utf-8').read()
ids = [i for i in re.findall(r'name=\"id\" value=\"(\d+)\"', h) if i != '0']
print(ids[0] if ids else '')")
[ -n "$PID" ] || fail "no package id"
echo "package row OK (id=$PID)"

# ---------- Runtime سندباکس: هدرها + فونت وزیر میزبان + متن متصل + settings ----------
curl -s -b "$JAR" -c "$JAR" -D "$T/rt.headers" "$B/Settings/ScreensaverRuntime" -o "$T/rt.html"
grep -qi "Content-Security-Policy: sandbox allow-scripts" "$T/rt.headers" || fail "CSP sandbox header"
python3 - "$T/rt.html" <<'PY' || exit 1
import re, sys
h = open(sys.argv[1], encoding='utf-8').read()
assert "connect-src 'none'" in h, "meta connect-src"
assert 'id="hr-ss-data"' in h, "host data"
assert '"settings"' in h, "settings passed to guest (phase 106)"
assert 'باران شعر' in h, "package title present"
assert '@font-face' in h and 'Vazirmatn' in h, "host-injected Vazirmatn @font-face (phase 106)"
assert 'data:font/woff2;base64,' in h, "font base64 embedded"
assert "fillText(text" in h, "whole-string fillText"
assert not re.search(r'letter-spacing\s*:', h, re.I), "no letter-spacing declaration"
print("runtime checks OK")
PY
echo "runtime OK (CSP + host font + settings + fillText)"

# ---------- بازگشت به محافظ داخلی + حذف همهٔ بسته‌های تستی (id در بدنهٔ فرم) ----------
ACODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/ActivateScreensaverPackage" \
  --data-urlencode "id=0" --data-urlencode "__RequestVerificationToken=$STOK" -H "X-Requested-With: XMLHttpRequest")
[ "$ACODE" = "200" ] || [ "$ACODE" = "302" ] || fail "activate builtin -> $ACODE"
for DID in $(python3 -c "
import re
h = open('$T/settings.html', encoding='utf-8').read()
ids = [i for i in re.findall(r'name=\"id\" value=\"(\d+)\"', h) if i != '0']
print(' '.join(dict.fromkeys(ids)))"); do
  DCODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/DeleteScreensaverPackage" \
    --data-urlencode "id=$DID" --data-urlencode "__RequestVerificationToken=$STOK" -H "X-Requested-With: XMLHttpRequest")
  [ "$DCODE" = "200" ] || [ "$DCODE" = "302" ] || fail "delete $DID -> $DCODE"
  echo "deleted test package id=$DID"
done
N=$(python3 -c "
import sqlite3
con=sqlite3.connect('$R/aspnet/sc-run/hr.db')
print(con.execute('SELECT COUNT(*) FROM SsPackages').fetchone()[0])")
[ "$N" = "0" ] || fail "SsPackages not clean (n=$N)"
echo "cleanup OK (builtin active, SsPackages empty)"
echo "E2E PASSED"
