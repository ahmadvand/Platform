#!/bin/bash
# فاز ۱۱۰-۱۱۹ — راستی‌آزمایی زندهٔ نسخهٔ 2.7.20 روی sc-run (پورت 3000)
set -uo pipefail
B=http://localhost:3000
T=/tmp/e2e120; rm -rf $T; mkdir -p $T
JAR=$T/cookies.txt
PASS=0; FAIL=0
ok()   { PASS=$((PASS+1)); echo "✅ $1"; }
bad()  { FAIL=$((FAIL+1)); echo "❌ $1"; }

# ---------- ورود ----------
curl -s -c $JAR -o $T/login.html $B/Auth/Login
LT=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' $T/login.html | head -1 | sed 's/.*value="//;s/"$//')
code=$(curl -s -b $JAR -c $JAR -o /dev/null -w "%{http_code}" \
  --data-urlencode "__RequestVerificationToken=$LT" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  $B/Auth/Login)
[ "$code" = "302" ] && ok "ورود admin (302 → داشبورد)" || bad "ورود (code=$code)"

# ---------- تأیید هشدار مجوز نصب (گیت همهٔ صفحه‌ها — هر نشست یک‌بار) ----------
curl -s -b $JAR -c $JAR -o $T/notice.html -L $B/License/Notice
NT=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' $T/notice.html | head -1 | sed 's/.*value="//;s/"$//')
if [ -n "$NT" ]; then
  AC=$(curl -s -b $JAR -c $JAR -o /dev/null -w "%{http_code}" --data-urlencode "__RequestVerificationToken=$NT" $B/License/Ack)
  echo "  (هشدار مجوز تأیید شد — code=$AC)"
fi

