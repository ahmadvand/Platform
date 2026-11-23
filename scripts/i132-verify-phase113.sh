#!/bin/bash
# فاز ۱۱۳ — راستی‌آزمایی صفحات زنده
cd /home/z/my-project || exit 1
CK=/tmp/i132-cookies.txt
rm -f "$CK"
get_token() { curl -s -b "$CK" -c "$CK" "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//'; }
post() { curl -s -b "$CK" -c "$CK" -o /dev/null -w "%{http_code}" -X POST "$1" "${@:2}"; }

T=$(get_token http://127.0.0.1:3000/Auth/Login)
echo "login: $(post http://127.0.0.1:3000/Auth/Login \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$T")"
T=$(get_token http://127.0.0.1:3000/License/Notice)
echo "ack:   $(post http://127.0.0.1:3000/License/Ack --data-urlencode "__RequestVerificationToken=$T")"

curl -s -b "$CK" http://127.0.0.1:3000/Setup > /tmp/i132-setup.html
curl -s -b "$CK" http://127.0.0.1:3000/Settings > /tmp/i132-settings.html
curl -s -b "$CK" http://127.0.0.1:3000/ > /tmp/i132-home.html

python3 - << 'PY'
import html,re
def has(f, *needles):
    s = html.unescape(open(f, encoding='utf-8').read())
    return [(n, n in s) for n in needles]
print("== /Setup (ویزارد سه‌تبی) ==")
for n, ok in has('/tmp/i132-setup.html', 'wizTab3', 'انتخاب زبان', 'اطلاعات بانک داده', 'اطلاعات مجوز',
                 'ادامه به بانک داده', 'بازگشت به زبان', 'btnToStep3', 'btnBackStep2', 'switchTab(3)'):
    print((' ✓' if ok else ' ✗'), n)
print("== /Settings (قفل محافظ صفحه) ==")
for n, ok in has('/tmp/i132-settings.html', 'screensaverLock', 'درخواست رمز عبور برای بازگشت', 'بازگشت فقط با رمز کاربر جاری'):
    print((' ✓' if ok else ' ✗'), n)
print("== / (Layout) ==")
s = html.unescape(open('/tmp/i132-home.html', encoding='utf-8').read())
for n in ['data-saver-idle', 'data-saver-lock', 'site.js']:
    print((' ✓' if n in s else ' ✗'), n)
m = re.search(r'data-saver-idle="(\d+)" data-saver-lock="(\d+)"', s)
print(' attrs:', m.groups() if m else 'NOT FOUND')
v = re.search(r'نسخه[:\s]*([\u06F0-\u06F9.\d]+)', s)
print(' footer version:', v.group(1) if v else '?')
PY

# endpoint قفل: POST بدون توکن باید ۴۰۰/۴۰۱ بدهد، نه ۴۰۴
CODE=$(curl -s -b "$CK" -o /dev/null -w "%{http_code}" -X POST http://127.0.0.1:3000/Account/VerifySaverLock -H "X-Requested-With: XMLHttpRequest")
echo "POST /Account/VerifySaverLock (بدون توکن CSRF): $CODE  (انتظار 400 = مسیر موجود)"
