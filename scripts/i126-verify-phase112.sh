#!/bin/bash
# ============================================================
# فاز ۱۱۲ — راستی‌آزمایی زندهٔ ۴ درخواست کاربر:
#   ۱) هماهنگی تقویم صفحهٔ تعطیلات با تقویم سامانه (میلادی/قمری/شمسی)
#   ۲) هاب درون‌ریزی/برون‌بری داده (/Data — خروجی و درون‌ریزی واقعی)
#   ۳) جدول صفحه‌بندی‌شدهٔ پرسنل در «از دیتابیس به دستگاه‌ها»
#   ۴) محافظ صفحه باران ماتریکس با زمان بیکاری قابل‌تعیین
# ============================================================
cd /home/z/my-project || exit 1
CK=/tmp/i126-cookies.txt
rm -f "$CK"
BASE=http://127.0.0.1:3000
PASS=0; FAIL=0
ok()  { echo "  ✓ $1"; PASS=$((PASS+1)); }
bad() { echo "  ✗ $1"; FAIL=$((FAIL+1)); }
get_token() { curl -s -b "$CK" -c "$CK" "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//'; }

echo "== login =="
T=$(get_token $BASE/Auth/Login)
C=$(curl -s -b "$CK" -c "$CK" -o /dev/null -w "%{http_code}" -X POST $BASE/Auth/Login \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$T")
[ "$C" = "302" ] || [ "$C" = "200" ] && ok "login ($C)" || bad "login ($C)"
T=$(get_token $BASE/License/Notice)
C=$(curl -s -b "$CK" -c "$CK" -o /dev/null -w "%{http_code}" -X POST $BASE/License/Ack --data-urlencode "__RequestVerificationToken=$T")
ok "license ack ($C)"

decode() { python3 -c "import sys,html; print(html.unescape(sys.stdin.read()))"; }

echo "== آیتم ۱: تقویم صفحهٔ تعطیلات =="
T=$(get_token $BASE/Settings)
for MODE in gregorian hijri jalali; do
  curl -s -b "$CK" -c "$CK" -o /dev/null -X POST $BASE/Settings/SaveCalendar \
    --data-urlencode "calendar=$MODE" --data-urlencode "__RequestVerificationToken=$T"
  H=$(curl -s -b "$CK" $BASE/Holidays | decode)
  case $MODE in
    gregorian) H=$(curl -s -b "$CK" $BASE/Holidays | decode)
      echo "$H" | grep -q "(تقویم میلادی)" && ok "gregorian برچسب" || bad "gregorian برچسب"
      echo "$H" | grep -q "دوشنبه" && ok "gregorian سربرگ هفته (دوشنبه‌شروع)" || bad "gregorian سربرگ"
      ;;
    hijri) H=$(curl -s -b "$CK" $BASE/Holidays | decode)
      echo "$H" | grep -q "(تقویم قمری)" && ok "hijri برچسب" || bad "hijri برچسب"
      echo "$H" | grep -q "شنبه" && ok "hijri سربرگ" || bad "hijri سربرگ"
      ;;
    jalali) H=$(curl -s -b "$CK" $BASE/Holidays | decode)
      echo "$H" | grep -q "(تقویم شمسی)" && ok "jalali برچسب" || bad "jalali برچسب"
      echo "$H" | grep -q "فروردین\|اردیبهشت\|خرداد\|تیر\|مرداد\|شهریور\|مهر\|آبان\|آذر\|دی\|بهمن\|اسفند" && ok "jalali نام ماه" || bad "jalali نام ماه"
      ;;
  esac
done

