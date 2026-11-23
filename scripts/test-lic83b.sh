#!/bin/bash
# راستی‌آزمایی فاز ۸۳ب — «فقط با مجوز»: راه‌اندازی بدون مجوز ممکن نیست و بدون مجوز،
# ناوبری صفحه به /License/Notice می‌رود؛ پس از Ack، سامانه در دسترس است.
B=http://localhost:3000
J=/tmp/lic83b
rm -rf $J && mkdir -p $J

echo "== 1) /health =="
curl -s -o /dev/null -w "%{http_code}\n" --max-time 5 $B/health

echo "== 2) GET / بدون نشست → ریدایرکت به لاگین =="
curl -s -o /dev/null -w "%{http_code} -> %{redirect_url}\n" --max-time 5 $B/

echo "== 3) لاگین مدیر =="
TOK=$(curl -s -c $J/c.txt --max-time 8 $B/Auth/Login | grep -o '__RequestVerificationToken[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//')
echo "token: ${TOK:0:16}…"
curl -s -b $J/c.txt -c $J/c.txt -o /dev/null -w "login: %{http_code} -> %{redirect_url}\n" --max-time 8 \
  -X POST $B/Auth/Login -e $B/Auth/Login \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOK"

echo "== 4) GET / با نشست و بدون Ack → باید به /License/Notice برود (بدون مجوز) =="
curl -s -b $J/c.txt -o /dev/null -w "%{http_code} -> %{redirect_url}\n" --max-time 8 $B/

echo "== 5) صفحهٔ Notice =="
curl -s -b $J/c.txt --max-time 8 $B/License/Notice -o $J/notice.html -w "notice: %{http_code}\n"
grep -o 'مجوز نصب ثبت نشده است[^<]*' $J/notice.html | head -1

echo "== 6) صفحهٔ /License =="
curl -s -b $J/c.txt --max-time 8 $B/License -o $J/lic.html -w "license: %{http_code}\n"
grep -o 'ثبت نشده' $J/lic.html | head -1
grep -c 'دورهٔ آزمایشی' $J/lic.html || true

echo "== 7) Ack → ادامه به سامانه =="
TOK2=$(curl -s -b $J/c.txt --max-time 8 $B/License/Notice | grep -o '__RequestVerificationToken[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//')
curl -s -b $J/c.txt -c $J/c.txt -o /dev/null -w "ack: %{http_code} -> %{redirect_url}\n" --max-time 8 \
  -X POST $B/License/Ack -e $B/License/Notice --data-urlencode "__RequestVerificationToken=$TOK2"
curl -s -b $J/c.txt -o /dev/null -w "home پس از Ack: %{http_code}\n" --max-time 8 $B/

echo "== 8) Save ویزارد بدون مجوز → باید رد شود (نمایشی — سامانه از قبل پیکربندی شده) =="
TOK3=$(curl -s -b $J/c.txt --max-time 8 $B/ | grep -o '__RequestVerificationToken[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//')
curl -s -b $J/c.txt --max-time 8 -X POST $B/Setup/Save -e $B/ \
  --data-urlencode "__RequestVerificationToken=$TOK3" | head -c 300; echo
