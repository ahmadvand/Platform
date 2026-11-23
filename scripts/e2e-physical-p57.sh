#!/usr/bin/env bash
# ======================================================================
# فاز ۵۷ — اثبات زمان اجرای «بستهٔ مشتری بدون ماژول» (HrModuleParking=false)
# پیش‌نیاز: اپ با بیلد حذف‌شدهٔ پارکینگ بالا آمده باشد (run-with-app.sh)
# ======================================================================
set -u
BASE=http://127.0.0.1:3000
J=/tmp/p57; mkdir -p "$J"; rm -f "$J"/*.txt
P=0; F=0
ok()  { P=$((P+1)); say "✓ $1"; }
bad() { F=$((F+1)); say "✗ $1"; }
say() { printf '%s\n' "$*"; }

# ---- ورود
AF=$(curl -s -c "$J/c.txt" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF" ] || { bad "توکن ورود"; exit 1; }
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$CODE" = "302" ] && ok "LOGIN 302" || bad "LOGIN=$CODE"

# ---- /Parking باید به صفحهٔ «نصب نشده» برود (نه «غیرفعال»، نه ۴۰۴ خام)
CODE2=$(curl -s -b "$J/c.txt" -o /dev/null -w "%{http_code} %{redirect_url}" "$BASE/Parking")
echo "$CODE2" | grep -q "^302 .*ModuleDisabled?code=parking$" && ok "PARKING → $CODE2" || bad "PARKING-LOC=$CODE2"
PAGE=$(curl -s -b "$J/c.txt" "$BASE/Home/ModuleDisabled?code=parking")
echo "$PAGE" | grep -q "نصب نشده است" && ok "MODULE-DISABLED-PAGE: «در این نسخه نصب نشده است»" || bad "MODULE-DISABLED-PAGE wrong variant"
echo "$PAGE" | grep -q "غیرفعال است\</h2\>" && bad "PAGE-TEXT: نسخهٔ خاموش رندر شد" || ok "PAGE-TEXT: واریانت نصب‌نشده"

# ---- وب‌هوک ANPR: کنترلر وجود ندارد → 404 (نه 503 گیت)
C=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/anpr/webhook?SN=TEST")
[ "$C" = "404" ] && ok "ANPR-WEBHOOK 404 (DLL حذف شده)" || bad "ANPR-WEBHOOK=$C"

# ---- منو بدون پارکینگ + سایر ماژول‌ها سالم
S=$(curl -s -b "$J/c.txt" "$BASE/Settings")
echo "$S" | grep -q "نصب نشده در این نسخه" && ok "SETTINGS: نشان «نصب نشده در این نسخه»" || bad "SETTINGS: badge نیست"
echo "$S" | grep -q 'href="/Parking"' && bad "MENU: پارکینگ هنوز در منوست" || ok "MENU: پارکینگ حذف شده"
echo "$S" | grep -q 'href="/Payroll"' && ok "MENU: حقوق سر جایش" || bad "MENU: حقوق نبود"
echo "$S" | grep -q 'href="/Employees"' && ok "MENU: پرسنل سر جایش" || bad "MENU: پرسنل نبود"

# ---- سایر ماژول‌ها و گزارش‌ساز
for r in /Payroll /Visitors /Access /Employees /Attendance /Reports /Devices; do
  c=$(curl -s -b "$J/c.txt" -o /dev/null -w "%{http_code}" "$BASE$r")
  [ "$c" = "200" ] && ok "ROUTE $r 200" || bad "ROUTE $r=$c"
done

# ---- گزارش پارکینگ (اکشن در هاست، ولی نگاشت ActMap → parking) باید بسته باشد
c=$(curl -s -b "$J/c.txt" -o /dev/null -w "%{http_code}" "$BASE/Reports/Parking")
[ "$c" = "302" ] && ok "REPORTS/PARKING 302 (بسته)" || bad "REPORTS/PARKING=$c"

# ---- toggle ماژول نصب‌نشده باید رد شود (بدنهٔ JSON یونیکد-اسکیپ است — با پایتون دیکود می‌کنیم)
AF=$(curl -s -b "$J/c.txt" "$BASE/Settings" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
R=$(curl -s -b "$J/c.txt" -X POST "$BASE/Settings/ToggleModule" \
  --data-urlencode "code=parking" --data-urlencode "enabled=false" \
  --data-urlencode "__RequestVerificationToken=$AF" -H "X-Requested-With: XMLHttpRequest")
echo "$R" | python3 -c "import sys,json; d=json.load(sys.stdin); sys.exit(0 if 'نصب‌شده وجود ندارد' in d.get('message','') else 1)" \
  && ok "TOGGLE رد شد: «در این نسخهٔ نصب‌شده وجود ندارد»" || bad "TOGGLE=$R"

say "-----------------------------"
say "P57-TRIMMED: PASS=$P FAIL=$F"
[ "$F" = "0" ]
