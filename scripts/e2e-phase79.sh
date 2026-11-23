#!/bin/bash
# ============================================================
# E2E فاز ۷۹ — سامانهٔ تقویم (شمسی/قمری/میلادی)
#  ۱) کارت «تقویم سامانه» در /Settings + پیش‌نمایش هر سه تقویم + HrDateCfg کلاینت
#  ۲) SaveCalendar قمری → نوار بالایی/شبکهٔ تعطیلات/پیکربندی JS بلافاصله قمری (بدون ری‌استارت)
#  ۳) SaveCalendar میلادی → هفته از دوشنبه + نام ماه نوامبر + میلادی
#  ۴) کد نامعتبر رد می‌شود؛ گارد settings.manage (کاربر بی‌دسترسی → 302)
#  ۵) بازگشت به شمسی + مقدار DB + دود صفحات تاریخ‌دار (تعطیلات/گزارش/درخواست/حضور/حقوق)
# ============================================================
B=http://localhost:3000
AJ=/tmp/p79-admin.jar
VJ=/tmp/p79-viewer.jar
DB="python3.13 /home/z/my-project/scripts/dbq.py"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "PASS $1"; }
bad() { FAIL=$((FAIL+1)); echo "FAIL $1"; }
token() { curl -s -b "$1" -c "$1" "$B$2" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }

