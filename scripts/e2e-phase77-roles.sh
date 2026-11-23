#!/bin/bash
# ============================================================
# E2E فاز ۷۷ — نقش‌ها (ایجاد/ویرایش/حذف) + درخت دسترسی‌ها + تفکیک تأیید/رد
# ------------------------------------------------------------
# ۱) صفحهٔ کاربران: درخت دسترسی‌ها + دکمهٔ «نقش جدید» + پیش‌تیک نقش
# ۲) CRUD نقش سفارشی + گاردها (نقش سیستمی حذف‌شدنی نیست)
# ۳) ماتریس دسترسی نقش با کلیدهای جدید ذخیره می‌شود
# ۴) تفکیک تأیید/رد درخواست: کاربرِ «فقط تأیید» نمی‌تواند رد کند (403)
# ۵) رگرسیون: صفحات اصلی + کلیدهای قدیمی کاتالوگ پاک شده‌اند
# ============================================================
B=http://localhost:3000
J=/tmp/p77-e2e.jar
DB="python3.13 /home/z/my-project/scripts/dbq.py"
PASS=0; FAIL=0
say() { printf '%s\n' "$*"; }
chk() { if [ "$2" = "1" ]; then PASS=$((PASS+1)); say "PASS $1"; else FAIL=$((FAIL+1)); say "FAIL $1"; fi; }
dec() { python3 -c "import sys,json;print(json.loads(sys.stdin.read()).get('ok',''))" 2>/dev/null; }
decmsg() { python3 -c "import sys,json;d=json.loads(sys.stdin.read());m=d.get('message','') or d.get('error','');print(m)" 2>/dev/null; }

