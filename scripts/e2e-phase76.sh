#!/bin/bash
# ============================================================
# E2E فاز ۷۶ — چندزبانه (fa/en) + انتخاب زبان در نصب اولیه + نمای دسترسی‌های مؤثر
#  ۱) CultureMiddleware: کوکی hr_lang ← کلید Ui:Language (ویزارد نصب) ← fa
#  ۲) سوئیچر /Ui/Lang — کوکی یک‌ساله + بازگشت محلی + کد نامعتبر نادیده
#  ۳) ترجمهٔ پوسته/ورود/داشبورد + dir=ltr + ارقام لاتین در en
#  ۴) نمای /Users/Perms/{id} — دسترسی مؤثر + منشأ کلید + گارد users.view
#  ۵) سلامت دیکشنری (کلید تکراری = کرش static-ctor → خودش را در لاگین نشان می‌دهد)
# ============================================================
B=http://localhost:3000
AJ=/tmp/p76-admin.jar
VJ=/tmp/p76-viewer.jar
rm -f "$AJ" "$VJ"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "PASS $1"; }
bad() { FAIL=$((FAIL+1)); echo "FAIL $1"; }

# ---------- ۰) سلامت + ورود (سلامت دیکشنری: کلید تکراری اینجا ۵۰۰ می‌دهد) ----------
code=$(curl -s -o /dev/null -w "%{http_code}" "$B/health")
[ "$code" = "200" ] && ok "health" || { bad "health ($code)"; exit 1; }
code=$(curl -s -c "$AJ" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && ok "login admin — static-ctor دیکشنری سالم" || { bad "login admin ($code)"; exit 1; }

# ---------- ۱) پیش‌فرض fa (بدون کوکی) ----------
H=$(curl -s "$B/Auth/Login")
echo "$H" | grep -q 'نام کاربری' && ok "صفحه ورود پیش‌فرض fa" || bad "صفحه ورود پیش‌فرض fa"
echo "$H" | grep -q 'dir="rtl"' && ok "html dir=rtl در fa" || bad "html dir=rtl در fa"

# ---------- ۲) کوکی hr_lang=en → ورود انگلیسی ----------
H=$(curl -s -b "hr_lang=en" "$B/Auth/Login")
echo "$H" | grep -q 'Username' && ok "ورود en (Username)" || bad "ورود en (Username)"
echo "$H" | grep -q 'dir="ltr"' && ok "html dir=ltr در en" || bad "html dir=ltr در en"
echo "$H" | grep -q 'Sign in' && ok "دکمهٔ Sign in" || bad "دکمهٔ Sign in"

# ---------- ۳) سوئیچر /Ui/Lang ----------
R=$(curl -s -o /dev/null -D - -w "%{http_code}" "$B/Ui/Lang?l=en&r=%2FAuth%2FLogin")
echo "$R" | grep -q '^302' && ok "Ui/Lang → 302 محلی" || bad "Ui/Lang → $(echo "$R" | head -1)"
echo "$R" | grep -qi 'hr_lang=en' && ok "Set-Cookie hr_lang=en" || bad "Set-Cookie hr_lang=en غایب"
R=$(curl -s -o /dev/null -D - "$B/Ui/Lang?l=fr")
echo "$R" | grep -qi 'hr_lang=' && bad "کد نامعتبر fr نباید کوکی ست کند" || ok "کد نامعتبر fr نادیده شد"

# ---------- ۴) داشبورد انگلیسی (کوکی از سوئیچر داخل شیشهٔ ادمین) ----------
curl -s -b "$AJ" -c "$AJ" -o /dev/null "$B/Ui/Lang?l=en&r=%2F"
H=$(curl -s -b "$AJ" "$B/")
echo "$H" | grep -q 'dir="ltr"' && ok "داشبورد en — dir=ltr" || bad "داشبورد en — dir=ltr"
echo "$H" | grep -q 'Integrated Human Resources System' && ok "برند en" || bad "برند en"
echo "$H" | grep -q '>Companies<' && ok "منوی en (Companies)" || bad "منوی en (Companies)"
echo "$H" | grep -q 'Present today' && ok "KPI en (Present today)" || bad "KPI en (Present today)"
echo "$H" | grep -qE 'ps-v t">[0-9]+' && ok "ارقام لاتین KPI در en" || bad "ارقام لاتین KPI در en"
echo "$H" | grep -q 'Latest access-control events' && ok "جدول رویداد en" || bad "جدول رویداد en"
echo "$H" | grep -q "legend: { position: 'bottom', rtl: false }" && ok "نمودار legend rtl=false در en" || bad "نمودار legend rtl=false در en"

# ---------- ۵) بازگشت به fa ----------
curl -s -b "$AJ" -c "$AJ" -o /dev/null "$B/Ui/Lang?l=fa&r=%2F"
H=$(curl -s -b "$AJ" "$B/")
echo "$H" | grep -q 'dir="rtl"' && ok "بازگشت dir=rtl" || bad "بازگشت dir=rtl"
echo "$H" | grep -q '>شرکت‌ها<' && ok "منوی fa (شرکت‌ها)" || bad "منوی fa (شرکت‌ها)"
echo "$H" | grep -q 'حاضران امروز' && ok "KPI fa" || bad "KPI fa"
echo "$H" | grep -qE 'ps-v t">[۰۱۲۳۴۵۶۷۸۹]+' && ok "ارقام فارسی KPI در fa" || bad "ارقام فارسی KPI در fa"

# ---------- ۶) نمای دسترسی‌های مؤثر (پیشنهاد ثبت‌شدهٔ فاز ۷۵) ----------
code=$(curl -s -b "$AJ" -o /tmp/p76-perms.html -w "%{http_code}" "$B/Users/Perms/1")
[ "$code" = "200" ] && ok "/Users/Perms/1 برای ادمین → 200" || bad "/Users/Perms/1 → $code"
grep -q 'superadmin' /tmp/p76-perms.html && ok "پرچم superadmin" || bad "پرچم superadmin"
grep -qE 'parking\.scan|users\.view|dashboard\.view' /tmp/p76-perms.html && ok "کلیدهای کاتالوگ" || bad "کلیدهای کاتالوگ"
grep -q 'مستقیم\|والد\|wildcard\|superadmin' /tmp/p76-perms.html && ok "ستون منشأ" || bad "ستون منشأ"
H=$(curl -s -b "$AJ" "$B/Users")
echo "$H" | grep -q 'دسترسی‌های مؤثر' && ok "دکمهٔ «دسترسی‌ها» روی جدول کاربران" || bad "دکمهٔ «دسترسی‌ها» روی جدول کاربران"
code=$(curl -s -c "$VJ" -H "Content-Type: application/json" -d '{"username":"viewer","password":"View@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && ok "login viewer" || bad "login viewer ($code)"
code=$(curl -s -b "$VJ" -o /dev/null -w "%{http_code}" "$B/Users/Perms/1")
[ "$code" = "302" ] && ok "کاربر بدون users.view → 302" || bad "کاربر بدون users.view → $code"

# ---------- ۷) گیت ویزارد (نصب‌شده → داشبورد) ----------
code=$(curl -s -o /dev/null -w "%{http_code}" "$B/Setup")
[ "$code" = "302" ] && ok "/Setup → 302 (نصب‌شده — گیت سالم)" || bad "/Setup → $code"

echo
echo "PASS=$PASS FAIL=$FAIL"
[ $FAIL -eq 0 ]
