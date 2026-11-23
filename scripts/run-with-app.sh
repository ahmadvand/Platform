#!/bin/bash
# ============================================================
# اجرای یک‌جا: استارت اپ (setsid) + انتظار برای 200 + اجرای دستور داده‌شده + توقف اپ
# استفاده: bash scripts/run-with-app.sh "bash scripts/smoke.sh" [...]
# دلیل: sandbox بین فراخوانی‌های ابزار پروسهٔ پس‌زمینه را می‌کشد؛
# اپ و تست باید در همان فراخوانی اجرا شوند.
# ============================================================
WEB=/home/z/my-project/aspnet/src/HrAttendance.Web
pkill -f "HrAttendance.Web.dll" 2>/dev/null
sleep 1
setsid bash /home/z/my-project/scripts/start-web.sh </dev/null >>/home/z/my-project/dev-boot.log 2>&1 &
OK=""
for i in $(seq 1 40); do
  sleep 2
  C=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 http://127.0.0.1:3000/Auth/Login 2>/dev/null)
  if [ "$C" = "200" ]; then OK=1; break; fi
done
[ -z "$OK" ] && { echo "APP-FAILED-TO-START"; tail -20 /home/z/my-project/dev.log; exit 9; }
echo "APP-UP"
bash -c "$*"
RC=$?
pkill -f "HrAttendance.Web.dll" 2>/dev/null
echo "RUNNER-RC=$RC"
exit $RC
