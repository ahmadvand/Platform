#!/bin/bash
# e2e-cards.sh — فاز ۲۹: مخزن کارت‌های شناسایی + کنترل درب + هشدار قطع دستگاه + تحلیل تردد
# ۱) دسترسی‌ها و کاتالوگ  ۲) چرخه کامل کارت: ثبت→تخصیص(دائم/موقت)→عودت/مفقود/ابطال/حذف
# ۳) اسکن کارت: ناشناس/مفقود/فعال + هشدارها  ۴) حالت درب: باز دائم/قفل دائم + دستور دستگاه
# ۵) تخلیه اضطراری منطقه + بازگشت  ۶) هشدار قطع/وصل دستگاه (sweep + webhook)
BASE=http://localhost:3000
UA="Mozilla/5.0 (e2e)"
CJ=$(mktemp /tmp/e2e29.XXXXXX)
PASS=0; FAIL=0
jq_msg() { python3 -c "import sys,json;print(json.load(sys.stdin).get('message',''))" 2>/dev/null; }
dbq() { python3 /home/z/my-project/scripts/dbq.py "$1" 2>/dev/null; }
tok() { curl -s -c $CJ -b $CJ -A "$UA" "$BASE$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
ok()  { PASS=$((PASS+1)); echo "✅ $1"; }
bad() { FAIL=$((FAIL+1)); echo "❌ $1 — got [$2]"; }
chk() { if [ "$2" = "$3" ]; then ok "$1"; else bad "$1 — expected [$3]" "$2"; fi }
AJ()  { curl -s -b $CJ -A "$UA" -H "X-Requested-With: XMLHttpRequest" "$@"; }
ST()  { curl -s -o /dev/null -w '%{http_code}' -b $CJ -A "$UA" "$1"; }

# ---------- ورود مدیر ----------
T=$(tok /Auth/Login)
curl -s -c $CJ -b $CJ -A "$UA" -d "identifier=admin" -d "secret=Admin@1234" -d "__RequestVerificationToken=$T" "$BASE/Auth/Login" -o /dev/null
chk "ورود admin" "$(ST $BASE/Cards)" "200"

# ---------- کاتالوگ و دسترسی‌ها ----------
chk "دسترسی cards.view در کاتالوگ" "$(dbq "SELECT COUNT(*) FROM Permissions WHERE Key='cards.view'")" "1"
chk "دسترسی cards.manage در کاتالوگ" "$(dbq "SELECT COUNT(*) FROM Permissions WHERE Key='cards.manage'")" "1"
chk "security → cards.manage" "$(dbq "SELECT COUNT(*) FROM RolePermissions rp JOIN Roles r ON r.Id=rp.RoleId JOIN Permissions p ON p.Id=rp.PermissionId WHERE r.Name='security' AND p.Key='cards.manage'")" "1"
chk "viewer → cards.view" "$(dbq "SELECT COUNT(*) FROM RolePermissions rp JOIN Roles r ON r.Id=rp.RoleId JOIN Permissions p ON p.Id=rp.PermissionId WHERE r.Name='viewer' AND p.Key='cards.view'")" "1"

# ---------- داده نمونه سیدر (خودترمیم) ----------
chk "کارت‌های نمونه سیدر شده" "$(dbq "SELECT CASE WHEN COUNT(*)>0 THEN '1' ELSE '0' END FROM Cards")" "1"
chk "کارت مفقود نمونه" "$(dbq "SELECT COUNT(*) FROM Cards WHERE CardNo='00997700' AND Status='گمشده'")" "1"
chk "کارت آزاد انبار" "$(dbq "SELECT COUNT(*) FROM Cards WHERE CardNo='00173800' AND Status='آزاد'")" "1"

# ---------- ثبت کارت جدید ----------
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "cardNo=E29-0001" -d "type=Mifare" -d "note=کارت آزمون فاز ۲۹" "$BASE/Cards/Create")
echo "$MSG" | jq_msg | grep -q "ثبت شد" && ok "ثبت کارت جدید" || bad "ثبت کارت جدید" "$(echo "$MSG" | jq_msg)"
CARD=$(dbq "SELECT Id FROM Cards WHERE CardNo='E29-0001'")
[ -n "$CARD" ] && ok "کارت در DB" || bad "کارت در DB" "$CARD"
chk "وضعیت اولیه آزاد" "$(dbq "SELECT Status FROM Cards WHERE Id='$CARD'")" "آزاد"

