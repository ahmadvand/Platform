#!/bin/bash
# ======================================================================
# پاک‌آزمایی فاز ۷۱ (بازخواست صریح کاربر: «یه بار دیگه حذف dll ها رو بررسی کن»)
# ----------------------------------------------------------------------
# سناریو: DLLهای UI+دیتای دو ماژول غیر-HR (پارکینگ/بازدیدکنندگان) از bin هاست
# حذف می‌شود؛ اپ ری‌استارت می‌شود؛ سپس باید:
#   ۱) /health = 200 (بوت بدون ماژول‌های حذف‌شده)
#   ۲) منوی کنار = هیچ آیتم پارکینگ/بازدیدکننده (href=/Parking و /Visitors محو)
#   ۳) داشبورد = هیچ ویجت پارکینگ/مهمان
#   ۴) /Parking و /Visitors = صفحهٔ دوستانهٔ «نصب نشده» (نه ۴۰۴ خام و نه کرش)
#   ۵) بقیهٔ صفحات سالم (کنترل تردد، حضور، دستگاه‌ها)
# سپس DLLها بازگردانده می‌شوند و خودبازگشتی بررسی می‌شود.
# ======================================================================
set -u
BASE=http://localhost:3000
BIN=/home/z/my-project/aspnet/src/Platform.Web/bin/Debug/net8.0
LOG=/home/z/my-project/dev.log
J=/tmp/p71test; rm -rf $J; mkdir -p $J
FAIL=0
say(){ echo "[$(date '+%T')] $*"; }
chk(){ # chk <شرح> <شرط 0/1>
  if [ "$2" = "1" ]; then say "✓ $1"; else say "✗ $1"; FAIL=1; fi
}

login(){ # login <jar> -> echo code
  local jar=$1 af
  af=$(curl -s -c "$jar" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
  [ -n "$af" ] || { echo NO-TOKEN; return 1; }
  curl -s -o /dev/null -w "%{http_code}" -b "$jar" -c "$jar" -X POST "$BASE/Auth/Login" \
    --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
    --data-urlencode "__RequestVerificationToken=$af"
}

wait_health(){ for i in $(seq 1 "${1:-90}"); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 $BASE/health 2>/dev/null)" = "200" ] && return 0; sleep 1
done; return 1; }

restart_app(){
  pkill -f "Platform.Web.dll" 2>/dev/null; sleep 4
  wait_health 90 || return 1
  sleep 2  # جانِ گرم: منو/کشف در اولین مصرف ساخته شود
}

# ---------- ۰) BASELINE: اپ سالم + منو شامل پارکینگ/بازدیدکننده ----------
say "— baseline —"
wait_health 30 || { say "✗ اپ از ابتدا سالم نیست"; exit 1; }
CODE=$(login $J/base.txt)
chk "baseline: ورود ادمین (302)" "$([ "$CODE" = "302" ] && echo 1 || echo 0)"
MENU=$(curl -s -b $J/base.txt "$BASE/Companies")
echo "$MENU" | grep -q 'href="/Parking"' ; chk "baseline: منو دارای «کنترل پارکینگ»" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$MENU" | grep -q 'href="/Visitors"' ; chk "baseline: منو دارای «ارباب رجوع»" "$([ $? -eq 0 ] && echo 1 || echo 0)"

# ---------- ۱) حذف DLL ----------
say "— حذف DLL پارکینگ/بازدیدکنندگان از bin هاست —"
ls $BIN/HrModules.Parking*.dll $BIN/HrModules.Visitors*.dll 2>/dev/null | wc -l
rm -f $BIN/HrModules.Parking*.dll $BIN/HrModules.Visitors*.dll
N=$(ls $BIN/ | grep -c "^Platform.Modules.*dll$" || true)
say "DLLهای نام‌قدیم Platform.Modules در bin: $N (باید 0 باشد)"

# ---------- ۲) ری‌استارت ----------
say "— ری‌استارت اپ بدون این دو ماژول —"
restart_app || { say "✗ اپ بعد از حذف DLL بالا نیامد"; exit 1; }
chk "بوت بدون DLL حذف‌شده → /health=200" 1
BOOT=$(tail -40 $LOG | grep -o "کشف پلاگین از 1 پوشه — [0-9]* اسمبلی" | tail -1)
say "کشف بوت: $BOOT (باید 9 باشد — ۱۳ منهای ۴ DLL حذف‌شده)"

# ---------- ۳) بررسی‌ها ----------
say "— راستی‌آزمایی بعد از حذف —"
CODE=$(login $J/after.txt)
chk "ورود ادمین بعد از حذف (302)" "$([ "$CODE" = "302" ] && echo 1 || echo 0)"
MENU=$(curl -s -b $J/after.txt "$BASE/Companies")
echo "$MENU" | grep -q 'href="/Parking"'; chk "منو بدون «کنترل پارکینگ»" "$([ $? -ne 0 ] && echo 1 || echo 0)"
echo "$MENU" | grep -q 'href="/Visitors"'; chk "منو بدون «ارباب رجوع»" "$([ $? -ne 0 ] && echo 1 || echo 0)"
echo "$MENU" | grep -q 'href="/Access"'; chk "منو همچنان دارای کنترل تردد (ماژول دست‌نخورده)" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$MENU" | grep -q 'href="/Attendance"'; chk "منو همچنان دارای حضور و غیاب" "$([ $? -eq 0 ] && echo 1 || echo 0)"

