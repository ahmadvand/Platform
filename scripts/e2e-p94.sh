#!/usr/bin/env bash
# ======================================================================
# فاز ۹۴ — تست E2E روی نمونهٔ زندهٔ پورت 3000:
#   T1) تب «پایگاه داده» تنظیمات = اطلاعات واقعی (مسیر/حجم/جدول/ردیف)
#   T2) صفحهٔ به‌روزرسانی = دستور ویندوزی ساخت بسته
#   T3) مرکز دانلود = بستهٔ ابزار ویندوزی
#   T4) سوییچ زبان en → تب پایگاه داده هنوز سالم (i18n)
# ======================================================================
set -u
T=/home/z/my-project/scripts/.p94tmp; mkdir -p $T; rm -f $T/*
BASE=http://127.0.0.1:3000
JAR=$T/cookies94.txt

echo "== Login =="
curl -s -c $JAR -o $T/login.html $BASE/Auth/Login
LT=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' $T/login.html | head -1 | sed 's/.*value="//;s/"$//')
curl -s -b $JAR -c $JAR -o /dev/null -w "  POST /Auth/Login → %{http_code} → %{redirect_url}\n" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "__RequestVerificationToken=$LT" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  $BASE/Auth/Login

# فاز ۹۵ — اگر مجوز در حالت warn است، گیتِ Notice ابتدا Ack نشستی می‌خواهد (رفتار عادی محصول)
curl -s -b $JAR -c $JAR -o $T/notice.html $BASE/License/Notice
if grep -q 'action="/License/Ack"' $T/notice.html; then
  NT=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' $T/notice.html | head -1 | sed 's/.*value="//;s/"$//')
  curl -s -b $JAR -c $JAR -o /dev/null -w "  POST /License/Ack → %{http_code}\n" \
    --data-urlencode "__RequestVerificationToken=$NT" $BASE/License/Ack
fi

echo "== T1) Settings — تب پایگاه داده: اطلاعات واقعی =="
curl -s -b $JAR -c $JAR -o $T/settings.html -w "  GET /Settings → %{http_code}\n" $BASE/Settings
for needle in "اطلاعات واقعی ثبت‌شده" "منبع پیکربندی" "رشتهٔ اتصال" "مسیر واقعی فایل بانک" "حجم فایل بانک" "حالت ژورنال" "تعداد جدول‌ها" "پررکوردترین جدول‌ها" "اتصال زنده برقرار است" "SQLite"; do
  c=$(grep -c "$needle" $T/settings.html)
  [ "$c" -gt 0 ] && echo "  ✓ $needle ($c)" || echo "  ✗ MISSING: $needle"
done
echo "  --- مقادیر واقعی خوانده‌شده از HTML ---"
grep -o 'Data Source=[^<]*' $T/settings.html | head -1
grep -o '/home/z/my-project/aspnet/src/Platform.Web/hr\.db' $T/settings.html | head -1
grep -oE '[0-9٫۰-۹]+ (مگابایت|کیلوبایت)' $T/settings.html | head -2
grep -oE 'wal|delete' $T/settings.html | head -1
grep -o 'SQLite [0-9.]*' $T/settings.html | head -1
grep -oE 'المپ|Employees|Users|AttRecords|AuditLogs' $T/settings.html | sort | uniq -c | head -5
echo "  --- رمز پوشانده شده؟ (نباید Password واقعی باشد) ---"
grep -o 'Password=[^;<"]*' $T/settings.html | head -2 || echo "  (رمزی در HTML نیست)"

echo "== T2) Updates — دستور ویندوزی =="
curl -s -b $JAR -c $JAR -o $T/updates.html -w "  GET /Updates → %{http_code}\n" $BASE/Updates
grep -c "make-update-package.ps1" $T/updates.html | xargs echo "  make-update-package.ps1 count:"
grep -c "make-update-package.sh" $T/updates.html | xargs echo "  make-update-package.sh count:"

echo "== T3) مرکز دانلود =="
curl -s -o $T/dl.html -w "  GET /dl/index.html → %{http_code}\n" $BASE/dl/index.html
grep -c "HR-Platform-Windows-Tools-2.7.5.zip" $T/dl.html | xargs echo "  windows-tools in index:"
curl -s -o $T/wt.zip -w "  GET /dl/HR-Platform-Windows-Tools-2.7.5.zip → %{http_code} (%{size_download} bytes)\n" $BASE/dl/HR-Platform-Windows-Tools-2.7.5.zip
python3 -c "import zipfile; z=zipfile.ZipFile('$T/wt.zip'); print('  zip OK,', len(z.namelist()), 'entries:', z.testzip() is None)"

echo "== T4) سوییچ زبان en → تب پایگاه داده سالم =="
curl -s -b $JAR -c $JAR -o /dev/null "$BASE/Ui/Lang?l=en&r=/Settings"
curl -s -b $JAR -c $JAR -o $T/settings-en.html -w "  GET /Settings (en) → %{http_code}\n" $BASE/Settings
grep -o 'lang="en" dir="ltr"' $T/settings-en.html | head -1 | xargs echo "  en mode marker:"
grep -c "hr\.db" $T/settings-en.html | xargs echo "  real db path still shown:"
grep -c "Database\|SQLite" $T/settings-en.html | xargs echo "  en labels count:"
curl -s -b $JAR -c $JAR -o /dev/null "$BASE/Ui/Lang?l=fa&r=/Settings"
echo "== DONE =="
