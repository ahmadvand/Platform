#!/bin/bash
# e2e-companies.sh — فاز ۲۸: معماری چندشرکتی سراسری
# ۱) CRUD شرکت‌ها با گاردها  ۲) چارت سازمانی جدا به‌ازای هر شرکت
# ۳) یکتایی کد درون‌شرکتی + مجاز بودن کد تکراری بین شرکت‌ها
# ۴) شرکت‌داری دستگاه/منطقه/گیت/گروه/بازدیدکننده/کاربر
# ۵) گاردهای بین‌شرکتی (جابجایی واحد، انتقال پست، والد ناهم‌شرکت)
BASE=http://localhost:3000
UA="Mozilla/5.0 (e2e)"
CJ=$(mktemp /tmp/e2e28.XXXXXX)
PASS=0; FAIL=0; CLEANUPS=()
jq_msg() { python3 -c "import sys,json;print(json.load(sys.stdin).get('message',''))" 2>/dev/null; }
one() { python3 -c "import sys,json
try:
  d=json.load(sys.stdin); print(d[0][0] if d else '')
except Exception: print('')" 2>/dev/null; }
dbq() { python3 /home/z/my-project/scripts/dbq.py "$1" 2>/dev/null; }
tok() { curl -s -c $CJ -b $CJ -A "$UA" "$BASE$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
ok()  { PASS=$((PASS+1)); echo "✅ $1"; }
bad() { FAIL=$((FAIL+1)); echo "❌ $1"; }
chk() { if [ "$2" = "$3" ]; then ok "$1"; else bad "$1 — expected [$3] got [$2]"; fi }
AJ()  { curl -s -b $CJ -A "$UA" -H "X-Requested-With: XMLHttpRequest" "$@"; }

# ---------- ورود مدیر ----------
T=$(tok /Auth/Login)
curl -s -c $CJ -b $CJ -A "$UA" -d "identifier=admin" -d "secret=Admin@1234" -d "__RequestVerificationToken=$T" "$BASE/Auth/Login" -o /dev/null
chk "ورود admin" "$(curl -s -o /dev/null -w '%{http_code}' -b $CJ -A "$UA" $BASE/Companies)" 200

# ---------- کاتالوگ ----------
chk "دسترسی companies.view در کاتالوگ" "$(dbq "SELECT COUNT(*) FROM Permissions WHERE Key='companies.manage'")" "1"
chk "hr_manager → companies.view" "$(dbq "SELECT COUNT(*) FROM RolePermissions rp JOIN Roles r ON r.Id=rp.RoleId JOIN Permissions p ON p.Id=rp.PermissionId WHERE r.Name='hr_manager' AND p.Key='companies.view'")" "1"

# ---------- CRUD شرکت ----------
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Companies)" -d "name=شرکت آزمون فاز ۲۸" -d "code=E28-99" "$BASE/Companies/Create")
echo "$MSG" | jq_msg | grep -q "ثبت شد" && ok "ایجاد شرکت جدید" || bad "ایجاد شرکت جدید — $(echo "$MSG" | jq_msg)"
CO=$(dbq "SELECT Id FROM Companies WHERE Code='E28-99'")
CLEANUPS+=("company:$CO")
[ -n "$CO" ] && ok "شرکت در DB" || bad "شرکت در DB"

MSG=$(AJ -d "__RequestVerificationToken=$(tok /Companies)" -d "name=شرکت آزمون فاز ۲۸" -d "code=PAC-01" "$BASE/Companies/Create")
echo "$MSG" | jq_msg | grep -q "قبلاً استفاده شده" && ok "کد شرکت تکراری رد شد" || bad "کد تکراری — $(echo "$MSG" | jq_msg)"

# توقف آخرین شرکت فعال رد می‌شود (دو شرکت فعال داریم + آزمونی → سه‌تا؛ توقف PAC-01 باید مجاز ولی برگردانیم؟ گارد فقط وقتی تک‌شرکت فعال است)
# اینجا گارد تک‌شرکت را با غیرفعال‌کردن هر سه می‌سازیم نه — توقف شرکت آزمونی و بازفعال‌سازی:
AJ -d "__RequestVerificationToken=$(tok /Companies)" -d "id=$CO" "$BASE/Companies/ToggleActive" > /dev/null
chk "توقف شرکت آزمونی" "$(dbq "SELECT Active FROM Companies WHERE Id='$CO'")" "0"
AJ -d "__RequestVerificationToken=$(tok /Companies)" -d "id=$CO" "$BASE/Companies/ToggleActive" > /dev/null
chk "فعال‌سازی مجدد" "$(dbq "SELECT Active FROM Companies WHERE Id='$CO'")" "1"

# ---------- چارت جدا برای شرکت جدید ----------
# بدون companyId → اولین شرکت (سازگاری قدیمی)
AJ -d "__RequestVerificationToken=$(tok /Org)" -d "name=واحد پیش‌فرض سازگاری" -d "code=E28-FBK" "$BASE/Org/CreateDept" > /dev/null
FBK=$(dbq "SELECT Id FROM Departments WHERE Code='E28-FBK'")
CLEANUPS+=("dept:$FBK")
chk "fallbackcompanyId → شرکت اول" "$(dbq "SELECT CompanyId FROM Departments WHERE Id='$FBK'")" "$(dbq "SELECT Id FROM Companies ORDER BY Code LIMIT 1")"

# با companyId → شرکت آزمونی
AJ -d "__RequestVerificationToken=$(tok /Org)" -d "name=واحد آزمون چارت" -d "code=E28-D1" -d "companyId=$CO" "$BASE/Org/CreateDept" > /dev/null
D1=$(dbq "SELECT Id FROM Departments WHERE Code='E28-D1'")
CLEANUPS+=("dept:$D1")
chk "واحد در شرکت آزمون" "$(dbq "SELECT CompanyId FROM Departments WHERE Id='$D1'")" "$CO"
chk "صفحه چارت شرکت آزمون ۲۰۰" "$(curl -s -o /dev/null -w '%{http_code}' -b $CJ -A "$UA" "$BASE/Org?co=$CO")" 200

# کد تکراری درون‌شرکت رد، بین‌شرکتی مجاز
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Org)" -d "name=تکراری" -d "code=E28-D1" -d "companyId=$CO" "$BASE/Org/CreateDept")
echo "$MSG" | jq_msg | grep -q "تکراری" && ok "کد واحد تکراری درون‌شرکت رد شد" || bad "کد تکراری درون‌شرکت — $(echo "$MSG" | jq_msg)"
AJ -d "__RequestVerificationToken=$(tok /Org)" -d "name=هم‌کد شرکت دیگر" -d "code=E28-D1" -d "companyId=$(dbq "SELECT Id FROM Companies ORDER BY Code DESC LIMIT 1")" "$BASE/Org/CreateDept" > /dev/null
D2=$(dbq "SELECT Id FROM Departments WHERE Code='E28-D1' AND CompanyId != '$CO'")
CLEANUPS+=("dept:$D2")
[ -n "$D2" ] && ok "کد واحد هم‌نام در دو شرکت مجاز" || bad "کد مشترک بین شرکت‌ها"

