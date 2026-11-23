#!/bin/bash
# ============================================================
# راه‌اندازی کامل استک: SQL Server (در صورت نیاز) + وب‌اپ ASP.NET
# Provider به‌صورت خودکار انتخاب می‌شود:
#   - SQL Server بالا آمد؟ → Database__Provider=SqlServer
#   - نشد؟ → fallback به Sqlite با هشدار
# ============================================================
pkill -f "HrAttendance.Web.dll" 2>/dev/null
sleep 2

PROVIDER="${Database__Provider:-}"
if [ -z "$PROVIDER" ]; then
  if bash /home/z/my-project/scripts/sqlservr-up.sh; then
    PROVIDER=SqlServer
  else
    echo "WARN: SQL Server unavailable — falling back to Sqlite"
    PROVIDER=Sqlite
  fi
fi
echo "Web will start with Database__Provider=$PROVIDER"

(setsid env Database__Provider="$PROVIDER" bash /home/z/my-project/scripts/start-web.sh < /dev/null > /dev/null 2>&1 &)
for i in $(seq 1 60); do
  code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:3000/Auth/Login --max-time 2)
  if [ "$code" = "200" ]; then echo "UP after ${i}s (port 3000, $PROVIDER)"; exit 0; fi
  sleep 1
done
echo "FAILED to start — last log lines:"
tail -20 /home/z/my-project/dev.log
exit 1
