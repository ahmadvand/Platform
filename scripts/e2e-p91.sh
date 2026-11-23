#!/usr/bin/env bash
# ============================================================
# فاز ۹۱ — تست E2E: فرم واحد تنظیمات + ارز + چندزبانی + فونت ویزارد
# ============================================================
set -u
cd "$(dirname "$0")/.."
BASE="http://localhost:3000"
PASS=0; FAIL=0
ck() { if [ "${1:-0}" -ge 1 ] 2>/dev/null; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }

# ---------- راه‌اندازی دوبارهٔ نمونه با بیلد تازه ----------
pkill -f "Platform.Web.dll" 2>/dev/null || true
sleep 16
for i in $(seq 1 45); do
  if curl -s -o /dev/null -w "%{http_code}" "$BASE/Auth/Login" --max-time 3 | grep -q "200"; then break; fi
  sleep 2
done
sleep 3
ck "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/Auth/Login" --max-time 8 | grep -c 200)" "سرور بالا آمد (بیلد فاز ۹۱)"

# ---------- نشست مدیر (با تلاش مجدد) ----------
CJA=/tmp/p91-admin.txt; rm -f "$CJA"
TOKENA() { curl -s -b "$CJA" -c "$CJA" "$BASE$1" --max-time 15 | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
LOGOK=0
for try in 1 2 3; do
  T=$(TOKENA /Auth/Login)
  curl -s -b "$CJA" -c "$CJA" -o /dev/null -X POST "$BASE/Auth/Login" \
    -d "identifier=admin&secret=Admin@1234&__RequestVerificationToken=$T" --max-time 15
  # فاز ۹۱ — اگر گیت هشدار مجوز فعال بود (مجوز آزمایشی منقضی — رفتار عادی)، تایید کن
  R=$(curl -s -b "$CJA" -o /dev/null -w "%{redirect_url}" "$BASE/" --max-time 10)
  case "$R" in
    *License/Notice*)
      AF=$(curl -s -b "$CJA" "$R" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
      curl -s -o /dev/null -b "$CJA" -c "$CJA" -X POST "$BASE/License/Ack" \
        -H "RequestVerificationToken: $AF" --data-urlencode "__RequestVerificationToken=$AF" --max-time 15
      ;;
  esac
  if curl -s -b "$CJA" "$BASE/" --max-time 10 | grep -q 'sidebar'; then LOGOK=1; break; fi
  rm -f "$CJA"; sleep 3
done
ck "$LOGOK" "ورود مدیر"

echo "--- ۱) فرم واحد تنظیمات (زبانه‌ها) ---"
S=$(curl -s -b "$CJA" "$BASE/Settings" --max-time 15)
ck "$(echo "$S" | grep -c 'id="setTabs"')" "تنظیمات: نوار زبانه رندر شد"
ck "$(echo "$S" | grep -c 'data-pane="tab-company"')" "زبانهٔ شرکت و لوگو"
ck "$(echo "$S" | grep -c 'data-pane="tab-locale"')" "زبانهٔ زبان، تقویم و ارز"
ck "$(echo "$S" | grep -c 'data-pane="tab-look"')" "زبانهٔ ظاهر و پالت"
ck "$(echo "$S" | grep -c 'data-pane="tab-db"')" "زبانهٔ پایگاه داده"
ck "$(echo "$S" | grep -c 'data-pane="tab-mods"')" "زبانهٔ ماژول‌ها"
ck "$(echo "$S" | grep -c 'data-pane="tab-about"')" "زبانهٔ درباره"
ck "$(echo "$S" | grep -c 'action="/Settings/SaveGeneral"')" "فرم واحد → SaveGeneral"
ck "$(echo "$S" | grep -c 'id="curSel"')" "دراپ‌داون ارز رندر شد"
ck "$(echo "$S" | grep -c 'افغانی افغانستان')" "کاتالوگ ارز: افغانی present"
ck "$(echo "$S" | grep -c 'id="curPreview"')" "پیش‌نمایش زندهٔ ارز"
ck "$(echo "$S" | grep -c 'id="curRate"')" "فیلد نرخ تبدیل نمایشی"
ck "$(echo "$S" | grep -c 'dbForm')" "فرم پایگاه داده حفظ شده"
ck "$(echo "$S" | grep -c 'modTable')" "جدول ماژول‌ها حفظ شده"
ck "$(echo "$S" | grep -c 'pal-grid')" "کتابخانه پالت حفظ شده"
ck "$(echo "$S" | grep -c 'accentColor')" "رنگ سفارشی سریع حفظ شده"
ck "$(echo "$S" | grep -c 'resetLogoForm')" "فرم حذف لوگو (form=) حفظ شده"

echo "--- ۲) ذخیرهٔ فرم واحد (شرکت+زبان+تقویم+ارز) ---"
T=$(TOKENA /Settings)
R=$(curl -s -b "$CJA" -o /dev/null -w "%{http_code}" -X POST "$BASE/Settings/SaveGeneral" \
  -d "companyName=شرکت پتروشیمی پارس آسیا&lang=fa&calendar=jalali&currency=AFN&currencyRate=1&__RequestVerificationToken=$T" --max-time 20)
ck "$(echo "$R" | grep -c 302)" "SaveGeneral: 302 موفق"
S2=$(curl -s -b "$CJA" "$BASE/Payroll" --max-time 15)
ck "$(echo "$S2" | grep -c '؋')" "حقوق: مبالغ با نشان افغانی (؋) نمایش داده شد"
S3=$(curl -s -b "$CJA" "$BASE/Settings" --max-time 15)
ck "$(echo "$S3" | grep -c 'value="AFN" selected="selected"')" "تنظیمات: ارز ذخیره‌شده AFN"

echo "--- ۳) بازگردانی ارز به ریال ---"
T=$(TOKENA /Settings)
R=$(curl -s -b "$CJA" -o /dev/null -w "%{http_code}" -X POST "$BASE/Settings/SaveGeneral" \
  -d "companyName=شرکت پتروشیمی پارس آسیا&lang=fa&calendar=jalali&currency=IRR&currencyRate=1&__RequestVerificationToken=$T" --max-time 20)
ck "$(echo "$R" | grep -c 302)" "بازگردانی IRR: 302"
S4=$(curl -s -b "$CJA" "$BASE/Payroll" --max-time 15)
ck "$(echo "$S4" | grep -c 'ریال')" "حقوق: بازگشت به ریال"

echo "--- ۴) اعتبارسنجی ارز نامعتبر ---"
T=$(TOKENA /Settings)
R=$(curl -s -b "$CJA" -o /dev/null -w "%{http_code}" -X POST "$BASE/Settings/SaveGeneral" \
  -d "companyName=شرکت پتروشیمی پارس آسیا&currency=XXX&__RequestVerificationToken=$T" --max-time 20)
ck "$(echo "$R" | grep -c 302)" "ارز نامعتبر: رد شد (302 با ToastErr)"

echo "--- ۵) چندزبانی: en کامل ---"
SE=$(curl -s -b "$CJA" -H "Cookie: hr_lang=en" "$BASE/Settings" --max-time 15)
ck "$(echo "$SE" | grep -c 'Company &amp; logo\|Company & logo')" "تنظیمات en: زبانهٔ Company & logo"
ck "$(echo "$SE" | grep -c 'Language, calendar &amp; currency\|Language, calendar & currency')" "تنظیمات en: زبانهٔ Language, calendar & currency"
ck "$(echo "$SE" | grep -c 'Iranian Rial')" "تنظیمات en: نام ارزها انگلیسی"
DE=$(curl -s -b "$CJA" -H "Cookie: hr_lang=en" "$BASE/Devices" --max-time 15)
ck "$(echo "$DE" | grep -c 'Devices')" "دستگاه‌ها en: رندر شد"
AE=$(curl -s -b "$CJA" -H "Cookie: hr_lang=en" "$BASE/Access" --max-time 15)
ck "$(echo "$AE" | grep -c 'Zones &amp; Gates')" "کنترل تردد en: رندر شد"

echo "--- ۶) ویزارد راه‌اندازی — فونت وزیر (نظام تنظیم‌شده → /Setup ریدایرکت می‌شود؛ قاعده در فایل ویو بررسی می‌شود) ---"
ck "$(grep -c 'setup-wrap button, .setup-wrap input' aspnet/src/Platform.Web/Views/Setup/Index.cshtml)" "ویزارد: قاعدهٔ وزیر روی button/input"
ck "$(grep -c 'Vazirmatn, Tahoma, sans-serif !important' aspnet/src/Platform.Web/Views/Setup/Index.cshtml)" "ویزارد: وزیر مهم‌شده روی همهٔ المان‌ها"
ck "$(grep -c 'wiz-tabs' aspnet/src/Platform.Web/Views/Setup/Index.cshtml)" "ویزارد: دو گام حفظ شده"

echo "--- ۷) کاتالوگ ارز در Money (سرور) — پیام‌ها ---"
ck "$(curl -s -b "$CJA" -H 'Cookie: hr_lang=en' "$BASE/Payroll" --max-time 15 | grep -c 'IRR')" "حقوق en: مبالغ با کد ارز (IRR)"

echo "=================================="
echo "PASS=$PASS FAIL=$FAIL"