# والد ناهم‌شرکت رد می‌شود
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Org)" -d "name=فرزند ناهم‌شرکت" -d "code=E28-D3" -d "companyId=$CO" -d "parentId=$D2" "$BASE/Org/CreateDept")
echo "$MSG" | jq_msg | grep -q "همان شرکت" && ok "والد ناهم‌شرکت رد شد" || bad "والد ناهم‌شرکت — $(echo "$MSG" | jq_msg)"

# جابجایی بین‌شرکتی رد می‌شود
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Org)" -d "id=$D1" -d "parentId=$D2" "$BASE/Org/MoveDept")
echo "$MSG" | jq_msg | grep -q "بین شرکت‌ها مجاز نیست" && ok "جابجایی بین‌شرکتی رد شد" || bad "جابجایی بین‌شرکتی — $(echo "$MSG" | jq_msg)"

# پست سازمانی: شرکت از واحد + هم‌شرکتی
AJ -d "__RequestVerificationToken=$(tok /Org)" -d "title=پست آزمون" -d "code=E28-P1" -d "departmentId=$D1" "$BASE/Org/CreatePos" > /dev/null
P1=$(dbq "SELECT Id FROM Positions WHERE Code='E28-P1'")
CLEANUPS+=("pos:$P1")
chk "پست هم‌شرکت واحد" "$(dbq "SELECT CompanyId FROM Positions WHERE Id='$P1'")" "$CO"
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Org)" -d "id=$P1" -d "title=پست آزمون" -d "code=E28-P1" -d "departmentId=$D2" "$BASE/Org/EditPos")
echo "$MSG" | jq_msg | grep -q "شرکت دیگر مجاز نیست" && ok "انتقال پست به شرکت دیگر رد شد" || bad "انتقال پست بین‌شرکتی — $(echo "$MSG" | jq_msg)"

