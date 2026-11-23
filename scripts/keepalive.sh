#!/bin/bash
# ============================================================
# نگه‌بان اپ HR — هر ۲۰ ثانیه سلامت اپ را چک می‌کند؛ اگر پایین
# بود (kill بی‌صدای سندباکس/OOM بین نوبت‌ها) دوباره بالا می‌آورد.
# شواهد: MSBuild بین نوبت‌ها زنده می‌ماند ولی اپ نه → kill هدفمند؛
# این حلقه هر دو حالت را خودترمیم می‌کند.
# اجرا:  setsid bash scripts/keepalive.sh &
# ============================================================
LOG=/home/z/my-project/watchdog.log
BASE=http://127.0.0.1:3000
echo "$(date '+%F %T') watchdog started (pid $$)" >> "$LOG"
while true; do
  sleep 20
  C=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$BASE/Auth/Login" 2>/dev/null)
  if [ "$C" != "200" ]; then
    echo "$(date '+%F %T') DOWN(code=$C) → restart" >> "$LOG"
    pkill -f "Platform.Web.dll" 2>/dev/null
    pkill -f "aspnet/sc-run/Platform.Web" 2>/dev/null
    sleep 1
    setsid bash /home/z/my-project/scripts/start-web.sh </dev/null >>/home/z/my-project/dev-boot.log 2>&1 &
  fi
done
