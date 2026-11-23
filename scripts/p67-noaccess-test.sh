#!/bin/bash
# ============================================================
# p67-noaccess-test.sh — تست «حذف دستی DLL ماژول از بستهٔ کامل»
#   ۱) نمونهٔ موقت از publish/fulltest (بدون HrModules.Access*.dll)
#   ۲) بوت روی پورت 3500 با بانک SQLite تازه
#   ۳) اجرای ویزارد /Setup (seedDemo=false — فقط اسکیما)
#   ۴) بررسی جدول‌های بانک: هیچ جدول Access، بقیهٔ ماژول‌ها حاضر
#   ۵) توقف نمونه — همهٔ چرخه در یک فراخوانی (پیش از ریپر)
# ============================================================
set -u
cd /home/z/my-project
export PATH="/home/z/.dotnet:$PATH"
T=tmp-p67/fulltest
DB=/home/z/my-project/tmp-p67/schema-test.db
B=http://127.0.0.1:3500

pkill -f "tmp-p67/fulltest" 2>/dev/null; sleep 1
rm -f tmp-p67/jar "$T/App_Data/hrsettings.bin" "$T"/*.db "$DB"
echo "== [1] boot (no Access DLLs, wizard mode) =="
cd "/home/z/my-project/$T"
ASPNETCORE_URLS=$B dotnet HrAttendance.Web.dll > /home/z/my-project/tmp-p67/schema-boot.log 2>&1 &
APP=$!
ok=""
for i in $(seq 1 25); do
  c=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 $B/health 2>/dev/null)
  { [ "$c" = "200" ] || [ "$c" = "503" ]; } && { ok=1; break; }
  sleep 1
done
[ -z "$ok" ] && { echo "FAIL-BOOT health=$c"; tail -5 /home/z/my-project/tmp-p67/schema-boot.log; kill $APP 2>/dev/null; exit 1; }
echo "boot OK (health=$c — 503 در حالت ویزارد طبیعی است)"

echo "== [2] wizard save =="
PAGE=$(curl -s -c /home/z/my-project/tmp-p67/jar --max-time 8 $B/Setup)
AF=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -z "$AF" ] && { echo "FAIL-AF (token not found)"; head -c 300 <<<"$PAGE"; kill $APP 2>/dev/null; exit 2; }
R=$(curl -s -b /home/z/my-project/tmp-p67/jar -c /home/z/my-project/tmp-p67/jar -X POST --max-time 60 $B/Setup/Save \
  --data-urlencode "provider=sqlite" --data-urlencode "dbPath=schema-test.db" \
  --data-urlencode "seedDemo=false" --data-urlencode "licenseMode=Off" \
  --data-urlencode "__RequestVerificationToken=$AF")
echo "SAVE: $R"
echo "$R" | grep -q '"ok":true' || { echo "FAIL-SAVE"; kill $APP 2>/dev/null; exit 3; }

echo "== [3] routes =="
for p in "/" "/Access" "/Attendance" "/health"; do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "$B$p")
  echo "  $p -> $code"
done

echo "== [4] schema =="
kill $APP 2>/dev/null
sleep 1
python3 - "/home/z/my-project/$T/schema-test.db" <<'PYEOF'
import sqlite3, sys
db = sqlite3.connect(sys.argv[1])
tables = sorted(r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"))
print(f"total tables: {len(tables)}")
acc = [t for t in tables if t in ('Zones','Gates','AccessPermissions','AccessEvents','Cards','AccessGroups','AccessGroupMembers','AccessGroupGates')]
print("ACCESS TABLES:", acc if acc else "NONE — ✔ مطابق انتظار")
mod = [t for t in tables if t in ('Shifts','AttendanceRecords','LeaveRequests','Missions','OvertimePermits','CorrectionRequests','PayrollRuns','Payslips','PayrollSettings','ParkingLots','ParkingEvents','AnprCameras','Contractors','Visitors','VisitorCards','VisitLogs')]
print("OTHER MODULE TABLES:", len(mod), mod[:8], "..." if len(mod) > 8 else "")
base = [t for t in tables if t in ('Users','Employees','Companies','Devices','AuditLogs','GatewaySettings','Roles','Permissions')]
print("BASE TABLES:", base)
fail = bool(acc) or len(mod) < 10 or len(base) < 7
print("VERDICT:", "FAIL" if fail else "PASS ✔")
PYEOF