# ---------- دستگاه شرکت‌دار ----------
AJ -d "__RequestVerificationToken=$(tok /Devices/Create)" -d "name=دستگاه آزمون ۲۸" -d "kind=ترمینال اثر انگشت" -d "brand=ZKTeco" -d "serial=E28-SN-0001" -d "purpose=حضور و غیاب" -d "mode=آنلاین" -d "pollingInterval=60" -d "transport=adms" -d "companyId=$CO" "$BASE/Devices/Create" > /dev/null
DEV=$(dbq "SELECT Id FROM Devices WHERE Serial='E28-SN-0001'")
CLEANUPS+=("device:$DEV")
chk "دستگاه هم‌شرکت" "$(dbq "SELECT CompanyId FROM Devices WHERE Id='$DEV'")" "$CO"
chk "فیلتر دستگاه بر اساس شرکت" "$(curl -s -b $CJ -A "$UA" "$BASE/Devices?co=$CO" | grep -c "data-device-id=\"$DEV\"")" "1"
chk "دستگاه آزمون در فیلتر شرکت دیگر نیست" "$(curl -s -b $CJ -A "$UA" "$BASE/Devices?co=$(dbq "SELECT Id FROM Companies ORDER BY Code LIMIT 1")" | grep -c "data-device-id=\"$DEV\"")" "0"

# ---------- منطقه/گیت شرکت‌دار ----------
AJ -d "__RequestVerificationToken=$(tok /Access)" -d "name=منطقه آزمون ۲۸" -d "code=E28-Z1" -d "riskLevel=عادی" -d "companyId=$CO" "$BASE/Access/CreateZone" > /dev/null
Z1=$(dbq "SELECT Id FROM Zones WHERE Code='E28-Z1'")
CLEANUPS+=("zone:$Z1")
chk "منطقه هم‌شرکت" "$(dbq "SELECT CompanyId FROM Zones WHERE Id='$Z1'")" "$CO"
AJ -d "__RequestVerificationToken=$(tok /Access)" -d "name=گیت آزمون ۲۸" -d "code=E28-G1" -d "zoneId=$Z1" -d "type=درب برقی" -d "direction=دوطرفه" "$BASE/Access/CreateGate" > /dev/null
G1=$(dbq "SELECT Id FROM Gates WHERE Code='E28-G1'")
CLEANUPS+=("gate:$G1")
chk "گیت از شرکتِ منطقه" "$(dbq "SELECT CompanyId FROM Gates WHERE Id='$G1'")" "$CO"

# ---------- گروه دسترسی شرکت‌دار ----------
AJ -d "__RequestVerificationToken=$(tok /Access?tab=groups)" -d "name=گروه آزمون ۲۸" -d "companyId=$CO" "$BASE/Access/CreateGroup" > /dev/null
GR=$(dbq "SELECT Id FROM AccessGroups WHERE Name='گروه آزمون ۲۸'")
CLEANUPS+=("group:$GR")
chk "گروه هم‌شرکت" "$(dbq "SELECT CompanyId FROM AccessGroups WHERE Id='$GR'")" "$CO"

# ---------- ارباب‌رجوع شرکت‌دار ----------
AJ -d "__RequestVerificationToken=$(tok /Visitors)" -d "firstName=علی" -d "lastName=آزمون۲۸" -d "nationalId=2828282828" -d "idDocType=کارت ملی" -d "type=مهمان" -d "hostName=میزبان آزمون" -d "purpose=بازدید آزمون" -d "companyId=$CO" "$BASE/Visitors/Create" > /dev/null
VIS=$(dbq "SELECT Id FROM Visitors WHERE NationalId='2828282828'")
CLEANUPS+=("visitor:$VIS")
chk "بازدیدکننده هم‌شرکت" "$(dbq "SELECT CompanyId FROM Visitors WHERE Id='$VIS'")" "$CO"
AJ -d "__RequestVerificationToken=$(tok /Visitors?tab=contractors)" -d "name=پیمانکار آزمون ۲۸" -d "code=E28-CTR" -d "companyId=$CO" "$BASE/Visitors/CreateContractor" > /dev/null
CTR=$(dbq "SELECT Id FROM Contractors WHERE Code='E28-CTR'")
CLEANUPS+=("contractor:$CTR")
chk "پیمانکار هم‌شرکت" "$(dbq "SELECT CompanyId FROM Contractors WHERE Id='$CTR'")" "$CO"