# ---------- ۰) سلامت + ورود ----------
code=$(curl -s -o /dev/null -w "%{http_code}" "$B/health")
[ "$code" = "200" ] && ok "health" || { bad "health ($code)"; exit 1; }
code=$(curl -s -c "$AJ" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && ok "login admin" || { bad "login admin ($code)"; exit 1; }

# ---------- ۱) پیش‌فرض شمسی ----------
H=$(curl -s -b "$AJ" "$B/Settings")
echo "$H" | grep -q 'تقویم سامانه' && ok "کارت «تقویم سامانه» در /Settings" || bad "کارت تقویم غایب"
echo "$H" | grep -q "cal: 'jalali'" && ok "HrDateCfg cal=jalali" || bad "HrDateCfg cal جلالی نیست"
echo "$H" | grep -q 'value="hijri"' && echo "$H" | grep -q 'value="gregorian"' && ok "سه گزینهٔ تقویم رندر شد" || bad "گزینه‌های تقویم ناقص"
echo "$H" | grep -q 'value="jalali" checked' && ok "پیش‌فرض شمسی تیک" || bad "تیک پیش‌فرض"
echo "$H" | grep -q 'آبان' && echo "$H" | grep -q 'جمادی' && echo "$H" | grep -q 'نوامبر' && ok "پیش‌نمایش «امروز» در هر سه تقویم" || bad "پیش‌نمایش سه تقویم ناقص"
D=$(curl -s -b "$AJ" "$B/")
echo "$D" | grep -oE 'tb-date[^<]*>[^<]*<' | head -1 | grep -q 'آبان\|آذر' && ok "نوار بالایی شمسی (ماه آبان/آذر)" || { echo "$D" | grep -q 'آبان\|آذر' && ok "نوار بالایی شمسی" || bad "نوار بالایی شمسی نیست"; }

# ---------- ۲) قمری ----------
T=$(token "$AJ" /Settings)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" --data-urlencode "calendar=hijri" "$B/Settings/SaveCalendar")
echo "$R" | grep -q '"ok":true' && ok "SaveCalendar hijri → ok" || bad "SaveCalendar hijri → $R"
CALDB=$($DB --json "SELECT Calendar FROM AppSettings WHERE Id='default'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Calendar'])" 2>/dev/null)
[ "$CALDB" = "hijri" ] && ok "DB AppSettings.Calendar=hijri" || bad "DB Calendar=$CALDB"
D=$(curl -s -b "$AJ" "$B/")
echo "$D" | grep -q "cal: 'hijri'" && ok "HrDateCfg → hijri (بدون ری‌استارت)" || bad "HrDateCfg hijri نشد"
echo "$D" | grep -q 'جمادی\|ربیع\|رجب\|شعبان\|رمضان\|شوال\|محرم\|صفر\|ذی' && ok "نوار بالایی قمری" || bad "نوار بالایی قمری نیست"
HOL=$(curl -s -b "$AJ" "$B/Holidays")
echo "$HOL" | grep -q 'جدید\|تقویم' && ok "/Holidays در حالت قمری ۲۰۰" || bad "/Holidays قمری"
echo "$HOL" | grep -q 'محرم\|صفر\|ربیع\|جمادی\|رجب\|شعبان\|رمضان\|شوال\|ذی' && ok "شبکهٔ تعطیلات قمری" || bad "شبکهٔ تعطیلات قمری نیست"

# ---------- ۳) میلادی ----------
T=$(token "$AJ" /Settings)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" --data-urlencode "calendar=gregorian" "$B/Settings/SaveCalendar")
echo "$R" | grep -q '"ok":true' && ok "SaveCalendar gregorian → ok" || bad "SaveCalendar gregorian → $R"
D=$(curl -s -b "$AJ" "$B/")
echo "$D" | grep -q "cal: 'gregorian'" && ok "HrDateCfg → gregorian" || bad "HrDateCfg gregorian نشد"
echo "$D" | grep -q 'نوامبر\|اکتبر\|دسامبر\|ژانویه' && ok "نوار بالایی میلادی (نام‌دار fa)" || bad "نوار بالایی میلادی نیست"
HOL=$(curl -s -b "$AJ" "$B/Holidays")
echo "$HOL" | grep -q 'نوامبر\|اکتبر\|دسامبر' && ok "شبکهٔ تعطیلات میلادی" || bad "شبکهٔ تعطیلات میلادی نیست"
echo "$HOL" | grep -q '>دوشنبه<' && ok "سربرگ هفته از دوشنبه (میلادی)" || bad "سربرگ دوشنبه نیست"
RPT=$(curl -s -b "$AJ" "$B/Reports/Custom")
echo "$RPT" | grep -q 'type="date"' && ok "ورودی تاریخ گزارش در حالت میلادی حاضر" || bad "ورودی تاریخ گزارش غایب"

# ---------- ۴) گاردها ----------
T=$(token "$AJ" /Settings)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" --data-urlencode "calendar=fr" "$B/Settings/SaveCalendar")
echo "$R" | grep -q '"ok":false' && ok "کد نامعتبر fr رد شد" || bad "کد نامعتبر پذیرفته شد → $R"
code=$(curl -s -c "$VJ" -H "Content-Type: application/json" -d '{"username":"viewer","password":"View@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && ok "login viewer" || bad "login viewer ($code)"
TV=$(token "$VJ" /Settings)
code=$(curl -s -b "$VJ" -o /dev/null -w "%{http_code}" --data-urlencode "__RequestVerificationToken=$TV" --data-urlencode "calendar=hijri" "$B/Settings/SaveCalendar")
[ "$code" = "302" ] && ok "viewer بدون settings.manage → 302" || bad "viewer SaveCalendar=$code"

# ---------- ۵) بازگشت به شمسی + دود صفحات ----------
T=$(token "$AJ" /Settings)
R=$(curl -s -b "$AJ" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" --data-urlencode "calendar=jalali" "$B/Settings/SaveCalendar")
echo "$R" | grep -q '"ok":true' && ok "بازگشت به شمسی" || bad "بازگشت شمسی → $R"
D=$(curl -s -b "$AJ" "$B/")
echo "$D" | grep -q "cal: 'jalali'" && ok "HrDateCfg بازگشت به jalali" || bad "بازگشت jalali نشد"
echo "$D" | grep -q 'آبان\|آذر' && ok "نوار بالایی شمسی بازگشت" || bad "نوار بالایی شمسی بازنگشت"
for P in /Holidays /Reports/Custom /Reports/Parking /Requests /Attendance /Payroll /Employees /Shifts /Users /Audit; do
  code=$(curl -s -b "$AJ" -o /dev/null -w "%{http_code}" "$B$P")
  [ "$code" = "200" ] && ok "دود $P" || bad "دود $P → $code"
done

# ---------- ۶) چک CLS/جایگاه hrdate.js ----------
code=$(curl -s -o /dev/null -w "%{http_code}" "$B/js/hrdate.js")
[ "$code" = "200" ] && ok "hrdate.js سرو می‌شود" || bad "hrdate.js=$code"
echo "$D" | grep -q 'hrdate.js' && ok "اسکریپت در layout لینک شده" || bad "لینک hrdate.js غایب"

echo "-----------------------------"
echo "PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ]
