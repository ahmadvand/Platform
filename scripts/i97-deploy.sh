#!/bin/bash
# فاز ۹۷ — انتشار نسخهٔ 2.7.10 روی محیط اجرا (sc-run) با حفظ کامل داده‌های stateful
# تغییرات این فاز: ماه‌شمار تعطیلات از تقویم نمایشی سامانه پیروی می‌کند (شمسی/میلادی/قمری + جبران)
set -uo pipefail
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
ROOT=/home/z/my-project
SRC=$ROOT/aspnet/src/Platform.Web
STAGE=$ROOT/aspnet/publish/sc-stage97
RUN=$ROOT/aspnet/sc-run
VER=2.7.10

echo ">> dotnet build sln (Release — همهٔ پلاگین‌های ماژول باید تازه شوند)"
dotnet build "$ROOT/aspnet/Platform.sln" -c Release --nologo -v q -p:WarningLevel=0 || { echo "BUILD FAILED"; exit 1; }

echo ">> dotnet publish (self-contained linux-x64, AppVersion=$VER)"
rm -rf "$STAGE"
dotnet publish "$SRC/Platform.Web.csproj" -c Release -r linux-x64 --self-contained true \
  -o "$STAGE" -v q --nologo -p:WarningLevel=0 -p:AppVersion=$VER || { echo "PUBLISH FAILED"; exit 1; }
rm -rf "$STAGE/wwwroot/dl"   # مرکز دانلود جداگانه مدیریت می‌شود

echo ">> stopping watchdog + app"
pkill -f "keepalive.sh" 2>/dev/null
pkill -f "aspnet/sc-run/Platform.Web" 2>/dev/null
pkill -f "sc-run/Platform.Web" 2>/dev/null
sleep 2

echo ">> rsync staging -> sc-run (stateful excluded)"
rsync -a --delete \
  --exclude "hr.db" --exclude "hr.db-shm" --exclude "hr.db-wal" \
  --exclude "appsettings.json" --exclude "appsettings.Development.json" \
  --exclude "App_Data" --exclude "logs" \
  --exclude "wwwroot/uploads" --exclude "wwwroot/dl" \
  "$STAGE/" "$RUN/"

echo ">> restart app"
(setsid bash /home/z/my-project/scripts/start-web.sh </dev/null >>/home/z/my-project/dev-boot.log 2>&1 &)
for i in $(seq 1 90); do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 http://localhost:3000/health 2>/dev/null || true)
  [ "$code" = "200" ] && { echo "HEALTH 200 after ~${i}x2s"; exit 0; }
  sleep 2
done
echo "HEALTH FAILED"; tail -30 /home/z/my-project/dev.log
exit 1
