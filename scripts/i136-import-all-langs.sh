#!/bin/bash
# فاز ۱۱۳ — درون‌ریزی پنلی همهٔ بسته‌های زبان (بدون ری‌استارت؛ langs.Bump فوری اعمال می‌شود)
cd /home/z/my-project || exit 1
CK=/tmp/i136-cookies.txt
rm -f "$CK"
get_token() { curl -s -b "$CK" -c "$CK" "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//'; }

T=$(get_token http://127.0.0.1:3000/Auth/Login)
echo "login: $(curl -s -b "$CK" -c "$CK" -o /dev/null -w "%{http_code}" -X POST http://127.0.0.1:3000/Auth/Login \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$T")"
T=$(get_token http://127.0.0.1:3000/License/Notice)
curl -s -b "$CK" -c "$CK" -o /dev/null -X POST http://127.0.0.1:3000/License/Ack --data-urlencode "__RequestVerificationToken=$T"

for LANG in en ru ar prs ps ka ky uz tg tk; do
  T=$(get_token http://127.0.0.1:3000/Langs/ImportForm)
  RES=$(curl -s -b "$CK" -X POST http://127.0.0.1:3000/Langs/Import \
    -F "file=@aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-$LANG.json;type=application/json" \
    -F "__RequestVerificationToken=$T" -H "X-Requested-With: XMLHttpRequest")
  echo "$LANG → $(echo "$RES" | head -c 160)"
done

echo "=== شمارش DB ==="
python3 -c "
import sqlite3
con = sqlite3.connect('aspnet/sc-run/hr.db')
for code, n in con.execute('SELECT LangCode, COUNT(*) FROM HrLangEntries GROUP BY LangCode ORDER BY 2 DESC'):
    print(f'  {code}: {n}')
"
