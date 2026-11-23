#!/bin/bash
# ============================================================
# E2E: اتصال دوطرفه پرسنل ↔ کاربر (تسک ۲۷)
# پوشش: ستون حساب کاربری در پرسنل، دکمه اتصال در کاربران،
#       مودال اتصال، اتصال/قطع AJAX، یکتایی اتصال، منع کاربر خودکار
#       emp:*، نقش پرسنل اختیاری، گارد ورود کد پرسنلی پس از اتصال،
#       دسترسی (users.manage)، آنتی‌فورجری، پاکسازی idempotent
# ============================================================
B=http://localhost:3000
J=/tmp/link-e2e.jar
rm -f "$J"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "PASS $1"; }
bad() { FAIL=$((FAIL+1)); echo "FAIL $1"; }
jmsg() { python3 -c "import sys,json; j=json.load(sys.stdin); print(j.get('message',''))" 2>/dev/null; }
jok()  { python3 -c "import sys,json; j=json.load(sys.stdin); sys.exit(0 if j.get('ok')==True else 1)" 2>/dev/null; }
# خروجی dbq برای نتیجه خالی «[]» است — یکسان‌سازی به رشته خالی
one() { python3 /home/z/my-project/scripts/dbq.py "$1" 2>/dev/null | sed 's/^\[\(.*\)\]$/\1/' | tr -d '"'; }

