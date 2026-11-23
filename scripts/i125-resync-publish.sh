#!/bin/bash
# فاز ۱۰۶ب — انتشار کامل: باینری single-file + DLL ماژول‌ها + App_Data/langpacks + پرچم‌ها
# (i121 فقط باینری اصلی را عوض می‌کرد؛ DLLهای ماژول و دارایی‌های استاتیک عقب می‌ماندند)
set -euo pipefail
cd /home/z/my-project
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

PUB=/tmp/sc-new
rm -rf "$PUB" && mkdir -p "$PUB"

echo ">> release build of full solution (module DLLs come from bin/Release — see csproj note)..."
dotnet build aspnet/Platform.sln -c Release -v q -p:WarningLevel=0 --nologo 2>&1 | tail -2

echo ">> publish (single-file self-contained linux-x64, v2.7.7)..."
dotnet publish aspnet/src/Platform.Web/Platform.Web.csproj -c Release \
  --self-contained true -r linux-x64 -p:PublishSingleFile=true \
  -p:AppVersion=2.7.7 -p:VendorName="گروه توسعه سامانه‌های سازمانی" \
  -o "$PUB" --nologo -v q -p:WarningLevel=0 2>&1 | tail -2

[ -f "$PUB/Platform.Web" ] || { echo "!! Platform.Web binary missing"; exit 1; }
SZ=$(stat -c%s "$PUB/Platform.Web")
[ "$SZ" -gt 50000000 ] || { echo "!! binary too small: $SZ"; exit 1; }
echo ">> binary OK ($((SZ/1024/1024))MB)"

RUNDIR=/home/z/my-project/aspnet/sc-run

# ۱) DLLهای ماژول‌ها (کشف بازتابی از کنار اپ — باید تازه باشند)
cp -f "$PUB"/*.dll "$RUNDIR"/ 2>/dev/null || true
# ۲) بسته‌های زبان تعبیه‌شده (مبنای درون‌ریزی بوت)
[ -d "$PUB/App_Data/langpacks" ] && cp -f "$PUB/App_Data/langpacks/"*.json "$RUNDIR/App_Data/langpacks"/
# ۳) پرچم‌ها و دارایی‌های استاتیک تغییرکننده
[ -d "$PUB/wwwroot/img/flags" ] && cp -f "$PUB/wwwroot/img/flags/"*.svg "$RUNDIR/wwwroot/img/flags"/
# ۴) فاز ۱۱۲ — js/css و بقیهٔ wwwroot (به‌جز dl/ که مرکز دانلود جداگانه rsync می‌شود)
for SUB in js css img fonts lib; do
  [ -d "$PUB/wwwroot/$SUB" ] && mkdir -p "$RUNDIR/wwwroot/$SUB" && \
    rsync -a --delete --exclude 'dl' "$PUB/wwwroot/$SUB/" "$RUNDIR/wwwroot/$SUB/" 2>/dev/null || true
done
echo ">> dll + langpacks + flags + js/css synced"

# تعویض اتمیک باینری اصلی (mv — پروسهٔ در حال اجرا روی inode قدیمی می‌ماند)
cp -f "$PUB/Platform.Web" "$RUNDIR/Platform.Web.new"
chmod +x "$RUNDIR/Platform.Web.new"
mv -f "$RUNDIR/Platform.Web.new" "$RUNDIR/Platform.Web"
echo ">> swapped. restarting app..."

pkill -f "aspnet/sc-run/Platform.Web" 2>/dev/null || true
sleep 2
cd /home/z/my-project && export PATH="/home/z/.dotnet:$PATH" && setsid bash scripts/start-web.sh > /dev/null 2>&1 &

for i in $(seq 1 40); do
  C=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 http://localhost:3000/health 2>/dev/null || echo 000)
  [ "$C" = "200" ] && { echo ">> health 200 after $((i*3))s"; exit 0; }
  sleep 3
done
echo "!! health not 200 after 120s"; exit 1
