#!/bin/bash
# ============================================================
# اجرای وب‌اپ ASP.NET Core (نسخه نهایی مهاجرت‌شده) روی پورت 3000
# این اسکریپت جایگزین next dev شده است — supervisor پلتفرم آن را نگه می‌دارد.
# ============================================================
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export Database__Provider="${Database__Provider:-Sqlite}"   # در پروداکشن: SqlServer
# سندباکس/پیش‌نمایش: ویزارد راه‌اندازی اولیه نمایش داده نشود (نصب کاربر بدون این متغیر، ویزارد می‌بیند)
export Setup__Configured="${Setup__Configured:-true}"
export TZ=Asia/Tehran
export ASPNETCORE_URLS=http://0.0.0.0:3000
export ASPNETCORE_ENVIRONMENT=Production

# فاز ۱۳۶ — پاک‌سازی خودکار شبح‌ها هنگام هر بوت (دستور صریح کاربر ۲۰۲۶-۱۱-۲۱):
# بازیابی سندباکس ممکن است این مسیرهای حذف‌شده را دوباره روی دیسک بیاورد؛
# بوت قبل از هر کاری آن‌ها را برمی‌دارد تا «دیگر هرگز برنگردند».
for _g in \
  /home/z/my-project/aspnet/src/HrAttendance.Web \
  /home/z/my-project/aspnet/src/HrPlatform.Contracts \
  /home/z/my-project/aspnet/src/HrPlatform.Core \
  /home/z/my-project/aspnet/src/modules/HrModules.Parking \
  /home/z/my-project/aspnet/src/modules/HrModules.Visitors \
  /home/z/my-project/aspnet/HrAttendance.sln \
  /home/z/my-project/scripts/finalverify; do
  [ -e "$_g" ] && rm -rf "$_g" && echo "[start-web] ghost removed: $_g"
done

WEB=/home/z/my-project/aspnet/src/Platform.Web
SC=/home/z/my-project/aspnet/sc-run/Platform.Web

# فاز ۶۲ — مسیر سریع بوت: باینری self-contained گیت‌شده → بدون نیاز به SDK و بیلد؛
# اپ چند ثانیه پس از بوت روی 3000 است تا پنجرهٔ ثبت مسیر preview در لبه از دست نرود.
if [ -x "$SC" ] && [ -s "$SC" ]; then
  RUNDIR=/home/z/my-project/aspnet/sc-run
  APPCMD="$SC"
else
  RUNDIR=$WEB
  cd "$WEB" || exit 1
  # ساخت در صورت نبود باینری
  if [ ! -f bin/Debug/net8.0/Platform.Web.dll ]; then
    dotnet build -v q
  fi
  APPCMD="/home/z/.dotnet/dotnet bin/Debug/net8.0/Platform.Web.dll"
fi

LOG=/home/z/my-project/dev.log

# فاز ۶۴ — قفل تک‌نمونه‌ای سوپروایزر: اجرای هم‌زمان چند نسخهٔ این اسکریپت
# (e2eها، کار سلامت cron، استارت دستی) باعث رقابت بایند پورت 3000 و کرش‌ریس
# می‌شد (تجربهٔ 2026-10-27). فقط نخستین نسخه قفل را می‌گیرد و می‌ماند؛ بقیه
# بی‌سروصدا خارج می‌شوند و اگر نگه‌دارندهٔ قفل مُرد، قفل آزاد و نسخهٔ بعدی
# مالک می‌شود — خودترمیم بدون ریسک تداخل.
exec 9>/home/z/my-project/.start-web.lock
if ! flock -n 9; then
  echo "[start-web] $(date '+%F %T') supervisor دیگری قفل را دارد — خروج" >> "$LOG"
  exit 0
fi

# سوپروایزر خودترمیم: اگر اپ کشته شود (مثلاً OOM در اوج بیلد سنگین regen-dl —
# علت ریشه‌ای مرگ اپ در بوت ۲۰۲۶-۱۰-۲۶)، ظرف ۳ ثانیه دوباره بالا می‌آید.
while true; do
  # فاز ۶۵ — گارد پورت: اگر نمونهٔ سالمی روی 3000 گوش می‌دهد، نمونهٔ دوم نساز
  # (جنگ بایند = کرش code=134 و نوسان تحویل ADMS؛ نگهبان فقط «نبودِ» اپ را جبران می‌کند)
  if ss -tln 2>/dev/null | grep -q ':3000 '; then
    sleep 3
    continue
  fi
  # جلوگیری از رشد بی‌نهایت لاگ (سقف ۵۰MB → نگه‌داشتن آخرین ۵MB)
  if [ -f "$LOG" ] && [ "$(stat -c%s "$LOG" 2>/dev/null || echo 0)" -gt 52428800 ]; then
    tail -c 5242880 "$LOG" > "$LOG.tmp" && mv "$LOG.tmp" "$LOG"
  fi
  echo "[start-web] $(date '+%F %T') launching Platform.Web ..." >> "$LOG"
  cd "$RUNDIR"
  # سینک مرکز دانلود به wwwroot باینری sc (هاردلینک — هر استارت؛ مپینگ /dlc فقط
  # اگر پوشهٔ dlc هنگام استارت موجود باشد ثبت می‌شود) — فاز ۱۰۶: مسیر قبلی /dl حذف شد
  if [ "$RUNDIR" != "$WEB" ] && [ -d "$WEB/wwwroot/dlc" ]; then
    rm -rf "$RUNDIR/wwwroot/dlc"
    cp -al "$WEB/wwwroot/dlc" "$RUNDIR/wwwroot/dlc" 2>/dev/null || cp -a "$WEB/wwwroot/dlc" "$RUNDIR/wwwroot/dlc"
  fi
  "$APPCMD" 2>&1 | tee -a "$LOG"
  rc=${PIPESTATUS[0]}
  echo "[start-web] $(date '+%F %T') app exited (code=$rc) — respawning in 3s" >> "$LOG"
  sleep 3
done
