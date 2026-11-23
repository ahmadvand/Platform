#!/bin/bash
# فاز ۱۳۲ (فاز ۱۳۵ کامل — دستهٔ بزرگ ROADMAP) — جمع‌شو/بازشو گروه‌های منوی کناری — نسخهٔ 2.7.38
# الگوی i125-resync-publish.sh + پاک‌کردن obj ویوها/ماژول‌ها (تلهٔ کش رازر — تجربهٔ فاز ۱۲۲)
# نسخه از csproj می‌آید (بدون -p:AppVersion کهنه)
set -euo pipefail
cd /home/z/my-project

# گارد شبح‌ها — دیپلوی روی درخت آلوده ممنوع (دستور کاربر ۲۰۲۶-۱۱-۲۱)
bash scripts/ghost-guard.sh || exit 1
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo ">> clean razor/plugin caches..."
rm -rf aspnet/src/Platform.Web/obj aspnet/src/Platform.Web/bin
for m in aspnet/src/modules/*/; do rm -rf "$m/obj" "$m/bin"; done
rm -rf aspnet/src/Platform.Contracts/obj aspnet/src/Platform.Core/obj 2>/dev/null || true

echo ">> release build of full solution..."
dotnet build aspnet/Platform.sln -c Release -v q -p:WarningLevel=0 --nologo 2>&1 | tail -3

echo ">> publish (single-file self-contained linux-x64, csproj AppVersion)..."
dotnet publish aspnet/src/Platform.Web/Platform.Web.csproj -c Release \
  --self-contained true -r linux-x64 -p:PublishSingleFile=true \
  -p:VendorName="گروه توسعه سامانه‌های سازمانی" \
  -o /tmp/sc-f136 --nologo -v q -p:WarningLevel=0 2>&1 | tail -2

[ -f /tmp/sc-f136/Platform.Web ] || { echo "!! binary missing"; exit 1; }
SZ=$(stat -c%s /tmp/sc-f136/Platform.Web)
[ "$SZ" -gt 50000000 ] || { echo "!! binary too small: $SZ"; exit 1; }
echo ">> binary OK ($((SZ/1024/1024))MB)"

RUNDIR=/home/z/my-project/aspnet/sc-run

cp -f /tmp/sc-f136/*.dll "$RUNDIR"/ 2>/dev/null || true
[ -d /tmp/sc-f136/App_Data/langpacks ] && cp -f /tmp/sc-f136/App_Data/langpacks/*.json "$RUNDIR/App_Data/langpacks"/
[ -d /tmp/sc-f136/wwwroot/img/flags ] && cp -f /tmp/sc-f136/wwwroot/img/flags/*.svg "$RUNDIR/wwwroot/img/flags"/
for SUB in js css img fonts lib; do
  [ -d "/tmp/sc-f136/wwwroot/$SUB" ] && mkdir -p "$RUNDIR/wwwroot/$SUB" && \
    rsync -a --delete --exclude 'dl' --exclude 'dlc' "/tmp/sc-f136/wwwroot/$SUB/" "$RUNDIR/wwwroot/$SUB/" 2>/dev/null || true
done
echo ">> dll + langpacks + flags + js/css synced"

cp -f /tmp/sc-f136/Platform.Web "$RUNDIR/Platform.Web.new"
chmod +x "$RUNDIR/Platform.Web.new"
mv -f "$RUNDIR/Platform.Web.new" "$RUNDIR/Platform.Web"
echo ">> swapped. restarting app..."

pkill -f "aspnet/sc-run/Platform.Web" 2>/dev/null || true
sleep 2
cd /home/z/my-project && setsid bash scripts/start-web.sh > /dev/null 2>&1 &

for i in $(seq 1 40); do
  C=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 http://localhost:3000/health 2>/dev/null || echo 000)
  [ "$C" = "200" ] && { echo ">> health 200 after $((i*3))s"; exit 0; }
  sleep 3
done
echo "!! health not 200 after 120s"; exit 1
