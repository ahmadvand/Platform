#!/bin/bash
# e2e فاز ۸۱ — خروجی‌های PDF دوزبانه (fa/en) — ورود ادمین + دانلود ۳ PDF در هر زبان
set -u
BASE=http://localhost:3000
OUT=/home/z/my-project/tmp-p81b
mkdir -p "$OUT"
JAR="$OUT/jar.txt"

# ورود ادمین
login=$(curl -s -c "$JAR" "$BASE/Auth/Login" -o "$OUT/login.html" -w "%{http_code}")
token=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' "$OUT/login.html" | head -1 | sed 's/.*value="//; s/"$//')
resp=$(curl -s -b "$JAR" -c "$JAR" -H "RequestVerificationToken: $token" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  -o "$OUT/login-resp.json" -w "%{http_code}")
echo "login=$login/$resp $(head -c 120 "$OUT/login-resp.json" 2>/dev/null)"

for lang in fa en; do
  # زبان با کوکی hr_lang — درخواست با کوکی زبان
  pdf_an=$(curl -s -b "$JAR" -b "hr_lang=$lang" "$BASE/Reports/AnalyticsPdf" -o "$OUT/analytics-$lang.pdf" -w "%{http_code}")
  pdf_pk=$(curl -s -b "$JAR" -b "hr_lang=$lang" "$BASE/Reports/ParkingPdf" -o "$OUT/parking-$lang.pdf" -w "%{http_code}")
  pdf_td=$(curl -s -b "$JAR" -b "hr_lang=$lang" "$BASE/Access/TrafficPdf" -o "$OUT/traffic-$lang.pdf" -w "%{http_code}")
  echo "$lang: analytics=$pdf_an parking=$pdf_pk traffic=$pdf_td"
done
ls -la "$OUT"/*.pdf 2>/dev/null | awk '{print $5, $9}'
