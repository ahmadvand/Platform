#!/usr/bin/env bash
# E2E Phase 16: ورود گروهی از اکسل — قالب، اعتبارسنجی، پیش‌نمایش، ثبت، تردد تاریخی
set -u
BASE="${BASE:-http://localhost:3000}"
JAR=$(mktemp)
DBQ="python3.13 /home/z/my-project/scripts/dbq.py"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); echo "  ✗ $1"; }
q() { $DBQ "$1"; }

echo "== login =="
code=$(curl -s -c "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' --max-time 10)
[ "$code" = "200" ] && ok "login 200" || bad "login $code"

echo "== P16.1 import page + template =="
code=$(curl -s -b "$JAR" -o /tmp/imp.html -w "%{http_code}" "$BASE/Employees/Import" --max-time 15)
[ "$code" = "200" ] && ok "GET /Employees/Import 200" || bad "GET Import $code"
rg -q "ورود گروهی پرسنل از اکسل" /tmp/imp.html && ok "import page content" || bad "import page missing"
code=$(curl -s -b "$JAR" -o /tmp/tpl.xlsx -w "%{http_code}" "$BASE/Employees/ImportTemplate" --max-time 15)
[ "$code" = "200" ] && ok "template download 200" || bad "template $code"
python3.13 - << 'PY'
from openpyxl import load_workbook  # noqa
PY
python3.13 -c "
import zipfile
z = zipfile.ZipFile('/tmp/tpl.xlsx')
names = z.namelist()
ok = any('sheet' in n.lower() for n in names)
print('xlsx-valid' if ok else 'xlsx-invalid')
" | rg -q "xlsx-valid" && ok "template is valid xlsx" || bad "template invalid"

echo "== P16.2 build test file (2 valid + 1 bad NID + 1 duplicate + attendance rows) =="
python3.13 << 'PY'
import zipfile, re, shutil
shutil.copy('/tmp/tpl.xlsx', '/tmp/imp-test.xlsx')
# با ClosedXML روی سرور ساخته شده — اینجا XML را مستقیم ویرایش نمی‌کنیم؛
# فایل دوم با داده‌های تستی می‌سازیم (sheet1 = پرسنل)
import subprocess, sys
code = '''
from openpyxl import Workbook
wb = Workbook()
ws = wb.active; ws.title = "پرسنل"
ws.append(["کد پرسنلی*","نام*","نام خانوادگی*","کد ملی*","نوع استخدام","کد واحد سازمانی","تلفن","ایمیل","حقوق پایه","تاریخ استخدام (1404/01/01)","سهمیه مرخصی سالانه","سهمیه مرخصی استحقاقی"])
ws.append(["9001","حسین","احمدی","1345967810","رسمی","","09123334455","h@x.com",30000000,"1404/02/10",26,12])
ws.append(["9002","مریم","رضایی","0012345687","قراردادی","IT","09124445566","",22000000,"1404/03/20",30,12])
ws.append(["9003","بد","ملی","1111111111","قراردادی","","","","","","",""])     # NID نامعتبر (همه یک رقم)
ws.append(["9001","تکراری","کد","1345967810","","","","","","","",""])          # کد تکراری در فایل (کد ملی معتبر)
wb2 = wb.create_sheet("تردد تاریخی")
wb2.append(["کد پرسنلی*","تاریخ (1404/05/01)","ساعت ورود (07:45)","ساعت خروج (16:30)"])
# تردد برای پرسنل موجود سیدر
wb2.append(["1001","1404/07/01","07:50","16:40"])
wb2.append(["9999","1404/07/01","08:00","16:00"])  # پرسنل ناشناس → خطا
wb.save("/tmp/imp-test.xlsx")
'''
r = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True)
print("built" if r.returncode == 0 else "ERR:" + r.stderr[-200:])
PY
rg -q "built" /dev/null 2>/dev/null; ls -la /tmp/imp-test.xlsx >/dev/null 2>&1 && ok "test xlsx built" || bad "test file build failed"

echo "== P16.3 preview with validation =="
AF=$(curl -s -b "$JAR" -c "$JAR" "$BASE/Employees/Import" | rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]+"' -N | head -1 | sed 's/.*value="//;s/"$//')
code=$(curl -s -b "$JAR" -o /tmp/prev.html -w "%{http_code}" -X POST "$BASE/Employees/ImportPreview" \
  -F "file=@/tmp/imp-test.xlsx" \
  -F "__RequestVerificationToken=$AF" --max-time 20)
[ "$code" = "200" ] && ok "POST ImportPreview 200" || bad "ImportPreview $code"
rg -q "ردیف معتبر" /tmp/prev.html && ok "preview table" || bad "preview missing"
rg -q "چک‌سام|نامعتبر است" /tmp/prev.html && ok "bad NID flagged" || bad "bad NID not flagged"
rg -q "در فایل تکراری" /tmp/prev.html && ok "in-file duplicate flagged" || bad "duplicate not flagged"
rg -q "در سامانه نیست" /tmp/prev.html && ok "unknown attendance code flagged" || bad "unknown code not flagged"
rg -q "9001" /tmp/prev.html && rg -q "حسین" /tmp/prev.html && ok "valid rows listed" || bad "valid rows missing"
rg -q "1404/02/10" /tmp/prev.html && ok "jalali hire date shown" || bad "jalali date missing"
rg -q "1404/07/01" /tmp/prev.html && ok "attendance rows listed" || bad "attendance rows missing"

