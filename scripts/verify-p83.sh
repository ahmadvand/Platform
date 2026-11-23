#!/bin/bash
# ============================================================
# فاز ۸۳ — صحت‌سنجی زمان‌اجرای ۷ اصلاح کاربر (بدون تغییر کد)
# ① منوی زبان‌ها و ترجمه در حالت انگلیسی  ⑦ زبان پیش‌فرض در تنظیمات
# ⑥ فاصلهٔ عمودی دکمه‌ها (CSS)  ② سرکوب پیام اعتبارسنجی مرورگر (JS)
# ============================================================
set -u
BASE=http://localhost:3000
J=/tmp/hr83.cookies
rm -f "$J"

pass() { echo "PASS: $1"; }
fail() { echo "FAIL: $1"; }

# --- ورود مدیر ارشد ---
af=$(curl -s -c "$J" "$BASE/Auth/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
[ -z "$af" ] && af=$(curl -s -c "$J" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
code=$(curl -s -b "$J" -c "$J" -o /tmp/hr83.login.html -w '%{http_code}' -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$af" -H "X-Requested-With: XMLHttpRequest")
echo "login POST -> $code"
grep -q 'hr_at' "$J" && pass "login cookie hr_at set" || fail "login cookie hr_at missing"

# --- ① برچسب ناوبری در حالت انگلیسی ---
curl -s -b "$J" -c "$J" -o /tmp/hr83.home-en.html -H 'Cookie: hr_lang=en' "$BASE/"
if grep -q 'Languages &amp; Translation' /tmp/hr83.home-en.html && ! grep -q 'زبان‌ها و ترجمه' /tmp/hr83.home-en.html; then
  pass "① nav label is English (Languages & Translation)"
else
  fail "① nav label still Persian or missing translation"
  grep -o 'زبان‌ها و ترجمه' /tmp/hr83.home-en.html | head -2
fi
# گروه‌های منو هم انگلیسی باشند
if grep -q 'System Administration' /tmp/hr83.home-en.html; then pass "① nav group labels English"; else fail "① group label untranslated"; fi

# --- ⑦ کارت زبان پیش‌فرض در تنظیمات (fa) ---
curl -s -b "$J" -c "$J" -o /tmp/hr83.settings-fa.html "$BASE/Settings"
grep -q 'زبان پیش‌فرض سامانه' /tmp/hr83.settings-fa.html && pass "⑦ default-language card present (fa)" || fail "⑦ card missing (fa)"
grep -q '/Settings/SaveDefaultLang' /tmp/hr83.settings-fa.html && pass "⑦ SaveDefaultLang form present" || fail "⑦ form missing"

# --- ⑦ تغییر زبان پیش‌فرض به en و اثرش روی صفحهٔ ورود بدون کوکی ---
af2=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' /tmp/hr83.settings-fa.html | head -1 | sed 's/.*value="//;s/"$//')
code=$(curl -s -b "$J" -c "$J" -o /tmp/hr83.sdl.json -w '%{http_code}' -X POST "$BASE/Settings/SaveDefaultLang" \
  --data-urlencode "lang=en" --data-urlencode "__RequestVerificationToken=$af2" -H "X-Requested-With: XMLHttpRequest")
echo "SaveDefaultLang(en) -> $code $(cat /tmp/hr83.sdl.json | head -c 120)"
grep -q '"ok":true' /tmp/hr83.sdl.json && pass "⑦ SaveDefaultLang(en) ok" || fail "⑦ SaveDefaultLang failed"
# صفحهٔ ورود با کلاینت تازه (بدون کوکی hr_lang) باید انگلیسی شود
code=$(curl -s -o /tmp/hr83.login-en.html -w '%{http_code}' "$BASE/Auth/Login")
if [ "$code" = "200" ] && grep -q 'dir="ltr"' /tmp/hr83.login-en.html && grep -q 'Sign in' /tmp/hr83.login-en.html; then
  pass "⑦ login page honors admin default language (en/ltr)"
else
  fail "⑦ login page did not switch to en (code=$code)"
fi
# بازگرداندن به fa
code=$(curl -s -b "$J" -c "$J" -o /tmp/hr83.sdl2.json -X POST "$BASE/Settings/SaveDefaultLang" \
  --data-urlencode "lang=fa" --data-urlencode "__RequestVerificationToken=$af2" -H "X-Requested-With: XMLHttpRequest")
grep -q '"ok":true' /tmp/hr83.sdl2.json && pass "⑦ SaveDefaultLang(fa) restore ok" || fail "⑦ restore failed"
code=$(curl -s -o /tmp/hr83.login-fa.html -w '%{http_code}' "$BASE/Auth/Login")
grep -q 'dir="rtl"' /tmp/hr83.login-fa.html && pass "⑦ login back to fa/rtl" || fail "⑦ fa restore not applied"

# --- ⑥ قاعدهٔ فاصلهٔ عمودی دکمه در CSS سرو‌شده ---
css=$(curl -s "$BASE/css/site.css")
echo "$css" | grep -q 'form > .btn, fieldset > .btn { margin-top: 16px; }' \
  && pass "⑥ css rule form>btn margin-top:16px served" || fail "⑥ css rule missing"
echo "$css" | grep -q '.btn-row { display: flex; gap: 6px; flex-wrap: wrap; align-items: center; margin-top: 16px; }' \
  && pass "⑥ .btn-row margin-top served" || fail "⑥ .btn-row margin missing"

# --- ② سرکوب اعتبارسنجی مرورگر در JS سرو‌شده ---
js=$(curl -s "$BASE/js/site.js")
echo "$js" | grep -q "addEventListener('invalid', function (e) {" \
  && pass "② native validation bubble suppressed (site.js served)" || fail "② suppression handler missing"
echo "$js" | grep -q "This field is required" \
  && pass "② localized validation messages (fa/en) in site.js" || fail "② messages missing"
jslang=$(echo "$js" | grep -o "var LANG = '[a-z]*'" | head -1)
echo "  site.js LANG = $jslang"

echo "=== done ==="