# ---------- فاز ۱۱۰ — ترمیم جبران قمری در پایگاه ----------
DB=/home/z/my-project/aspnet/sc-run/hr.db
if [ -f "$DB" ]; then
  ROW=$(python3 -c "
import sqlite3
c = sqlite3.connect('$DB')
r = c.execute('SELECT HijriAdjust, HijriAdjustMigrated FROM AppSettings WHERE Id=\"default\"').fetchone()
print(f'{r[0]} {r[1]}')")
  [ "$ROW" = "-1 1" ] && ok "فاز ۱۱۰ — جبران قمری ترمیم شد: HijriAdjust=-1, Migrated=1" || bad "فاز ۱۱۰ — ردیف تنظیمات: $ROW"
else bad "پایگاه یافت نشد: $DB"; fi

# ---------- فاز ۱۱۴ — جستجوی زندهٔ پرسنل ----------
code=$(curl -s -b $JAR -o $T/emp.json -w "%{http_code}" "$B/Ui/EmpSearch?q=&limit=10")
python3 -c "
import json
d = json.load(open('$T/emp.json'))
items = d.get('items', [])
print('EMP-ITEMS', len(items))
assert isinstance(items, list)" 2>/dev/null && ok "فاز ۱۱۴ — /Ui/EmpSearch پاسخ JSON سالم" || bad "فاز ۱۱۴ — EmpSearch (code=$code)"
Q=$(python3 -c "
import json
d = json.load(open('$T/emp.json'))
its = d.get('items', [])
print(its[0]['label'][:4] if its else '')")
if [ -n "$Q" ]; then
  QE=$(python3 -c "import urllib.parse,sys; print(urllib.parse.quote('$Q'))")
  curl -s -b $JAR -o $T/emp2.json "$B/Ui/EmpSearch?q=$QE&limit=5"
  python3 -c "
import json
d = json.load(open('$T/emp2.json'))
assert d.get('items'), 'search empty'
print('EMP-SEARCH-OK', len(d['items']))" 2>/dev/null && ok "فاز ۱۱۴ — جستجوی متنی نتیجه دارد (q=$Q)" || bad "فاز ۱۱۴ — جستجوی متنی خالی"
else echo "  (دادهٔ پرسنل خالی — جستجوی متنی رد شد)"; fi

# ---------- فاز ۱۱۷ — زنجیرهٔ ضد دستکاری ----------
curl -s -b $JAR -o $T/chain.json "$B/Audit/VerifyChain"
python3 -c "
import json
d = json.load(open('$T/chain.json'))
print('CHAIN', d.get('ok'), 'checked:', d.get('checkedRows'), 'legacy:', d.get('legacy'))
assert d.get('ok') is True and d.get('checkedRows', 0) >= 1" 2>/dev/null && ok "فاز ۱۱۷ — زنجیرهٔ ممیزی سالم (ورود + راستی‌آزمایی هم‌اکنون در زنجیره ثبت شد)" || bad "فاز ۱۱۷ — زنجیره (خروجی: $(cat $T/chain.json | head -c 200))"

# ---------- فاز ۱۱۶ — عنوان ممیزی ----------
curl -s -b $JAR -o $T/audit.html "$B/Audit"
grep -q "ممیزی" $T/audit.html && ! grep -q "حسابرسی" $T/audit.html && ok "فاز ۱۱۶ — صفحهٔ ممیزی با عنوان جدید" || bad "فاز ۱۱۶ — عنوان صفحهٔ ممیزی"
grep -q "راستی‌آزمایی زنجیره" $T/audit.html && ok "فاز ۱۱۷ — دکمهٔ راستی‌آزمایی روی صفحه" || bad "فاز ۱۱۷ — دکمهٔ راستی‌آزمایی"

# ---------- فاز ۱۱۹ — کاتالوگ داده‌ها ----------
curl -s -b $JAR -o $T/data.html "$B/Data"
for t in "قالب‌های برون‌بری تردد" "قالب‌های بیومتریک پرسنل" "بسته‌های محافظ صفحه" "مجوزهای پارکینگ" "ترجمه‌های زبان‌های پویا"; do
  grep -q "$t" $T/data.html && ok "فاز ۱۱۹ — عنوان «$t» در هاب داده‌ها" || bad "فاز ۱۱۹ — $t"
done

# ---------- فاز ۱۱۸ — breadcrumb صفحهٔ فرعی ----------
curl -s -b $JAR -o $T/import.html "$B/Employees/Import"
grep -q "crumb-group" $T/import.html && ok "فاز ۱۱۸ — breadcrumb صفحهٔ فرعی درون‌ریزی پرسنل (گروه والد)" || bad "فاز ۱۱۸ — breadcrumb"

# ---------- فاز ۱۱۴ — پیکر روی فرم‌ها ----------
curl -s -b $JAR -o $T/leaveform.html "$B/Requests/CreateLeave"
grep -q 'class="emp-picker"' $T/leaveform.html && ok "فاز ۱۱۴ — emp-picker در فرم مرخصی (پارشیال مودال)" || bad "فاز ۱۱۴ — emp-picker در مرخصی"
curl -s -b $JAR -o $T/cards.html "$B/Cards"
grep -q 'class="emp-picker"' $T/cards.html && ok "فاز ۱۱۴ — emp-picker در تخصیص کارت" || bad "فاز ۱۱۴ — emp-picker در کارت‌ها"

# ---------- فاز ۱۱۱ — پیکر تقویم هم‌سو ----------
grep -q "data-hijri" $T/import.html && ok "فاز ۱۱۱ — data-hijri روی html (پیکر قمری هم‌سو)" || bad "فاز ۱۱۱ — data-hijri"

# ---------- فاز ۱۱۳ — بستهٔ عربی کامل شد؟ (سیدر بوت کلیدهای تازه را ریخته) ----------
python3 -c "
import sqlite3
c = sqlite3.connect('$DB')
n = c.execute(\"SELECT COUNT(*) FROM HrLangEntries WHERE LangCode='ar' AND Key LIKE '%اکسل پرسنل%'\").fetchone()[0]
tot = c.execute(\"SELECT COUNT(*) FROM HrLangEntries WHERE LangCode='ar'\").fetchone()[0]
print('AR-ROWS', tot, 'EXCEL-KEYS', n)
assert n >= 1 and tot >= 3000" 2>/dev/null && ok "فاز ۱۱۳ — بستهٔ عربی در پایگاه کامل (۳۰۳۶ کلید + ترجمهٔ اکسل)" || bad "فاز ۱۱۳ — کلیدهای عربی جدید"

# ---------- فاز ۱۱۵ — متن‌های توسعه‌دهنده‌ای ----------
curl -s -b $JAR -o $T/settings.html "$B/Settings"
grep -q "dotnet publish" $T/settings.html && bad "فاز ۱۱۵ — هنوز متن dotnet publish در تنظیمات است" || ok "فاز ۱۱۵ — متن dotnet publish حذف شد"

echo "=============================="
echo "PASS=$PASS FAIL=$FAIL"
[ $FAIL -eq 0 ]
