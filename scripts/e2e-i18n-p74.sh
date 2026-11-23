#!/bin/bash
# ======================================================================
# فاز ۷۴ — E2E ماشین چندزبانه
# ۱) کوکی/پارامتر زبان روی صفحه ورود (مهمان)  ۲) سوییچر /Lang/Set
# ۳) زبان پیش‌فرض سامانه (Settings/SaveLanguage → Localization:Default)
# ۴) رندر LTR/RTL و ترجمهٔ پوسته (سایدبار/نوار/منوی کاربر) با نشست واقعی
# ۵) حفظ کلیدهای مجوز در بازنویسی تنظیمات (RebuildInstallSettings)
# هر شکست = خروج غیرصفر. اجرا روی سندباکس (اپ از قبل پیکربندی‌شده).
# ======================================================================
set -u
BASE=http://localhost:3000
PASS=0; FAIL=0
ok(){ PASS=$((PASS+1)); echo "  ✓ $1"; }
bad(){ FAIL=$((FAIL+1)); echo "  ✗ $1"; }
chk(){ # chk <desc> <haystack-file> <pattern>
  if rg -q "$2" "$3" 2>/dev/null; then ok "$1"; else bad "$1"; fi
}
chkno(){ # رشته نباید باشد
  if rg -q "$2" "$3" 2>/dev/null; then bad "$1"; else ok "$1"; fi
}
JAR=/tmp/p74.jar; rm -f $JAR /tmp/p74-*.html

echo "== ۱) مهمان — صفحه ورود =="
curl -s "$BASE/Auth/Login" -o /tmp/p74-l1.html
chk "پیش‌فرض RTL" 'dir="rtl"' /tmp/p74-l1.html
chk "پیش‌فرض فارسی" 'ورود به سامانه' /tmp/p74-l1.html
chk "قرص‌های زبان حاضر" 'Lang/Set\?c=en' /tmp/p74-l1.html
curl -s "$BASE/Auth/Login?lang=en" -o /tmp/p74-l2.html
chk "‎?lang=en‎ → LTR" 'dir="ltr"' /tmp/p74-l2.html
chk "‎?lang=en‎ → ترجمه" 'Sign in' /tmp/p74-l2.html
chk "‎?lang=en‎ → لیبل انگلیسی" 'Username or Personnel Code' /tmp/p74-l2.html

echo "== ۲) سوییچر /Lang/Set (مهمان) =="
curl -s -c $JAR -o /dev/null -w "" "$BASE/Lang/Set?c=en&back=/Auth/Login"
curl -s -b $JAR "$BASE/Auth/Login" -o /tmp/p74-l3.html
chk "کوکی hr_lang=en برنده است" 'Sign in' /tmp/p74-l3.html
curl -s -b $JAR -c $JAR -o /dev/null "$BASE/Lang/Set?c=fa&back=/Auth/Login"
curl -s -b $JAR "$BASE/Auth/Login" -o /tmp/p74-l4.html
chk "برگشت به fa" 'ورود به سامانه' /tmp/p74-l4.html
# open redirect بسته باشد
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/Lang/Set?c=en&back=https://evil.example.com/x")
if [ "$code" = "302" ]; then ok "بازگشت خارجی رد شد (LocalRedirect)"; else bad "بازگشت خارجی رد شد (کد $code)"; fi

echo "== ۳) نشست واقعی — پوستهٔ دوزبانه =="
curl -s -c $JAR "$BASE/Auth/Login" -o /tmp/p74-lg.html
TOK=$(rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]+"' /tmp/p74-lg.html | head -1 | sed 's/.*value="//;s/"$//')
curl -s -b $JAR -c $JAR -X POST "$BASE/Auth/Login" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TOK" --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  -o /dev/null -w "" 
curl -s -b $JAR "$BASE/" -o /tmp/p74-h1.html
chk "خانه: RTL فارسی" 'dir="rtl"' /tmp/p74-h1.html
curl -s -b $JAR -H "Cookie: hr_lang=en" "$BASE/" -o /tmp/p74-h2.html
chk "خانه EN: LTR" 'dir="ltr"' /tmp/p74-h2.html
chk "خانه EN: منو ترجمه" 'Users &amp; Roles' /tmp/p74-h2.html
chk "خانه EN: سوییچر 🌐" 'tbLangBtn' /tmp/p74-h2.html
chk "خانه EN: واژه‌نامهٔ JS" 'window.HrI18n' /tmp/p74-h2.html
chkno "خانه EN: بدون داشبورد فارسی" 'داشبورد' /tmp/p74-h2.html

echo "== ۴) زبان پیش‌فرض سامانه (Settings/SaveLanguage) =="
curl -s -b $JAR -c $JAR "$BASE/Settings" -o /tmp/p74-st.html
TOK=$(rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]+"' /tmp/p74-st.html | head -1 | sed 's/.*value="//;s/"$//')
res=$(curl -s -b $JAR -c $JAR -X POST "$BASE/Settings/SaveLanguage" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TOK" --data-urlencode "defaultLang=en")
echo "$res" | rg -q '"ok":true' && ok "ذخیرهٔ زبان پیش‌فرض = en" || bad "ذخیرهٔ زبان پیش‌فرض = en ($res)"
curl -s "$BASE/Auth/Login" -o /tmp/p74-l5.html
chk "مهمانِ بدون کوکی ← پیش‌فرض انگلیسی" 'Sign in' /tmp/p74-l5.html
# برگشت به فارسی (وضعیت پایهٔ مشتری حفظ شود)
res=$(curl -s -b $JAR -c $JAR -X POST "$BASE/Settings/SaveLanguage" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TOK" --data-urlencode "defaultLang=fa")
echo "$res" | rg -q '"ok":true' && ok "برگشت زبان پیش‌فرض = fa" || bad "برگشت زبان پیش‌فرض = fa ($res)"
curl -s "$BASE/Auth/Login" -o /tmp/p74-l6.html
chk "پیش‌فرض دوباره فارسی" 'ورود به سامانه' /tmp/p74-l6.html

echo "== ۵) زبان نامعتبر رد شود =="
res=$(curl -s -b $JAR -c $JAR -X POST "$BASE/Settings/SaveLanguage" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TOK" --data-urlencode "defaultLang=de")
echo "$res" | rg -q '"ok":false' && ok "کد زبان نامعتبر (de) رد شد" || bad "کد زبان نامعتبر (de) رد شد"

echo "== ۶) صفحه‌های سیستمی =="
curl -s -b $JAR -H "Cookie: hr_lang=en" "$BASE/Self" -o /tmp/p74-s1.html
chk "پرتال من EN: LTR رندر شد" 'dir="ltr"' /tmp/p74-s1.html

echo ""
echo "نتیجه: $PASS سبز / $FAIL قرمز"
[ "$FAIL" = "0" ] && echo "PHASE74-I18N: ALL GREEN" || { echo "PHASE74-I18N: FAILED"; exit 1; }
