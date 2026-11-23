#!/bin/bash
# ============================================================
# p69-selfdescribing-test.sh — راستی‌آزمایی فاز ۶۹ (ماژول‌های خودتوصیف)
#   A) publish کامل → بوت + ویزارد + اسکیما: هر ۸ خانوادهٔ ماژولی حاضر
#   B) حذف HrModules.Org*.dll از همان نمونه → بوت + ویزارد + اسکیما:
#      صفر جدول org (حتی ترانزیتیو از تردد/دستگاه‌ها — گارد Snapshot)
#   C) حذف HrModules.Devices*.dll هم → صفر جدول devices
#   همهٔ چرخه در یک فراخوانی — روی پورت 3501
# ============================================================
set -u
cd /home/z/my-project
export PATH="/home/z/.dotnet:$PATH"
ROOT=/home/z/my-project
T=$ROOT/tmp-p69/p69test
B=http://127.0.0.1:3501
LOG=$ROOT/tmp-p69/p69-boot.log

pkill -f "tmp-p69/p69test" 2>/dev/null; sleep 1
rm -rf "$T"
mkdir -p "$ROOT/tmp-p69"

echo "== [0] publish (Debug — شامل modules/) =="
cd "$ROOT/aspnet-wt69/aspnet"
dotnet publish src/HrAttendance.Web -c Debug -o "$T" --nologo -v q 2>&1 | tail -2
[ -f "$T/HrAttendance.Web.dll" ] || { echo "FAIL-PUBLISH"; exit 1; }
ls "$T/modules/" 2>/dev/null | head -20 || ls "$T"/HrModules.*.dll 2>/dev/null | head -5
echo "DLL count (flat): $(ls "$T"/HrModules.*.dll 2>/dev/null | wc -l)"

run_wizard () { # $1 = db file name, $2 = seedDemo (true/false)
  local jar=$ROOT/tmp-p69/jar69
  rm -f "$jar"
  PAGE=$(curl -s -c "$jar" --max-time 8 $B/Setup)
  AF=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
  [ -z "$AF" ] && { echo "FAIL-AF"; return 1; }
  R=$(curl -s -b "$jar" -c "$jar" -X POST --max-time 90 $B/Setup/Save \
    --data-urlencode "provider=sqlite" --data-urlencode "dbPath=$1" \
    --data-urlencode "seedDemo=$2" --data-urlencode "licenseMode=Off" \
    --data-urlencode "__RequestVerificationToken=$AF")
  echo "WIZARD: $(echo "$R" | head -c 160)"
  echo "$R" | grep -q '"ok":true'
}