MSG=$(AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "cardNo=E29-0001" -d "type=Mifare" "$BASE/Cards/Create")
echo "$MSG" | jq_msg | grep -q "قبلاً ثبت شده" && ok "شماره تکراری رد شد" || bad "شماره تکراری" "$(echo "$MSG" | jq_msg)"

# ---------- اسکن کارت ناشناس ----------
GATE=$(dbq "SELECT Id FROM Gates WHERE Status='فعال' LIMIT 1")
[ -n "$GATE" ] && ok "گیت آزمون" || bad "گیت آزمون" "$GATE"
EV_BEFORE=$(dbq "SELECT COUNT(*) FROM AccessEvents WHERE Reason='کارت در مخزن ثبت نشده است' AND Method='کارت'")
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Access)" -d "personnelCode=NO-SUCH-999" -d "gateId=$GATE" -d "direction=ورود" -d "method=کارت" "$BASE/Access/Scan")
echo "$MSG" | jq_msg | grep -q "مخزن ثبت نشده" && ok "اسکن کارت ناشناس رد شد" || bad "اسکن ناشناس" "$(echo "$MSG" | jq_msg)"
chk "رویداد غیرمجاز ناشناس" "$(dbq "SELECT COUNT(*) FROM AccessEvents WHERE Reason='کارت در مخزن ثبت نشده است' AND Method='کارت'")" "$((EV_BEFORE + 1))"

# ---------- اسکن کارت مفقود ----------
LF_EV_BEFORE=$(dbq "SELECT COUNT(*) FROM AccessEvents WHERE Reason='کارت به‌عنوان مفقود اعلام شده است'")
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Access)" -d "personnelCode=00997700" -d "gateId=$GATE" -d "direction=ورود" -d "method=کارت" "$BASE/Access/Scan")
echo "$MSG" | jq_msg | grep -q "مفقود اعلام شده" && ok "اسکن کارت مفقود رد شد" || bad "اسکن مفقود" "$(echo "$MSG" | jq_msg)"
chk "رویداد رد مفقود" "$(dbq "SELECT COUNT(*) FROM AccessEvents WHERE Reason='کارت به‌عنوان مفقود اعلام شده است'")" "$((LF_EV_BEFORE + 1))"

# ---------- تخصیص دائم ----------
EMP=$(dbq "SELECT Id FROM Employees WHERE Status='فعال' ORDER BY PersonnelCode LIMIT 1")
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" -d "employeeId=$EMP" "$BASE/Cards/Issue")
echo "$MSG" | jq_msg | grep -q "تخصیص یافت" && ok "تخصیص دائم کارت" || bad "تخصیص دائم" "$(echo "$MSG" | jq_msg)"
chk "وضعیت فعال شد" "$(dbq "SELECT Status FROM Cards WHERE Id='$CARD'")" "فعال"

# ---------- تخصیص موقت ----------
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" -d "employeeId=$EMP" -d "days=30" "$BASE/Cards/Issue")
echo "$MSG" | jq_msg | grep -q "موقت" && ok "تخصیص موقت ۳۰ روزه" || bad "تخصیص موقت" "$(echo "$MSG" | jq_msg)"
chk "وضعیت موقت" "$(dbq "SELECT Status FROM Cards WHERE Id='$CARD'")" "موقت"
chk "انقضا ثبت شد" "$(dbq "SELECT CASE WHEN ExpiresAt IS NOT NULL THEN '1' ELSE '0' END FROM Cards WHERE Id='$CARD'")" "1"

