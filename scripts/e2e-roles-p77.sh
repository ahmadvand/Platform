#!/bin/bash
# ============================================================
# E2E فاز ۷۷ — سلسله‌مراتب نقش‌ها + جدول صفحه‌بندی‌شده + مودال درخت دسترسی‌ها
# ============================================================
B=http://localhost:3000
J=/tmp/r77.jar
rm -f "$J"
code=$(curl -s -c "$J" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}")
[ "$code" = "200" ] || { echo "FAIL login ($code)"; exit 1; }
echo "PASS login"

tok() { curl -s -b "$J" -c "$J" "$B$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
q() { python3.13 /home/z/my-project/scripts/dbq.py --json "$1"; }

T=$(tok /Users/Roles)
# پیش‌پاکسازی اجراهای قبلی
for old in $(q "SELECT Id FROM Roles WHERE Name LIKE 'p77%'" | python3 -c "import sys,json;[print(x['Id']) for x in json.load(sys.stdin)]"); do
  curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
    --data-urlencode "__RequestVerificationToken=$T" --data "id=$old" "$B/Users/RoleDelete" > /dev/null
done
[ -n "$T" ] || { echo "FAIL token"; exit 1; }
echo "PASS antiforgery"

# ---- ۱) صفحهٔ نقش‌ها رندر شد؟
n=$(curl -s -b "$J" "$B/Users/Roles" | grep -c "roles-tbl")
[ "$n" -ge 1 ] && echo "PASS roles page renders" || { echo "FAIL roles page"; exit 1; }
n=$(curl -s -b "$J" "$B/Users/Roles?q=$RANDOM" | grep -c "نقشی یافت نشد")
[ "$n" -ge 1 ] && echo "PASS roles search empty-state" || echo "WARN search empty-state"

# ---- ۲) ایجاد نقش والدِ آزمون (بدون والد)
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data "name=p77_parent" --data "title=نقش والد آزمون" --data "scopeKind=all" \
  "$B/Users/RoleCreate")
echo "$r" | grep -q '"ok":true' && echo "PASS create parent role" || { echo "FAIL create parent: $r"; exit 1; }
PARENT_ID=$(q "SELECT Id FROM Roles WHERE Name='p77_parent'" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
echo "  parent id = $PARENT_ID"

# ---- ۳) چند دسترسی به نقش والد بده (کلیدهای واقعی کاتالوگ)
curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data "roleId=$PARENT_ID" --data "permKeys=employees.view,employees.manage,reports.view" \
  "$B/Users/SetRolePermissions" | grep -q '"ok":true' && echo "PASS set parent perms" || { echo "FAIL set parent perms"; exit 1; }

# ---- ۴) ایجاد نقش فرزند با والد
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data "name=p77_child" --data "title=نقش فرزند آزمون" --data "scopeKind=all" --data "parentRoleId=$PARENT_ID" \
  "$B/Users/RoleCreate")
echo "$r" | grep -q '"ok":true' && echo "PASS create child role" || { echo "FAIL create child: $r"; exit 1; }
CHILD_ID=$(q "SELECT Id FROM Roles WHERE Name='p77_child'" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
DB_PARENT=$(q "SELECT ParentRoleId FROM Roles WHERE Id=$CHILD_ID" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['ParentRoleId'])")
[ "$DB_PARENT" = "$PARENT_ID" ] && echo "PASS child.ParentRoleId persisted" || { echo "FAIL parent not persisted ($DB_PARENT)"; exit 1; }

# ---- ۵) مودال درخت دسترسی‌های فرزند: ارثی‌ها باید دیده شوند
h=$(curl -s -b "$J" "$B/Users/RolePerms?roleId=$CHILD_ID")
echo "$h" | grep -q "perm-tree" && echo "PASS perms tree modal renders" || { echo "FAIL tree modal"; exit 1; }
inh=$(echo "$h" | grep -o 'permKeysHidden' >/dev/null; echo "$h" | python3 -c "
import sys,re
h=sys.stdin.read()
m=re.search(r'id=\"permKeysHidden\" value=\"([^\"]*)\"', h)
print('hidden-ok' if m else 'no-hidden')
m2=re.search(r'\[\"employees.view\"', h) or re.search(r'\[\"employees\.view\",', h)
print('inherited-in-json' if ('employees.view' in h and 'ارثی' in h) else 'no-inherited-marker')
")
echo "  tree: $inh"

# ---- ۶) کاربر آزمون + اتصال نقش فرزند + بررسی دسترسی مؤثر (ارث‌بری در RbacService)
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data "username=p77user" --data "fullName=کاربر آزمون ۷۷" --data "password=Pass77!x" --data "password2=Pass77!x" --data "isActive=true" \
  "$B/Users/Create")
echo "$r" | grep -q '"ok":true' && echo "PASS create test user" || echo "WARN create user: $r"
TUID=$(q "SELECT Id FROM Users WHERE Username='p77user'" | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" --data "id=$TUID" --data "roleIds=$CHILD_ID" \
  "$B/Users/SetRoles")
echo "$r" | grep -q '"ok":true' && echo "PASS assign child role to user" || echo "WARN set roles: $r"
# RbacService کش نسخه‌دار دارد — یک بار Bump هم شده؛ صفحهٔ دسترسی مؤثر را بگیر
eff=$(curl -s -b "$J" "$B/Users/Perms/$TUID")
echo "$eff" | grep -q "employees.view" && echo "PASS inherited perm visible in effective view" || { echo "FAIL inheritance not visible"; echo "$eff" | grep -o "employees.[a-z]*" | head -3; exit 1; }
echo "$eff" | grep -q "ارثی\|مستقیم" && echo "PASS effective view renders origin badges" || echo "WARN origin badges"

# ---- ۷) ضدچرخه: والدِ نقش والد = نقش فرزند → باید رد شود
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" \
  --data "id=$PARENT_ID" --data "title=نقش والد آزمون" --data "scopeKind=all" --data "parentRoleId=$CHILD_ID" \
  "$B/Users/RoleEdit")
echo "$r" | grep -q '"ok":false' && echo "PASS cycle rejected" || { echo "FAIL cycle accepted: $r"; exit 1; }

# ---- ۸) حذف والد دارای فرزند → باید رد شود
r=$(curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" --data "id=$PARENT_ID" \
  "$B/Users/RoleDelete")
echo "$r" | grep -q '"ok":false' && echo "PASS parent-with-children delete blocked" || { echo "FAIL parent deleted despite children"; exit 1; }

# ---- ۹) صفحه‌بندی: page=1 همیشه 200
code=$(curl -s -b "$J" -o /dev/null -w "%{http_code}" "$B/Users/Roles?page=1")
[ "$code" = "200" ] && echo "PASS paging param" || { echo "FAIL paging ($code)"; exit 1; }

# ---- ۱۰) پاکسازی
curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" --data "id=$TUID" "$B/Users/Delete/$TUID" > /dev/null
curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" --data "id=$CHILD_ID" "$B/Users/RoleDelete" | grep -q '"ok":true' && echo "PASS cleanup child" || echo "WARN cleanup child"
curl -s -b "$J" -c "$J" -H "X-Requested-With: XMLHttpRequest" -H "RequestVerificationToken: $T" \
  --data-urlencode "__RequestVerificationToken=$T" --data "id=$PARENT_ID" "$B/Users/RoleDelete" | grep -q '"ok":true' && echo "PASS cleanup parent" || echo "WARN cleanup parent"

echo "ALL DONE"