boot_and_check () { # $1 = label, $2 = db file
  local label="$1" dbf="$2"
  pkill -f "tmp-p69/p69test" 2>/dev/null; fuser -k 3501/tcp 2>/dev/null; sleep 2
  # وضعیت تازه برای هر سناریو — حذف تنظیمات (ریشهٔ اپ) و بانک قبلی (ویزارد اجباری)
  rm -f "$T/hrsettings.bin" "$T/hrsettings.json" "$T"/*.db "$T"/*.db-wal "$T"/*.db-shm "$T/App_Data"/*.db 2>/dev/null
  cd "$T"
  ASPNETCORE_URLS=$B dotnet HrAttendance.Web.dll > "$LOG" 2>&1 &
  local APP=$!
  local ok="" c=""
  for i in $(seq 1 30); do
    c=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 $B/health 2>/dev/null)
    { [ "$c" = "200" ] || [ "$c" = "503" ]; } && { ok=1; break; }
    sleep 1
  done
  if [ -z "$ok" ]; then echo "FAIL-BOOT[$label] health=$c"; tail -8 "$LOG"; kill $APP 2>/dev/null; return 1; fi
  echo "boot[$label] OK (health=$c)"

  if ! run_wizard "$dbf" false; then echo "FAIL-WIZARD[$label]"; kill $APP 2>/dev/null; return 1; fi

  # صفحات کلیدی بعد از ویزارد (روتینگ ماژول‌محور)
  for p in "/health" "/" "/Org" "/Devices" "/Attendance" "/Payroll" "/Access" "/Parking" "/Visitors"; do
    code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "$B$p")
    echo "  route $p -> $code"
  done
  # کشف پلاگین‌ها از لاگ بوت
  echo "  discovered: $(grep -o 'ماژول[^ ]* کشف شد[^,]*' "$LOG" | head -3 | tr '\n' ' ')"
  grep -o 'HrModules\.[A-Za-z]*' "$LOG" | sort -u | tr '\n' ' ' | sed 's/^/  plugins-in-log: /'; echo
  kill $APP 2>/dev/null; sleep 2
  return 0
}

check_schema () { # $1 = db path, $2 = expected-ABSENT family list "org devices", $3 = label
  python3 - "$1" "$2" "$3" <<'PYEOF'
import sqlite3, sys
import os
if not os.path.exists(sys.argv[1]) or os.path.getsize(sys.argv[1]) == 0:
    print(f"[{sys.argv[3]}] FAIL: db missing/empty: {sys.argv[1]}"); sys.exit(1)
db = sqlite3.connect(sys.argv[1])
tables = set(r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"))
print(f"[{sys.argv[3]}] total tables: {len(tables)}")
fams = {
 'org': {'Departments','Positions','Employees','Contracts','Decrees','Holidays'},
 'devices': {'Devices','DeviceCommands','DeviceUsers','DeviceBiometrics','EmployeeBiometrics','DeviceOfflinePunches','DevicePunchLogs','GatewaySettings'},
 'attendance': {'Shifts','AttendanceRecords','LeaveRequests','Missions','OvertimePermits','CorrectionRequests'},
 'access': {'Zones','Gates','AccessPermissions','AccessEvents','Cards','AccessGroups','AccessGroupMembers','AccessGroupGates'},
 'payroll': {'PayrollRuns','Payslips','PayItems','PayrollParameters','PayrollSettings','PayslipLines','PayrollBonusRows','PayrollSettlements'},
 'parking': {'ParkingLots','ParkingGates','Vehicles','ParkingPermits','ParkingEvents','AnprCameras','AnprCommands'},
 'visitors': {'Contractors','Visitors','VisitorCards','VisitLogs'},
}
absent_fams = set(sys.argv[2].lower().split()) if sys.argv[2] else set()
bad = []
for fam, tset in fams.items():
    found = tables & tset
    if fam in absent_fams:
        if found: bad.append(f"{fam} PHANTOM: {sorted(found)}")
        else: print(f"  {fam}: NONE — ✔ (ماژول غایب)")
    else:
        print(f"  {fam}: {len(found)}/{len(tset)} tables")
print("VERDICT:", "FAIL " + "; ".join(bad) if bad else "PASS")
PYEOF
}

echo "=================== تست A: بوت کامل (هر ۸ پلاگین) ==================="
boot_and_check "full" "p69full.db" || exit 1
check_schema "$T/p69full.db" "" "FULL" || true

echo "=================== تست B: بدون org ==================="
rm -f "$T"/HrModules.Org*.dll "$T"/modules/HrModules.Org*.dll
boot_and_check "no-org" "p69noorg.db" || exit 1
check_schema "$T/p69noorg.db" "org" "NO-ORG" || true

echo "=================== تست C: بدون devices هم ==================="
rm -f "$T"/HrModules.Devices*.dll "$T"/modules/HrModules.Devices*.dll
boot_and_check "no-org-no-dev" "p69noorg-nodev.db" || exit 1
check_schema "$T/p69noorg-nodev.db" "org devices" "NO-ORG-NO-DEV" || true

pkill -f "tmp-p69/p69test" 2>/dev/null
echo "=== پ69 تست تمام شد ==="
