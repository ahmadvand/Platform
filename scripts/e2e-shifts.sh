#!/bin/bash
# ============================================================
# E2E: مدیریت شیفت‌ها (ATT-02) — پنل CRUD + اعتبارسنجی + گاردها
# پوشش: منو، صفحه لیست، فرم مودال، ایجاد/ویرایش/حذف AJAX،
#       نام تکراری، قالب ساعت، الگوی چرخش اقماری، حذف شیفت متصل،
#       آنتی‌فورجری، fallback بدون-JS، انکار پرسنل فاقد دسترسی
# ============================================================
B=http://localhost:3000
J=/tmp/shifts-e2e.jar
rm -f "$J"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "PASS $1"; }
bad() { FAIL=$((FAIL+1)); echo "FAIL $1"; }
chk() { [ "$1" = "0" ] && ok "$2" || bad "$2"; }  # $1 = exit code of previous assert

code=$(curl -s -c "$J" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { echo "LOGIN FAILED ($code)"; exit 1; }
ok "login admin"

token() {
  curl -s -b "$J" -c "$J" "$B/Shifts" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'
}
T=$(token)
[ -n "$T" ] || { echo "NO ANTIFORGERY TOKEN"; exit 1; }
ok "antiforgery token"

ajax() { # $1=path  rest: data
  curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" \
    -H "RequestVerificationToken: $T" \
    --data-urlencode "__RequestVerificationToken=$T" "${@:2}" "$B$1"
}
post_plain() { # بدون هدر AJAX — مسیر fallback کلاسیک
  curl -s -o /dev/null -b "$J" -c "$J" -w "%{http_code}" \
    -H "RequestVerificationToken: $T" \
    --data-urlencode "__RequestVerificationToken=$T" "${@:2}" "$B$1"
}

# --- ۱) صفحه لیست + منو ---
HTML=$(curl -s -b "$J" "$B/Shifts")
echo "$HTML" | grep -q 'شیفت اداری' && ok "seeded shifts visible in panel" || bad "seeded shifts visible in panel"
echo "$HTML" | grep -q 'نوبت سوم (شب)' && ok "night shift (23:00→07:00) listed" || bad "night shift listed"
echo "$HTML" | grep -q 'data-modal="/Shifts/Create"' && ok "create button present" || bad "create button present"
echo "$HTML" | grep -q '14/14' && ok "cycle pattern column works" || bad "cycle pattern column"
MENU=$(curl -s -b "$J" "$B/Attendance")
echo "$MENU" | grep -q '/Shifts' && ok "menu item under حضور و غیاب" || bad "menu item under حضور و غیاب"

# --- ۲) فرم مودال ---
F=$(curl -s -b "$J" "$B/Shifts/Create?partial=1")
echo "$F" | grep -q 'name="cyclePattern"' && ok "modal form has cyclePattern field" || bad "modal form"
echo "$F" | grep -q 'نوع شیفت' && ok "modal form has type select" || bad "modal form type"

# --- ۳) ایجاد شیفت آزمایشی (اقماری) ---
RES=$(ajax /Shifts/Create -d "name=شیفت آزمون e2e&type=اقماری&startTime=22:00&endTime=06:00&breakMinutes=45&toleranceLate=10&cyclePattern=14/14&active=true")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==True and 'تعریف شد' in j['message']" 2>/dev/null && ok "create AJAX shift" || bad "create AJAX shift"

HTML=$(curl -s -b "$J" "$B/Shifts")
SID=$(echo "$HTML" | grep -o 'value="[a-f0-9]\{32\}"[^>]*>🗑' | grep -o '[a-f0-9]\{32\}' | while read -r x; do
  : ; done; echo "")
# استخراج id شیفت آزمایشی از ردیف جدول
SID=$(python3 -c "
import re
html = '''$HTML'''
i = html.find('شیفت آزمون e2e')
seg = html[i:i+4000] if i >= 0 else ''
m = re.search(r'name=\"id\" value=\"([a-f0-9]{32})\"', seg)
print(m.group(1) if m else '')
")
[ -n "$SID" ] && ok "test shift row found (id extracted)" || bad "test shift row found"

# --- ۴) اعتبارسنجی‌ها ---
RES=$(ajax /Shifts/Create -d "name=شیفت آزمون e2e&type=عادی&startTime=08:00&endTime=16:00&breakMinutes=60&toleranceLate=10&active=true")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==False and 'قبلاً' in j['message']" 2>/dev/null && ok "duplicate name rejected" || bad "duplicate name rejected"

RES=$(ajax /Shifts/Create -d "name=شیفت ساعت بد&type=عادی&startTime=25:99&endTime=16:00&breakMinutes=60&toleranceLate=10&active=true")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==False and 'HH:MM' in j['message']" 2>/dev/null && ok "invalid time rejected" || bad "invalid time rejected"

RES=$(ajax /Shifts/Create -d "name=شیفت الگوی بد&type=اقماری&startTime=08:00&endTime=16:00&breakMinutes=60&toleranceLate=10&cyclePattern=abc&active=true")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==False and 'الگو' in j['message']" 2>/dev/null && ok "bad cycle pattern rejected" || bad "bad cycle pattern rejected"

RES=$(ajax /Shifts/Create -d "name=شیفت اقماری بی‌الگو&type=اقماری&startTime=08:00&endTime=16:00&breakMinutes=60&toleranceLate=10&active=true")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==False and 'الگوی چرخش' in j['message']" 2>/dev/null && ok "camp shift without cycle rejected" || bad "camp shift without cycle rejected"

RES=$(ajax /Shifts/Create -d "name=شیفت صفر&type=عادی&startTime=08:00&endTime=08:00&breakMinutes=60&toleranceLate=10&active=true")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==False and 'یکسان' in j['message']" 2>/dev/null && ok "zero-length shift rejected" || bad "zero-length shift rejected"

# --- ۵) ویرایش ---
RES=$(ajax "/Shifts/Edit/$SID" -d "name=شیفت آزمون e2e ویرایش‌شده&type=چرخشی&startTime=21:30&endTime=05:30&breakMinutes=30&toleranceLate=5&cyclePattern=21/7&active=true")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==True and 'به‌روزرسانی شد' in j['message']" 2>/dev/null && ok "edit AJAX shift" || bad "edit AJAX shift"
HTML=$(curl -s -b "$J" "$B/Shifts")
echo "$HTML" | grep -q '21:30 → 05:30' && ok "edited times visible" || bad "edited times visible"
echo "$HTML" | grep -q '21/7' && ok "edited cycle visible" || bad "edited cycle visible"

# --- ۶) حذف شیفت متصل به پرسنل (شیفت اداری seed شده به پرسنل متصل است) ---
AID=$(python3 -c "
import re
html = '''$HTML'''
i = html.find('شیفت اداری')
seg = html[i:i+4000] if i >= 0 else ''
m = re.search(r'name=\"id\" value=\"([a-f0-9]{32})\"', seg)
print(m.group(1) if m else '')
")
RES=$(ajax /Shifts/Delete -d "id=$AID")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==False and 'متصل است' in j['message']" 2>/dev/null && ok "delete assigned shift blocked" || bad "delete assigned shift blocked"

# --- ۷) حذف شیفت آزمایشی ---
RES=$(ajax /Shifts/Delete -d "id=$SID")
echo "$RES" | python3 -c "import sys,json; j=json.load(sys.stdin); assert j['ok']==True and 'حذف شد' in j['message']" 2>/dev/null && ok "delete unassigned shift" || bad "delete unassigned shift"
HTML=$(curl -s -b "$J" "$B/Shifts")
echo "$HTML" | grep -q 'شیفت آزمون e2e' && bad "deleted shift gone from list" || ok "deleted shift gone from list"

# --- ۸) آنتی‌فورجری و fallback ---
C1=$(curl -s -o /dev/null -b "$J" -w "%{http_code}" -H "X-Requested-With: XMLHttpRequest" -d "name=x&type=عادی&startTime=08:00&endTime=16:00" "$B/Shifts/Create")
[ "$C1" = "400" ] && ok "missing antiforgery → 400" || bad "missing antiforgery → 400 ($C1)"

C2=$(post_plain /Shifts/Create -d "name=شیفت فالبک&type=عادی&startTime=08:00&endTime=16:00&breakMinutes=60&toleranceLate=10&active=true" -L)
[ "$C2" = "200" ] && ok "no-JS fallback → 302 + banner page" || bad "no-JS fallback ($C2)"
HTML=$(curl -s -b "$J" "$B/Shifts")
echo "$HTML" | grep -q 'شیفت فالبک' && ok "fallback created shift" || bad "fallback created shift"
# پاکسازی فالبک
FID=$(python3 -c "
import re
html = '''$HTML'''
i = html.find('شیفت فالبک')
seg = html[i:i+4000] if i >= 0 else ''
m = re.search(r'name=\"id\" value=\"([a-f0-9]{32})\"', seg)
print(m.group(1) if m else '')
")
[ -n "$FID" ] && ajax /Shifts/Delete -d "id=$FID" > /dev/null && ok "fallback shift cleaned up" || bad "fallback cleanup"

# --- ۹) انکار پرسنل فاقد دسترسی (کارمند 1001) ---
J2=/tmp/shifts-e2e-emp.jar
rm -f "$J2"
LTP=$(curl -s -c "$J2" "$B/Auth/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
CLOGIN=$(curl -s -o /dev/null -b "$J2" -c "$J2" -w "%{http_code}" -L \
  --data-urlencode "__RequestVerificationToken=$LTP" \
  --data-urlencode "personnelCode=1001" --data-urlencode "nationalId=1483146377" "$B/Auth/LoginEmployee")
[ "$CLOGIN" = "200" ] || { echo "EMP LOGIN FAILED ($CLOGIN)"; exit 1; }
C3=$(curl -s -o /dev/null -b "$J2" -w "%{url_effective}" -L "$B/Shifts")
echo "$C3" | grep -q "Denied?perm=shifts.view" && ok "employee without shifts.view denied" || bad "employee without shifts.view denied ($C3)"
rm -f "$J2"

echo "----------------------------------------"
echo "RESULT: $PASS pass, $FAIL fail"
exit $([ "$FAIL" = "0" ] && echo 0 || echo 1)
