#!/bin/bash
# فاز ۱۲۴ — دیپلوی با پارامتر نسخه (پایه: i120-deploy.sh الگوی رسمی)
set -uo pipefail
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
ROOT=/home/z/my-project
SRC=$ROOT/aspnet/src/Platform.Web
STAGE=$ROOT/aspnet/publish/sc-stage-tmp
RUN=$ROOT/aspnet/sc-run
VER=${1:-2.7.25}

echo ">> dotnet build sln (Release) — ماژول‌ها پلاگین‌اند: dll آنها فقط با بیلد سولوشن تازه می‌شود"
rm -rf "$SRC/obj"   # جلوگیری از flip-flop حالت restore بین بیلد بدون-RID و publish با-RID
dotnet build "$ROOT/aspnet/Platform.sln" -c Release --nologo -v q -p:WarningLevel=0 || { echo "BUILD FAILED"; exit 1; }

echo ">> dotnet publish (self-contained linux-x64, AppVersion=$VER)"
rm -rf "$STAGE"
dotnet restore "$SRC/Platform.Web.csproj" -r linux-x64 --nologo -v q || { echo "RESTORE FAILED"; exit 1; }
dotnet publish "$SRC/Platform.Web.csproj" -c Release -r linux-x64 --self-contained true \
  -o "$STAGE" -v q --nologo -p:WarningLevel=0 -p:AppVersion=$VER || { echo "PUBLISH FAILED"; exit 1; }
rm -rf "$STAGE/wwwroot/dl"

echo ">> stopping app"
pkill -f "keepalive.sh" 2>/dev/null
pkill -f "sc-run/Platform.Web" 2>/dev/null
sleep 2

echo ">> rsync staging -> sc-run (stateful excluded; langpacks synced)"
rsync -a --delete \
  --exclude "hr.db" --exclude "hr.db-shm" --exclude "hr.db-wal" \
  --exclude "appsettings.json" --exclude "appsettings.Development.json" \
  --exclude "App_Data" --exclude "logs" \
  --exclude "wwwroot/uploads" --exclude "wwwroot/dl" \
  "$STAGE/" "$RUN/"
mkdir -p "$RUN/App_Data"
rsync -a --delete "$SRC/App_Data/langpacks/" "$RUN/App_Data/langpacks/"

echo ">> restart app"
(setsid bash /home/z/my-project/scripts/start-web.sh </dev/null >>/home/z/my-project/dev-boot.log 2>&1 &)
for i in $(seq 1 90); do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 http://localhost:3000/health 2>/dev/null || true)
  [ "$code" = "200" ] && { echo "HEALTH 200 after ~${i}x2s"; exit 0; }
  sleep 2
done
echo "HEALTH FAILED"; tail -30 /home/z/my-project/dev.log
exit 1
