#!/usr/bin/env bash
# ============================================================
# فاز ۷۴ — ماشین چندزبانه (fa/en)
# ۱) پیش‌فرض فارسی (RTL) — بدون هیچ تغییر رفتار برای نصب‌های فعلی
# ۲) ‎?lang=en‎ → رندر LTR انگلیسی + ثبت کوکی hr_lang
# ۳) /Lang/Set → 302 محلی + کوکی یک‌ساله
# ۴) انتخاب شخصی کاربر (کوکی) روی صفحه‌های احراز-شده (نو/برچسب/پرم)
# ۵) زبان پیش‌فرض سامانه از تنظیمات (SaveLanguage) — کاربر بدون انتخاب
# ۶) بازگشت به فارسی و بازگردانی تنظیمات (پاکسازی)
# اجرا: bash scripts/e2e-l10n.sh
# ============================================================
set -u
cd "$(dirname "$0")/.."
BASE="http://localhost:3000"
PASS=0; FAIL=0
ck() { if [ "${1:-0}" -ge 1 ] 2>/dev/null; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }

# ---------- نشست مدیر (فارسی) ----------
CJA=/tmp/l10n-admin.txt; rm -f "$CJA"
TOKENA() { curl -s -b "$CJA" -c "$CJA" "$BASE$1" --max-time 15 | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
T=$(TOKENA /Auth/Login)
curl -s -b "$CJA" -c "$CJA" -o /dev/null -X POST "$BASE/Auth/Login" \
  -d "identifier=admin&secret=Admin@1234&__RequestVerificationToken=$T" --max-time 15

echo "--- ۱) پیش‌فرض فارسی (بدون کوکی) ---"
H=$(curl -s "$BASE/Auth/Login" --max-time 15)
ck "$(echo "$H" | grep -c 'lang="fa" dir="rtl"')" "ورود: پیش‌فرض fa و rtl"
ck "$(echo "$H" | grep -c 'ورود به سامانه')" "ورود: متن فارسی دست‌نخورده"

echo "--- ۲) ‎?lang=en‎ روی صفحه ورود ---"
H=$(curl -s -i "$BASE/Auth/Login?lang=en" --max-time 15)
ck "$(echo "$H" | grep -c 'lang="en" dir="ltr"')" "ورود: en و ltr"
ck "$(echo "$H" | grep -c 'Sign in')" "ورود: برچسب انگلیسی"
ck "$(echo "$H" | grep -ic 'set-cookie: hr_lang=en')" "ورود: کوکی hr_lang ثبت شد"

echo "--- ۳) /Lang/Set ---"
R=$(curl -s -o /dev/null -w "%{http_code}|%{redirect_url}" "$BASE/Lang/Set?c=fa&back=/Auth/Login" --max-time 15)
ck "$(echo "$R" | grep -c '302|')" "/Lang/Set: 302"
ck "$(echo "$R" | grep -c '|.*/Auth/Login')" "/Lang/Set: برگشت محلی به همان صفحه"

echo "--- ۴) صفحه‌های احرازشده با کوکی en ---"
DA=$(curl -s -b "hr_lang=en" -b "$CJA" "$BASE/" --max-time 15)
ck "$(echo "$DA" | grep -c 'lang="en" dir="ltr"')" "داشبورد: en/ltr"
ck "$(echo "$DA" | grep -c '>Dashboard<')" "داشبورد: آیتم منوی Dashboard"
ck "$(echo "$DA" | grep -c 'Unified Organization Suite')" "داشبورد: زیرعنوان برند انگلیسی"
UA=$(curl -s -b "hr_lang=en" -b "$CJA" "$BASE/Users" --max-time 15)
ck "$(echo "$UA" | grep -c 'System Users')" "کاربران: عنوان انگلیسی"
ck "$(echo "$UA" | grep -c 'View dashboard')" "کاربران: عنوان دسترسی‌ها انگلیسی"
ck "$(echo "$UA" | grep -c 'HR Manager')" "کاربران: عنوان نقش انگلیسی"
SA=$(curl -s -b "hr_lang=en" -b "$CJA" "$BASE/Settings" --max-time 15)
ck "$(echo "$SA" | grep -c 'Default system language')" "تنظیمات: کارت زبان انگلیسی"
LG=$(curl -s -b "hr_lang=en" -b "$CJA" "$BASE/Auth/Logout" --max-time 15 -o /dev/null -w "%{http_code}")

echo "--- ۵) زبان پیش‌فرض سامانه (SaveLanguage → en؛ کاربر بدون انتخاب) ---"
T=$(TOKENA /Auth/Login)
curl -s -b "$CJA" -c "$CJA" -o /dev/null -X POST "$BASE/Auth/Login" \
  -d "identifier=admin&secret=Admin@1234&__RequestVerificationToken=$T" --max-time 15
T=$(TOKENA /Settings)
J=$(curl -s -b "$CJA" -c "$CJA" -X POST "$BASE/Settings/SaveLanguage" \
  -H "X-Requested-With: XMLHttpRequest" \
  -d "defaultLang=en&__RequestVerificationToken=$T" --max-time 15)
ck "$(echo "$J" | grep -c '"ok":true')" "SaveLanguage(en): ok"
H=$(curl -s "$BASE/Auth/Login" --max-time 15)   # بدون هیچ کوکی زبان
ck "$(echo "$H" | grep -c 'lang="en" dir="ltr"')" "کاربر بدون انتخاب: پیش‌فرض جدید en اعمال شد"

echo "--- ۶) پاکسازی: بازگردانی پیش‌فرض فارسی + پاک‌سازی کوکی‌ها ---"
T=$(TOKENA /Settings)
J=$(curl -s -b "$CJA" -c "$CJA" -X POST "$BASE/Settings/SaveLanguage" \
  -H "X-Requested-With: XMLHttpRequest" \
  -d "defaultLang=fa&__RequestVerificationToken=$T" --max-time 15)
ck "$(echo "$J" | grep -c '"ok":true')" "SaveLanguage(fa): ok"
H=$(curl -s "$BASE/Auth/Login" --max-time 15)
ck "$(echo "$H" | grep -c 'lang="fa" dir="rtl"')" "بازگشت به پیش‌فرض فارسی"

echo ""
echo "نتیجه: $PASS سبز / $FAIL قرمز"
[ "$FAIL" = "0" ]
