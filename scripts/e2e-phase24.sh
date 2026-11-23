#!/usr/bin/env bash
# =============================================================
# e2e-phase24.sh — موتور حقوق داینامیک چندشرکتی (فرمول‌ساز)
# نکته: بین دو اجرای پشت‌سرهم حداقل ۹۰ ثانیه فاصله بگذارید (محدودیت نرخ ورود)؛
# ورود از /api/auth/login استفاده می‌کند و داده‌های آزمون ابتدا پاک‌سازی می‌شوند.
# پوشش: صفحات اقلام/پارامترها/تنظیمات، CRUD قلم، آزمایش فرمول،
# لیست حقوق چندشرکتی، جزئیات خطی فیش، نهایی‌سازی، خروجی اکسل،
# گاردها (فرمول بد/کد تکراری/قلم سیستمی/تکراری بودن لیست)، fallback بدون-JS.
# =============================================================
set -u
BASE="http://localhost:3000"
PASS=0; FAIL=0
ok(){ PASS=$((PASS+1)); echo "  ✔ $1"; }
bad(){ FAIL=$((FAIL+1)); echo "  ✘ $1"; }
chk(){ if [ "$1" = "$2" ]; then ok "$3"; else bad "$3 (انتظار $2، واقعی $1)"; fi }
has(){ if echo "$1" | grep -q "$2"; then ok "$3"; else bad "$3 — یافت نشد: $2"; fi }
notHas(){ if echo "$1" | grep -q "$2"; then bad "$3 — نباید باشد: $2"; else ok "$3"; fi }
aj(){ curl -s -H "X-Requested-With: XMLHttpRequest" "$@"; }
msgof(){ echo "$1" | python3 -c "import sys,json;print(json.load(sys.stdin).get('message',''))" 2>/dev/null; }
tok(){ curl -s -b "$CJ" -c "$CJ" "$BASE/Payroll" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }

