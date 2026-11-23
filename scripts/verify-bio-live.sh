#!/bin/bash
# تست E2E امکانات جدید داده‌های هویتی
B=http://localhost:3000
J=/tmp/hr-cookies.txt
rm -f $J

echo "== 1) ورود ادمین =="
LOGIN=$(curl -s -c $J -b $J -X POST "$B/api/auth/login" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}')
echo "$LOGIN" | head -c 200; echo

echo "== 2) صفحه پنل داده‌های هویتی =="
curl -s -b $J -c $J "$B/Biometrics" -o /tmp/bio.html -w "HTTP %{http_code}\n"
for kw in "داده‌های هویتی" "دریافت از دستگاه" "انتقال به دستگاه" "اثر انگشت"; do
  if grep -q "$kw" /tmp/bio.html; then echo "  ✓ یافت شد: $kw"; else echo "  ✗ نیست: $kw"; fi
done

echo "== 3) شمار قالب‌ها و کاربران دستگاه در صفحه =="
grep -o 'data-pin="[0-9]*"' /tmp/bio.html | wc -l | xargs echo "  ردیف‌های پرسنل per-device:"

echo "== 4) شبیه‌ساز دریافت از دستگاه dev4 (خالی) =="
SIM=$(curl -s -b $J -c $J -X POST "$B/api/biometrics/simulate-fetch" -H "Content-Type: application/json" -d '{"deviceId":"dev4"}')
echo "$SIM" | head -c 300; echo

echo "== 5) انتقال یک پرسنل به دستگاه dev5 =="
PINS=$(curl -s -b $J -c $J "$B/Biometrics" | grep -o 'data-pin="[0-9]*"' | head -1 | grep -o '[0-9]*')
echo "  پین انتخابی: $PINS"
TR=$(curl -s -b $J -c $J -X POST "$B/api/biometrics/push" -H "Content-Type: application/json" -d "{\"pins\":[\"$PINS\"],\"targetDeviceIds\":[\"dev5\"],\"includeBios\":true}")
echo "$TR" | head -c 300; echo

echo "== 6) صفحه‌های کلیدی =="
for p in "" "Employees" "Devices" "Biometrics" "Payroll" "Users"; do
  code=$(curl -s -b $J -o /dev/null -w "%{http_code}" "$B/$p")
  echo "  /$p → $code"
done
