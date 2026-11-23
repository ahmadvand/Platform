#!/bin/bash
# ============================================================
# p68-test.sh — تست ماتریس فاز ۶۸ (کشف کامل رانتایم)
#   A) publish کامل (پلاگین‌ها با تارگت CopyHrModulePluginsAfterPublish کنار اپ می‌نشینند)
#   B) نمونهٔ ۱: همهٔ ماژول‌ها — ویزارد + seedDemo=true → همهٔ جدول‌های ماژولی + سیدها
#   C) نمونهٔ ۲: حذف دستی HrModules.Access*.dll — بانک تازه → صفر جدول Access
#      + صفحهٔ دوستانهٔ «نصب نشده» برای /Access (گیت با مانیفست کشف‌شده)
#   همهٔ چرخه در یک فراخوانی (پیش از ریپر ۹۰ث سندباکس)
# ============================================================
set -u
cd /home/z/my-project
export PATH="/home/z/.dotnet:$PATH"
SRC=aspnet/src/HrAttendance.Web
T=tmp-p68/full
B1=http://127.0.0.1:3501
B2=http://127.0.0.1:3502

echo "== [A] publish =="
rm -rf tmp-p68
dotnet publish $SRC -c Debug -o "$T" --nologo -v q 2>&1 | tail -2
N=$(ls "$T"/HrModules.*.dll 2>/dev/null | wc -l)
echo "plugin DLLs in publish: $N"
[ "$N" -lt 11 ] && { echo "FAIL-PUBLISH-PLUGINS ($N)"; exit 1; }

boot() { # $1=dir $2=url $3=log
  cd "/home/z/my-project/$1"
  ASPNETCORE_URLS=$2 dotnet HrAttendance.Web.dll > "/home/z/my-project/$3" 2>&1 &
  echo $!
}
waitboot() { # $1=url $2=pid
  for i in $(seq 1 30); do
    c=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 "$1/health" 2>/dev/null)
    { [ "$c" = "200" ] || [ "$c" = "503" ]; } && { echo "boot OK (health=$c)"; return 0; }
    sleep 1
  done
  echo "FAIL-BOOT health=$c"; return 1
}
wizard() { # $1=url $2=jar $3=dbfile $4=seeddemo
  local PAGE AF R
  PAGE=$(curl -s -c "$2" --max-time 8 "$1/Setup")
  AF=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
  [ -z "$AF" ] && { echo "FAIL-AF"; return 1; }
  R=$(curl -s -b "$2" -c "$2" -X POST --max-time 90 "$1/Setup/Save" \
    --data-urlencode "provider=sqlite" --data-urlencode "dbPath=$3" \
    --data-urlencode "seedDemo=$4" --data-urlencode "licenseMode=Off" \
    --data-urlencode "__RequestVerificationToken=$AF")
  echo "wizard: $R"
}

tables() { # $1=db
  python3 - "$1" <<'PYEOF'
import sqlite3, sys
c = sqlite3.connect("/home/z/my-project/" + sys.argv[1])
print(c.execute("SELECT count(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%'").fetchone()[0])
PYEOF
}
has() { # $1=db $2=table
  python3 - "$1" "$2" <<'PYEOF'
import sqlite3, sys
c = sqlite3.connect("/home/z/my-project/" + sys.argv[1])
print(c.execute("SELECT count(*) FROM sqlite_master WHERE type='table' AND name=?", (sys.argv[2],)).fetchone()[0])
PYEOF
}

echo "== [B] instance 1: FULL modules + demo seed =="
rm -f "$T/hrsettings.bin" "$T/license-trial.json" "$T"/demo68.db; rm -rf "$T/App_Data"
P1=$(boot "$T" "$B1" tmp-p68/boot1.log)
waitboot "$B1" $P1 || { kill $P1 2>/dev/null; exit 2; }
wizard "$B1" tmp-p68/jar1 demo68.db true
sleep 3
T1=$(tables "$T/demo68.db")
echo "tables(full)=$T1"
echo "  Shifts=$(has $T/demo68.db Shifts) AttendanceRecords=$(has $T/demo68.db AttendanceRecords) Zones=$(has $T/demo68.db Zones) Gates=$(has $T/demo68.db Gates) Visitors=$(has $T/demo68.db Visitors) PayrollRuns=$(has $T/demo68.db PayrollRuns) ParkingLots=$(has $T/demo68.db ParkingLots) Devices=$(has $T/demo68.db Devices) Employees=$(has $T/demo68.db Employees)"
ST=$(curl -s -o /dev/null -w "%{http_code}" -L --max-redirs 3 --max-time 8 "$B1/Attendance")
echo "  /Attendance (module endpoint)=$ST"
rg "سید ماژول" tmp-p68/boot1.log | head -6
kill $P1 2>/dev/null; sleep 2

echo "== [C] instance 2: بدون Access DLLs — بانک تازه =="
kill $P1 2>/dev/null; pkill -f "tmp-p68" 2>/dev/null; sleep 3
rm -f "$T"/HrModules.Access.dll "$T"/HrModules.Access.Data.dll
N2=$(ls "$T"/HrModules.*.dll 2>/dev/null | wc -l)
echo "plugin DLLs after removal: $N2 (باید ۹ باشد)"
rm -f "$T/hrsettings.bin" "$T/license-trial.json" "$T"/noacc68.db; rm -rf "$T/App_Data"
echo "hrsettings.bin removed: $(ls "$T/hrsettings.bin" 2>/dev/null || echo yes)"
P2=$(boot "$T" "$B2" tmp-p68/boot2.log)
waitboot "$B2" $P2 || { kill $P2 2>/dev/null; exit 3; }
wizard "$B2" tmp-p68/jar2 noacc68.db false || { echo "--- setup page head:"; curl -s --max-time 8 "$B2/Setup" | head -c 300; kill $P2 2>/dev/null; exit 3; }
sleep 2
T2=$(tables "$T/noacc68.db")
echo "tables(no-access)=$T2 (آخرین تایید فاز ۶۷ روی همان معماری: ۵۵)"
echo "  Zones=$(has $T/noacc68.db Zones) (باید ۰) Gates=$(has $T/noacc68.db Gates) (۰) AccessEvents=$(has $T/noacc68.db AccessEvents) (۰)"
echo "  Shifts=$(has $T/noacc68.db Shifts) (۱ — ماژول حضور حاضر) ParkingLots=$(has $T/noacc68.db ParkingLots) (۱)"
GA=$(curl -s -o /dev/null -w "%{http_code} → %{redirect_url}" --max-time 8 "$B2/Access")
echo "  /Access gate=$GA (باید ریدایرکت دوستانه به ModuleDisabled?code=access باشد)"
GB=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$B2/Attendance")
echo "  /Attendance=$GB (ماژول حاضر — endpointexists؛ 302 لاگین طبیعی است)"
H2=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$B2/health")
echo "  health=$H2"
kill $P2 2>/dev/null

echo "== VERDICT =="
ok=1
[ "$T1" -lt 60 ] && { echo "FAIL: جدول‌های کامل کمتر از انتظار ($T1)"; ok=0; }
[ "$(has $T/noacc68.db Zones)" != "0" ] && { echo "FAIL: جدول Zones بدون ماژول access ساخته شده!"; ok=0; }
[ "$(has $T/noacc68.db Shifts)" != "1" ] && { echo "FAIL: جدول Shifts با ماژول حضورِ حاضر ساخته نشده!"; ok=0; }
[ "$ok" = "1" ] && echo "PHASE-68 VERDICT: PASS" || echo "PHASE-68 VERDICT: FAIL"