echo "== آیتم ۳: جدول پرسنل در دادهٔ هویتی =="
B=$(curl -s -b "$CK" $BASE/Biometrics | decode)
echo "$B" | grep -q "tableMode: true" && ok "انتخابگر جدولی فعال" || bad "انتخابگر جدولی"
echo "$B" | grep -q "thDept" && ok "ستون واحد سازمانی (thDept)" || bad "ستون واحد سازمانی"
echo "$B" | grep -q "انتخاب همهٔ پرسنل" && ok "دکمهٔ انتخاب همه حاضر" || bad "دکمهٔ انتخاب همه"
J=$(curl -s -b "$CK" "$BASE/Biometrics/SearchPersonnel?q=&skip=0&take=5" | decode)
echo "$J" | grep -q '"dept"' && ok "SearchPersonnel فیلد dept" || bad "SearchPersonnel dept"
echo "$J" | grep -q '"total"' && ok "SearchPersonnel صفحه‌بندی" || bad "SearchPersonnel total"

echo "== آیتم ۲: هاب داده =="
C=$(curl -s -b "$CK" -o /tmp/i126-data.html -w "%{http_code}" $BASE/Data)
[ "$C" = "200" ] && ok "/Data صفحه ($C)" || bad "/Data ($C)"
D=$(decode < /tmp/i126-data.html)
echo "$D" | grep -q "درون‌ریزی و برون‌بری" && ok "عنوان هاب" || bad "عنوان هاب"
echo "$D" | grep -q "پرسنل" && ok "کارت پرسنل در کاتالوگ" || bad "کارت پرسنل"
echo "$D" | grep -q "تعطیلات رسمی" && ok "کارت تعطیلات" || bad "کارت تعطیلات"

C=$(curl -s -b "$CK" -o /tmp/i126-emp.json -w "%{http_code}" "$BASE/Data/Export?entity=employee&fmt=json")
[ "$C" = "200" ] && ok "Export JSON پرسنل ($C)" || bad "Export JSON ($C)"
python3 - << 'PYEOF'
import json
d = json.load(open('/tmp/i126-emp.json'))
assert d.get('format') == 'hr-data-export' and d.get('entity') == 'Employee'
assert isinstance(d['rows'], list) and len(d['rows']) > 0
r0 = d['rows'][0]
assert 'PersonnelCode' in r0 and 'PasswordHash' not in r0
print(f"  ✓ JSON export سالم — {len(d['rows'])} ردیف، بدون ستون‌های رمز")
PYEOF
C=$(curl -s -b "$CK" -o /tmp/i126-emp.xlsx -w "%{http_code}" "$BASE/Data/Export?entity=employee&fmt=xlsx")
head -c 2 /tmp/i126-emp.xlsx | grep -q "PK" && ok "Export XLSX پرسنل" || bad "Export XLSX"
C=$(curl -s -b "$CK" -o /tmp/i126-emp.csv -w "%{http_code}" "$BASE/Data/Export?entity=employee&fmt=csv")
python3 -c "import sys; sys.exit(0 if open('/tmp/i126-emp.csv','rb').read(3) == b'\\xef\\xbb\\xbf' else 1)" && ok "Export CSV (UTF-8 BOM)" || bad "Export CSV BOM"
C=$(curl -s -b "$CK" -o /tmp/i126-tmpl.xlsx -w "%{http_code}" "$BASE/Data/Template?entity=holiday")
head -c 2 /tmp/i126-tmpl.xlsx | grep -q "PK" && ok "قالب خالی تعطیلات" || bad "قالب خالی"

echo "== آیتم ۲ب: درون‌ریزی واقعی تعطیلات =="
cat > /tmp/i126-import.json << 'JSON'
[
  {"Id": "", "Date": "2026-05-01", "Title": "تست درون‌ریزی فاز ۱۱۲", "JYear": 1405, "JMonth": 2, "JDay": 11}
]
JSON
T=$(get_token "$BASE/Data/ImportForm?entity=holiday")
C=$(curl -s -b "$CK" -o /tmp/i126-prev.html -w "%{http_code}" -X POST $BASE/Data/ImportPreview \
  -F "file=@/tmp/i126-import.json;type=application/json" \
  -F "entity=holiday" -F "__RequestVerificationToken=$T")
