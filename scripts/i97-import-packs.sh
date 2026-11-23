#!/bin/bash
# فاز ۹۷ — درون‌ریزی پنلی بسته‌های به‌روز ۸ زبان به دیتابیس زنده (بدون ری‌استارت)
set -uo pipefail
B=http://localhost:3000
J=/tmp/i97-cookies.txt; rm -f $J
TOK=$(curl -s -c $J $B/Auth/Login | grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
C=$(curl -s -o /dev/null -w '%{http_code}' -b $J -c $J -X POST $B/Auth/Login \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOK")
echo "login=$C"
curl -s -o /dev/null -b $J -c $J $B/License/Notice
TOK2=$(curl -s -b $J -c $J $B/License/Notice | grep -o 'name="__RequestVerificationToken"[^>]*' | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
curl -s -o /dev/null -b $J -c $J -X POST $B/License/Ack --data-urlencode "__RequestVerificationToken=$TOK2"
SRC=/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks
TOKL=$(curl -s -b $J -c $J $B/Langs | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
for L in ka uz prs tk ky tg ar ps; do
  R=$(curl -s -b $J -c $J -X POST $B/Langs/Import \
    -H "X-Requested-With: XMLHttpRequest" \
    -F "file=@$SRC/hr-lang-$L.json;type=application/json" \
    -F "__RequestVerificationToken=$TOKL")
  echo "$L -> $(echo "$R" | head -c 200)"
done
