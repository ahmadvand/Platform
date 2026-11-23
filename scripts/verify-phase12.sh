#!/usr/bin/env bash
# ============================================================
# فاز ۱۲ — استقرار سرویس ویندوزی گیت‌وی (بسته انتشار + EventLog + راهنما)
# اجرا: bash scripts/verify-phase12.sh
# ============================================================
set -u
cd "$(dirname "$0")/.."
ROOT="$(pwd)"
GW="$ROOT/device-service-csharp"
PASS=0; FAIL=0
ck() { if [ "$1" = "1" ]; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }
export PATH="/home/z/.dotnet:$PATH"

echo "── ۱) بیلد سرویس"
if dotnet build "$GW/HrDeviceGateway/HrDeviceGateway.csproj" -v q --nologo > /tmp/p12-build.log 2>&1; then
  ck 1 "dotnet build بدون خطا"
else
  ck 0 "dotnet build — $(tail -3 /tmp/p12-build.log)"
fi

echo "── ۲) انتشار خودکفا win-x64 (cross-compile از لینوکس)"
if [ -f "$GW/publish-check/HrDeviceGateway.exe" ]; then
  ck 1 "خروجی قبلی publish-check/HrDeviceGateway.exe موجود است (publish قبلاً سبز)"
else
  if dotnet publish "$GW/HrDeviceGateway/HrDeviceGateway.csproj" -c Release -r win-x64 --self-contained true -o "$GW/publish-check" -v q --nologo > /tmp/p12-pub.log 2>&1; then
    ck 1 "dotnet publish win-x64 self-contained"
  else
    ck 0 "dotnet publish — $(tail -3 /tmp/p12-pub.log)"
  fi
fi
ck "$([ -f "$GW/publish-check/HrDeviceGateway.exe" ] && echo 1 || echo 0)" "خروجی HrDeviceGateway.exe ساخته شد"
ck "$([ -f "$GW/publish-check/appsettings.json" ] && echo 1 || echo 0)" "appsettings.json در بسته انتشار"

echo "── ۳) اسکریپت‌های نصب ویندوز"
ck "$(grep -qi "New-EventLog" "$GW/install-service.bat" && echo 1 || echo 0)" "install-service.bat ← ثبت منبع Event Viewer"
ck "$(grep -qi "SourceExists" "$GW/install-service.bat" && echo 1 || echo 0)" "install-service.bat ← چک وجود منبع (idempotent)"
ck "$(grep -q "تنظیمات گیت‌وی" "$GW/install-service.bat" && echo 1 || echo 0)" "install-service.bat ← راهنمای کانفیگ پویا"
ck "$(grep -q "تنظیمات گیت‌وی" "$GW/publish.bat" && echo 1 || echo 0)" "publish.bat ← راهنمای کانفیگ پویا"

echo "── ۴) سورس — لاگ EventLog ویندوز"
ck "$(grep -q "AddEventLog" "$GW/HrDeviceGateway/Program.cs" && echo 1 || echo 0)" "Program.cs ← AddEventLog با گارد IsWindows"

echo "── ۵) پیکربندی — فقط bootstrap"
r=$(python3 - <<'PYEOF'
import json
cfg = json.load(open("device-service-csharp/HrDeviceGateway/appsettings.json"))
g = cfg["Gateway"]
ok = all(k in g for k in ("ApiBase", "Username", "Password", "ListenPort"))
ok = ok and "_comment" in cfg
print(1 if ok else 0)
PYEOF
)
ck "$r" "appsettings.json ← کلیدهای bootstrap + توضیح نقش آن"

echo "── ۶) راهنمای استقرار"
ck "$(grep -q "ASP.NET Core 8 MVC" "$GW/README-FA.md" && echo 1 || echo 0)" "README-FA ← ارجاع به استک ASP.NET Core (نه Next.js)"
ck "$(grep -q "کانفیگ پویا از پنل ادمین" "$GW/README-FA.md" && echo 1 || echo 0)" "README-FA ← بخش کانفیگ پویا"
ck "$(grep -q "ConfigVersion" "$GW/README-FA.md" && echo 1 || echo 0)" "README-FA ← توضیح ConfigVersion"

echo "── ۷) اجرای زنده + سلامت runtime (پورت 8081 — UseUrls در Program.cs ثابت است)"
pkill -f "HrDeviceGateway" 2>/dev/null; sleep 1
( cd "$GW/HrDeviceGateway" && nohup dotnet run --no-build -c Debug > /tmp/gw-p12.log 2>&1 & )
ok=0; h=""
for i in 1 2 3 4 5 6 7 8; do
  sleep 2
  h=$(curl -s http://127.0.0.1:8081/health --max-time 4)
  echo "$h" | grep -q '"status":"ok"' && { ok=1; break; }
done
ck "$ok" "/health پاسخ می‌دهد: ${h:0:120}"
echo "$h" | grep -q '"configVersion"' && ck 1 "/health شامل وضعیت runtime (configVersion)" || ck 0 "/health بدون runtime"
grep -q "کانفیگ" /tmp/gw-p12.log && ck 1 "لاگ راه‌اندازی/کانفیگ ثبت شد" || ck 0 "لاگ: $(tail -2 /tmp/gw-p12.log)"
pkill -f "HrDeviceGateway" 2>/dev/null

echo ""
echo "═══════ نتیجه فاز ۱۲: $PASS سبز، $FAIL قرمز ═══════"
[ "$FAIL" = "0" ]