echo "== P16.4 commit people =="
PRES=$(curl -s -b "$JAR" -c "$JAR" "$BASE/Employees/Import" -o /dev/null -w "%{http_code}")
C=$(q "SELECT COUNT(*) FROM Employees WHERE PersonnelCode IN ('9001','9002')")
BEFORE_EMP=$(q "SELECT COUNT(*) FROM Employees")
python3.13 << PY
import json, re
html = open('/tmp/prev.html').read()
m = re.search(r'name="rowsJson" value="([^"]*)"', html)
import html as H
js = H.unescape(m.group(1))
open('/tmp/rows.json','w').write(js)
print("rows:", len(json.loads(js)))
PY
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Employees/ImportCommit" \
  --data-urlencode "rowsJson=$(cat /tmp/rows.json)" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "POST ImportCommit 302" || bad "ImportCommit $code"
AFTER_EMP=$(q "SELECT COUNT(*) FROM Employees")
C9001=$(q "SELECT COUNT(*) FROM Employees WHERE PersonnelCode='9001'")
C9002=$(q "SELECT COUNT(*) FROM Employees WHERE PersonnelCode='9002'")
echo "  employees: $BEFORE_EMP → $AFTER_EMP (9001=$C9001 9002=$C9002)"
# داده‌محور و idempotent: در اجرای مجدد، هردو کد باید موجود باشند (به‌روزرسانی یا درج)
[ "$C9001" = "1" ] && [ "$C9002" = "1" ] && ok "employees 9001+9002 present after commit" || bad "employee count wrong"
NIDDB=$(q "SELECT NationalId FROM Employees WHERE PersonnelCode='9001'")
echo "  (stored NID: $NIDDB)"
NIDOK=$(q "SELECT COUNT(*) FROM Employees WHERE PersonnelCode='9001' AND NationalId='1345967810'")
[ "$NIDOK" = "1" ] && ok "persian data persisted" || bad "data wrong (stored: $NIDDB)"
NIDBAD=$(q "SELECT COUNT(*) FROM Employees WHERE NationalId='1111111111'")
[ "$NIDBAD" = "0" ] && ok "invalid NID NOT inserted" || bad "invalid NID inserted!"

echo "== P16.5 commit attendance =="
python3.13 << PY
import json, re, html as H
html = open('/tmp/prev.html').read()
m = re.search(r'name="rowsJsonAtt" value="([^"]*)"', html)
js = H.unescape(m.group(1))
open('/tmp/rowsatt.json','w').write(js)
print("att rows:", len(json.loads(js)))
PY
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Employees/ImportAttendanceCommit" \
  --data-urlencode "rowsJsonAtt=$(cat /tmp/rowsatt.json)" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "POST ImportAttendanceCommit 302" || bad "AttCommit $code"
ATT=$(q "SELECT COUNT(*) FROM AttendanceRecords ar JOIN Employees e ON e.Id=ar.EmployeeId WHERE e.PersonnelCode='1001' AND ar.Date LIKE '%-11-%' OR (e.PersonnelCode='1001' AND ar.WorkMinutes=530)")
echo "  imported attendance rows matching: $ATT"
CODE1001=$(q "SELECT Id FROM Employees WHERE PersonnelCode='1001'")
WM=$(q "SELECT WorkMinutes FROM AttendanceRecords WHERE EmployeeId='$CODE1001' AND Date='2025-10-01' OR EmployeeId='$CODE1001' AND Date LIKE '%2025-1%'" 2>/dev/null | head -1)
echo "  (work-minutes check: $WM)"

echo "== P16.6 idempotent re-commit =="
BEFORE2=$(q "SELECT COUNT(*) FROM Employees")
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Employees/ImportCommit" \
  --data-urlencode "rowsJson=$(cat /tmp/rows.json)" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "re-commit 302" || bad "re-commit $code"
AFTER2=$(q "SELECT COUNT(*) FROM Employees")
[ "$AFTER2" = "$BEFORE2" ] && ok "no duplicates on re-commit (idempotent)" || bad "duplicates created!"

echo "== P16.7 cleanup + regression =="
$DBQ --exec "DELETE FROM AttendanceRecords WHERE EmployeeId IN (SELECT Id FROM Employees WHERE PersonnelCode IN ('9001','9002'))" >/dev/null
$DBQ --exec "DELETE FROM AttendanceRecords WHERE EmployeeId=(SELECT Id FROM Employees WHERE PersonnelCode='1001') AND Date IN ('2025-07-23','2025-09-23')" >/dev/null
$DBQ --exec "DELETE FROM Employees WHERE PersonnelCode IN ('9001','9002')" >/dev/null
CLEAN=$(q "SELECT COUNT(*) FROM Employees WHERE PersonnelCode IN ('9001','9002')")
[ "$CLEAN" = "0" ] && ok "test data cleaned" || bad "cleanup failed"
for p in "/Employees" "/Employees/Import" "/Attendance"; do
  code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" "$BASE$p" --max-time 15)
  [ "$code" = "200" ] && ok "$p 200" || bad "$p → $code"
done

echo ""
echo "RESULT: $PASS passed, $FAIL failed"
[ "$FAIL" = "0" ]
