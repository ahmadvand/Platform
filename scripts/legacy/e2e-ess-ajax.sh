#!/usr/bin/env bash
# ============================================================
# ESS AJAX — تبدیل سه فرم خودخدمتی (مرخصی/مأموریت/اصلاح تردد)
# به «ثبت AJAX + toast + تعویض درجای لیست» (تکمیل تسک ۲۱ برای ESS)
# بررسی‌ها: مارک‌های فرانت، JSON دوحالته، بنر رو (نبود TempData شبح)،
# اعتبارسنجی، پایان نشست، fallback بدون JS، پاکسازی idempotent
# اجرا: bash scripts/e2e-ess-ajax.sh
# ============================================================
set -u
cd "$(dirname "$0")/.."
BASE="http://localhost:3000"
PASS=0; FAIL=0
ck() { if [ "$1" = "1" ]; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }
TSMARK=$(date +%s)

# ---------- کمکی‌های curl با کوکی ----------
CJ=/tmp/ess-ajax-cookies.txt; rm -f "$CJ"
GET()  { curl -s -b "$CJ" -c "$CJ" "$BASE$1" --max-time 15; }
POST() { local url="$1"; shift; curl -s -b "$CJ" -c "$CJ" -X POST "$BASE$url" "$@" --max-time 15; }
TOKEN() { GET "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
XHR="X-Requested-With: XMLHttpRequest"
# دیکود JSON فارسی (System.Text.Json یونیکد را escape می‌کند)
jmsg() { python3 -c "import sys,json;d=json.load(sys.stdin);print(d.get('$1',''))" 2>/dev/null; }

# ---------- داده پایه: ورود ESS با اولین پرسنل فعال ----------
EMP=$(python3 scripts/dbq.py --json "SELECT Id, PersonnelCode, NationalId FROM Employees WHERE Status='فعال' ORDER BY PersonnelCode LIMIT 1" --json 2>/dev/null)
CODE=$(echo "$EMP" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['PersonnelCode'])")
NID=$(echo "$EMP" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['NationalId'])")
EID=$(echo "$EMP" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
echo "پرسنل نمونه: $CODE"

tk=$(TOKEN /Ess/Login)
POST /Ess/Login --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "personnelCode=$CODE" --data-urlencode "nationalId=$NID" > /dev/null
GET /Ess/Requests | grep -q "ثبت درخواست مرخصی" && ck 1 "ورود ESS موفق (صفحه درخواست‌ها)" || ck 0 "ورود ESS موفق"

echo "── ۱) مارک‌های فرانت AJAX"
P=$(GET /Ess/Requests)
echo "$P" | grep -q 'data-refresh-host' && ck 1 "host تعویض درجا (data-refresh-host)" || ck 0 "host تعویض درجا"
n=$(echo "$P" | grep -o 'data-ajax' | wc -l)
ck "$([ "$n" -ge 3 ] && echo 1 || echo 0)" "سه فرم data-ajax (یافت: $n)"
n=$(echo "$P" | grep -o 'data-refresh' | wc -l)
ck "$([ "$n" -ge 3 ] && echo 1 || echo 0)" "سه فرم data-refresh (یافت: $n)"
echo "$P" | grep -q 'site.js' && ck 1 "site.js در layout خودخدمتی لود شده" || ck 0 "site.js در layout خودخدمتی"
echo "$P" | grep -q '/Ess/Logout' && ck 1 "فرم خروج دست‌نخورده (طرح صفحه‌ای)" || ck 0 "فرم خروج دست‌نخورده"

echo "── ۲) ثبت مرخصی با AJAX"
tk=$(TOKEN /Ess/Requests)
LMARK="e2eajax-leave-$TSMARK"
r=$(POST /Ess/CreateLeave -H "$XHR" \
  --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "type=استحقاقی" --data-urlencode "fromDate=2026-11-01" \
  --data-urlencode "toDate=2026-11-03" --data-urlencode "days=1.5" \
  --data-urlencode "reason=$LMARK")
echo "$r" | grep -q '"ok":true' && ck 1 "پاسخ JSON ok:true (بدون ریدایرکت)" || ck 0 "پاسخ JSON ok:true"
m=$(echo "$r" | jmsg message)
echo "$m" | grep -q "در انتظار تأیید سرپرست" && ck 1 "پیام فارسی toast: $m" || ck 0 "پیام فارسی toast (یافت: $m)"

echo "── ۳) تعویض درجای لیست (همان‌کاری که refreshHost می‌کند)"
P=$(GET /Ess/Requests)
# جدول مرخصی‌ها ستون «دلیل» ندارد → ماندگاری ردیف با DB راستی‌آزمایی می‌شود (رندر درجای لیست‌ها با مارک مأموریت/اصلاح در بخش‌های ۵ و ۶)
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM LeaveRequests WHERE EmployeeId='$EID' AND Reason='$LMARK'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])" 2>/dev/null)
ck "$([ "$n" = "1" ] && echo 1 || echo 0)" "مرخصی تازه ماندگار شد (DB، یافت: $n)"
echo "$P" | grep -q 'toast toast-ok' && ck 0 "بنر رو شبح نباید باشد (TempData مصرف‌نشده)" || ck 1 "بنر رو شبح نیامده (TempData ست نشده)"