DASH=$(curl -s -b $J/after.txt "$BASE/")
echo "$DASH" | grep -qi "پارکینگ"; chk "داشبورد بدون ویجت پارکینگ" "$([ $? -ne 0 ] && echo 1 || echo 0)"
echo "$DASH" | grep -qi "مهمان داخل سازمان"; chk "داشبورد بدون KPI مهمان" "$([ $? -ne 0 ] && echo 1 || echo 0)"

PLoc=$(curl -s -o /dev/null -D - -b $J/after.txt "$BASE/Parking" | grep -i '^Location:' | tr -d '\r' | awk '{print $2}')
P=$(curl -s -L -w "|%{http_code}" -b $J/after.txt "$BASE/Parking")
PC=${P##*|}; PB=${P%|*}
say "   /Parking بدون ماژول → ریدایرکت: ${PLoc:-بدون} — کد نهایی: $PC"
echo "$PB" | grep -q "نصب نشده"; chk "صفحهٔ دوستانهٔ «نصب نشده» برای پارکینگ" "$([ $? -eq 0 ] && echo 1 || echo 0)"
say "   متن /Parking: $(echo "$PB" | grep -o 'نصب نشده' | head -1) — code=$PC"
V=$(curl -s -L -w "|%{http_code}" -b $J/after.txt "$BASE/Visitors")
VB=${V%|*}
echo "$VB" | grep -q "نصب نشده"; chk "صفحهٔ دوستانهٔ «نصب نشده» برای بازدیدکنندگان" "$([ $? -eq 0 ] && echo 1 || echo 0)"

ACC=$(curl -s -o /dev/null -w "%{http_code}" -b $J/after.txt "$BASE/Access")
chk "صفحهٔ ماژول سالم /Access → 200" "$([ "$ACC" = "200" ] && echo 1 || echo 0)"
ATT=$(curl -s -o /dev/null -w "%{http_code}" -b $J/after.txt "$BASE/Attendance")
chk "صفحهٔ ماژول سالم /Attendance → 200" "$([ "$ATT" = "200" ] && echo 1 || echo 0)"
USR=$(curl -s -o /dev/null -w "%{http_code}" -b $J/after.txt "$BASE/Users")
chk "صفحهٔ هسته /Users → 200" "$([ "$USR" = "200" ] && echo 1 || echo 0)"
ERR=$(tail -30 $LOG | grep -ci "Unhandled exception" || true)
chk "لاگ بوت بدون Unhandled exception" "$([ "$ERR" = "0" ] && echo 1 || echo 0)"

# ---------- ۴) بازگردانی و خودبازگشتی ----------
say "— بازگردانی DLL (بیلد هاست: کپی خودکار پلاگین‌ها) —"
cd /home/z/my-project/aspnet && export PATH="/home/z/.dotnet:$PATH" && dotnet build src/Platform.Web/Platform.Web.csproj -v q --nologo 2>&1 | tail -2
N=$(ls $BIN/ | grep -c "^Platform.Modules.*dll$" || true)
say "DLLهای نام‌قدیم بازگشته: $N (باید 0 باشد)"
N2=$(ls $BIN/HrModules.Parking*.dll $BIN/HrModules.Visitors*.dll 2>/dev/null | wc -l)
say "DLLهای پارکینگ/مهمانان بازگشته: $N2 (باید 4 باشد)"
restart_app || { say "✗ اپ بعد از بازگردانی بالا نیامد"; exit 1; }
CODE=$(login $J/restore.txt)
chk "ورود ادمین بعد از بازگردانی (302)" "$([ "$CODE" = "302" ] && echo 1 || echo 0)"
MENU=$(curl -s -b $J/restore.txt "$BASE/Companies")
echo "$MENU" | grep -q 'href="/Parking"'; chk "منوی پارکینگ خودبه‌خود برگشت" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$MENU" | grep -q 'href="/Visitors"'; chk "منوی بازدیدکنندگان خودبه‌خود برگشت" "$([ $? -eq 0 ] && echo 1 || echo 0)"
BOOT2=$(tail -20 $LOG | grep -o "کشف پلاگین از 1 پوشه — [0-9]* اسمبلی" | tail -1)
say "کشف بوت نهایی: $BOOT2 (باید 13 باشد)"
P2=$(curl -s -o /dev/null -w "%{http_code}" -b $J/restore.txt "$BASE/Parking")
chk "صفحهٔ پارکینگ دوباره واقعی → 200" "$([ "$P2" = "200" ] && echo 1 || echo 0)"

# ---------- جمع‌بندی ----------
echo "=================================================="
[ "$FAIL" = "0" ] && say "VERDICT: PASS — حذف DLL ⇒ محو کامل همهٔ ردهای ماژول؛ بازگردانی ⇒ بازگشت خودکار" \
                    || say "VERDICT: FAIL — موارد ✗ بالا را ببینید"
exit $FAIL
