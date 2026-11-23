#!/usr/bin/env bash
# فاز ۷۳ — وارسی رفع دو کرش وب: /Payroll/Items (DbSet Department) و /Self (DateTime.Value)
set -uo pipefail
B=http://localhost:3000
J=/tmp/ck.txt; rm -f "$J"

tok=$(curl -s -c "$J" "$B/Auth/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//')
[ -z "$tok" ] && { echo "LOGIN-PAGE-FAIL (no antiforgery token)"; exit 1; }

code=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$tok" -H "X-Requested-With: XMLHttpRequest")
echo "login POST → $code"

for p in /Payroll/Items /Self /Payroll/ItemCreate /Payroll /; do
  c=$(curl -s -b "$J" -o /tmp/page.html -w "%{http_code}" "$B$p")
  echo "GET $p → $c"
  if [ "$c" != "200" ]; then grep -o 'InvalidOperationException[^<]*\|RuntimeBinderException[^<]*' /tmp/page.html | head -2; fi
done