echo "── ۴) اعتبارسنجی سمت سرور با AJAX"
tk=$(TOKEN /Ess/Requests)
r=$(POST /Ess/CreateLeave -H "$XHR" \
  --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "type=استحقاقی" --data-urlencode "fromDate=2026-11-01" \
  --data-urlencode "toDate=2026-11-03" --data-urlencode "days=0" \
  --data-urlencode "reason=invalid")
echo "$r" | grep -q '"ok":false' && ck 1 "رد JSON ok:false" || ck 0 "رد JSON ok:false (یافت: $r)"
m=$(echo "$r" | jmsg message)
echo "$m" | grep -q "درست وارد کنید" && ck 1 "پیام خطای فارسی: $m" || ck 0 "پیام خطای فارسی (یافت: $m)"

echo "── ۵) مأموریت با AJAX"
tk=$(TOKEN /Ess/Requests)
MMARK="e2eajax-mission-$TSMARK"
r=$(POST /Ess/CreateMission -H "$XHR" \
  --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "type=داخلی" --data-urlencode "destination=$MMARK" \
  --data-urlencode "fromDate=2026-11-05" --data-urlencode "toDate=2026-11-06" \
  --data-urlencode "purpose=جلسه فنی")
echo "$r" | grep -q '"ok":true' && ck 1 "مأموریت JSON ok:true" || ck 0 "مأموریت JSON ok:true (یافت: $r)"
GET /Ess/Requests | grep -q "$MMARK" && ck 1 "مأموریت تازه در لیست" || ck 0 "مأموریت تازه در لیست"

echo "── ۶) اصلاح تردد با AJAX"
tk=$(TOKEN /Ess/Requests)
CMARK="e2eajax-corr-$TSMARK"
r=$(POST /Ess/CreateCorrection -H "$XHR" \
  --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "date=2026-10-15" --data-urlencode "requestedIn=08:30" \
  --data-urlencode "requestedOut=17:45" --data-urlencode "reason=$CMARK")
echo "$r" | grep -q '"ok":true' && ck 1 "اصلاح تردد JSON ok:true" || ck 0 "اصلاح تردد JSON ok:true (یافت: $r)"
P=$(GET /Ess/Requests)
echo "$P" | grep -q "$CMARK" && ck 1 "اصلاح تازه در لیست" || ck 0 "اصلاح تازه در لیست"
echo "$P" | grep -q "08:30" && ck 1 "زمان ورود درخواستی ثبت شد" || ck 0 "زمان ورود درخواستی"