# ---------- عودت / گارد حذف / مفقود / فعال‌سازی / ابطال / حذف ----------
AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" "$BASE/Cards/Return" > /dev/null
chk "عودت → آزاد" "$(dbq "SELECT Status FROM Cards WHERE Id='$CARD'")" "آزاد"
chk "دارنده حذف شد" "$(dbq "SELECT CASE WHEN EmployeeId IS NULL THEN '1' ELSE '0' END FROM Cards WHERE Id='$CARD'")" "1"

AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" -d "employeeId=$EMP" "$BASE/Cards/Issue" > /dev/null
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" "$BASE/Cards/Delete")
echo "$MSG" | jq_msg | grep -q "دارنده" && ok "حذف کارت دارنده‌دار رد شد" || bad "گارد حذف" "$(echo "$MSG" | jq_msg)"

AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" "$BASE/Cards/MarkLost" > /dev/null
chk "مفقود شد" "$(dbq "SELECT Status FROM Cards WHERE Id='$CARD'")" "گمشده"
LA_BEFORE=$(dbq "SELECT COUNT(*) FROM SecurityAlerts WHERE Type='کارت گمشده' AND Message LIKE '%E29-0001%'")
AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" "$BASE/Cards/MarkLost" > /dev/null
chk "هشدار مفقودی" "$(dbq "SELECT COUNT(*) FROM SecurityAlerts WHERE Type='کارت گمشده' AND Message LIKE '%E29-0001%'")" "$((LA_BEFORE + 1))"
chk "مفقود مجدد مجاز" "$(dbq "SELECT Status FROM Cards WHERE Id='$CARD'")" "گمشده"
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" "$BASE/Cards/Activate")
echo "$MSG" | jq_msg | grep -q "فعال شد" && ok "فعال‌سازی مجدد" || bad "فعال‌سازی" "$(echo "$MSG" | jq_msg)"
AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" "$BASE/Cards/Void" > /dev/null
chk "ابطال" "$(dbq "SELECT Status FROM Cards WHERE Id='$CARD'")" "باطل"
AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD" "$BASE/Cards/Delete" > /dev/null
chk "حذف نهایی" "$(dbq "SELECT COUNT(*) FROM Cards WHERE Id='$CARD'")" "0"

# ---------- حالت درب + اسکن مجاز/قفل با داده کنترل‌شده ----------
# منطقه‌ای که مجوز دسترسی دارد + گیت فعال آن
ZONE=$(dbq "SELECT ap.ZoneId FROM AccessPermissions ap JOIN Gates g ON g.ZoneId=ap.ZoneId WHERE g.Status='فعال' AND ap.EmployeeId IS NOT NULL LIMIT 1")
GATEZ=$(dbq "SELECT Id FROM Gates WHERE ZoneId='$ZONE' AND Status='فعال' LIMIT 1")
[ -n "$GATEZ" ] && ok "گیت کنترل درب" || bad "گیت کنترل درب" "$GATEZ"

# کارت دوم → پرسنلی که مجوز منطقه را دارد
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "cardNo=E29-0002" -d "type=UHF" "$BASE/Cards/Create")
EMPW=$(dbq "SELECT EmployeeId FROM AccessPermissions WHERE ZoneId='$ZONE' AND EmployeeId IS NOT NULL LIMIT 1")
CARD2=$(dbq "SELECT Id FROM Cards WHERE CardNo='E29-0002'")
AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD2" -d "employeeId=$EMPW" "$BASE/Cards/Issue" > /dev/null
chk "کارت دوم فعال" "$(dbq "SELECT Status FROM Cards WHERE Id='$CARD2'")" "فعال"