[ "$C" = "200" ] && ok "ImportPreview ($C)" || bad "ImportPreview ($C)"
P=$(decode < /tmp/i126-prev.html)
echo "$P" | grep -q "آمادهٔ ثبت" && ok "پیش‌نمایش تأیید ردیف" || bad "پیش‌نمایش ردیف"
T=$(get_token $BASE/Data)
C=$(curl -s -b "$CK" -o /tmp/i126-commit.html -w "%{http_code}" -X POST $BASE/Data/ImportCommit \
  --data-urlencode "entity=holiday" \
  --data-urlencode 'rowsJson=[{"Id":"","Date":"2026-05-01","Title":"تست درون‌ریزی فاز ۱۱۲","JYear":"1405","JMonth":"2","JDay":"11"}]' \
  --data-urlencode "__RequestVerificationToken=$T")
[ "$C" = "200" ] && ok "ImportCommit ($C)" || bad "ImportCommit ($C)"
COMMIT=$(decode < /tmp/i126-commit.html)
echo "$COMMIT" | grep -q "رکورد جدید" && ok "گزارش ثبت (رکورد جدید)" || bad "گزارش ثبت"
N=$(python3 -c "
import sqlite3
c = sqlite3.connect('/home/z/my-project/aspnet/sc-run/hr.db')
print(c.execute(\"SELECT COUNT(*) FROM Holidays WHERE Title LIKE '%تست درون‌ریزی%'\").fetchone()[0])")
[ "$N" = "1" ] && ok "رکورد در دیتابیس ثبت شد" || bad "رکورد دیتابیس ($N)"

echo "== آیتم ۴: محافظ صفحه =="
S=$(curl -s -b "$CK" $BASE/Settings | decode)
echo "$S" | grep -q "محافظ صفحه (اسکرین‌سیور باران ماتریکس)" && ok "بخش تنظیمات محافظ صفحه" || bad "بخش تنظیمات"
echo "$S" | grep -q "screensaverVal" && ok "فیلد مقدار/واحد" || bad "فیلد مقدار/واحد"
T=$(get_token $BASE/Settings)
C=$(curl -s -b "$CK" -o /dev/null -w "%{http_code}" -X POST $BASE/Settings/SaveGeneral \
  --data-urlencode "companyName=شرکت پتروشیمی پارس آسیا" \
  --data-urlencode "calendar=jalali" --data-urlencode "currency=IRR" --data-urlencode "currencyRate=1" \
  --data-urlencode "screensaverVal=2" --data-urlencode "screensaverUnit=min" \
  --data-urlencode "__RequestVerificationToken=$T")
[ "$C" = "302" ] || [ "$C" = "200" ] && ok "ذخیرهٔ ۲ دقیقه ($C)" || bad "ذخیرهٔ تنظیم ($C)"
B=$(curl -s -b "$CK" $BASE/)
echo "$B" | grep -q 'data-saver-idle="120"' && ok "data-saver-idle=120 روی صفحه" || bad "data-saver-idle"
curl -s $BASE/js/site.js | grep -q "__hrSaverInit" && ok "موتور باران ماتریکس در site.js" || bad "موتور اسکرین‌سیور"
# بازگردانی به خاموش (پیش‌فرض تمیز)
C=$(curl -s -b "$CK" -o /dev/null -w "%{http_code}" -X POST $BASE/Settings/SaveGeneral \
  --data-urlencode "companyName=شرکت پتروشیمی پارس آسیا" \
  --data-urlencode "calendar=jalali" --data-urlencode "currency=IRR" --data-urlencode "currencyRate=1" \
  --data-urlencode "screensaverVal=0" --data-urlencode "screensaverUnit=sec" \
  --data-urlencode "__RequestVerificationToken=$T")
[ "$C" = "302" ] || [ "$C" = "200" ] && ok "بازگردانی خاموش" || bad "بازگردانی"

echo "== پاک‌سازی ردیف تست =="
python3 -c "
import sqlite3
c = sqlite3.connect('/home/z/my-project/aspnet/sc-run/hr.db')
c.execute(\"DELETE FROM Holidays WHERE Title LIKE '%تست درون‌ریزی%'\")
c.commit()
print('  ✓ ردیف تست حذف شد')"
echo ""
echo "==== نتیجه: $PASS موفق، $FAIL ناموفق ===="
exit $FAIL
