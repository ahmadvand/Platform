#!/usr/bin/env bash
# ============================================================
# تسک ۲۵ — فرم ورود واحد + رفع باگ خروج + عیدی/سنوات سالانه + تسویه‌حساب + PDF رسمی
# اجرا: bash scripts/e2e-phase25.sh
# ============================================================
set -u
cd "$(dirname "$0")/.."
BASE="http://localhost:3000"
PASS=0; FAIL=0
ck() { if [ "$1" = "1" ]; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }
TSMARK=$(date +%s)

CJA=/tmp/p25-admin.txt; rm -f "$CJA"
GETA()  { curl -s -b "$CJA" -c "$CJA" "$BASE$1" --max-time 20; }
POSTA() { local url="$1"; shift; curl -s -b "$CJA" -c "$CJA" -X POST "$BASE$url" "$@" --max-time 20; }
TOKENA(){ GETA "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
XHR="X-Requested-With: XMLHttpRequest"
jok()  { echo "$1" | python3 -c "import sys,json;d=json.load(sys.stdin);print(d.get('ok',''))" 2>/dev/null; }
jmsg() { echo "$1" | python3 -c "import sys,json;d=json.load(sys.stdin);print(d.get('message',''))" 2>/dev/null; }
# ورود با فرم واحد در یک jar مشخص — GET برای توکن، POST با همان jar (آنتی‌فورجری نیاز به کوکی دارد)
ULOGIN() { local jar="$1" ident="$2" sec="$3"; local tk
  tk=$(curl -s -c "$jar" "$BASE/Auth/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
  curl -s -o /dev/null -w "%{http_code} %{redirect_url}" -b "$jar" -c "$jar" "$BASE/Auth/Login" -X POST \
    --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "identifier=$ident" --data-urlencode "secret=$sec" --max-time 15; }

echo "── ۱) صفحه ورود — یک فرم واحد"
P=$(curl -s "$BASE/Auth/Login")
echo "$P" | grep -q 'name="identifier"' && echo "$P" | grep -q 'name="secret"' && ck 1 "فیلدهای identifier/secret" || ck 0 "فیلدهای identifier/secret"
n=$(echo "$P" | grep -c '<form method="post"')
ck "$([ "$n" = "1" ] && echo 1 || echo 0)" "دقیقاً یک فرم (یافت: $n)"
echo "$P" | grep -q 'کد پرسنلی' && echo "$P" | grep -q 'کد ملی' && ck 1 "برچسب دوگانه (نام کاربری/کد پرسنلی)" || ck 0 "برچسب دوگانه"
echo "$P" | grep -q 'LoginEmployee' && ck 0 "فرم دوم کارکنان حذف شده باشد" || ck 1 "فرم دوم حذف شد"

echo "── ۲) ورود یکپارچه — هر دو مسیر از همان فرم"
CJw=/tmp/p25-wrong.txt; rm -f "$CJw"
# پاسخ POST مستقیم بررسی شود (خطا در همان پاسخ رندر می‌شود نه ریدایرکت)
LOGBODY() { local jar="$1" ident="$2" sec="$3"; local tk
  tk=$(curl -s -c "$jar" "$BASE/Auth/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
  curl -s -b "$jar" -c "$jar" "$BASE/Auth/Login" -X POST \
    --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "identifier=$ident" --data-urlencode "secret=$sec" --max-time 15; }
b=$(LOGBODY "$CJw" admin WRONG-PASS)
echo "$b" | grep -q "نام کاربری یا رمز عبور اشتباه" && ck 1 "رمز اشتباه مدیر — بدون fallback به مسیر پرسنل" || ck 0 "رد مدیر (یافت: $(echo "$b" | head -c 100))"
r=$(ULOGIN "$CJA" admin Admin@1234)
echo "$r" | grep -qE "^302 http://localhost:3000/$" && ck 1 "ورود مدیر از فرم واحد → /" || ck 0 "ورود مدیر (یافت: $r)"

CODE=$(python3 scripts/dbq.py --json "SELECT PersonnelCode, NationalId FROM Employees WHERE Status='فعال' ORDER BY PersonnelCode LIMIT 1" 2>/dev/null | python3 -c "import sys,json;d=json.load(sys.stdin);print(d[0]['PersonnelCode'])")
NID=$(python3 scripts/dbq.py --json "SELECT PersonnelCode, NationalId FROM Employees WHERE Status='فعال' ORDER BY PersonnelCode LIMIT 1" 2>/dev/null | python3 -c "import sys,json;d=json.load(sys.stdin);print(d[0]['NationalId'])")
CJe=/tmp/p25-emp.txt; rm -f "$CJe"
r=$(ULOGIN "$CJe" "$CODE" "$NID")
echo "$r" | grep -q "/Self" && ck 1 "ورود کارمند (کد+ملی) از همان فرم → /Self" || ck 0 "ورود کارمند (یافت: $r)"
loc=$(curl -s -o /dev/null -w "%{redirect_url}" -b "$CJe" "$BASE/" --max-time 15)
echo "$loc" | grep -q "/Self" && ck 1 "تشخیص دسترسی: کارمند → /Self" || ck 0 "تشخیص دسترسی کارمند (یافت: $loc)"

echo "── ۳) خروج — حذف هر دو واریانت کوکی (CHIPS)"
tk=$(curl -s -b "$CJe" -c "$CJe" "$BASE/Self" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
H=$(curl -s -D - -o /dev/null -b "$CJe" -c "$CJe" "$BASE/Auth/Logout" -X POST \
  --data-urlencode "__RequestVerificationToken=$tk" --max-time 15)
echo "$H" | grep -i "set-cookie" | grep -q "hr_at=;" && echo "$H" | grep -i "set-cookie: hr_at=;" | grep -qi "partitioned" && ck 1 "حذف واریانت Partitioned" || ck 0 "حذف Partitioned"
echo "$H" | grep -i "set-cookie" | grep -q "hr_rt=;" && echo "$H" | grep -i "set-cookie: hr_rt=;" | grep -qi "partitioned" && ck 1 "حذف RefreshCookie واریانت Partitioned" || ck 0 "حذف rt Partitioned"
n=$(echo "$H" | grep -ci "set-cookie: hr_at=;")
ck "$([ "$n" = "2" ] && echo 1 || echo 0)" "دو هدر حذف برای hr_at (پارتیشن‌شده + ساده — یافت: $n)"
code=$(curl -s -o /dev/null -w "%{http_code} %{redirect_url}" -b "$CJe" "$BASE/Self" --max-time 15)
echo "$code" | grep -q "/Auth/Login" && ck 1 "پس از خروج، صفحه لاگین می‌آید (نشست مرده)" || ck 0 "نشست پس از خروج (یافت: $code)"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM RefreshTokens WHERE RevokedAt IS NOT NULL" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "توکن بازخوانی در دیتابیس ابطال شد (یافت: $n)"

echo "── ۴) ورود مدیر برای بخش حقوق"
loc=$(curl -s -o /dev/null -w "%{redirect_url}" -b "$CJA" "$BASE/" --max-time 15)
echo "$loc" | grep -q "/Auth/Login" && ck 0 "نشست مدیر برقرار نشد" || ck 1 "نشست مدیر برقرار"

echo "── ۵) عیدی و سنوات سالانه"
CID=$(python3 scripts/dbq.py --json "SELECT Id FROM Companies ORDER BY Code LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
JY=$(python3 scripts/dbq.py --json "SELECT MAX(JYear) AS y FROM PayrollParameters" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['y'])")
P=$(GETA /Payroll/Bonus)
echo "$P" | grep -q "عیدی و پایه سنوات سالانه" && echo "$P" | grep -q "تسویه" && ck 1 "صفحه عیدی و تسویه با دو کارت" || ck 0 "صفحه عیدی/تسویه"
echo "$P" | grep -q "عیدی و تسویه" && ck 1 "آیتم منوی «عیدی و تسویه»" || ck 0 "آیتم منو"
python3 scripts/dbq.py --exec "DELETE FROM PayrollBonusRows WHERE CompanyId='$CID' AND JYear=$JY" > /dev/null 2>&1
tk=$(TOKENA /Payroll/Bonus)
r=$(POSTA /Payroll/BonusCompute --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "companyId=$CID" --data-urlencode "jYear=$JY" -H "$XHR")
[ "$(jok "$r")" = "True" ] && ck 1 "محاسبه عیدی/سنوات سالانه" || ck 0 "محاسبه (یافت: $(jmsg "$r" | head -c 100))"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM PayrollBonusRows WHERE CompanyId='$CID' AND JYear=$JY" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "ردیف‌ها در دیتابیس (یافت: $n)"
# صحت عددی: عیدی = min(پایه×۲، حداقل×۳) × روز/۳۶۵ (مقادیر dbq رشته‌ای‌اند → float)
V=$(python3 scripts/dbq.py --json "SELECT e.BaseSalary AS base, b.Days AS days, b.Eid AS eid FROM PayrollBonusRows b JOIN Employees e ON e.Id=b.EmployeeId WHERE b.CompanyId='$CID' AND b.JYear=$JY ORDER BY e.BaseSalary DESC LIMIT 1" 2>/dev/null | python3 -c "
import sys,json
d=json.load(sys.stdin)[0]
base,days,eid=float(d['base']),float(d['days']),float(d['eid'])
cap=166255500*3
expect=round(min(base*2,cap)*days/365)
print(1 if abs(eid-expect)<=1 else 0)")
ck "$([ "$V" = "1" ] && echo 1 || echo 0)" "صحت فرمول عیدی با سقف ۳× حداقل (min(پایه×۲، حداقل×۳)×روز/۳۶۵)"
tk=$(TOKENA /Payroll/Bonus)
r=$(POSTA /Payroll/BonusFinalize --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "companyId=$CID" --data-urlencode "jYear=$JY" -H "$XHR")
[ "$(jok "$r")" = "True" ] && ck 1 "نهایی‌سازی ردیف‌ها" || ck 0 "نهایی‌سازی"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM PayrollBonusRows WHERE CompanyId='$CID' AND JYear=$JY AND Status='نهایی'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "وضعیت نهایی در دیتابیس (یافت: $n)"
tk=$(TOKENA /Payroll/Bonus)
r=$(POSTA /Payroll/BonusFinalize --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "companyId=$CID" --data-urlencode "jYear=$JY" -H "$XHR")
echo "$(jmsg "$r")" | grep -q "پیش‌نویسی برای نهایی" && ck 1 "نهایی‌سازی دوباره بدون پیش‌نویس رد شد" || ck 0 "گارد نهایی‌سازی دوباره (یافت: $(jmsg "$r" | head -c 90))"
tk=$(TOKENA /Payroll/Bonus)
r=$(POSTA /Payroll/BonusCompute --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "companyId=$CID" --data-urlencode "jYear=$JY" -H "$XHR")
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM PayrollBonusRows WHERE CompanyId='$CID' AND JYear=$JY" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
f=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM PayrollBonusRows WHERE CompanyId='$CID' AND JYear=$JY AND Status='نهایی'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
[ "$(jok "$r")" = "True" ] && echo "$f" | grep -q "0" && echo "$n" | grep -q "0" && ck 0 "بازمحاسبه باید نهایی‌ها را حفظ کند" || ck 1 "بازمحاسبه ردیف‌های نهایی را حفظ کرد (کل: $n، نهایی: $f)"
curl -s -o /tmp/p25-bonus.xlsx -w "" -b "$CJA" "$BASE/Payroll/BonusExport?companyId=$CID&jYear=$JY" --max-time 20
file /tmp/p25-bonus.xlsx | grep -qi "excel\|zip" && ck 1 "خروجی اکسل عیدی معتبر" || ck 0 "اکسل عیدی"
P=$(GETA /Payroll/Bonus)
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM PayItems WHERE Code='عیدی_سالانه' AND Active=1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "0" ] && echo 1 || echo 0)" "اقلام سالانه غیرفعال برای فیش ماهانه (Active=0 — یافت: $n)"

echo "── ۶) تسویه‌حساب"
EID=$(python3 scripts/dbq.py --json "SELECT Id, PersonnelCode FROM Employees WHERE Status='فعال' AND CompanyId='$CID' ORDER BY PersonnelCode DESC LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
ECODE=$(python3 scripts/dbq.py --json "SELECT Id, PersonnelCode FROM Employees WHERE Id='$EID'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['PersonnelCode'])")
echo "$ECODE" > /tmp/p25-empcode.txt
python3 scripts/dbq.py --exec "DELETE FROM PayrollSettlements WHERE EmployeeId='$EID'" > /dev/null 2>&1
tk=$(TOKENA /Payroll/Bonus)
r=$(POSTA /Payroll/SettlementCompute --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "employeeId=$EID" --data-urlencode "endDate=1400/01/01" -H "$XHR")
echo "$(jmsg "$r")" | grep -q "قبل از تاریخ استخدام" && ck 1 "تاریخ قبل از استخدام رد شد" || ck 0 "گارد تاریخ استخدام (یافت: $(jmsg "$r" | head -c 90))"
r=$(POSTA /Payroll/SettlementCompute --data-urlencode "__RequestVerificationToken=$tk" \
  --data-urlencode "employeeId=$EID" --data-urlencode "endDate=1405/07/28" -H "$XHR")
[ "$(jok "$r")" = "True" ] && ck 1 "محاسبه تسویه‌حساب" || ck 0 "محاسبه تسویه (یافت: $(jmsg "$r" | head -c 100))"
SID=$(python3 scripts/dbq.py --json "SELECT Id FROM PayrollSettlements WHERE EmployeeId='$EID' LIMIT 1" 2>/dev/null | python3 -c "import sys,json;d=json.load(sys.stdin);print(d[0]['Id'] if d else '')")
[ -n "$SID" ] && ck 1 "پیش‌نویس تسویه در دیتابیس" || ck 0 "پیش‌نویس تسویه"
V=$(python3 scripts/dbq.py --json "SELECT LeaveBalance, Daily, LeavePay, Eid, Seniority FROM PayrollSettlements WHERE Id='$SID'" 2>/dev/null | python3 -c "
import sys,json
d=json.load(sys.stdin)[0]
lb,day,lp,eid,sen=[float(d[k]) for k in ['LeaveBalance','Daily','LeavePay','Eid','Seniority']]
print(1 if abs(lb*day-lp)<=1 and lp>0 and eid>0 and sen>0 else 0)")
ck "$([ "$V" = "1" ] && echo 1 || echo 0)" "صحت اجزای تسویه (مرخصی×روزانه + عیدی + سنوات)"
tk=$(TOKENA /Payroll/Bonus)
r=$(POSTA /Payroll/SettlementFinalize --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "id=$SID" -H "$XHR")
[ "$(jok "$r")" = "True" ] && ck 1 "ثبت نهایی تسویه" || ck 0 "نهایی تسویه (یافت: $(jmsg "$r" | head -c 100))"
ST=$(python3 scripts/dbq.py --json "SELECT Status FROM Employees WHERE Id='$EID'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Status'])")
[ "$ST" = "تسویه‌شده" ] && ck 1 "وضعیت پرسنل → تسویه‌شده" || ck 0 "وضعیت پرسنل (یافت: $ST)"
r=$(POSTA /Payroll/SettlementFinalize --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "id=$SID" -H "$XHR")
echo "$(jmsg "$r")" | grep -q "قبلاً نهایی" && ck 1 "نهایی‌سازی دوباره رد شد" || ck 0 "گارد دوباره (یافت: $(jmsg "$r" | head -c 90))"
r=$(POSTA /Payroll/SettlementDelete --data-urlencode "__RequestVerificationToken=$tk" --data-urlencode "id=$SID" -H "$XHR")
echo "$(jmsg "$r")" | grep -q "قابل حذف نیست" && ck 1 "حذف نهایی‌شده رد شد" || ck 0 "گارد حذف نهایی (یافت: $(jmsg "$r" | head -c 90))"

echo "── ۷) اسناد PDF رسمی"
RUN=$(python3 scripts/dbq.py --json "SELECT Id FROM PayrollRuns WHERE Status='نهایی' LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
[ -n "$RUN" ] || RUN=$(python3 scripts/dbq.py --json "SELECT Id FROM PayrollRuns LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
SLIP=$(python3 scripts/dbq.py --json "SELECT Id FROM Payslips WHERE RunId='$RUN' LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
curl -s -o /tmp/p25-run.pdf -w "" -b "$CJA" "$BASE/Payroll/RunPdf?runId=$RUN" --max-time 30
file /tmp/p25-run.pdf | grep -qi "PDF document" && ck 1 "PDF رسمی لیست حقوق" || ck 0 "PDF لیست"
curl -s -o /tmp/p25-slip.pdf -w "" -b "$CJA" "$BASE/Payroll/SlipPdf?id=$SLIP" --max-time 30
file /tmp/p25-slip.pdf | grep -qi "PDF document" && ck 1 "PDF فیش حقوقی" || ck 0 "PDF فیش"
curl -s -o /tmp/p25-st.pdf -w "" -b "$CJA" "$BASE/Payroll/SettlementPdf?id=$SID" --max-time 30
file /tmp/p25-st.pdf | grep -qi "PDF document" && ck 1 "PDF برگ تسویه‌حساب" || ck 0 "PDF تسویه"
curl -s -o /dev/null -w "" -b "$CJA" "$BASE/Payroll/RunPdf?runId=NOT-EXISTS" --max-time 20
c=$(curl -s -o /dev/null -w "%{http_code}" -b "$CJA" "$BASE/Payroll/RunPdf?runId=NOT-EXISTS" --max-time 20)
[ "$c" = "404" ] && ck 1 "PDF لیست ناموجود → 404" || ck 0 "404 لیست (یافت: $c)"
P=$(GETA "/Payroll/Index?runId=$RUN")
echo "$P" | grep -q "RunPdf" && echo "$P" | grep -q "SlipPdf" && ck 1 "دکمه‌های PDF در صفحه لیست حقوق" || ck 0 "دکمه‌های PDF"

echo "── ۸) فیش PDF خودخدمتی — مالکیت‌سنجی"
# ورود همان پرسنلی که فیش نهایی دارد (تست واقعی پرتال — نه مدیر بدون اتصال)
OWNSLIP=$(python3 scripts/dbq.py --json "SELECT Id FROM Payslips WHERE RunId='$RUN' ORDER BY Id LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
SLIPCODE=$(python3 scripts/dbq.py "SELECT PersonnelCode FROM Employees WHERE Id=(SELECT EmployeeId FROM Payslips WHERE Id='$OWNSLIP')")
SLIPNID=$(python3 scripts/dbq.py "SELECT NationalId FROM Employees WHERE PersonnelCode='$SLIPCODE'")
CJs=/tmp/p25-self.txt; rm -f "$CJs"
r=$(ULOGIN "$CJs" "$SLIPCODE" "$SLIPNID")
echo "$r" | grep -q "/Self" && ck 1 "ورود پرسنل صاحب فیش" || ck 0 "ورود صاحب فیش (یافت: $r)"
P=$(curl -s -b "$CJs" "$BASE/Self/Payslips")
echo "$P" | grep -q "PayslipPdf" && ck 1 "دکمه PDF در پرتال من" || ck 0 "دکمه PDF پرتال"
curl -s -o /tmp/p25-self.pdf -w "" -b "$CJs" "$BASE/Self/PayslipPdf?id=$OWNSLIP" --max-time 30
file /tmp/p25-self.pdf | grep -qi "PDF document" && ck 1 "فیش PDF خود برای صاحبش" || ck 0 "فیش خودخدمتی"
OTHER=$(python3 scripts/dbq.py --json "SELECT Id FROM Payslips WHERE EmployeeId != (SELECT EmployeeId FROM Payslips WHERE Id='$OWNSLIP') LIMIT 1" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['Id'])")
code=$(curl -s -o /dev/null -w "%{http_code}" -b "$CJs" "$BASE/Self/PayslipPdf?id=$OTHER" --max-time 20)
[ "$code" = "404" ] && ck 1 "فیش دیگران → 404" || ck 0 "مالکیت‌سنجی (یافت: $code)"
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/Self/PayslipPdf?id=$SLIP" --max-time 20)
[ "$code" = "302" ] && ck 1 "بدون نشست → ورود" || ck 0 "بدون نشست (یافت: $code)"

echo "── ۹) دسترسی‌ها"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM Permissions WHERE Key='payroll.bonus'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "1" ] && echo 1 || echo 0)" "دسترسی payroll.bonus در کاتالوگ"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM RolePermissions rp JOIN Roles r ON r.Id=rp.RoleId JOIN Permissions p ON p.Id=rp.PermissionId WHERE p.Key='payroll.bonus' AND r.Name IN ('hr_manager','superadmin')" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" -ge 1 ] && echo 1 || echo 0)" "نقش hr دسترسی عیدی دارد (superadmin خودکار)"
CJv=/tmp/p25-viewer.txt; rm -f "$CJv"
r=$(ULOGIN "$CJv" viewer View@1234)
loc=$(curl -s -o /dev/null -w "%{redirect_url}" -b "$CJv" "$BASE/Payroll/Bonus" --max-time 15)
echo "$loc" | grep -q "Denied" && ck 1 "viewer → صفحه عیدی Denied" || ck 0 "دسترسی viewer (یافت: $loc)"
loc=$(curl -s -o /dev/null -w "%{redirect_url}" -b "$CJv" "$BASE/Payroll/SettlementPdf?id=x" --max-time 15)
echo "$loc" | grep -q "Denied" && ck 1 "viewer → PDF تسویه Denied" || ck 0 "دسترسی viewer تسویه (یافت: $loc)"
tk=$(TOKENA /Payroll/Bonus)
code=$(curl -s -o /dev/null -w "%{http_code}" -b "$CJA" "$BASE/Payroll/BonusCompute" -X POST \
  --data-urlencode "companyId=$CID" --data-urlencode "jYear=$JY" --max-time 15)
[ "$code" = "400" ] && ck 1 "بدون آنتی‌فورجری → 400" || ck 0 "آنتی‌فورجری (یافت: $code)"

echo "── پاکسازی"
EC=$(cat /tmp/p25-empcode.txt)
python3 scripts/dbq.py --exec "UPDATE Employees SET Status='فعال', EndDate=NULL WHERE PersonnelCode='$EC' AND CompanyId='$CID'" > /dev/null 2>&1
python3 scripts/dbq.py --exec "DELETE FROM PayrollSettlements WHERE EmployeeId='$EID'" > /dev/null 2>&1
python3 scripts/dbq.py --exec "DELETE FROM PayrollBonusRows WHERE CompanyId='$CID' AND JYear=$JY" > /dev/null 2>&1
python3 scripts/dbq.py --exec "UPDATE Users SET FailedLoginAttempts=0, LockedUntil=NULL WHERE Username='admin'" > /dev/null 2>&1
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM PayrollSettlements WHERE EmployeeId='$EID'" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])")
ck "$([ "$n" = "0" ] && echo 1 || echo 0)" "پاکسازی کامل"

echo "════════════════════════════"
echo "PASS=$PASS FAIL=$FAIL"
[ "$FAIL" = "0" ] && echo "DONE ✓" || echo "DONE ✗"
exit $([ "$FAIL" = "0" ] && echo 0 || echo 1)
