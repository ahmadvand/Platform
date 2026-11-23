#!/bin/bash
# فاز ۷۴ — راستی‌آزمایی RBAC پیشرفته: کلیدهای ریز، CRUD نقش، دامنهٔ رکوردی، گزارش‌های محدودشده
export TZ=Asia/Tehran
B=http://localhost:3000
J=/tmp/r74
DB=/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db
rm -f $J-*.txt
PASS=0; FAIL=0
ok(){ if [ "$1" = "1" ]; then PASS=$((PASS+1)); echo "  ✔ $2"; else FAIL=$((FAIL+1)); echo "  ✘ $2 (got: $3)"; fi; }
q(){ python3 -c "
import sqlite3;c=sqlite3.connect('$DB')
print(c.execute('$1').fetchone()[0])" 2>/dev/null; }

login(){ # $1=user $2=pass $3=jar $4=check-page
  rm -f $3
  curl -s -c $3 -o /dev/null "$B/Auth/Login"
  T=$(curl -s -b $3 -c $3 "$B/Auth/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
  curl -s -b $3 -c $3 -o /dev/null -X POST "$B/Auth/Login" \
    -d "identifier=$1&secret=$2&__RequestVerificationToken=$T"
  [ "$(curl -s -o /dev/null -w '%{http_code}' -b $3 "$B$4")" = "200" ]
}

echo "== ۱) ورود مدیر =="
login admin Admin@1234 $J-adm.txt /Users; ok $? "login admin" "-"

echo "== ۲) صفحات کلیدی ۲۰۰ =="
for p in "/Users" "/Payroll/Items" "/Self" "/Attendance" "/Employees" "/Reports" "/Reports/Custom" "/Payroll/Bonus" "/Payroll/Params"; do
  c=$(curl -s -o /dev/null -w "%{http_code}" -b $J-adm.txt "$B$p")
  ok "$([ "$c" = "200" ] && echo 1 || echo 0)" "GET $p → 200" "$c"
done

echo "== ۳) UI نقش‌ها: دکمهٔ نقش جدید + ماتریس با کلیدهای ریز ۷۴ =="
U=$(curl -s -b $J-adm.txt "$B/Users")
echo "$U" | grep -q 'RoleForm' && ok 1 "دکمهٔ «نقش جدید» (data-modal RoleForm)" || ok 0 "دکمهٔ «نقش جدید»" missing
echo "$U" | grep -q 'roles.manage' && ok 1 "کلید roles.manage در ماتریس" || ok 0 "کلید roles.manage" missing
echo "$U" | grep -q 'payroll.items.edit' && ok 1 "کلید payroll.items.edit در ماتریس" || ok 0 "کلید payroll.items.edit" missing
echo "$U" | grep -q 'devices.simulate' && ok 1 "کلید devices.simulate در ماتریس" || ok 0 "کلید devices.simulate" missing
echo "$U" | grep -q 'RoleDelete' && ok 1 "دکمهٔ حذف نقش" || ok 0 "دکمهٔ حذف نقش" missing

echo "== ۴) ایجاد نقش سفارشی با دامنهٔ واحد سازمانی =="
T=$(curl -s -b $J-adm.txt -c $J-adm.txt "$B/Users" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
DEPT=$(python3 -c "
import sqlite3;c=sqlite3.connect('$DB')
r=c.execute('select Id,Name from Departments limit 1').fetchone();print(r[0])")
DEPT_NAME=$(python3 -c "
import sqlite3;c=sqlite3.connect('$DB')
r=c.execute('select Name from Departments limit 1').fetchone();print(r[0])")
echo "  dept=$DEPT ($DEPT_NAME)"
# پاک‌سازی بقایای اجرای قبلی تست
OLD=$(q "select Id from Roles where Name='dept_scope_r74'")
[ -n "$OLD" ] && curl -s -b $J-adm.txt -X POST "$B/Users/RoleDelete" -d "id=$OLD&__RequestVerificationToken=$T" -H "X-Requested-With: XMLHttpRequest" > /dev/null
R=$(curl -s -b $J-adm.txt -X POST "$B/Users/RoleCreate" \
  -d "name=dept_scope_r74&title=تست دامنهٔ واحد ۷۴&description=تست فاز ۷۴&scopeKind=dept&scopeRefs=$DEPT&__RequestVerificationToken=$T" \
  -H "X-Requested-With: XMLHttpRequest")
echo "$R" | grep -q '"ok":true' && ok 1 "RoleCreate (دامنهٔ واحد)" "$R" || ok 0 "RoleCreate" "$R"
RID=$(q "select Id from Roles where Name='dept_scope_r74'")
SROW=$(python3 -c "
import sqlite3;c=sqlite3.connect('$DB')
r=c.execute('select Kind,RefId from RoleScopes').fetchall();print(r if r else 'NONE')")
[ "$SROW" != "NONE" ] && ok 1 "سطر RoleScope ساخته شد ($SROW)" || ok 0 "سطر RoleScope" "$SROW"

echo "== ۵) دسترسی‌های نقش آزمایشی + کاربر محدودشده + ورود =="
R=$(curl -s -b $J-adm.txt -X POST "$B/Users/SetRolePermissions" \
  -d "roleId=$RID&permKeys=dashboard.view,employees.view,attendance.view,reports.view,payroll.view&__RequestVerificationToken=$T" \
  -H "X-Requested-With: XMLHttpRequest")
echo "$R" | grep -q '"ok":true' && ok 1 "SetRolePermissions روی نقش جدید (اعمال فوری)" "$R" || ok 0 "SetRolePermissions" "$R"
R=$(curl -s -b $J-adm.txt -X POST "$B/Users/Create" \
  -d "username=scope74&fullName=کاربر تست دامنه&password=Scope@1234&password2=Scope@1234&roleIds=$RID&__RequestVerificationToken=$T" \
  -H "X-Requested-With: XMLHttpRequest")
echo "$R" | grep -q '"ok":true' && ok 1 "ایجاد کاربر scope74" "$R" || ok 0 "ایجاد کاربر scope74" "$R"
login scope74 Scope@1234 $J-sc.txt /Employees; ok $? "login scope74" "-"

echo "== ۶) دامنهٔ رکوردی روی لیست‌ها (پرسنل فقط همان واحد) =="
ALL_N=$(q "select count(*) from Employees")
DEPT_N=$(q "select count(*) from Employees where DepartmentId='$DEPT'")
EMP_HTML=$(curl -s -b $J-sc.txt "$B/Employees")
echo "  total=$ALL_N dept=$DEPT_N"
OTHER=$(python3 -c "
import sqlite3;c=sqlite3.connect('$DB')
r=c.execute('select Name from Departments where Id<>?',('$DEPT',)).fetchall()
print('|'.join(x[0] for x in r if x[0]))")
FOUND_OTHER=0
IFS='|' read -ra NAMES <<< "$OTHER"
for n in "${NAMES[@]}"; do echo "$EMP_HTML" | grep -qF "$n" && FOUND_OTHER=1 && break; done
[ "$DEPT_N" -lt "$ALL_N" ] && [ "$FOUND_OTHER" = "0" ] && ok 1 "لیست پرسنل فقط واحد مجاز ($DEPT_N از $ALL_N)" "other-dept-visible" || ok 0 "لیست پرسنل دامنه‌شده" "total=$ALL_N dept=$DEPT_N otherVisible=$FOUND_OTHER"

c=$(curl -s -o /dev/null -w "%{http_code}" -b $J-sc.txt "$B/Attendance")
ok "$([ "$c" = "200" ] && echo 1 || echo 0)" "صفحهٔ حضور و غیاب برای کاربر محدود (۲۰۰)" "$c"
REP=$(curl -s -b $J-sc.txt "$B/Reports/Custom")
ok "$([ -n "$REP" ] && echo 1 || echo 0)" "گزارش سفارشی برای کاربر محدود (۲۰۰)" "-"
echo "$REP" | grep -qF "$DEPT_NAME" && ok 1 "گزارش فقط واحد مجاز را نشان می‌دهد" "-" || ok 0 "گزارش واحد مجاز" "dept-name-not-in-page"
OTH_IN_REP=0
for n in "${NAMES[@]}"; do echo "$REP" | grep -qF "$n" && OTH_IN_REP=1 && break; done
[ "$OTH_IN_REP" = "0" ] && ok 1 "گزارش واحد بیرون دامنه را نشان نمی‌دهد" "-" || ok 0 "گزارش بدون واحد بیرون دامنه" "other-dept-in-report"

echo "== ۷) گاردهای نوشتن بیرون دامنه =="
c=$(curl -s -o /dev/null -w "%{http_code}" -b $J-sc.txt "$B/Payroll/ItemCreate?companyId=x-unknown-company")
ok "$([ "$c" = "200" ] || [ "$c" = "302" ] && echo 1 || echo 0)" "فرمول‌ساز اقلام با شرکت ناموجود (بدون ۵۰۰)" "$c"

echo "== ۸) نقش بدون دامنه = همهٔ داده‌ها (رفتار سابق) =="
login viewer View@1234 $J-vw.txt /Employees
VEMP=$(curl -s -b $J-vw.txt "$B/Employees")
ok $([ -n "$VEMP" ] && echo 1 || echo 0) "viewer (بدون دامنه) صفحهٔ پرسنل ۲۰۰" "-"
VFOUND=0
for n in "${NAMES[@]}"; do echo "$VEMP" | grep -qF "$n" && VFOUND=1 && break; done
[ "$VFOUND" = "1" ] && ok 1 "viewer همهٔ واحدها را می‌بیند (بدون محدودیت)" "-" || ok 0 "viewer همهٔ واحدها" "restricted?!"

echo "== ۹) گاردهای نقش‌ها =="
SUPER=$(q "select Id from Roles where Name='superadmin'")
R=$(curl -s -b $J-adm.txt -X POST "$B/Users/RoleDelete" -d "id=$SUPER&__RequestVerificationToken=$T" -H "X-Requested-With: XMLHttpRequest")
echo "$R" | grep -q '"ok":false' && ok 1 "حذف نقش داخلی رد شد" "$R" || ok 0 "حذف نقش داخلی باید رد شود" "$R"
R=$(curl -s -b $J-adm.txt -X POST "$B/Users/RoleCreate" \
  -d "name=bad name!&title=نقش بد&scopeKind=all&__RequestVerificationToken=$T" \
  -H "X-Requested-With: XMLHttpRequest")
echo "$R" | grep -q '"ok":false' && ok 1 "نام نامعتبر نقش رد شد" "$R" || ok 0 "اعتبارسنجی نام نقش" "$R"

echo ""
echo "==== نتیجه: $PASS موفق / $FAIL ناموفق ===="
exit $FAIL
