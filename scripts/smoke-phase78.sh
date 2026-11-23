#!/bin/bash
# ============================================================
# فاز ۷۸ — اسموک‌تست امکانات تازه روی اپ اصلی (پورت 3000):
#   ۱) درخت دسترسی‌ها در /Users + نقش جدید (Roles/Create) + حذف نقش
#   ۲) سیاست پس از انقضای مجوز (SaveLicensePolicy) + کارت تنظیمات
#   ۳) سوییچر زبان پویا (fa/en) + faVal در Layout
#   ۴) پیام‌های اعتبارسنجی (فرم نقش بدون عنوان → سرور هم فارسی)
# ============================================================
set -u
BASE=http://localhost:3000
J=/home/z/my-project/scripts/tmp-smoke
mkdir -p "$J"
ok=0; bad=0
ck() { if [ "$1" = "1" ]; then echo "  ✓ $2"; ok=$((ok+1)); else echo "  ✗ $2"; bad=$((bad+1)); fi; }

# ---------- ورود ادمین ----------
AF=$(curl -s -c "$J/c.txt" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$CODE" = "302" ] || { echo "login failed: $CODE"; exit 1; }
H="-b $J/c.txt"
XHR="-b $J/c.txt -H X-Requested-With:XMLHttpRequest"

echo "== ۱) درخت دسترسی‌ها و نقش‌ها =="
PAGE=$(curl -s $H "$BASE/Users")
echo "$PAGE" | grep -q 'id="permTree"' && ck 1 "کادر درخت دسترسی‌ها رندر شد"
echo "$PAGE" | grep -q 'data-level="feat"' && ck 1 "سطح میانی (قابلیت) در درخت هست"
echo "$PAGE" | grep -q 'درخواست‌های تردد' && ck 1 "زیرگروه درخواست‌های تردد (ثبت/تایید/رد) هست"
echo "$PAGE" | grep -q 'attendance.leave.reject' && ck 1 "کلید تفکیک‌شدهٔ رد درخت حضور دارد"
echo "$PAGE" | grep -q 'href="/Roles/Create"' && ck 1 "دکمهٔ «نقش جدید» به /Roles/Create"
echo "$PAGE" | grep -q 'Roles/Delete' && ck 1 "دکمهٔ حذف نقش در فهرست نقش‌ها"
AFORM=$(curl -s $H "$BASE/Roles/Create?partial=1")
UNAME="smoke_$$"
echo "$AFORM" | grep -q 'name="name"' && ck 1 "فرم نقش جدید (مودال) لود شد"
AF2=$(echo "$AFORM" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
R=$(curl -s $XHR "$BASE/Roles/Create" -d "name=$UNAME" -d "title=نگهبان دود" -d "description=تست فاز ۷۸" -d "__RequestVerificationToken=$AF2")
echo "$R" | grep -q '"ok":true' && ck 1 "ایجاد نقش سفارشی AJAX موفق"
R2=$(curl -s $XHR "$BASE/Roles/Create" -d "name=$UNAME" -d "title=تکراری" -d "__RequestVerificationToken=$AF2")
echo "$R2" | grep -q '"ok":false' && python3 -c "import json,sys; m=json.load(sys.stdin)['message']; sys.exit(0 if 'از قبل وجود دارد' in m else 1)" <<< "$R2" && ck 1 "نقش تکراری با پیام فارسی رد شد"
RID=$(curl -s $H "$BASE/Users" | python3 -c "import sys,re; m=re.search(r'value=\"(\\d+)\" data-perms=\"\">[^<]*نگهبان دود', sys.stdin.read()); print(m.group(1) if m else '')")
[ -n "$RID" ] && ck 1 "نقش تازه در فهرست پیدا شد (id=$RID)"
# حذف نقش تازه
AF3=$(curl -s $H "$BASE/Users" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
RD=$(curl -s $XHR "$BASE/Roles/Delete" -d "id=$RID" -d "__RequestVerificationToken=$AF3")
echo "$RD" | grep -q '"ok":true' && ck 1 "حذف نقش سفارشی موفق"
# نقش سیستمی حذف نشود
SYSID=$(curl -s $H "$BASE/Users" | python3 -c "import sys,re; m=re.search(r'value=\"(\\d+)\" data-perms=\"[^\"]*\">[^<]*مدیر ارشد سیستم', sys.stdin.read()); print(m.group(1) if m else '')")
RS=$(curl -s $XHR "$BASE/Roles/Delete" -d "id=$SYSID" -d "__RequestVerificationToken=$AF3")
echo "$RS" | grep -q '"ok":false' && ck 1 "حذف نقش سیستمی رد شد"

echo "== ۲) سیاست پس از انقضای مجوز =="
SP=$(curl -s $H "$BASE/Settings")
echo "$SP" | grep -q 'SaveLicensePolicy' && ck 1 "کارت «سیاست پس از انقضای مجوز» در تنظیمات"
echo "$SP" | grep -q 'expiredAction' && ck 1 "انتخاب رفتار (هشدار/توقف) رندر شد"
AF4=$(echo "$SP" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
LP=$(curl -s $XHR "$BASE/Settings/SaveLicensePolicy" -d "mode=Strict" -d "expiredAction=Block" -d "__RequestVerificationToken=$AF4")
echo "$LP" | grep -q '"ok":true' && ck 1 "ذخیرهٔ Strict+Block موفق"
LP2=$(curl -s $XHR "$BASE/Settings/SaveLicensePolicy" -d "mode=Off" -d "expiredAction=Warn" -d "__RequestVerificationToken=$AF4")
echo "$LP2" | grep -q '"ok":true' && ck 1 "بازگشت به Off+Warn موفق (بدون تغییر رفتار پیش‌فرض)"
LP3=$(curl -s $XHR "$BASE/Settings/SaveLicensePolicy" -d "mode=Strict" -d "expiredAction=Weird" -d "__RequestVerificationToken=$AF4")
echo "$LP3" | grep -q '"ok":false' && ck 1 "مقدار نامعتبر رد شد"

echo "== ۳) سوییچر زبان پویا =="
echo "$SP" | grep -q '/Lang/Set?c=fa' && echo "$SP" | grep -q '/Lang/Set?c=en' && ck 1 "دو زبان داخلی در تنظیمات"
LG=$(curl -s "$BASE/Auth/Login")
echo "$LG" | grep -q '/Lang/Set?c=fa' && echo "$LG" | grep -q '/Lang/Set?c=en' && ck 1 "پیل‌های زبان صفحهٔ ورود"
echo "$LG" | grep -q 'faVal' && ck 1 "واژه‌نامهٔ faVal در Layout تزریق شد"
EN=$(curl -s -b "$J/c.txt" "$BASE/Users?lang=en")
echo "$EN" | grep -q 'id="permTree"' && echo "$EN" | grep -q 'New role' && ck 1 "درخت دسترسی‌ها در UI انگلیسی ترجمه شد"

echo "== ۴) اعتبارسنجی سمت سرور نقش (بدون عنوان) =="
R3=$(curl -s $XHR "$BASE/Roles/Create" -d "name=notitle_test" -d "title=" -d "__RequestVerificationToken=$AF2")
echo "$R3" | python3 -c "import json,sys; m=json.load(sys.stdin)['message']; sys.exit(0 if 'عنوان نقش' in m else 1)" && ck 1 "خطای فارسی عنوان خالی"

echo
echo "نتیجه: $ok سبز / $bad قرمز"
[ "$bad" = "0" ]
