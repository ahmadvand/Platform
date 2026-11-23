#!/bin/bash
# ============================================================
# E2E v4: جابجایی واحد با انتخاب «همراهی پرسنل» — داوری با وضعیت DB (نه toast)
# ============================================================
B=http://localhost:3000
J=/tmp/org-e2e.jar
DBQ="python3 /home/z/my-project/scripts/dbq.py"
rm -f "$J"

code=$(curl -s -c "$J" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { echo "LOGIN FAILED ($code)"; exit 1; }
echo "PASS login"

token() { curl -s -b "$J" -c "$J" "$B/Org" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
T=$(token); [ -n "$T" ] || { echo "NO TOKEN"; exit 1; }

post() { curl -s -b "$J" -c "$J" -L -H "RequestVerificationToken: $T" --data-urlencode "__RequestVerificationToken=$T" "${@:2}" "$B$1" > /dev/null; }

# دو ستون همیشه → خروجی JSON قابل پارس (dbq تک‌ستونه را خام چاپ می‌کند)
dept_exists() { $DBQ "SELECT Id, Code FROM Departments WHERE Code='$1'" | python3 -c "import sys,json; print(json.load(sys.stdin)[0][0] if json.load(open('/dev/null')) else '')" 2>/dev/null || true; }

dept_id() { $DBQ "SELECT Id, Code FROM Departments WHERE Code='$1'" | python3 -c "
import sys, json
rows = json.load(sys.stdin)
print(rows[0][0] if rows else '')
"; }
emp_dept() { $DBQ "SELECT DepartmentId, Id FROM Employees WHERE Id='$1'" | python3 -c "
import sys, json
rows = json.load(sys.stdin)
v = rows[0][0] if rows else None
print('NULL' if v is None else v)
"; }

# --- پاکسازی اجرای قبلی ---
for CODE in TESTQ9 TESTC9; do
  OLD=$(dept_id "$CODE")
  [ -n "$OLD" ] && post /Org/DeleteDept -d "id=$OLD"
done

# --- ۱) ویو: صفت‌های دیالوگ ---
HTML=$(curl -s -b "$J" "$B/Org")
echo "$HTML" | grep -q 'data-choices="move"' && echo "PASS data-choices on move forms" || echo "FAIL data-choices"
echo "$HTML" | grep -q 'name="leaveEmployees"' && echo "PASS leaveEmployees hidden input" || echo "FAIL leaveEmployees"
echo "$HTML" | grep -q 'data-count=' && echo "PASS data-count attr" || echo "FAIL data-count"
echo "$HTML" | grep -q 'data-confirm=' && echo "PASS data-confirm (delete forms)" || echo "FAIL data-confirm"

# --- ۲) ساخت واحدهای آزمایشی X، Y ---
post /Org/CreateDept -d "name=تست-جابجایی-X&code=TESTQ9&parentId="
post /Org/CreateDept -d "name=تست-جابجایی-Y&code=TESTC9&parentId="
X=$(dept_id 'TESTQ9'); Y=$(dept_id 'TESTC9')
[ -n "$X" ] && [ -n "$Y" ] && echo "PASS create X,Y" || { echo "FAIL create X,Y ($X/$Y)"; exit 1; }

# --- ۳) انتقال یک پرسنل واقعی به X (ذخیره وضعیت اصلی) ---
EMP_ROW=$($DBQ "SELECT Id, DepartmentId FROM Employees WHERE DepartmentId IS NOT NULL AND DepartmentId != '$X' AND DepartmentId != '$Y' LIMIT 1")
EMP=$(echo "$EMP_ROW" | python3 -c "import sys,json; print(json.load(sys.stdin)[0][0])")
ORIG=$(echo "$EMP_ROW" | python3 -c "import sys,json; print(json.load(sys.stdin)[0][1])")
$DBQ "UPDATE Employees SET DepartmentId='$X' WHERE Id='$EMP'" --exec > /dev/null
[ "$(emp_dept $EMP)" = "$X" ] && echo "PASS employee staged into X" || { echo "FAIL staging"; exit 1; }

# --- ۴) X زیر Y با leaveEmployees=true → پرسنل در والد قبلی (ریشه → بدون واحد) ---
post /Org/MoveDept -d "id=$X&parentId=$Y&leaveEmployees=true"
[ "$(emp_dept $EMP)" = "NULL" ] && echo "PASS staff stayed at old parent (NULL)" || echo "FAIL staff moved anyway: $(emp_dept $EMP)"
X_PARENT=$($DBQ "SELECT ParentId, Id FROM Departments WHERE Id='$X'" | python3 -c "import sys,json; print(json.load(sys.stdin)[0][0])")
[ "$X_PARENT" = "$Y" ] && echo "PASS X re-parented under Y" || echo "FAIL X parent=$X_PARENT"

# --- ۵) برگشت پرسنل به X؛ X به ریشه با leaveEmployees=false → پرسنل همراه واحد ---
$DBQ "UPDATE Employees SET DepartmentId='$X' WHERE Id='$EMP'" --exec > /dev/null
post /Org/MoveDept -d "id=$X&parentId=&leaveEmployees=false"
[ "$(emp_dept $EMP)" = "$X" ] && echo "PASS staff followed unit" || echo "FAIL staff left behind: $(emp_dept $EMP)"

# --- ۶) گارد حذف: X دارای پرسنل ---
R=$(curl -s -b "$J" -c "$J" -L -H "RequestVerificationToken: $T" --data-urlencode "__RequestVerificationToken=$T" -d "id=$X" "$B/Org/DeleteDept")
echo "$R" | grep -q "حذف ممکن نیست" && echo "PASS delete guard (has staff)" || echo "FAIL delete guard"

# --- ۷) پاکسازی + بازگردانی ---
$DBQ "UPDATE Employees SET DepartmentId='$ORIG' WHERE Id='$EMP'" --exec > /dev/null
post /Org/DeleteDept -d "id=$X"
post /Org/DeleteDept -d "id=$Y"
[ -z "$(dept_id TESTQ9)" ] && [ -z "$(dept_id TESTC9)" ] && echo "PASS cleanup X,Y" || echo "FAIL leftovers in DB"
[ "$(emp_dept $EMP)" = "$ORIG" ] && echo "PASS employee restored" || echo "FAIL employee restore"

echo "=== done ==="
