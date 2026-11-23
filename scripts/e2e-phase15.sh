#!/usr/bin/env bash
# ============================================================
# فاز ۱۵ — گزارش‌ساز و داشبورد تحلیلی
# روند ماهانه، KPI واحدها، گزارش سفارشی، خروجی PDF (QuestPDF) و اکسل
# اجرا: bash scripts/e2e-phase15.sh
# ============================================================
set -u
cd "$(dirname "$0")/.."
BASE="http://localhost:3000"
PASS=0; FAIL=0
ck() { if [ "$1" = "1" ]; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }

CJ=/tmp/p15-cookies.txt; rm -f "$CJ"
GET()  { curl -s -b "$CJ" -c "$CJ" "$BASE$1" --max-time 30; }
curl -s -c "$CJ" -X POST "$BASE/api/auth/login" -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' > /dev/null

echo "── ۱) داشبورد تحلیلی"
r=$(GET /Reports)
code=$(curl -s -b "$CJ" -o /dev/null -w "%{http_code}" "$BASE/Reports")
ck "$([ "$code" = "200" ] && echo 1 || echo 0)" "GET /Reports → 200"
echo "$r" | grep -q "روند ماهانه" && ck 1 "کارت روند ماهانه" || ck 0 "کارت روند ماهانه"
echo "$r" | grep -q "KPI واحدهای سازمانی" && ck 1 "جدول KPI واحدها" || ck 0 "جدول KPI واحدها"
echo "$r" | grep -q "trendChart" && ck 1 "نمودار روند (canvas)" || ck 0 "نمودار روند"

echo "── ۲) گزارش سفارشی"
code=$(curl -s -b "$CJ" -o /dev/null -w "%{http_code}" "$BASE/Reports/Custom")
ck "$([ "$code" = "200" ] && echo 1 || echo 0)" "GET /Reports/Custom → 200"
r=$(GET "/Reports/Custom?status=%D8%BA%D8%A7%DB%8C%D8%A8")
echo "$r" | grep -q "نتایج" && ck 1 "فیلتر وضعیت (غایب) اعمال شد" || ck 0 "فیلتر وضعیت"
FROM=$(date -d "-30 day" +%F); TO=$(date +%F)
code=$(curl -s -b "$CJ" -o /dev/null -w "%{http_code}" "$BASE/Reports/Custom?from=$FROM&to=$TO")
ck "$([ "$code" = "200" ] && echo 1 || echo 0)" "فیلتر بازه ۳۰ روزه → 200"

echo "── ۳) خروجی اکسل"
f=/tmp/p15-report.xlsx
curl -s -b "$CJ" -o "$f" -w "%{http_code}" "$BASE/Reports/CustomXlsx?from=$FROM&to=$TO" > /tmp/p15-xlsx-code
ck "$([ "$(cat /tmp/p15-xlsx-code)" = "200" ] && echo 1 || echo 0)" "CustomXlsx → 200"
r=$(python3 - "$f" <<'EOF'
import sys, zipfile
try:
    z = zipfile.ZipFile(sys.argv[1])
    ok = 'xl/workbook.xml' in z.namelist()
except Exception:
    ok = False
print(1 if ok else 0)
EOF
)
ck "$r" "فایل xlsx معتبر (zip با workbook.xml)"

echo "── ۴) خروجی PDF تحلیلی (QuestPDF + فونت وزیرمتن)"
f=/tmp/p15-report.pdf
code=$(curl -s -b "$CJ" -o "$f" -w "%{http_code}" "$BASE/Reports/AnalyticsPdf")
ck "$([ "$code" = "200" ] && echo 1 || echo 0)" "AnalyticsPdf → 200"
head -c 4 "$f" | grep -q "%PDF" && ck 1 "امضای %PDF" || ck 0 "امضای %PDF"
sz=$(stat -c%s "$f" 2>/dev/null || echo 0)
ck "$([ "$sz" -gt 20000 ] && echo 1 || echo 0)" "حجم فایل (${sz} بایت — فونت جاسازی‌شده)"

echo "── ۵) دسترسی‌ها"
CJ2=/tmp/p15-guard.txt; rm -f "$CJ2"
curl -s -c "$CJ2" -X POST "$BASE/api/auth/login" -H "Content-Type: application/json" -d '{"username":"guard","password":"Guard@1234"}' > /dev/null
code=$(curl -s -b "$CJ2" -o /dev/null -w "%{http_code}" "$BASE/Reports")
ck "$([ "$code" = "302" ] || [ "$code" = "403" ] && echo 1 || echo 0)" "حراست (بدون reports.view) → رد دسترسی ($code)"
CJ3=/tmp/p15-op.txt; rm -f "$CJ3"
curl -s -c "$CJ3" -X POST "$BASE/api/auth/login" -H "Content-Type: application/json" -d '{"username":"operator","password":"Oper@1234"}' > /dev/null
code=$(curl -s -b "$CJ3" -o /dev/null -w "%{http_code}" "$BASE/Reports")
ck "$([ "$code" = "200" ] && echo 1 || echo 0)" "اپراتور (با reports.view) → 200"

echo "── ۶) حسابرسی خروجی‌ها"
n=$(python3 scripts/dbq.py --json "SELECT COUNT(*) AS n FROM AuditLogs WHERE Action='export' AND (Entity='report_pdf' OR Entity='report_custom')" 2>/dev/null | python3 -c "import sys,json;print(json.load(sys.stdin)[0]['n'])" 2>/dev/null || echo 0)
ck "$([ "$n" -ge 2 ] && echo 1 || echo 0)" "رویدادهای export در حسابرسی ($n)"

echo "── ۷) منو"
r=$(curl -s -b "$CJ" "$BASE/")
echo "$r" | grep -q "گزارش‌ساز تحلیلی" && ck 1 "آیتم منوی «گزارش‌ساز تحلیلی»" || ck 0 "آیتم منو"

echo ""
echo "═══════ نتیجه فاز ۱۵: $PASS سبز، $FAIL قرمز ═══════"
[ "$FAIL" = "0" ]
