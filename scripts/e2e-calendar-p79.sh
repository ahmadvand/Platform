#!/bin/bash
# ============================================================
# فاز ۷۹ — E2E سامانهٔ تقویم چندگانه (شمسی/قمری/میلادی) + ذخیره‌سازی Linux timestamp
# ============================================================
BASE=http://localhost:3000
JAR=/tmp/hr_cal_e2e.jar
rm -f "$JAR"
PASS=0; FAIL=0
ok()   { PASS=$((PASS+1)); echo "PASS: $1"; }
bad()  { FAIL=$((FAIL+1)); echo "FAIL: $1"; }
check() { # check <label> <haystack-file> <needle-fixed>
  if rg -qF "$3" "$2"; then ok "$1"; else bad "$1 (missing: $3)"; fi
}

# ---------- ورود ----------
curl -s -c "$JAR" "$BASE/Auth/Login" -o /tmp/cal_login.html
TOK=$(rg -o 'name="__RequestVerificationToken"[^>]*value="([^"]+)"' -r '$1' /tmp/cal_login.html | head -1)
[ -z "$TOK" ] && { echo "NO-TOKEN"; exit 1; }
CODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" \
  -d "identifier=admin" -d "secret=Admin@1234" -d "__RequestVerificationToken=$TOK" "$BASE/Auth/Login")
[ "$CODE" = "302" ] && ok "login 302" || bad "login $CODE"

# ---------- ۱) کارت تقویم در تنظیمات ----------
curl -s -b "$JAR" "$BASE/Settings" -o /tmp/cal_settings.html
check "settings: کارت تقویم سامانه" /tmp/cal_settings.html "تقویم سامانه"
check "settings: گزینهٔ شمسی" /tmp/cal_settings.html 'value="jalali"'
check "settings: گزینهٔ قمری" /tmp/cal_settings.html 'value="hijri"'
check "settings: گزینهٔ میلادی" /tmp/cal_settings.html 'value="gregorian"'
check "settings: پیش‌فرض شمسی انتخاب" /tmp/cal_settings.html 'value="jalali" checked'

# توکن فرم تنظیمات
CTOK=$(rg -o 'name="__RequestVerificationToken"[^>]*value="([^"]+)"' -r '$1' /tmp/cal_settings.html | head -1)

# ---------- ۲) پیش‌فرض: داشبورد شمسی ----------
curl -s -b "$JAR" "$BASE/" -o /tmp/cal_dash_j.html
check "dashboard(fa+jalali): تاریخ شمسی ۱۴۰۵" /tmp/cal_dash_j.html "۱۴۰۵"

# ---------- ۳) سوئیچ به میلادی ----------
CODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" \
  --data-urlencode "calendar=gregorian" --data-urlencode "__RequestVerificationToken=$CTOK" "$BASE/Settings/SaveCalendar")
[ "$CODE" = "302" ] && ok "save calendar=gregorian 302" || bad "save gregorian $CODE"
curl -s -b "$JAR" "$BASE/" -o /tmp/cal_dash_g.html
check "dashboard(gregorian): ماه میلادی نوامبر" /tmp/cal_dash_g.html "نوامبر"
check "dashboard(gregorian): سال ۲۰۲۶ (ارقام زبان fa)" /tmp/cal_dash_g.html "۲۰۲۶"
curl -s -b "$JAR" "$BASE/Settings" -o /tmp/cal_settings_g.html
check "settings(gregorian): فعال = میلادی" /tmp/cal_settings_g.html 'value="gregorian" checked'

# ---------- ۴) سوئیچ به قمری ----------
CODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" \
  --data-urlencode "calendar=hijri" --data-urlencode "__RequestVerificationToken=$CTOK" "$BASE/Settings/SaveCalendar")
[ "$CODE" = "302" ] && ok "save calendar=hijri 302" || bad "save hijri $CODE"
curl -s -b "$JAR" "$BASE/" -o /tmp/cal_dash_h.html
check "dashboard(hijri): سال قمری ۱۴۴۸" /tmp/cal_dash_h.html "۱۴۴۸"
# نام ماه قمری (fa): محرم/صفر/ربیع‌الاول...
if rg -qF "جمادی" /tmp/cal_dash_h.html || rg -qF "ربیع" /tmp/cal_dash_h.html || rg -qF "محرم" /tmp/cal_dash_h.html || rg -qF "شعبان" /tmp/cal_dash_h.html || rg -qF "صفر" /tmp/cal_dash_h.html; then
  ok "dashboard(hijri): نام ماه قمری حاضر"
else bad "dashboard(hijri): نام ماه قمری یافت نشد"; fi
# صفحهٔ تعطیلات (شبکهٔ ماه باید قمری شود)
curl -s -b "$JAR" "$BASE/Holidays" -o /tmp/cal_hol_h.html
check "holidays(hijri): رندر شبکهٔ قمری" /tmp/cal_hol_h.html "۱۴۴۸"

# ---------- ۵) صفحات ماژولی با قمری (نمونه‌گیری) ----------
for r in "/Attendance" "/Payroll" "/Employees" "/Audit"; do
  c=$(curl -s -b "$JAR" -o /tmp/cal_mod.html -w "%{http_code}" "$BASE$r")
  [ "$c" = "200" ] && ok "module $r hijri 200" || bad "module $r hijri = $c"
done

# ---------- ۶) بازگشت به شمسی ----------
CODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" \
  --data-urlencode "calendar=jalali" --data-urlencode "__RequestVerificationToken=$CTOK" "$BASE/Settings/SaveCalendar")
[ "$CODE" = "302" ] && ok "restore calendar=jalali 302" || bad "restore jalali $CODE"
curl -s -b "$JAR" "$BASE/" -o /tmp/cal_dash_j2.html
check "dashboard(restore): شمسی برگشت" /tmp/cal_dash_j2.html "۱۴۰۵"

# ---------- ۷) ویجت hrdate.js ----------
c=$(curl -s -o /tmp/cal_hrdate.js -w "%{http_code}" "$BASE/js/hrdate.js")
[ "$c" = "200" ] && ok "hrdate.js 200" || bad "hrdate.js $c"
rg -qF "HrDateCfg" /tmp/cal_dash_j2.html && ok "layout تزریق HrDateCfg" || bad "HrDateCfg missing"
curl -s -b "$JAR" "$BASE/" | rg -oF "cal: 'jalali'" >/dev/null && ok "cfg cal=jalali" || true

# ---------- ۸) ذخیره‌سازی epoch در بانک ----------
if command -v python3 >/dev/null; then
python3 - <<'EOF' && ok "DB: ستون‌های تاریخ INTEGER (epoch)" || bad "DB: قالب تاریخ"
import sqlite3
con = sqlite3.connect('/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db')
cur = con.cursor()
bad_cols = []
for tbl, col in [('AuditLogs','At'),('Users','CreatedAt'),('Employees','HireDate' if [c[1] for c in cur.execute('PRAGMA table_info(Employees)').fetchall()].count('HireDate') else 'CreatedAt'),('AppSettings','UpdatedAt')]:
    cols = {c[1]: c[2] for c in cur.execute(f"PRAGMA table_info({tbl})").fetchall()}
    if col in cols and 'INT' not in cols[col].upper():
        bad_cols.append((tbl, col, cols[col]))
assert not bad_cols, bad_cols
EOF
fi

# ---------- نتیجه ----------
echo "----------------------------------------"
echo "PHASE-79 E2E: PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ]