CJ=$(mktemp)
echo "— ورود مدیر —"
CODE=$(curl -s -c "$CJ" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$BASE/api/auth/login" -o /dev/null -w "%{http_code}")
chk "$CODE" "200" "ورود admin"

# ---------- صفحات ----------
P=$(curl -s -b "$CJ" "$BASE/Payroll")
has "$P" "شرکت" "صفحه لیست حقوق — سلکت شرکت"
has "$P" "لیست‌های حقوق" "صفحه لیست حقوق — کارت لیست‌ها"
has "$P" "پارس آسیا" "لیست حقوق — شرکت اول"
has "$P" "آریا سازه" "لیست حقوق — شرکت دوم (چندشرکتی)"

I=$(curl -s -b "$CJ" "$BASE/Payroll/Items")
has "$I" "فرمول‌ساز اقلام" "صفحه فرمول‌ساز"
has "$I" "آزمایش فرمول" "پنل آزمایش فرمول"
has "$I" "پایه_محاسبه" "جدول متغیرها"
has "$I" "فوق_العاده_نوبت_کاری" "اقلام شیفت‌محور نوبت‌کاری"

PA=$(curl -s -b "$CJ" "$BASE/Payroll/Params")
has "$PA" "حداقل_روزانه" "پارامترها — حداقل روزانه"
has "$PA" "پلکان_مالیات" "پارامترها — پلکان مالیات"
has "$PA" "۵,۵۴۱,۸۵۰" "پارامترها — مقدار ۱۴۰۵ (ریال، ارقام فارسی)"

ST=$(curl -s -b "$CJ" "$BASE/Payroll/Settings")
has "$ST" "ماهانه ثابت" "تنظیمات — گزینه ماهانه"
has "$ST" "کارکردی" "تنظیمات — گزینه کارکردی (هر دو روش)"
has "$ST" "روزشمار با وضعیت‌های تأییدشده" "تنظیمات — اولویت ۲ پیش‌فرض"

# ---------- شناسه‌ها ----------
CID1=$(python3 scripts/dbq.py "SELECT Id FROM Companies WHERE Code='PAC-01'" | python3 scripts/dbval.py)
CID2=$(python3 scripts/dbq.py "SELECT Id FROM Companies WHERE Code='ASC-02'" | python3 scripts/dbval.py)
EID=$(python3 scripts/dbq.py "SELECT Id FROM Employees WHERE PersonnelCode='2003'" | python3 scripts/dbval.py)
# پاک‌سازی لیست ماه ۶ اجرای قبلی تست — برای تکرارپذیری
python3 scripts/dbq.py --exec "DELETE FROM PayslipLines WHERE PayslipId IN (SELECT Id FROM Payslips WHERE RunId IN (SELECT Id FROM PayrollRuns WHERE CompanyId='$CID2' AND JMonth=6))" > /dev/null
python3 scripts/dbq.py --exec "DELETE FROM Payslips WHERE RunId IN (SELECT Id FROM PayrollRuns WHERE CompanyId='$CID2' AND JMonth=6)" > /dev/null
python3 scripts/dbq.py --exec "DELETE FROM PayrollRuns WHERE CompanyId='$CID2' AND JMonth=6" > /dev/null
python3 scripts/dbq.py --exec "DELETE FROM PayItems WHERE Code IN ('test_item','nojs_item')" > /dev/null
python3 scripts/dbq.py --exec "DELETE FROM PayrollParameters WHERE Code='آزمون_پارام'" > /dev/null
T2=$(tok)
echo "— CRUD قلم (فرمول‌ساز) —"
# قلم بد — فرمول نامعتبر
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID1&code=test_bad&title=قلم آزمون&kind=مزیت&formula=دقیقه_کارکرد + (&taxable=on&insurable=on&continuous=on&scopeType=همه&priority=0&order=200&active=on" "$BASE/Payroll/ItemSave")
has "$R" '"ok":false' "قلم با فرمول معیوب رد شد"
has "$(msgof "$R")" 'فرمول ناقص' "پیام خطای فرمول فارسی"
# قلم بد — متغیر ناموجود (زمان ارزیابی → اینجا فقط نحوی؛ متغیر غلط نحوی نیست)
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID1&code=test_item&title=حق مسئولیت&kind=مزیت&formula=if(سال_خدمت >= 10, 15000000, 0)&taxable=on&continuous=on&scopeType=همه&priority=0&order=200&active=on" "$BASE/Payroll/ItemSave")
has "$R" '"ok":true' "ایجاد قلم آزمایشی با if/سال_خدمت"
ITEM_ID=$(python3 scripts/dbq.py "SELECT Id FROM PayItems WHERE Code='test_item'" | python3 scripts/dbval.py)
# کد تکراری
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID1&code=test_item&title=تکراری&kind=مزیت&formula=1&scopeType=همه&priority=0&order=210&active=on" "$BASE/Payroll/ItemSave")
has "$R" '"ok":false' "کد تکراری در همان شرکت رد شد"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID2&code=test_item&title=شرکت دیگر&kind=مزیت&formula=1&scopeType=همه&priority=0&order=210&active=on" "$BASE/Payroll/ItemSave")
has "$R" '"ok":true' "همان کد در شرکت دیگر مجاز (چندشرکتی)"
ITEM2_ID=$(python3 scripts/dbq.py "SELECT Id FROM PayItems WHERE CompanyId='$CID2' AND Code='test_item'" | python3 scripts/dbval.py)
# حذف قلم سیستمی
SYS_ID=$(python3 scripts/dbq.py "SELECT Id FROM PayItems WHERE CompanyId='$CID1' AND Code='پایه'" | python3 scripts/dbval.py)
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&id=$SYS_ID" "$BASE/Payroll/ItemDelete")
has "$R" '"ok":false' "حذف قلم سیستمی رد شد"
# حذف قلم‌های آزمایشی
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&id=$ITEM_ID" "$BASE/Payroll/ItemDelete"); has "$R" '"ok":true' "حذف قلم آزمایشی ۱"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&id=$ITEM2_ID" "$BASE/Payroll/ItemDelete"); has "$R" '"ok":true' "حذف قلم آزمایشی ۲"

echo "— آزمایش فرمول —"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&employeeId=$EID&jYear=1405&jMonth=7&formula=دقیقه_اضافه_کاری / 60 * ساعتی * 1.4" "$BASE/Payroll/TestFormula")
has "$R" '"ok":true' "ارزیابی فرمول اضافه‌کاری"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&employeeId=$EID&jYear=1405&jMonth=7&formula=متغیر_ناموجود %2B 1" "$BASE/Payroll/TestFormula")
has "$R" '"ok":false' "متغیر ناموجود → خطای فارسی"
has "$(msgof "$R")" 'تعریف نشده' "پیام «متغیر تعریف نشده»"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&employeeId=$EID&jYear=1405&jMonth=7&formula=مالیات(600000000)" "$BASE/Payroll/TestFormula")
has "$R" '"ok":true' "تابع مالیات با پلکان شرکت کار می‌کند"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&employeeId=$EID&jYear=1405&jMonth=7&formula=۱۰۰ %2B ۲۰۰" "$BASE/Payroll/TestFormula")
has "$R" '"ok":true' "ارقام فارسی پذیرفته می‌شود"

echo "— لیست حقوق چندشرکتی —"
# تکراری بودن
RUN1=$(python3 scripts/dbq.py "SELECT Id FROM PayrollRuns WHERE CompanyId='$CID2'" | python3 scripts/dbval.py)
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID2&jYear=1405&jMonth=7" "$BASE/Payroll/Create")
has "$R" '"ok":false' "لیست تکراری برای همان شرکت/ماه رد شد"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID2&jYear=1405&jMonth=6" "$BASE/Payroll/Create")
has "$R" '"ok":true' "ایجاد لیست ماه قبل برای شرکت دوم"
RUN2=$(python3 scripts/dbq.py "SELECT Id FROM PayrollRuns WHERE CompanyId='$CID2' AND JMonth=6" | python3 scripts/dbval.py)
LINES2=$(python3 scripts/dbq.py "SELECT COUNT(*) FROM PayslipLines l JOIN Payslips p ON p.Id=l.PayslipId WHERE p.RunId='$RUN2'" | python3 scripts/dbval.py)
if [ "$LINES2" -gt 0 ]; then ok "لیست ماه بدون تردد: فیش با اقلام ساخته شد ($LINES2 خط)"; else bad "لیست ماه بدون تردد — خطی ساخته نشد"; fi
# ماه ۶: هیچ رکوردی نیست → غیبت کامل → پایه کارکردی صفر و کسور غیبت = کل روزانه
GROSSZERO=$(python3 scripts/dbq.py "SELECT COUNT(*) FROM Payslips p JOIN Employees e ON e.Id=p.EmployeeId WHERE p.RunId='$RUN2' AND e.PersonnelCode='2003' AND CAST(p.BaseSalary AS REAL)=0" | python3 scripts/dbval.py)
chk "$GROSSZERO" "1" "روش کارکردی: ماه بدون تردد → پایه صفر (کارکردی روزشمار)"

echo "— جزئیات خطی فیش —"
SLIP=$(python3 scripts/dbq.py "SELECT Id FROM Payslips WHERE RunId='$RUN1' LIMIT 1" | python3 scripts/dbval.py)
S=$(curl -s -b "$CJ" "$BASE/Payroll/Slip?id=$SLIP")
has "$S" "جمع مزایا" "مودال فیش — جمع مزایا"
has "$S" "خالص پرداختی" "مودال فیش — خالص"
has "$S" "بیمه سهم کارگر" "مودال فیش — خط بیمه"

echo "— نهایی‌سازی و گاردها —"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&id=$RUN2" "$BASE/Payroll/Finalize"); has "$R" '"ok":true' "نهایی‌سازی لیست"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&id=$RUN2" "$BASE/Payroll/Delete"); has "$R" '"ok":false' "حذف لیست نهایی رد شد"
CODE=$(curl -s -b "$CJ" -o /tmp/p24-x.xlsx -w "%{http_code}" "$BASE/Payroll/Export?runId=$RUN1")
chk "$CODE" "200" "خروجی اکسل لیست"
file /tmp/p24-x.xlsx | grep -q "Excel\|Zip" && ok "فایل اکسل معتبر" || bad "فایل اکسل نامعتبر"