# (الف) در حالت نرمال → اسکن مجاز (مجوز فردی)
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Access)" -d "personnelCode=E29-0002" -d "gateId=$GATEZ" -d "direction=ورود" -d "method=کارت" "$BASE/Access/Scan")
echo "$MSG" | jq_msg | grep -q "اسکن مجاز" && ok "اسکن کارت فعال مجاز (نرمال)" || bad "اسکن مجاز نرمال" "$(echo "$MSG" | jq_msg)"
chk "رویداد مجاز با نگاشت کارت" "$(dbq "SELECT Result FROM AccessEvents WHERE GateId='$GATEZ' AND Method='کارت' ORDER BY Timestamp DESC, RowId DESC LIMIT 1")" "مجاز"

# (ب) قفل دائم → همان کارت مجاز هم رد می‌شود + دستور disable_device
DEV=$(dbq "SELECT DeviceId FROM Gates WHERE Id='$GATEZ'")
DD_BEFORE=$(dbq "SELECT COUNT(*) FROM DeviceCommands WHERE DeviceId='$DEV' AND Type='disable_device' AND Status IN ('pending','delivered')")
AJ -d "__RequestVerificationToken=$(tok /Access)" -d "id=$GATEZ" -d "state=قفل دائم" "$BASE/Access/SetDoorState" > /dev/null
chk "حالت قفل دائم" "$(dbq "SELECT DoorState FROM Gates WHERE Id='$GATEZ'")" "قفل دائم"
MSG=$(AJ -d "__RequestVerificationToken=$(tok /Access)" -d "personnelCode=E29-0002" -d "gateId=$GATEZ" -d "direction=ورود" -d "method=کارت" "$BASE/Access/Scan")
echo "$MSG" | jq_msg | grep -q "قفل دائم" && ok "اسکن در قفل دائم رد شد" || bad "قفل دائم رد" "$(echo "$MSG" | jq_msg)"
if [ "$DEV" != "" ] && [ "$DEV" != "None" ]; then
  chk "دستور disable_device صف شد" "$(dbq "SELECT COUNT(*) FROM DeviceCommands WHERE DeviceId='$DEV' AND Type='disable_device' AND Status IN ('pending','delivered')")" "$((DD_BEFORE + 1))"
fi

# (ج) باز دائم → عبور آزاد حتی بدون مجوز
AJ -d "__RequestVerificationToken=$(tok /Access)" -d "id=$GATEZ" -d "state=باز دائم" "$BASE/Access/SetDoorState" > /dev/null
chk "حالت باز دائم" "$(dbq "SELECT DoorState FROM Gates WHERE Id='$GATEZ'")" "باز دائم"
NOPERM=$(dbq "SELECT e.PersonnelCode FROM Employees e WHERE e.Status='فعال' AND NOT EXISTS (SELECT 1 FROM AccessPermissions ap WHERE ap.EmployeeId=e.Id AND ap.ZoneId='$ZONE') AND NOT EXISTS (SELECT 1 FROM AccessGroupMembers m JOIN AccessGroupGates gg ON gg.GroupId=m.GroupId WHERE m.EmployeeId=e.Id AND gg.GateId='$GATEZ') LIMIT 1")
if [ -n "$NOPERM" ]; then
  MSG=$(AJ -d "__RequestVerificationToken=$(tok /Access)" -d "personnelCode=$NOPERM" -d "gateId=$GATEZ" -d "direction=ورود" -d "method=اثر انگشت" "$BASE/Access/Scan")
  echo "$MSG" | jq_msg | grep -q "اسکن مجاز" && ok "باز دائم = عبور آزاد (بدون مجوز)" || bad "باز دائم" "$(echo "$MSG" | jq_msg)"
else
  ok "باز دائم (پرسنل بدون مجوز یافت نشد — رد شد)"
fi

