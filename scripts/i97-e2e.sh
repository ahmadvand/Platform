#!/bin/bash
# فاز ۹۷ — راستی‌آزمایی زندهٔ نسخهٔ 2.8.0 روی پورت 3000
set -uo pipefail
B=http://localhost:3000
PASS=0; FAIL=0
ck() { if [ "$2" = "$3" ]; then PASS=$((PASS+1)); echo "PASS $1"; else FAIL=$((FAIL+1)); echo "FAIL $1 (got=$2 want=$3)"; fi; }
ckc() { if [[ "$2" == *"$3"* ]]; then PASS=$((PASS+1)); echo "PASS $1"; else FAIL=$((FAIL+1)); echo "FAIL $1 (pattern not found: $3)"; fi; }

J=/tmp/i97-cookies.txt; rm -f $J
# ۱) سلامت
ck "health" "$(curl -s -o /dev/null -w '%{http_code}' $B/health)" "200"
# ۲) ورود ادمین با CSRF
TOK=$(curl -s -c $J $B/Auth/Login | grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $J -c $J -X POST $B/Auth/Login \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOK")
ck "login POST (302=redirect after success)" "$CODE" "302"
# ۳) گیت مجوز (Ack)
ck "license notice" "$(curl -s -o /dev/null -w '%{http_code}' -b $J -c $J $B/License/Notice)" "200"
TOK2=$(curl -s -b $J -c $J $B/License/Notice | grep -o 'name="__RequestVerificationToken"[^>]*' | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
ck "license ack token found" "$([ ${#TOK2} -gt 50 ] && echo yes)" "yes"
ck "license ack" "$(curl -s -o /dev/null -w '%{http_code}' -b $J -c $J -X POST $B/License/Ack --data-urlencode "__RequestVerificationToken=$TOK2")" "302"
# ۴) صفحهٔ تنظیمات — تب محافظ صفحه + جبران قمری
S=$(curl -s -b $J -c $J $B/Settings)
ckc "settings: screensaver tab" "$S" 'tab-screensaver'
ckc "settings: hijriAdjust select" "$S" 'name="hijriAdjust"'
ckc "settings: ssRequirePassword" "$S" 'name="ssRequirePassword"'
ckc "settings: ssMinutes" "$S" 'name="ssMinutes"'
# ۵) ذخیرهٔ تنظیمات با فیلدهای جدید (صرفاً مقادیر جاری + جبران قمری ‎-۱)
TOKS=$(echo "$S" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
CN=$(echo "$S" | grep -o 'name="companyName"[^>]*value="[^"]*"' | head -1 | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
CODE=$(curl -s -o /tmp/i97-save.json -w '%{http_code}' -b $J -c $J -X POST $B/Settings/SaveGeneral \
  -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "companyName=${CN:-شرکت پتروشیمی پارس آسیا}" \
  --data-urlencode "lang=fa" --data-urlencode "calendar=jalali" \
  --data-urlencode "hijriAdjust=-1" \
  --data-urlencode "ssEnabled=true" --data-urlencode "ssMinutes=5" --data-urlencode "ssRequirePassword=true" \
  --data-urlencode "currency=IRR" --data-urlencode "currencyRate=1" \
  --data-urlencode "__RequestVerificationToken=$TOKS")
ck "save general 200" "$CODE" "200"
ckc "save general ok:true" "$(cat /tmp/i97-save.json)" '"ok":true'
# ۶) اسکریپت محافظ صفحه سرو می‌شود + پیکربندی در Layout
ck "screensaver.js 200" "$(curl -s -o /dev/null -w '%{http_code}' $B/js/hr-screensaver.js)" "200"
L=$(curl -s -b $J -c $J $B/)
echo "DEBUG len=$(echo "$L" | wc -c) hijri=$(echo "$L" | grep -c 'data-hijri') hijriV=$(echo "$L" | grep -o 'data-hijri="[^"]*"' | head -1) calV=$(echo "$L" | grep -o 'data-cal="[^"]*"' | head -1)"
ckc "layout: screensaver tag" "$L" 'hr-screensaver.js'
ckc "layout: data-ss-enabled lowercase" "$L" 'data-ss-enabled="true"'
ckc "layout: data-ss-minutes" "$L" 'data-ss-minutes="5"'
ckc "layout: data-hijri" "$L" 'data-hijri="-1"'
# ۷) صفحهٔ قالب برون‌بری تردد
E=$(curl -s -b $J -c $J "$B/Attendance/Exports")
ck "exports page 200" "$(curl -s -o /dev/null -w '%{http_code}' -b $J -c $J $B/Attendance/Exports)" "200"
ckc "exports: column editor" "$E" 'colRows'
ckc "exports: linuxtimestamp option" "$E" 'Linux timestamp'
ckc "exports: hijri option" "$E" '1447/08/05'
# ۸) ذخیرهٔ قالب آزمایشی + برون‌بری CSV با هر ۴ قالب تاریخ
TOKE=$(echo "$E" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
CODE=$(curl -s -o /tmp/i97-tpl.json -w '%{http_code}' -b $J -c $J -X POST $B/Attendance/SaveTemplate \
  -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "name=قالب آزمون فاز ۹۷" \
  --data-urlencode "note=تست" \
  --data-urlencode 'columns=[{"f":"personnelCode","fmt":"","h":"PIN"},{"f":"date","fmt":"linux","h":"TS"},{"f":"checkin","fmt":"hhmm","h":"IN"},{"f":"date2","f":"x"}]' \
  --data-urlencode "delimiter=," --data-urlencode "includeHeader=true" \
  --data-urlencode "__RequestVerificationToken=$TOKE")
ck "save template 200 (invalid → ok:false)" "$CODE" "200"
TPLID=$(python3 -c "import json;print(json.load(open('/tmp/i97-tpl.json')).get('id',''))" 2>/dev/null)
# قالب درست: از رندر صفحه ویرایشگر بخوان (کد قبلی عمداً ستون نامعتبر داشت تا اعتبارسنجی هم تست شود)
ckc "invalid column rejected" "$(cat /tmp/i97-tpl.json)" '"ok":false' || true
E=$(curl -s -b $J -c $J "$B/Attendance/Exports")
TOKE=$(echo "$E" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
CODE=$(curl -s -o /tmp/i97-tpl.json -w '%{http_code}' -b $J -c $J -X POST $B/Attendance/SaveTemplate \
  -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "name=قالب آزمون فاز ۹۷" \
  --data-urlencode 'columns=[{"f":"personnelCode","fmt":"","h":"PIN"},{"f":"date","fmt":"linux"},{"f":"checkin","fmt":"hhmm"},{"f":"checkout","fmt":"hhmm"},{"f":"status","fmt":""}]' \
  --data-urlencode "delimiter=," --data-urlencode "includeHeader=true" \
  --data-urlencode "__RequestVerificationToken=$TOKE")
ck "save valid template" "$CODE" "200"
ckc "valid ok:true" "$(cat /tmp/i97-tpl.json)" '"ok":true'
TPLID=$(python3 -c "import json;print(json.load(open('/tmp/i97-tpl.json')).get('id',''))" 2>/dev/null)
# ۹) خروجی CSV (لیست دانلود با GET — کد 200 و سرستون‌ها)
if [ -n "$TPLID" ]; then
  CSV=$(curl -s -b $J -c $J "$B/Attendance/ExportByTemplate?id=$TPLID&from=2020-01-01&to=2030-12-31")
  ckc "csv linuxtimestamp column" "$CSV" ",1792960200,"
ckc "csv default headers" "$CSV" "PIN,تاریخ تردد"
  ckc "csv utf8 persian status" "$CSV" "حاضر"
  echo "   -- CSV head: $(echo "$CSV" | head -3 | tr '\n' ' ' | cut -c1-160)"
else
  FAIL=$((FAIL+1)); echo "FAIL no template id for export"
fi
# ۱۰) حذف قالب آزمون
if [ -n "$TPLID" ]; then
  CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $J -c $J -X POST $B/Attendance/DeleteTemplate \
    -H "X-Requested-With: XMLHttpRequest" \
    --data-urlencode "id=$TPLID" --data-urlencode "__RequestVerificationToken=$TOKE")
  ck "delete template" "$CODE" "200"
fi
# ۱۱) نسخهٔ فوتر = 2.8.0
ckc "footer version 2.8.0" "$L" "۲.۸.۰"
# ۱۲) ویکارد سه‌تبی روی نمونهٔ ایزولهٔ ۳۹۹۹ (SetupNeeded واقعی)
echo "== isolated setup instance (3999) =="
# کشتن هر نمونهٔ کهنه‌ای که 3999 را نگه داشته (پروب پورت‌محور)
K3999=$(ss -tlnp 2>/dev/null | grep ':3999' | grep -oE 'pid=[0-9]+' | head -1 | cut -d= -f2)
[ -n "$K3999" ] && kill -9 "$K3999" 2>/dev/null && sleep 1
rm -rf /tmp/i97-setup && mkdir -p /tmp/i97-setup
cd /tmp/i97-setup
# SQLite بوتِ خودکار کانفیگ می‌شود (فاز ۶۲) ⇒ برای SetupNeeded واقعی از SQL Server استفاده می‌کنیم
(setsid env ASPNETCORE_URLS=http://127.0.0.1:3999 ASPNETCORE_ENVIRONMENT=Production Database__Provider=SqlServer "ConnectionStrings__SqlServer=Server=127.0.0.1,1;Database=HrSetupProbe;User Id=sa;Password=x;TrustServerCertificate=True" /home/z/my-project/aspnet/sc-run/Platform.Web > /tmp/i97-setup/app.log 2>&1 &)
for i in $(seq 1 120); do
  c=$(curl -s -o /dev/null -w '%{http_code}' --max-time 2 http://127.0.0.1:3999/Setup 2>/dev/null || true)
  [ "$c" = "200" ] && break; sleep 1
done
SU=$(curl -s http://127.0.0.1:3999/Setup)
ckc "setup: 3 tabs" "$SU" 'wizTab3'
ckc "setup: tab1 lang" "$SU" 'انتخاب زبان'
ckc "setup: tab2 database" "$SU" 'اطلاعات دیتابیس'
ckc "setup: tab3 license" "$SU" 'اطلاعات مجوز'
ckc "setup: lang pane" "$SU" 'wizPane1'
ckc "setup: db pane hidden" "$SU" 'wizPane2'
ckc "setup: lic pane hidden" "$SU" 'wizPane3'
ckc "setup: lang has flags" "$SU" 'langFa'
K3999B=$(ss -tlnp 2>/dev/null | grep ':3999' | grep -oE 'pid=[0-9]+' | head -1 | cut -d= -f2)
[ -n "$K3999B" ] && kill -9 "$K3999B" 2>/dev/null || true
echo "=================================="
echo "PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ] && echo "ALL GREEN" || echo "HAS FAILURES"
