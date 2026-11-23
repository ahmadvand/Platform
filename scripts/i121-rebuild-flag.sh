#!/bin/bash
# فاز ۱۰۵ — بازسازی Platform.Web با نگاشت پرچم ru + تعویض باینری sc-run
# فقط فایل اجرایی اصلی عوض می‌شود؛ hr.db / App_Data / appsettings / ماژول‌ها دست‌نخورده می‌مانند.
set -euo pipefail
cd /home/z/my-project
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

PUB=/tmp/sc-new
rm -rf "$PUB" && mkdir -p "$PUB"

echo ">> publish (single-file self-contained linux-x64, v2.7.7)..."
dotnet publish aspnet/src/Platform.Web/Platform.Web.csproj -c Release \
  --self-contained true -r linux-x64 -p:PublishSingleFile=true \
  -p:AppVersion=2.7.7 -p:VendorName="گروه توسعه سامانه‌های سازمانی" \
  -o "$PUB" --nologo -v q -p:WarningLevel=0 2>&1 | tail -3

echo ">> publish output:"
ls -la "$PUB" | head -15

# اعتبارسنجی خروجی
[ -f "$PUB/Platform.Web" ] || { echo "!! Platform.Web binary missing"; exit 1; }
SZ=$(stat -c%s "$PUB/Platform.Web")
[ "$SZ" -gt 50000000 ] || { echo "!! binary too small: $SZ"; exit 1; }
echo ">> binary OK ($((SZ/1024/1024))MB)"

# تعویض اتمیک (mv — پروسهٔ در حال اجرا روی inode قدیمی می‌ماند)
cp -f "$PUB/Platform.Web" aspnet/sc-run/Platform.Web.new
chmod +x aspnet/sc-run/Platform.Web.new
mv -f aspnet/sc-run/Platform.Web.new aspnet/sc-run/Platform.Web
echo ">> swapped. restarting app..."

pkill -f "aspnet/sc-run/Platform.Web" 2>/dev/null || true
sleep 2
cd /home/z/my-project && export PATH="/home/z/.dotnet:$PATH" && setsid bash scripts/start-web.sh > /dev/null 2>&1 &

# انتظار برای سلامت (حداکثر ۹۰ ثانیه)
for i in $(seq 1 30); do
  C=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 http://localhost:3000/health 2>/dev/null || echo 000)
  [ "$C" = "200" ] && { echo ">> health 200 after $((i*3))s"; exit 0; }
  sleep 3
done
echo "!! health not 200 after 90s"; exit 1