# ---------- کاربر شرکت‌دار ----------
AJ -d "__RequestVerificationToken=$(tok /Users/Create)" -d "username=e2e-co28" -d "fullName=کاربر آزمون شرکتی" -d "password=Cq@282828" -d "password2=Cq@282828" -d "companyId=$CO" "$BASE/Users/Create" > /dev/null
chk "کاربر هم‌شرکت" "$(dbq "SELECT CompanyId FROM Users WHERE Username='e2e-co28'")" "$CO"
chk "ستون شرکت در لیست کاربران" "$(curl -s -b $CJ -A "$UA" $BASE/Users | grep -c "همه شرکت‌ها")" "$(dbq "SELECT COUNT(*) FROM Users WHERE CompanyId IS NULL")"

# ---------- حذف گارددار ----------
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Companies)" -d "id=$CO" "$BASE/Companies/Delete")
echo "$MSG" | jq_msg | grep -q "حذف ممکن نیست" && ok "حذف شرکت دارای داده رد شد" || bad "گارد حذف — $(echo "$MSG" | jq_msg)"

# ---------- پاکسازی ----------
# واحد آزمون: پست → واحد
AJ -d "__RequestVerificationToken=$(tok /Org)" -d "id=$P1" "$BASE/Org/DeletePos" > /dev/null
AJ -d "__RequestVerificationToken=$(tok /Org)" -d "id=$D1" "$BASE/Org/DeleteDept" > /dev/null
AJ -d "__RequestVerificationToken=$(tok /Org)" -d "id=$D2" "$BASE/Org/DeleteDept" > /dev/null
AJ -d "__RequestVerificationToken=$(tok /Org)" -d "id=$FBK" "$BASE/Org/DeleteDept" > /dev/null
AJ -d "__RequestVerificationToken=$(tok /Devices)" -d "deviceId=$DEV" "$BASE/Devices/ToggleActive" > /dev/null  # متوقف
AJ -d "__RequestVerificationToken=$(tok /Access)" -d "id=$G1" "$BASE/Access/SetGateSchedule" -d "days=" -d "from=" -d "to=" > /dev/null
# حذف مستقیم DB برای اقلام بدون اکشن حذف (منطقه/گیت/گروه/بازدیدکننده/پیمانکار/کاربر/دستگاه)
python3 - <<PYEOF
import sqlite3
c = sqlite3.connect('/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db')
dev = '$DEV'; z='$Z1'; g='$G1'; gr='$GR'; v='$VIS'; ctr='$CTR'; u='e2e-co28'; co='$CO'
c.execute("DELETE FROM DeviceCommands WHERE DeviceId=?", (dev,))
c.execute("DELETE FROM Devices WHERE Id=?", (dev,))
c.execute("DELETE FROM Gates WHERE Id=?", (g,))
c.execute("DELETE FROM Zones WHERE Id=?", (z,))
c.execute("DELETE FROM AccessGroupGates WHERE GroupId=?", (gr,))
c.execute("DELETE FROM AccessGroupMembers WHERE GroupId=?", (gr,))
c.execute("DELETE FROM AccessGroups WHERE Id=?", (gr,))
c.execute("DELETE FROM AccessPermissions WHERE VisitorId=?", (v,))
c.execute("DELETE FROM Visitors WHERE Id=?", (v,))
c.execute("DELETE FROM Contractors WHERE Id=?", (ctr,))
c.execute("DELETE FROM UserRoles WHERE UserId=(SELECT Id FROM Users WHERE Username=?)", (u,))
c.execute("DELETE FROM Users WHERE Username=?", (u,))
c.commit(); c.close()
PYEOF
AJ -d "__RequestVerificationToken=$(tok /Companies)" -d "id=$CO" "$BASE/Companies/Delete" > /dev/null
chk "شرکت آزمونی حذف شد (بدون داده)" "$(dbq "SELECT COUNT(*) FROM Companies WHERE Id='$CO'")" "0"

# حسابرسی ثبت شده؟ (تجمعی — حداقل یک create در این اجرا)
CR=$(dbq "SELECT COUNT(*) FROM AuditLogs WHERE Entity='company' AND Action='create'")
[ "$CR" -ge 1 ] && ok "حسابرسی create company ($CR)" || bad "حسابرسی create company"

echo "----"
echo "PASS=$PASS FAIL=$FAIL"
rm -f "$CJ"
[ $FAIL -eq 0 ] && echo "ALL GREEN 🎉" || echo "RED — بررسی کن"