# بازگشت به نرمال
AJ -d "__RequestVerificationToken=$(tok /Access)" -d "id=$GATEZ" -d "state=نرمال" "$BASE/Access/SetDoorState" > /dev/null
chk "بازگشت نرمال" "$(dbq "SELECT DoorState FROM Gates WHERE Id='$GATEZ'")" "نرمال"

# ---------- تخلیه اضطراری منطقه ----------
EO_BEFORE=$(dbq "SELECT COUNT(*) FROM SecurityAlerts WHERE Type='تخلیه اضطراری' AND Severity='بحرانی'")
AJ -d "__RequestVerificationToken=$(tok /Access)" -d "zoneId=$ZONE" "$BASE/Access/EmergencyOpen" > /dev/null
chk "همه گیت‌های فعال منطقه باز دائم" "$(dbq "SELECT COUNT(*) FROM Gates WHERE ZoneId='$ZONE' AND Status='فعال' AND DoorState='باز دائم'")" "$(dbq "SELECT COUNT(*) FROM Gates WHERE ZoneId='$ZONE' AND Status='فعال'")"
chk "هشدار تخلیه بحرانی" "$(dbq "SELECT COUNT(*) FROM SecurityAlerts WHERE Type='تخلیه اضطراری' AND Severity='بحرانی'")" "$((EO_BEFORE + 1))"
AJ -d "__RequestVerificationToken=$(tok /Access)" -d "zoneId=$ZONE" "$BASE/Access/RestoreNormal" > /dev/null
chk "بازگشت همه به نرمال" "$(dbq "SELECT COUNT(*) FROM Gates WHERE ZoneId='$ZONE' AND DoorState!='نرمال'")" "0"

# ---------- هشدار قطع/وصل دستگاه ----------
curl -s -b $CJ -A "$UA" "$BASE/Devices/Live" > /dev/null   # sweep تنبلی
chk "هشدار قطع دستگاه ثبت شد" "$(dbq "SELECT CASE WHEN COUNT(*)>0 THEN '1' ELSE '0' END FROM SecurityAlerts WHERE Type='قطع دستگاه' AND Source='دستگاه‌های تردد'")" "1"
SN=$(dbq "SELECT Serial FROM Devices WHERE Mode='آفلاین' AND Active=1 LIMIT 1")
if [ -n "$SN" ]; then
  TOKEN=$(dbq "SELECT WebhookToken FROM Devices WHERE Serial='$SN'")
  curl -s -H "X-Device-Token: $TOKEN" "$BASE/api/devices/webhook?SN=$SN&consume=false" > /dev/null
  chk "وصل دستگاه → آنلاین" "$(dbq "SELECT Mode FROM Devices WHERE Serial='$SN'")" "آنلاین"
  chk "هشدار قطع همان دستگاه حل شد" "$(dbq "SELECT COUNT(*) FROM SecurityAlerts WHERE Type='قطع دستگاه' AND RefId=(SELECT Id FROM Devices WHERE Serial='$SN') AND Resolved=0")" "0"
fi

# ---------- پاکسازی کارت آزمون دوم ----------
AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD2" "$BASE/Cards/Return" > /dev/null
AJ -d "__RequestVerificationToken=$(tok /Cards)" -d "id=$CARD2" "$BASE/Cards/Delete" > /dev/null
chk "پاکسازی کارت آزمون" "$(dbq "SELECT COUNT(*) FROM Cards WHERE CardNo='E29-0002'")" "0"

# ---------- صفحات ----------
chk "تب تحلیل تردد ۲۰۰" "$(ST "$BASE/Access?tab=traffic")" "200"
chk "صفحه کارت‌ها ۲۰۰" "$(ST $BASE/Cards)" "200"

echo ""
echo "================ نتیجه فاز ۲۹ ================"
echo "✅ PASS: $PASS   ❌ FAIL: $FAIL"
[ $FAIL -eq 0 ] && echo "ALL GREEN 🎉" || echo "NEEDS FIX"