code=$(curl -s -c "$J" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { echo "LOGIN FAILED ($code)"; exit 1; }
ok "login admin"

token() {
  curl -s -b "$J" -c "$J" "$B/Users" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'
}
T=$(token)
[ -n "$T" ] || { echo "NO ANTIFORGERY TOKEN"; exit 1; }
ok "antiforgery token"

ajax() { # $1=path, rest: data
  curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" \
    -H "RequestVerificationToken: $T" \
    --data-urlencode "__RequestVerificationToken=$T" "${@:2}" "$B$1"
}
dbq() { python3 /home/z/my-project/scripts/dbq.py "$1" 2>/dev/null; }

# --- ساخت/بازیابی کاربر آزمون (idempotent) ---
UADMIN=$(one "SELECT Id FROM Users WHERE Username='admin' LIMIT 1")
[ -n "$UADMIN" ] || { echo "NO ADMIN"; exit 1; }

UID2=$(one "SELECT Id FROM Users WHERE Username='e2e-link'" | head -1)
if [ -z "$UID2" ]; then
  RES=$(ajax /Users/Create -d "username=e2e-link&fullName=کاربر آزمون اتصال&password=Link@12345&password2=Link@12345&isActive=true")
  echo "$RES" | jok && ok "test user e2e-link created" || bad "test user e2e-link created ($(echo "$RES" | jmsg))"
  UID2=$(one "SELECT Id FROM Users WHERE Username='e2e-link'" | head -1)
else
  # احتیاط: اگر اجرای قبلی وسط راه قطع شده باشد، اتصال جامانده باز می‌شود
  ajax /Users/LinkEmployee -d "userId=$UID2&employeeId=" > /dev/null
  ok "test user e2e-link reused"
fi
[ -n "$UID2" ] || { echo "NO TEST USER"; exit 1; }
# فعال‌سازی قطعی (نه تاگل کورکورانه — ToggleActive وضعیت را برعکس می‌کند)
ST=$(one "SELECT IFNULL(IsActive,0) FROM Users WHERE Id=$UID2")
[ "${ST:-0}" = "0" ] && { ajax /Users/ToggleActive -d "id=$UID2" > /dev/null; ok "test user activated (was inactive)"; } || ok "test user already active"

EMPID=$(one "SELECT Id FROM Employees WHERE Status='فعال' AND Id NOT IN (SELECT IFNULL(EmployeeId,'') FROM Users) ORDER BY PersonnelCode LIMIT 1" | head -1)
[ -n "$EMPID" ] || { echo "NO FREE EMPLOYEE"; exit 1; }
PCODE=$(one "SELECT PersonnelCode FROM Employees WHERE Id='$EMPID'")
NID=$(one "SELECT NationalId FROM Employees WHERE Id='$EMPID'")
ok "free employee picked: $PCODE"

# --- ۱) سمت کاربران: دکمه و مودال ---
HTML=$(curl -s -b "$J" "$B/Users")
echo "$HTML" | grep -q 'پرسنل مرتبط' && ok "users page has linked-personnel column" || bad "linked-personnel column"
echo "$HTML" | grep -q "data-modal=\"/Users/LinkEmployee?userId=$UID2\"" && ok "link button for unlinked user" || bad "link button for unlinked user"

F=$(curl -s -b "$J" "$B/Users/LinkEmployee?userId=$UID2&partial=1")
echo "$F" | grep -q 'name="employeeId"' && ok "link modal form has employeeId select" || bad "link modal form"
echo "$F" | grep -q 'name="addEmployeeRole"' && ok "link modal has employee-role checkbox" || bad "role checkbox"
echo "$F" | grep -q "$EMPID" && ok "free employee listed in modal" || bad "free employee in modal"

# --- ۲) اتصال AJAX کاربر → پرسنل ---
RES=$(ajax /Users/LinkEmployee -d "userId=$UID2&employeeId=$EMPID&addEmployeeRole=true")
M=$(echo "$RES" | jmsg)
echo "$RES" | jok && ok "link via AJAX: $M" || bad "link via AJAX ($M)"

DBL=$(one "SELECT IFNULL(EmployeeId,'') FROM Users WHERE Id=$UID2")
[ "$DBL" = "$EMPID" ] && ok "DB: user.EmployeeId set" || bad "DB: user.EmployeeId ($DBL)"

ROLE=$(one "SELECT COUNT(*) FROM UserRoles ur JOIN Roles r ON r.Id=ur.RoleId WHERE ur.UserId=$UID2 AND r.Name='employee'")
[ "${ROLE:-0}" = "1" ] && ok "DB: employee role auto-added" || bad "employee role ($ROLE)"

# --- ۳) یکتایی: کاربر دیگر (admin) به همان پرسنل ---
HTML2=$(curl -s -b "$J" "$B/Users")
echo "$HTML2" | grep -q "$PCODE — " && ok "users page shows personnel badge" || bad "personnel badge on users page"

RES=$(ajax /Users/LinkEmployee -d "userId=$UADMIN&employeeId=$EMPID")
echo "$RES" | jok && bad "uniqueness guard (should fail)" || ok "uniqueness guard: another user cannot link same employee"
echo "$RES" | jmsg | grep -q "قبلاً به کاربر" && ok "uniqueness error message Persian" || bad "uniqueness message"

# --- ۴) اتصال مجدد همان جفت (idempotent) ---
RES=$(ajax /Users/LinkEmployee -d "userId=$UID2&employeeId=$EMPID&addEmployeeRole=true")
echo "$RES" | jok && ok "re-link same pair idempotent" || bad "re-link idempotent"
ROLE=$(one "SELECT COUNT(*) FROM UserRoles ur JOIN Roles r ON r.Id=ur.RoleId WHERE ur.UserId=$UID2 AND r.Name='employee'")
[ "${ROLE:-0}" = "1" ] && ok "no duplicate role after re-link" || bad "duplicate role ($ROLE)"

# --- ۵) گارد ورود کد پرسنلی پس از اتصال — جریان واقعی مرورگر: GET صفحه ورود → توکن تازه → POST ---
J4=/tmp/link-e2e-emp.jar; rm -f "$J4"
T4=$(curl -s -b "$J4" -c "$J4" "$B/Auth/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
BODY=$(curl -s -b "$J4" -c "$J4" -X POST "$B/Auth/Login" \
  --data-urlencode "__RequestVerificationToken=$T4" \
  --data-urlencode "identifier=$PCODE" --data-urlencode "secret=$NID")
if echo "$BODY" | grep -q "حساب کاربری اختصاصی دارد"; then
  ok "employee-code login blocked after link"
else
  bad "employee-code login blocked after link (len=${#BODY})"
  echo "   retry state: linked-users=$(dbq "SELECT Username, IsActive FROM Users WHERE EmployeeId='$EMPID'")"
fi

# — گارد قوی‌تر: حتی حساب متصلِ غیرفعال هم مسدود است (تسک ۲۷ — جلوی دورزدن غیرفعال‌سازی) —
ST=$(one "SELECT IFNULL(IsActive,0) FROM Users WHERE Id=$UID2")
[ "${ST:-0}" = "1" ] && ajax /Users/ToggleActive -d "id=$UID2" > /dev/null
BODYI=$(curl -s -b "$J4" -c "$J4" -X POST "$B/Auth/Login" \
  --data-urlencode "__RequestVerificationToken=$T4" \
  --data-urlencode "identifier=$PCODE" --data-urlencode "secret=$NID")
echo "$BODYI" | grep -q "حساب کاربری اختصاصی دارد" && ok "inactive linked user also blocks code-login" || bad "inactive linked user also blocks"
ajax /Users/ToggleActive -d "id=$UID2" > /dev/null  # بازگشت به فعال (از 0 قطعی به 1)

# --- ۶) قطع اتصال ---
RES=$(ajax /Users/LinkEmployee -d "userId=$UID2&employeeId=")
echo "$RES" | jok && ok "unlink via AJAX: $(echo "$RES" | jmsg)" || bad "unlink via AJAX"
DBL=$(one "SELECT IFNULL(EmployeeId,'') FROM Users WHERE Id=$UID2")
[ -z "$DBL" ] && ok "DB: user.EmployeeId cleared" || bad "DB unlink ($DBL)"

RES=$(ajax /Users/LinkEmployee -d "userId=$UID2&employeeId=")
echo "$RES" | jok && bad "double-unlink should fail" || ok "double-unlink rejected"
echo "$RES" | jmsg | grep -q "به هیچ پرسنلی متصل نیست" && ok "double-unlink Persian error" || bad "double-unlink message"

# --- ۷) سمت پرسنل: اتصال از مودال ویرایش ---
RES=$(ajax /Users/LinkEmployee -d "userId=$UID2&employeeId=$EMPID&addEmployeeRole=true")
echo "$RES" | jok && ok "re-link for employee-side test" || bad "re-link"
EH=$(curl -s -b "$J" "$B/Employees")
echo "$EH" | grep -q '<th>حساب کاربری</th>' && ok "employees page has account column" || bad "account column"
echo "$EH" | grep -q 'title="کاربر متصل به این پرسنل">e2e-link' && ok "employees page shows linked username" || bad "linked username shown"

EF=$(curl -s -b "$J" "$B/Employees/Edit/$EMPID?partial=1")
echo "$EF" | grep -q 'حساب کاربری متصل' && ok "edit modal has linked-account section" || bad "linked-account section"
echo "$EF" | grep -q 'name="userId"' && echo "$EF" | grep -q '/Users/LinkEmployee' && ok "edit modal link form targets /Users/LinkEmployee" || bad "edit modal link form"
echo "$EF" | grep -q "قطع اتصال حساب" && ok "edit modal shows unlink button" || bad "unlink button in modal"

# --- ۸) کاربر خودکار emp:* نباید قابل اتصال باشد ---
EMPUID=$(one "SELECT Id FROM Users WHERE Username LIKE 'emp:%' LIMIT 1" | head -1)
if [ -n "$EMPUID" ]; then
  RES=$(ajax /Users/LinkEmployee -d "userId=$EMPUID&employeeId=$EMPID")
  echo "$RES" | jmsg | grep -q "حساب خودکار پرسنل" && ok "synthetic emp:* user rejected" || bad "synthetic emp:* guard"
else
  ok "synthetic emp:* user (none yet — guard coded)"
fi

# --- ۹) دسترسی: کاربر بدون users.manage ---
J3=/tmp/link-e2e-op.jar; rm -f "$J3"
curl -s -c "$J3" -H "Content-Type: application/json" -d '{"username":"operator","password":"Oper@1234"}' "$B/api/auth/login" -o /dev/null
code=$(curl -s -b "$J3" -o /dev/null -w "%{http_code}" "$B/Users/LinkEmployee?userId=$UID2")
[ "$code" = "302" ] && ok "operator denied link page (302)" || bad "operator link page ($code)"
code=$(curl -s -b "$J3" -H "X-Requested-With: XMLHttpRequest" -o /dev/null -w "%{http_code}" -X POST "$B/Users/LinkEmployee" -d "userId=$UID2&employeeId=")
[ "$code" = "302" ] && ok "operator denied link POST (302)" || bad "operator link POST ($code)"

# --- ۱۰) آنتی‌فورجری ---
code=$(curl -s -b "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Users/LinkEmployee" -d "userId=$UID2&employeeId=")
[ "$code" = "400" ] && ok "antiforgery rejects tokenless POST (400)" || bad "antiforgery ($code)"

# --- ۱۱) پاکسازی idempotent: قطع اتصال + غیرفعال‌سازی قطعی کاربر آزمون ---
ajax /Users/LinkEmployee -d "userId=$UID2&employeeId=" > /dev/null
ST=$(one "SELECT IFNULL(IsActive,0) FROM Users WHERE Id=$UID2")
[ "${ST:-0}" = "1" ] && ajax /Users/ToggleActive -d "id=$UID2" > /dev/null
ST=$(one "SELECT IFNULL(IsActive,0) FROM Users WHERE Id=$UID2")
[ "${ST:-1}" = "0" ] && ok "cleanup: test user disabled + unlinked" || bad "cleanup state"
# پاکسازی نقش «پرسنل» چسبیده به admin از اجرای خراب قدیمی (idempotent)
ADMINROLE=$(one "SELECT COUNT(*) FROM UserRoles WHERE UserId=$UADMIN AND RoleId=(SELECT Id FROM Roles WHERE Name='employee')")
[ "${ADMINROLE:-0}" = "1" ] && { python3 /home/z/my-project/scripts/dbq.py --exec "DELETE FROM UserRoles WHERE UserId=$UADMIN AND RoleId=(SELECT Id FROM Roles WHERE Name='employee')" > /dev/null; ok "cleanup: removed stray employee role from admin"; } || ok "cleanup: admin roles clean"
# پاکسازی اتصال جامانده admin ← پرسنل (از اجرای خراب قدیمی) — وگرنه ورود کد پرسنلی آن پرسنل مسدود می‌ماند
ALINK=$(one "SELECT IFNULL(EmployeeId,'') FROM Users WHERE Id=$UADMIN")
[ -n "$ALINK" ] && { ajax /Users/LinkEmployee -d "userId=$UADMIN&employeeId=" > /dev/null; ok "cleanup: admin unlinked from stray employee"; } || ok "cleanup: admin has no link"

echo ""
echo "=============================="
echo "PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ] && echo "ALL GREEN ✓" || echo "FAILURES ✗"
exit $FAIL