echo "— تنظیمات محاسبه —"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID2&calcMethod=ماهانه&workSource=3&monthDays=30&hourBase=192&roundUnit=1000" "$BASE/Payroll/SettingsSave")
has "$R" '"ok":true' "ذخیره تنظیمات (کارکردی→ماهانه، اولویت ۳)"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID2&calcMethod=بد&workSource=3&monthDays=30&hourBase=192&roundUnit=0" "$BASE/Payroll/SettingsSave")
has "$R" '"ok":false' "روش نامعتبر رد شد"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID2&calcMethod=کارکردی&workSource=2&monthDays=30&hourBase=192&roundUnit=0" "$BASE/Payroll/SettingsSave")
has "$R" '"ok":true' "بازگردانی تنظیمات (کارکردی/اولویت ۲)"

echo "— پارامترها —"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID1&jYear=1405&code=پلکان_مالیات&title=x&value=0&strValue=بد JSON" "$BASE/Payroll/ParamSave")
has "$R" '"ok":false' "پلکان مالیات با JSON بد رد شد"
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&companyId=$CID1&jYear=1405&code=آزمون_پارام&title=پارامتر آزمون&value=123000" "$BASE/Payroll/ParamSave")
has "$R" '"ok":true' "ایجاد پارامتر جدید"
PID=$(python3 scripts/dbq.py "SELECT Id FROM PayrollParameters WHERE Code='آزمون_پارام' AND CompanyId='$CID1' AND JYear=1405" | python3 scripts/dbval.py)
R=$(aj -b "$CJ" -d "__RequestVerificationToken=$T2&id=$PID&companyId=$CID1&jYear=1405" "$BASE/Payroll/ParamDelete")
has "$R" '"ok":true' "حذف پارامتر آزمون"

