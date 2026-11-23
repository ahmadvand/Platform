#!/bin/bash
# ============================================================
# E2E: ایجاد کاربر جدید (صفحه کاربران و نقش‌ها) + دسترسی users.create
# - فرم AJAX با پاسخ JSON و toast
# - اعتبارسنجی‌ها: یکتایی، سیاست رمز، تطبیق، کاراکترهای نام کاربری
# - ورود موفق کاربر تازه + ردیف حسابرسی
# - دسترسی منفی: hr (بدون users.create) نباید فرم را ببیند
# ============================================================
B=http://localhost:3000
J=/tmp/users-e2e.jar
HRJ=/tmp/users-e2e-hr.jar
rm -f "$J" "$HRJ"
DB="python3.13 /home/z/my-project/scripts/dbq.py"

# پیام فارسی از پاسخ JSON (System.Text.Json یونیکد را escape می‌کند)
dec() { python3 -c "import sys,json;print(json.loads(sys.stdin.read()).get('message',''))" 2>/dev/null; }

code=$(curl -s -c "$J" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { echo "FAIL login admin ($code)"; exit 1; }
echo "PASS login admin"

# --- پاکسازی اجرای قبلی (idempotent) ---
U=$($DB "SELECT Id FROM Users WHERE Username='e2e_tmp1'" 2>/dev/null | tr -d '[]"' )
if [ -n "$U" ]; then
  $DB --exec "DELETE FROM UserRoles WHERE UserId=$U" >/dev/null 2>&1
  $DB --exec "DELETE FROM PasswordHistories WHERE UserId=$U" >/dev/null 2>&1
  $DB --exec "DELETE FROM RefreshTokens WHERE UserId=$U" >/dev/null 2>&1
  $DB --exec "DELETE FROM Users WHERE Id=$U" >/dev/null 2>&1
  echo "PASS cleanup previous run"
fi

# --- صفحه کاربران: دکمه + دسترسی در ماتریس ---
HTML=$(curl -s -b "$J" "$B/Users")
echo "$HTML" | grep -q 'href="/Users/Create"' && echo "PASS create button in /Users" || echo "FAIL create button"
echo "$HTML" | grep -q 'data-modal="/Users/Create"' && echo "PASS create button opens modal (data-modal)" || echo "FAIL data-modal button"
echo "$HTML" | grep -q 'data-refresh-host' && echo "PASS refresh host declared" || echo "FAIL refresh host"
# فاز ۷۷ — ماتریس دسترسی به مودال درختی نقش‌ها منتقل شد؛ کلید در مودال بررسی می‌شود
MODAL=$(curl -s -b "$J" "$B/Users/RolePerms?roleId=1" --max-time 15)
echo "$MODAL" | grep -q 'users\.create' && echo "PASS users.create in permission tree modal" || echo "FAIL users.create in modal"

# --- همه فرم‌های صفحه کاربران باید AJAX باشند (بدون استثنا) ---
NOAJAX=$(echo "$HTML" | grep -o '<form[^>]*action="/Users/[^"]*"[^>]*>' | grep -cv 'data-ajax' || true)
[ "$NOAJAX" = "0" ] && echo "PASS all /Users forms are ajax" || echo "FAIL $NOAJAX non-ajax forms"

# --- partial (مودال) — بدون layout ---
PART=$(curl -s -b "$J" "$B/Users/Create?partial=1")
echo "$PART" | grep -q 'name="username"' && echo "$PART" | grep -q 'name="roleIds"' \
  && echo "PASS partial form fields" || echo "FAIL partial form fields"
echo "$PART" | grep -q 'data-modal-title' && echo "PASS partial has modal title" || echo "FAIL partial title"
echo "$PART" | grep -q '<!DOCTYPE\|<html\|class="sidebar"' && echo "FAIL partial leaks layout" || echo "PASS partial has no layout"
# حالت عادی (fallback صفحه کامل) هنوز کار می‌کند
curl -s -b "$J" "$B/Users/Create" | grep -q 'class="sidebar"' && echo "PASS full-page mode intact" || echo "FAIL full-page mode"

# --- فرم ایجاد ---
token() { curl -s -b "$J" -c "$J" "$B$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
T=$(token /Users/Create)
[ -n "$T" ] || { echo "FAIL antiforgery token"; exit 1; }
FORM=$(curl -s -b "$J" "$B/Users/Create")
echo "$FORM" | grep -q 'name="username"' && echo "$FORM" | grep -q 'name="password2"' && echo "$FORM" | grep -q 'name="roleIds"' && echo "$FORM" | grep -q 'name="employeeId"' \
  && echo "PASS create form fields" || echo "FAIL create form fields"

VIEWER_ID=$($DB "SELECT Id FROM Roles WHERE Name='viewer'" 2>/dev/null | tr -d '[]"' )

# --- ایجاد موفق با AJAX ---
R=$(curl -s -b "$J" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "username=e2e_tmp1" \
  --data-urlencode "fullName=کاربر آزمایشی E2E" \
  --data-urlencode "password=Test@1234" \
  --data-urlencode "password2=Test@1234" \
  --data-urlencode "roleIds=$VIEWER_ID" \
  --data-urlencode "isActive=true" \
  "$B/Users/Create")
echo "$R" | grep -q '"ok":true' && echo "PASS ajax create ok" || echo "FAIL ajax create → $R"
echo "$R" | dec | grep -q 'ایجاد شد' && echo "PASS success message (toast payload)" || echo "FAIL success message → $R"

# --- کاربر در فهرست ---
curl -s -b "$J" "$B/Users" | grep -q 'e2e_tmp1' && echo "PASS user listed in /Users" || echo "FAIL user not listed"

# --- خطاها: تکراری / رمز ضعیف / عدم تطبیق / کاراکتر غیرمجاز ---
R=$(curl -s -b "$J" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "username=e2e_tmp1" --data-urlencode "fullName=تکراری" \
  --data-urlencode "password=Test@1234" --data-urlencode "password2=Test@1234" "$B/Users/Create")
echo "$R" | dec | grep -q 'قبلاً استفاده شده' && echo "PASS duplicate username rejected" || echo "FAIL duplicate → $R"

R=$(curl -s -b "$J" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "username=e2e_tmp2" --data-urlencode "fullName=ضعیف" \
  --data-urlencode "password=abc" --data-urlencode "password2=abc" "$B/Users/Create")
echo "$R" | dec | grep -q 'حداقل ۸ کاراکتر' && echo "PASS weak password rejected" || echo "FAIL weak → $R"

R=$(curl -s -b "$J" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "username=e2e_tmp2" --data-urlencode "fullName=نامنطبق" \
  --data-urlencode "password=Test@1234" --data-urlencode "password2=Test@9999" "$B/Users/Create")
echo "$R" | dec | grep -q 'یکسان نیست' && echo "PASS password mismatch rejected" || echo "FAIL mismatch → $R"

R=$(curl -s -b "$J" -H "X-Requested-With: XMLHttpRequest" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data-urlencode "username=سلام" --data-urlencode "fullName=غیرلاتین" \
  --data-urlencode "password=Test@1234" --data-urlencode "password2=Test@1234" "$B/Users/Create")
echo "$R" | dec | grep -q 'حروف لاتین' && echo "PASS non-latin username rejected" || echo "FAIL charset → $R"

# --- ورود کاربر تازه (رمز درست هش شده و حساب فعال است) ---
code=$(curl -s -H "Content-Type: application/json" -d '{"username":"e2e_tmp1","password":"Test@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && echo "PASS new user can login" || echo "FAIL new user login ($code)"

# --- شش عملیات صفحه کاربران: همه باید AJAX → JSON بدهند ---
U2=$($DB "SELECT Id FROM Users WHERE Username='e2e_tmp1'" 2>/dev/null | tr -d '[]"' )
aj() { curl -s -b "$J" -H "X-Requested-With: XMLHttpRequest" --data-urlencode "__RequestVerificationToken=$T" "${@:2}" "$B$1"; }

R=$(aj /Users/ToggleActive -d "id=$U2") ; echo "$R" | grep -q '"ok":true' && echo "PASS ToggleActive ajax json" || echo "FAIL ToggleActive → $R"
R=$(aj /Users/ToggleActive -d "id=$U2") ; echo "$R" | grep -q '"ok":true' && echo "PASS ToggleActive revert" || echo "FAIL ToggleActive revert → $R"
R=$(aj /Users/RevokeSessions -d "id=$U2") ; echo "$R" | grep -q '"ok":true' && echo "PASS RevokeSessions ajax json" || echo "FAIL RevokeSessions → $R"
R=$(aj /Users/Unlock -d "id=$U2") ; echo "$R" | grep -q '"ok":true' && echo "PASS Unlock ajax json" || echo "FAIL Unlock → $R"
R=$(aj /Users/SetRoles -d "id=$U2" -d "roleIds=$VIEWER_ID") ; echo "$R" | grep -q '"ok":true' && echo "PASS SetRoles ajax json" || echo "FAIL SetRoles → $R"
KEYS=$($DB --json "SELECT p.[Key] AS k FROM RolePermissions rp JOIN Permissions p ON p.Id=rp.PermissionId JOIN Roles r ON r.Id=rp.RoleId WHERE r.Name='viewer' ORDER BY p.[Key]" 2>/dev/null | python3 -c "import sys,json;print(','.join(r['k'] for r in json.load(sys.stdin)))" 2>/dev/null)
# گارد: هرگز permKeys خالی نفرست — دسترسی‌های نقش پاک می‌شود
if [ -n "$KEYS" ]; then
  R=$(aj /Users/SetRolePermissions -d "roleId=$VIEWER_ID" --data-urlencode "permKeys=$KEYS") ; echo "$R" | grep -q '"ok":true' && echo "PASS SetRolePermissions ajax json" || echo "FAIL SetRolePermissions → $R"
  N=$($DB --json "SELECT COUNT(*) AS n FROM RolePermissions rp JOIN Roles r ON r.Id=rp.RoleId WHERE r.Name='viewer'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])" 2>/dev/null)
  VN=$(echo "$KEYS" | awk -F, '{print NF}')
  [ "$N" = "$VN" ] && echo "PASS viewer perms intact ($N)" || echo "FAIL viewer perms wiped ($N != $VN)"
else
  echo "SKIP SetRolePermissions (keys extraction failed)"
fi
R=$(aj /Users/ResetPassword -d "id=$U2" --data-urlencode "newPassword=Test@5678") ; echo "$R" | grep -q '"ok":true' && echo "PASS ResetPassword ajax json" || echo "FAIL ResetPassword → $R"
code=$(curl -s -H "Content-Type: application/json" -d '{"username":"e2e_tmp1","password":"Test@5678"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] && echo "PASS login with reset password" || echo "FAIL reset login ($code)"

# --- تاریخ انقضای رمز + تاریخچه رمز ---
PE=$($DB "SELECT COUNT(*) FROM Users WHERE Username='e2e_tmp1' AND PasswordExpiresAt IS NOT NULL" 2>/dev/null | tr -d '[]"')
[ "$PE" = "1" ] && echo "PASS password expiry set (90d)" || echo "FAIL password expiry ($PE)"
PH=$($DB "SELECT COUNT(*) FROM PasswordHistories ph JOIN Users u ON u.Id=ph.UserId WHERE u.Username='e2e_tmp1'" 2>/dev/null | tr -d '[]"')
[ "$PH" -ge "1" ] && echo "PASS initial password history row" || echo "FAIL password history ($PH)"

# --- ردیف حسابرسی ---
AU=$($DB "SELECT COUNT(*) FROM AuditLogs WHERE Action='create' AND Entity='user' AND EntityId='e2e_tmp1'" 2>/dev/null | tr -d '[]"')
[ "$AU" -ge "1" ] && echo "PASS audit row created" || echo "FAIL audit row ($AU)"

# --- دسترسی منفی: hr فقط users.view دارد ---
code=$(curl -s -c "$HRJ" -H "Content-Type: application/json" -d '{"username":"hr","password":"Hr@123456"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { echo "FAIL login hr ($code)"; exit 1; }
HRPAGE=$(curl -s -b "$HRJ" "$B/Users")
echo "$HRPAGE" | grep -q 'href="/Users/Create"' && echo "FAIL hr sees create button (should not)" || echo "PASS hr has no create button"
code=$(curl -s -b "$HRJ" -o /dev/null -w "%{http_code}" "$B/Users/Create")
[ "$code" = "302" ] && echo "PASS hr GET /Users/Create → Denied redirect ($code)" || echo "FAIL hr GET create ($code)"
T2=$(curl -s -b "$HRJ" -c "$HRJ" "$B/Users" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
code=$(curl -s -b "$HRJ" -o /dev/null -w "%{http_code}" \
  --data-urlencode "__RequestVerificationToken=$T2" \
  --data-urlencode "username=e2e_hack" --data-urlencode "fullName=هک" \
  --data-urlencode "password=Test@1234" --data-urlencode "password2=Test@1234" "$B/Users/Create")
[ "$code" = "302" ] && echo "PASS hr POST /Users/Create blocked ($code)" || echo "FAIL hr POST create ($code)"

# --- پاکسازی پایانی ---
U=$($DB "SELECT Id FROM Users WHERE Username='e2e_tmp1'" 2>/dev/null | tr -d '[]"' )
[ -n "$U" ] && { $DB --exec "DELETE FROM UserRoles WHERE UserId=$U" >/dev/null 2>&1
  $DB --exec "DELETE FROM PasswordHistories WHERE UserId=$U" >/dev/null 2>&1
  $DB --exec "DELETE FROM RefreshTokens WHERE UserId=$U" >/dev/null 2>&1
  $DB --exec "DELETE FROM Users WHERE Id=$U" >/dev/null 2>&1
  echo "PASS final cleanup"; } || echo "WARN final cleanup skipped"
echo "DONE"
