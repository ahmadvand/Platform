#!/bin/bash
# فاز ۱۰۲ — تنظیم سریع اسکرین‌سیور با curl (بدون دست‌کاری جلسهٔ مرورگر تست)
# مصرف: bash scripts/i102-set-ss.sh <minutes> <seconds>
set -euo pipefail
R=/home/z/my-project
T=/tmp/i102ss; rm -rf "$T"; mkdir -p "$T"
B=http://localhost:3000
MIN=${1:-0}; SEC=${2:-6}

TOK=$(curl -s -c "$T/jar" "$B/Auth/Login" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
CODE=$(curl -s -b "$T/jar" -c "$T/jar" -o /dev/null -w "%{http_code}" -X POST "$B/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOK")
echo "login POST -> $CODE"; [ "$CODE" = "302" ]

REDIR=$(curl -s -b "$T/jar" -o /dev/null -w "%{redirect_url}" "$B/Settings")
if [[ "$REDIR" == *"/License/Notice"* ]]; then
  ATOK=$(curl -s -b "$T/jar" -c "$T/jar" "$B/License/Notice" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
  curl -s -b "$T/jar" -c "$T/jar" -o /dev/null -X POST "$B/License/Ack" \
    --data-urlencode "__RequestVerificationToken=$ATOK"
  echo "license ack done"
fi

curl -s -b "$T/jar" -c "$T/jar" "$B/Settings" -o "$T/settings.html"
python3 "$R/scripts/i100-parse-form.py" "$T/settings.html" > "$T/body.txt"

# جایگزینی مقادیر اسکرین‌سیور در بدنهٔ فرم (بقیهٔ فیلدها حفظ می‌شوند — شامل companyName)
python3 - "$T/body.txt" "$MIN" "$SEC" <<'PY'
import sys, urllib.parse, io
p, mn, sc = sys.argv[1], sys.argv[2], sys.argv[3]
pairs = urllib.parse.parse_qsl(io.open(p, encoding='utf-8').read(), keep_blank_values=True)
out = []
for k, v in pairs:
    if k == 'ssMinutes': v = mn
    elif k == 'ssSeconds': v = sc
    out.append((k, v))
io.open(p, 'w', encoding='utf-8').write(urllib.parse.urlencode(out, encoding='utf-8'))
print('ss patched ->', mn, 'min +', sc, 'sec')
PY

CODE=$(curl -s -b "$T/jar" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/SaveGeneral" \
  -H "Content-Type: application/x-www-form-urlencoded" --data-binary @"$T/body.txt")
echo "SaveGeneral POST -> $CODE"; [ "$CODE" = "302" ]
curl -s -b "$T/jar" "$B/" | python3 -c "
import re, sys
attrs = dict(re.findall(r'data-(ss-[a-z-]+)=\"([^\"]*)\"', sys.stdin.read()))
print('rendered:', attrs)
assert attrs.get('ss-minutes') == '$MIN' and attrs.get('ss-seconds') == '$SEC', 'PATCH FAILED'
print('SS SET OK')"