echo "── ۷) پایان نشست خودخدمتی در حالت AJAX"
CJ2=/tmp/ess-ajax-guest.txt; rm -f "$CJ2"
tk=$(curl -s -b "$CJ2" -c "$CJ2" "$BASE/Ess/Login" --max-time 15 | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
r=$(curl -s -b "$CJ2" -c "$CJ2" -X POST "$BASE/Ess/CreateLeave" -H "$XHR" \
  --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "type=استحقاقی" --data-urlencode "fromDate=2026-11-01" \
  --data-urlencode "toDate=2026-11-02" --data-urlencode "days=1" --max-time 15)
m=$(echo "$r" | jmsg message)
echo "$r" | grep -q '"ok":false' && echo "$m" | grep -q 'نشست' && ck 1 "بدون نشست: JSON پیام روشن (نه ریدایرکت HTML): $m" || ck 0 "بدون نشست: JSON پیام روشن (یافت: $r)"

echo "── ۸) fallback بدون JS (رفتار قدیمی سالم)"
tk=$(TOKEN /Ess/Requests)
FMARK="e2eajax-fb-$TSMARK"
hd=$(curl -s -o /dev/null -D - -b "$CJ" -c "$CJ" -X POST "$BASE/Ess/CreateMission" \
  --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "type=داخلی" --data-urlencode "destination=$FMARK" \
  --data-urlencode "fromDate=2026-11-07" --data-urlencode "toDate=2026-11-08" --max-time 15)
echo "$hd" | head -1 | grep -q "302" && ck 1 "بدون هدر AJAX → 302 ریدایرکت" || ck 0 "بدون هدر AJAX → 302 (یافت: $(echo "$hd" | head -1))"
echo "$hd" | grep -q "Location: /Ess/Requests" && ck 1 "مقصد ریدایرکت درست" || ck 0 "مقصد ریدایرکت"
GET /Ess/Requests | grep -q "toast toast-ok" && ck 1 "بنر TempData در حالت عادی سالم" || ck 0 "بنر TempData در حالت عادی"
GET /Ess/Requests | grep -q "$FMARK" && ck 1 "fallback هم ثبت کرده" || ck 0 "fallback ثبت"

echo "── ۹) آنتی‌فورجری پابرجا"
tk=$(TOKEN /Ess/Requests)
code=$(curl -s -o /dev/null -w "%{http_code}" -b "$CJ" -X POST "$BASE/Ess/CreateLeave" -H "$XHR" \
  --data-urlencode "type=استحقاقی" --data-urlencode "fromDate=2026-11-01" \
  --data-urlencode "toDate=2026-11-02" --data-urlencode "days=1" --max-time 15)
ck "$([ "$code" = "400" ] && echo 1 || echo 0)" "بدون توکن → 400 (دزد نشد)"

echo "── پاکسازی"
python3 scripts/dbq.py --exec "DELETE FROM LeaveRequests WHERE Reason LIKE 'e2eajax-leave-%'" > /dev/null 2>&1
python3 scripts/dbq.py --exec "DELETE FROM Missions WHERE Destination LIKE 'e2eajax-mission-%' OR Destination LIKE 'e2eajax-fb-%'" > /dev/null 2>&1
python3 scripts/dbq.py --exec "DELETE FROM CorrectionRequests WHERE Reason LIKE 'e2eajax-corr-%'" > /dev/null 2>&1
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM LeaveRequests WHERE EmployeeId='$EID' AND Reason LIKE 'e2eajax-%'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])" 2>/dev/null)
ck "$([ "$n" = "0" ] && echo 1 || echo 0)" "پاکسازی کامل ردیف‌های تست"

echo "════════════════════════════"
echo "PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ] && echo "DONE ✓" || echo "DONE ✗"
exit $([ "$FAIL" = "0" ] && echo 0 || echo 1)
