#!/bin/bash
# فاز ۱۰۴ن — درون‌ریزی بستهٔ روسی از طریق پنل ادمین (بدون ری‌استارت)
cd /home/z/my-project || exit 1
CK=/tmp/i117-cookies.txt
rm -f "$CK"
get_token() { curl -s -b "$CK" -c "$CK" "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//'; }
post() { curl -s -b "$CK" -c "$CK" -o /dev/null -w "%{http_code}" -X POST "$1" "${@:2}"; }

T=$(get_token http://127.0.0.1:3000/Auth/Login)
echo "login: $(post http://127.0.0.1:3000/Auth/Login \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$T")"

# پذیرش هشدار مجوز (گیت صفحات ادمین)
T=$(get_token http://127.0.0.1:3000/License/Notice)
echo "ack:   $(post http://127.0.0.1:3000/License/Ack --data-urlencode "__RequestVerificationToken=$T")"

curl -s -b "$CK" -c "$CK" -o /dev/null -w "langs page: %{http_code}\n" http://127.0.0.1:3000/Langs

T=$(get_token http://127.0.0.1:3000/Langs/ImportForm)
RES=$(curl -s -b "$CK" -X POST http://127.0.0.1:3000/Langs/Import \
  -F "file=@aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-ru.json;type=application/json" \
  -F "__RequestVerificationToken=$T" -H "X-Requested-With: XMLHttpRequest")
echo "import: $RES" | head -c 500; echo
