#!/bin/bash
# فاز ۱۰۴ — پس از پایان i115: استقرار بسته‌های به‌روز + درون‌ریزی پنلی (بدون ری‌استارت)
LOG=/home/z/my-project/scripts/i116.log
cd /home/z/my-project || exit 1
# انتظار برای پایان i115 (حداکثر ۳ ساعت)
for i in $(seq 1 60); do
  grep -q "i115 end" scripts/i115.log 2>/dev/null && break
  sleep 180
done
echo "=== i116 start $(date '+%F %T') ===" >> "$LOG"
cp -f aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-*.json aspnet/sc-run/App_Data/langpacks/
# ورود ادمین
rm -f /tmp/i116-cookies.txt
TOKEN=$(curl -s -c /tmp/i116-cookies.txt http://127.0.0.1:3000/Auth/Login | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//')
curl -s -b /tmp/i116-cookies.txt -c /tmp/i116-cookies.txt -o /dev/null -X POST http://127.0.0.1:3000/Auth/Login \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOKEN"
for code in ar ka ky prs ps tg tk uz ru; do
  T2=$(curl -s -b /tmp/i116-cookies.txt http://127.0.0.1:3000/Langs/ImportForm | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//')
  RES=$(curl -s -b /tmp/i116-cookies.txt -X POST http://127.0.0.1:3000/Langs/Import \
    -F "file=@aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-$code.json;type=application/json" \
    -F "__RequestVerificationToken=$T2" -H "X-Requested-With: XMLHttpRequest")
  echo "$code: $(echo "$RES" | python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("ok"), d.get("message","")[:50])' 2>/dev/null || echo ERR)" >> "$LOG"
done
echo "=== i116 done $(date '+%F %T') ===" >> "$LOG"