echo "— بدون-JS fallback —"
CJ3=$(mktemp); curl -s -c "$CJ3" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' "$BASE/api/auth/login" -o /dev/null
T3=$(curl -s -b "$CJ3" -c "$CJ3" "$BASE/Payroll" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
R=$(curl -s -L -b "$CJ3" -c "$CJ3" -d "__RequestVerificationToken=$T3&companyId=$CID1&code=nojs_item&title=بدون JS&kind=مزیت&formula=1000&scopeType=همه&priority=0&order=220&active=on" "$BASE/Payroll/ItemSave")
has "$R" "بدون JS" "fallback بدون-JS — بنر TempData + ریدایرکت"
NOJS=$(python3 scripts/dbq.py "SELECT Id FROM PayItems WHERE Code='nojs_item'" | python3 scripts/dbval.py)
T3=$(curl -s -b "$CJ3" "$BASE/Payroll" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
R=$(curl -s -L -b "$CJ3" -d "__RequestVerificationToken=$T3&id=$NOJS" "$BASE/Payroll/ItemDelete")
has "$R" "حذف شد" "fallback بدون-JS — حذف با بنر"
rm -f "$CJ3"

echo "— دسترسی‌ها —"
CODE=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/Payroll/Items")
chk "$CODE" "302" "بدون نشست → ورود"
CODE=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/payroll/employees?companyId=$CID1")
chk "$CODE" "401" "API پرسنل فرمول‌ساز بدون نشست → 401"

rm -f "$CJ"
echo ""
echo "==================== نتیجه: $PASS سبز / $FAIL قرمز ===================="
[ "$FAIL" -eq 0 ]
