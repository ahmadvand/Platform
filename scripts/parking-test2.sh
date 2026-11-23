#!/bin/bash
# تست عملکردی موتور مجوز پارکینگ — نسخه ۲ (پلاک‌های هدفمند)
BASE=http://localhost:3000
JAR=/tmp/hr_smoke.jar

TOK=$(curl -s -b "$JAR" "$BASE/Parking/ScanForm" | rg -o 'name="__RequestVerificationToken"[^>]*value="([^"]+)"' -r '$1' | head -1)
[ -z "$TOK" ] && { echo "NO-TOKEN"; exit 1; }

# PG-IN1 = راهبند ورودی اصلی (PK-01)
IN1=$(curl -s -b "$JAR" "$BASE/Parking/ScanForm" | python3 -c "
import sys, re
h = sys.stdin.read()
for oid, label in re.findall(r'<option value=\"([^\"]+)\">([^<]+)</option>', h):
    if 'راهبند ورودی اصلی' in label: print(oid); break
")
echo "GATE=PG-IN1: $IN1"

post () {
  curl -s -b "$JAR" -H "X-Requested-With: XMLHttpRequest" \
    -d "plate=$1" -d "gateId=$2" -d "direction=$3" -d "method=پلاک‌خوان" \
    -d "__RequestVerificationToken=$TOK" "$BASE/Parking/Scan" | python3 -c "import sys,json; j=json.load(sys.stdin); print('ok=%s | %s' % (j['ok'], j['message']))"
}

echo "1) ورود پلاک دارای مجوز فعال PK-01 (29س642 ایران 87):"
post "29س642 ایران 87" "$IN1" "ورود"
echo "2) ورود تکراری همان پلاک (آنتی‌پس‌بک):"
post "29س642 ایران 87" "$IN1" "ورود"
echo "3) خروج همان پلاک:"
post "29س642 ایران 87" "$IN1" "خروج"
echo "4) ورود خودروی بدون مجوز این پارکینگ (23ع494 ایران 36 — مجوزش PK-02 است):"
post "23ع494 ایران 36" "$IN1" "ورود"
echo "5) قفل دائم گیت و تلاش ورود:"
curl -s -b "$JAR" -H "X-Requested-With: XMLHttpRequest" -d "id=$IN1" -d "state=قفل دائم" \
  -d "__RequestVerificationToken=$TOK" "$BASE/Parking/SetGateOpenState" | python3 -c "import sys,json; j=json.load(sys.stdin); print('ok=%s | %s' % (j['ok'], j['message']))"
post "14ب619 ایران 68" "$IN1" "ورود"
echo "6) بازگشت به نرمال و ورود دوباره:"
curl -s -b "$JAR" -H "X-Requested-With: XMLHttpRequest" -d "id=$IN1" -d "state=نرمال" \
  -d "__RequestVerificationToken=$TOK" "$BASE/Parking/SetGateOpenState" | python3 -c "import sys,json; j=json.load(sys.stdin); print('ok=%s | %s' % (j['ok'], j['message']))"
post "14ب619 ایران 68" "$IN1" "ورود"
echo "7) خروج همان برای بازگرداندن وضعیت دمو:"
post "14ب619 ایران 68" "$IN1" "خروج"
