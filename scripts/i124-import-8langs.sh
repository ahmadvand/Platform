#!/bin/bash
# فاز ۱۰۵ب۳ — درون‌ریزی پنلی بسته‌های به‌روز (بدون ری‌استارت)
cd /home/z/my-project || exit 1
CK=/tmp/i124-ck.txt
rm -f "$CK"
get_token() { curl -s -b "$CK" -c "$CK" "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//'; }
post() { curl -s -b "$CK" -c "$CK" -o /dev/null -w "%{http_code}" -X POST "$1" "${@:2}"; }

T=$(get_token http://127.0.0.1:3000/Auth/Login)
post http://127.0.0.1:3000/Auth/Login --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" --data-urlencode "__RequestVerificationToken=$T" >/dev/null
T=$(get_token http://127.0.0.1:3000/License/Notice)
post http://127.0.0.1:3000/License/Ack --data-urlencode "__RequestVerificationToken=$T" >/dev/null

for code in ar ka ky prs ps tg tk uz; do
  T2=$(get_token http://127.0.0.1:3000/Langs/ImportForm)
  RES=$(curl -s -b "$CK" -X POST http://127.0.0.1:3000/Langs/Import \
    -F "file=@aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-$code.json;type=application/json" \
    -F "__RequestVerificationToken=$T2" -H "X-Requested-With: XMLHttpRequest")
  echo "$code: $(echo "$RES" | python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("ok"), d.get("message","")[:80])' 2>/dev/null || echo ERR)"
done
