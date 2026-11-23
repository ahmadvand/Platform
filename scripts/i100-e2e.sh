#!/bin/bash
# فاز ۱۰۰ — تست زندهٔ E2E محافظ صفحه با curl: ورود → ذخیرهٔ ۰ دقیقه+۱۰ ثانیه → راستی‌آزمایی اتریبیوت‌ها
set -euo pipefail
R=/home/z/my-project
T=/tmp/i100; rm -rf "$T"; mkdir -p "$T"
B=http://localhost:3000

TOK=$(curl -s -c "$T/jar" "$B/Auth/Login" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
CODE=$(curl -s -b "$T/jar" -c "$T/jar" -o /dev/null -w "%{http_code}" -X POST "$B/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOK")
echo "login POST -> $CODE"; [ "$CODE" = "302" ]

# گیت هشدار مجوز (فاز ۵۱) — با هر ورود باید یک بار تایید شود
REDIR=$(curl -s -b "$T/jar" -o /dev/null -w "%{redirect_url}" "$B/Settings")
if [[ "$REDIR" == *"/License/Notice"* ]]; then
  ATOK=$(curl -s -b "$T/jar" -c "$T/jar" "$B/License/Notice" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
  ACODE=$(curl -s -b "$T/jar" -c "$T/jar" -o /dev/null -w "%{http_code}" -X POST "$B/License/Ack" \
    --data-urlencode "__RequestVerificationToken=$ATOK")
  echo "license ack -> $ACODE"; [ "$ACODE" = "302" ]
fi

curl -s -b "$T/jar" -c "$T/jar" "$B/Settings" -o "$T/settings.html"
python3 "$R/scripts/i100-parse-form.py" "$T/settings.html" > "$T/body.txt"
echo "parsed body bytes: $(wc -c < "$T/body.txt")"

CODE=$(curl -s -b "$T/jar" -o "$T/save.html" -w "%{http_code}" -X POST "$B/Settings/SaveGeneral" \
  -H "Content-Type: application/x-www-form-urlencoded" --data-binary @"$T/body.txt")
echo "SaveGeneral POST -> $CODE"; [ "$CODE" = "302" ]

curl -s -b "$T/jar" "$B/" -o "$T/home.html"
python3 - "$T/home.html" <<'PY'
import re, sys, io
html = io.open(sys.argv[1], encoding="utf-8").read()
attrs = dict(re.findall(r'data-(ss-[a-z-]+)="([^"]*)"', html))
print("rendered:", attrs)
assert attrs.get("ss-enabled") == "true", "ss-enabled must be lowercase true"
assert attrs.get("ss-minutes") == "0", "ss-minutes must be 0"
assert attrs.get("ss-seconds") == "10", "ss-seconds must be 10"
assert attrs.get("ss-require-pass") == "true"
print("PASS: attributes rendered correctly for 0min+10s, lowercase booleans")
PY
echo "ALL GREEN"
