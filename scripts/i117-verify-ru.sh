#!/bin/bash
# فاز ۱۰۴ن — راستی‌آزمایی UI در زبان روسی (صفحه‌های اصلی + صفحات کلیدهای تازه)
cd /home/z/my-project || exit 1
CK=/tmp/ru-ver-ck.txt
rm -f "$CK"
get_token() { curl -s -b "$CK" -c "$CK" "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//'; }
post() { curl -s -b "$CK" -c "$CK" -o /dev/null -w "%{http_code}" -X POST "$1" "${@:2}"; }
fetch() { curl -s -b "$CK" -b "hr_lang=ru" -o "$2" -w "%{http_code}" "$1"; }

T=$(get_token http://127.0.0.1:3000/Auth/Login)
post http://127.0.0.1:3000/Auth/Login --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" --data-urlencode "__RequestVerificationToken=$T" >/dev/null
T=$(get_token http://127.0.0.1:3000/License/Notice)
post http://127.0.0.1:3000/License/Ack --data-urlencode "__RequestVerificationToken=$T" >/dev/null

mkdir -p /tmp/ru-ver
for pg in "/:dash" "/Pers:pers" "/Audit:audit" "/Devices:devices" "/Langs:langs" "/Settings:settings" "/Org:org" "/Reports:reports" "/Payroll:payroll" "/Access:access"; do
  url="${pg%%:*}"; name="${pg##*:}"
  code=$(fetch "http://127.0.0.1:3000$url" "/tmp/ru-ver/$name.html")
  echo "$url -> $code ($(wc -c < /tmp/ru-ver/$name.html 2>/dev/null)B)"
done