code=$(curl -s -c "$J" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { say "FAIL login admin ($code)"; exit 1; }
say "PASS login admin"

# ---------- ۱) صفحهٔ کاربران: درخت + دکمهٔ نقش جدید ----------
HTML=$(curl -s -b "$J" "$B/Users")
echo "$HTML" | grep -q 'id="permTree"' ; chk "درخت دسترسی‌ها در صفحهٔ کاربران رندر شد" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$HTML" | grep -q 'href="/Roles/Create"' ; chk "دکمهٔ «نقش جدید» حاضر است" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$HTML" | grep -q 'data-level="grp"' ; chk "سطح گروه (ماژول) در درخت" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$HTML" | grep -q 'data-level="feat"' ; chk "سطح قابلیت در درخت" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$HTML" | grep -q 'attendance.leave.approve' ; chk "کلید جدید تأیید در کاتالوگ/درخت" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$HTML" | grep -q 'attendance.leave.reject' ; chk "کلید جدید رد در کاتالوگ/درخت" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$HTML" | grep -q 'data-perms=' ; chk "پیش‌تیک دسترسی نقش (data-perms)" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$HTML" | grep -q 'attendance.approve' && ! echo "$HTML" | grep -q 'attendance.leave.approve' ; chk "کلید قدیمی attendance.approve دیگر عرضه نمی‌شود" "$([ $? -ne 0 ] && echo 1 || echo 0)"

# ---------- ۲) CRUD نقش سفارشی ----------
token() { curl -s -b "$J" -c "$J" "$B$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }

# پاکسازی اجرای قبلی
RID=$($DB "SELECT Id FROM Roles WHERE Name='e2e_p77_role'" 2>/dev/null | tr -d '[]"' )
if [ -n "$RID" ]; then
  $DB --exec "DELETE FROM RolePermissions WHERE RoleId=$RID" >/dev/null 2>&1
  $DB --exec "DELETE FROM UserRoles WHERE RoleId=$RID" >/dev/null 2>&1
  $DB --exec "DELETE FROM Roles WHERE Id=$RID" >/dev/null 2>&1
fi
T=$(token "/Roles/Create?partial=1")
PART=$(curl -s -b "$J" "$B/Roles/Create?partial=1")
echo "$PART" | grep -q 'name="name"' && echo "$PART" | grep -q 'name="title"' ; chk "فرم ایجاد نقش (partial مودال)" "$([ $? -eq 0 ] && echo 1 || echo 0)"

R=$(curl -s -b "$J" "$B/Roles/Create" -H "X-Requested-With: XMLHttpRequest" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "name=e2e_p77_role" --data-urlencode "title=نقش آزمون ۷۷" --data-urlencode "description=تست e2e")
[ "$(echo "$R" | dec)" = "True" ] ; chk "ایجاد نقش جدید (AJAX ok)" "$([ $? -eq 0 ] && echo 1 || echo 0)"

RID=$($DB "SELECT Id FROM Roles WHERE Name='e2e_p77_role'" 2>/dev/null | tr -d '[]"' )
[ -n "$RID" ] ; chk "نقش در بانک ثبت شد" "$([ -n "$RID" ] && echo 1 || echo 0)"

# نام تکراری رد شود
T=$(token "/Roles/Create?partial=1")
R=$(curl -s -b "$J" "$B/Roles/Create" -H "X-Requested-With: XMLHttpRequest" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "name=e2e_p77_role" --data-urlencode "title=تکراری")
echo "$R" | decmsg | grep -q 'از قبل وجود دارد' ; chk "نام تکراری نقش رد شد" "$([ $? -eq 0 ] && echo 1 || echo 0)"

# نام فنی فارسی رد شود
T=$(token "/Roles/Create?partial=1")
R=$(curl -s -b "$J" "$B/Roles/Create" -H "X-Requested-With: XMLHttpRequest" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "name=Persian-Name-۱۲۳" --data-urlencode "title=تست")
echo "$R" | decmsg | grep -q 'لاتین کوچک' ; chk "نام فنی غیرلاتین رد شد" "$([ $? -eq 0 ] && echo 1 || echo 0)"

# حذف نقش سیستمی رد شود
T=$(token "/Users")
SVID=$($DB "SELECT Id FROM Roles WHERE Name='viewer'" 2>/dev/null | tr -d '[]"' )
R=$(curl -s -b "$J" "$B/Roles/Delete" -H "X-Requested-With: XMLHttpRequest" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$T" --data-urlencode "id=$SVID")
echo "$R" | decmsg | grep -q 'سیستمی' ; chk "حذف نقش سیستمی رد شد" "$([ $? -eq 0 ] && echo 1 || echo 0)"

# ویرایش عنوان نقش
T=$(token "/Roles/Edit/$RID?partial=1")
R=$(curl -s -b "$J" "$B/Roles/Edit/$RID" -H "X-Requested-With: XMLHttpRequest" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$T" --data-urlencode "title=نقش آزمون ۷۷ ویرایش شد" --data-urlencode "description=d")
[ "$(echo "$R" | dec)" = "True" ] ; chk "ویرایش نقش (AJAX ok)" "$([ $? -eq 0 ] && echo 1 || echo 0)"

# ---------- ۳) ماتریس دسترسی نقش با کلیدهای جدید ----------
T=$(token "/Users")
R=$(curl -s -b "$J" "$B/Users/SetRolePermissions" -H "X-Requested-With: XMLHttpRequest" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$T" --data-urlencode "roleId=$RID" \
  --data-urlencode "permKeys=dashboard.view,attendance.view,attendance.leave.approve")
[ "$(echo "$R" | dec)" = "True" ] ; chk "ذخیرهٔ دسترسی‌های نقش (درخت → کلیدها)" "$([ $? -eq 0 ] && echo 1 || echo 0)"
N=$($DB "SELECT COUNT(*) FROM RolePermissions WHERE RoleId=$RID" 2>/dev/null | tr -d '[]"' )
[ "$N" = "3" ] ; chk "دقیقاً ۳ دسترسی برای نقش ثبت شد ($N)" "$([ "$N" = "3" ] && echo 1 || echo 0)"

# ---------- ۴) تفکیک تأیید/رد — کاربر «فقط تأیید» ----------
# کاربر آزمون با نقش سفارشی
TUID=$($DB "SELECT Id FROM Users WHERE Username='e2e_p77_user'" 2>/dev/null | tr -d '[]"' )
if [ -n "$TUID" ]; then
  $DB --exec "DELETE FROM UserRoles WHERE UserId=$TUID" >/dev/null 2>&1
  $DB --exec "DELETE FROM PasswordHistories WHERE UserId=$TUID" >/dev/null 2>&1
  $DB --exec "DELETE FROM RefreshTokens WHERE UserId=$TUID" >/dev/null 2>&1
  $DB --exec "DELETE FROM Users WHERE Id=$TUID" >/dev/null 2>&1
fi
HASH=$(python3 - <<'PYEOF'
import hashlib, os, base64
salt = os.urandom(16); password = "Test@7777".encode()
h = hashlib.pbkdf2_hmac('sha256', password, salt, 100000)
print("pbkdf2-sha256$100000$"+base64.b64encode(salt).decode()+"$"+base64.b64encode(h).decode())
PYEOF
)
$DB --exec "INSERT INTO Users (Username, PasswordHash, FullName, IsActive, IsSuperadmin, FailedLoginAttempts, MustChangePassword, PasswordChangedAt, CreatedAt, UpdatedAt) VALUES ('e2e_p77_user', '$HASH', 'کاربر آزمون ۷۷', 1, 0, 0, 0, datetime('now'), datetime('now'), datetime('now'))" >/dev/null 2>&1
TUID=$($DB "SELECT Id FROM Users WHERE Username='e2e_p77_user'" 2>/dev/null | tr -d '[]"' )
$DB --exec "INSERT INTO UserRoles (UserId, RoleId) VALUES ($TUID, $RID)" >/dev/null 2>&1

# یک درخواست مرخصی در انتظار تأیید از دادهٔ نمونه — پس از آزمون، وضعیتش برمی‌گردد
LID=$($DB "SELECT Id FROM LeaveRequests WHERE Status='در انتظار تأیید' LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.loads(sys.stdin.read())[0][0] if sys.stdin.read(0)==None else '')" 2>/dev/null)
[ -z "$LID" ] && LID=$($DB --json "SELECT Id FROM LeaveRequests WHERE Status='در انتظار تأیید' LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.loads(sys.stdin.read())[0]['Id'])" 2>/dev/null)
[ -n "$LID" ] ; chk "درخواست در انتظار تأیید برای آزمون موجود است" "$([ -n "$LID" ] && echo 1 || echo 0)"

JU=/tmp/p77-user.jar
code=$(curl -s -c "$JU" -H "Content-Type: application/json" -d '{"username":"e2e_p77_user","password":"Test@7777"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] ; chk "ورود کاربر نقش سفارشی" "$([ "$code" = "200" ] && echo 1 || echo 0)"

# فهرست درخواست‌ها: دکمهٔ تأیید هست، دکمهٔ رد نیست
REQ=$(curl -s -b "$JU" "$B/Requests")
echo "$REQ" | grep -q 'DecideLeave' ; chk "کاربر فقط‌تأیید صفحهٔ درخواست‌ها را می‌بیند" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$REQ" | grep -q 'approve" value="true"' ; chk "دکمهٔ تأیید برای کاربر فقط‌تأیید هست" "$([ $? -eq 0 ] && echo 1 || echo 0)"
echo "$REQ" | grep -q 'approve" value="false"' ; chk "دکمهٔ رد برای کاربر فقط‌تأیید پنهان است" "$([ $? -ne 0 ] && echo 1 || echo 0)"

# رد با API مستقیم → 403
TU=$(curl -s -b "$JU" -c "$JU" "$B/Requests" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
CODE=$(curl -s -b "$JU" "$B/Requests/DecideLeave" -H "X-Requested-With: XMLHttpRequest" -o /tmp/p77-reject.json -w "%{http_code}" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TU" --data-urlencode "id=$LID" --data-urlencode "approve=false")
[ "$CODE" = "403" ] ; chk "رد درخواست برای فقط‌تأیید → 403" "$([ "$CODE" = "403" ] && echo 1 || echo 0)"

# تأیید → 200 و وضعیت عوض می‌شود
CODE=$(curl -s -b "$JU" "$B/Requests/DecideLeave" -H "X-Requested-With: XMLHttpRequest" -o /tmp/p77-approve.json -w "%{http_code}" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$TU" --data-urlencode "id=$LID" --data-urlencode "approve=true")
[ "$CODE" = "200" ] ; chk "تأیید درخواست برای فقط‌تأیید → 200" "$([ "$CODE" = "200" ] && echo 1 || echo 0)"
ST=$($DB "SELECT Status FROM LeaveRequests WHERE Id='$LID'" 2>/dev/null | tr -d '[]"' )
[ "$ST" = "تأیید شده" ] ; chk "وضعیت درخواست تأیید شد ($ST)" "$([ "$ST" = "تأیید شده" ] && echo 1 || echo 0)"

# ---------- ۵) رگرسیون ----------
for P in / /Companies /Attendance /Parking /Visitors /Settings /Audit; do
  CODE=$(curl -s -b "$J" -o /dev/null -w "%{http_code}" "$B$P")
  [ "$CODE" = "200" ] ; chk "رگرسیون GET $P = 200" "$([ "$CODE" = "200" ] && echo 1 || echo 0)"
done
STALE=$($DB "SELECT COUNT(*) FROM Permissions WHERE Key='attendance.approve'" 2>/dev/null | tr -d '[]"' )
[ "$STALE" = "0" ] ; chk "کلید قدیمی از کاتالوگ بانک پاک شد" "$([ "$STALE" = "0" ] && echo 1 || echo 0)"

# ---------- پاکسازی نقش آزمون ----------
$DB --exec "DELETE FROM UserRoles WHERE RoleId=$RID" >/dev/null 2>&1
$DB --exec "DELETE FROM RolePermissions WHERE RoleId=$RID" >/dev/null 2>&1
$DB --exec "DELETE FROM Roles WHERE Id=$RID" >/dev/null 2>&1
$DB --exec "DELETE FROM UserRoles WHERE UserId=$TUID" >/dev/null 2>&1
$DB --exec "DELETE FROM PasswordHistories WHERE UserId=$TUID" >/dev/null 2>&1
$DB --exec "DELETE FROM RefreshTokens WHERE UserId=$TUID" >/dev/null 2>&1
$DB --exec "DELETE FROM Users WHERE Id=$TUID" >/dev/null 2>&1
$DB --exec "UPDATE LeaveRequests SET Status='در انتظار تأیید', Approver=NULL WHERE Id='$LID'" >/dev/null 2>&1

say ""
say "== نتیجه: $PASS موفق، $FAIL ناموفق =="
[ "$FAIL" = "0" ]
