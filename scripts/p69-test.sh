#!/bin/bash
# ============================================================
# p69-test.sh — تست ماتریس فاز ۶۹ (org/devices پلاگین حذف‌شدنی)
#   A) publish کامل — ۱۳ پلاگین (۵ ماژول سیستم + دروازه + org/devices + دیتاها)
#   B) نمونهٔ ۱: همهٔ ماژول‌ها — ویزارد + seedDemo=true → جدول‌های پایه/ماژولی
#      + لاگین ادمین → ترتیب گروه‌های منو از Sort خود ماژول‌ها
#   C) نمونهٔ ۲: حذف دستی HrModules.Org.dll و HrModules.Devices.dll — بانک تازه:
#      بوت سالم، جدول‌های پایهٔ پرسنلی/دستگاه سرجای‌شان، مسیرها بدون endpoint
#   D) نمونهٔ ۳: همان حذف + modules.json با disabled=["org","devices"] —
#      صفحهٔ دوستانهٔ «نصب نشده» (منبع شناخت = دادهٔ مدیر، نه کد هاست)
# ============================================================
set -u
cd /home/z/my-project
export PATH="/home/z/.dotnet:$PATH"
SRC=aspnet/src/HrAttendance.Web
T=tmp-p69/full
B1=http://127.0.0.1:3511
B2=http://127.0.0.1:3512
B3=http://127.0.0.1:3513

echo "== [A] publish =="
rm -rf tmp-p69
dotnet publish $SRC -c Debug -o "$T" --nologo -v q 2>&1 | tail -2
N=$(ls "$T"/HrModules.*.dll 2>/dev/null | wc -l)
echo "plugin DLLs in publish: $N (انتظار ۱۳)"
[ "$N" -lt 13 ] && { echo "FAIL-PUBLISH-PLUGINS ($N)"; exit 1; }
ls "$T"/HrModules.Org.dll "$T"/HrModules.Devices.dll >/dev/null 2>&1 || { echo "FAIL: DLL org/devices در خروجی نیست"; exit 1; }

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
login() { # $1=url $2=jar — لاگین ادمین؛ خروجی: ok/fail
  local PAGE AF R
  PAGE=$(curl -s -c "$2" --max-time 8 "$1/Auth/Login")
  AF=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
  [ -z "$AF" ] && { echo "FAIL-AF-LOGIN"; return 1; }
  curl -s -b "$2" -c "$2" -X POST --max-time 15 "$1/Auth/Login" \
    --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
    --data-urlencode "__RequestVerificationToken=$AF" -o /dev/null
  R=$(curl -s -b "$2" --max-time 10 -L --max-redirs 2 "$1/")
  echo "$R" | grep -q "خانه من" && return 0 || return 1
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

echo "== [B] instance 1: FULL + demo seed + منو =="
rm -f "$T/hrsettings.bin" "$T/license-trial.json" "$T"/demo69.db "$T"/App_Data/modules.json; rm -rf "$T/App_Data"
P1=$(boot "$T" "$B1" tmp-p69/boot1.log)
waitboot "$B1" $P1 || { kill $P1 2>/dev/null; exit 2; }
wizard "$B1" tmp-p69/jar1 demo69.db true || { kill $P1 2>/dev/null; exit 2; }
sleep 3
T1=$(tables "$T/demo69.db")
echo "tables(full)=$T1"
echo "  Employees=$(has $T/demo69.db Employees) Devices=$(has $T/demo69.db Devices) Shifts=$(has $T/demo69.db Shifts) Zones=$(has $T/demo69.db Zones) PayrollRuns=$(has $T/demo69.db PayrollRuns)"
rg "کشف پلاگین" tmp-p69/boot1.log | tail -1
if login "$B1" tmp-p69/jar1; then
  echo "  login OK — ترتیب گروه‌های منو:"
  curl -s -b tmp-p69/jar1 --max-time 10 -L --max-redirs 2 "$B1/" | grep -o 'class="nav-section-title"[^>]*>[^<]*' | sed 's/.*>//' | head -12
else
  echo "  login FAIL (منو چک نشد)"
fi
E1=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$B1/Employees")
D1=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$B1/Devices")
echo "  /Employees=$E1 /Devices=$D1 (endpoint موجود — 302 لاگین طبیعی)"
kill $P1 2>/dev/null; sleep 2

echo "== [C] instance 2: بدون Org/Devices DLL — بانک تازه =="
pkill -f "tmp-p69" 2>/dev/null; sleep 3
rm -f "$T"/HrModules.Org.dll "$T"/HrModules.Devices.dll
N2=$(ls "$T"/HrModules.*.dll 2>/dev/null | wc -l)
echo "plugin DLLs after removal: $N2 (باید ۱۱ باشد)"
rm -f "$T/hrsettings.bin" "$T/license-trial.json" "$T"/noorg69.db "$T"/App_Data/modules.json; rm -rf "$T/App_Data"
P2=$(boot "$T" "$B2" tmp-p69/boot2.log)
waitboot "$B2" $P2 || { kill $P2 2>/dev/null; exit 3; }
wizard "$B2" tmp-p69/jar2 noorg69.db false || { kill $P2 2>/dev/null; exit 3; }
sleep 2
T2=$(tables "$T/noorg69.db")
echo "tables(no-org/devices)=$T2 — جدول‌های پایهٔ پرسنلی/دستگاه باید سرجای‌شان باشند (پلتفرم):"
echo "  Employees=$(has $T/noorg69.db Employees) (۱) Devices=$(has $T/noorg69.db Devices) (۱) Shifts=$(has $T/noorg69.db Shifts) (۱) Zones=$(has $T/noorg69.db Zones) (۱)"
E2=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$B2/Employees")
D2=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$B2/Devices")
H2=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$B2/health")
echo "  /Employees=$E2 /Devices=$D2 (کنترلر غایب — ۴۰۴ بدون کرش) health=$H2"
echo "  درخت پلاگین پس از حذف: $(rg -c 'کشف پلاگین' tmp-p69/boot2.log >/dev/null && rg 'کشف پلاگین' tmp-p69/boot2.log | tail -1)"
kill $P2 2>/dev/null; sleep 2

echo "== [D] instance 3: حذف + modules.json (disabled org/devices) =="
pkill -f "tmp-p69" 2>/dev/null; sleep 3
mkdir -p "$T/App_Data"
printf '{ "disabled": ["org", "devices"] }' > "$T/App_Data/modules.json"
rm -f "$T/hrsettings.bin" "$T/license-trial.json" "$T"/known69.db; rm -rf "$T"/hr.db*
P3=$(boot "$T" "$B3" tmp-p69/boot3.log)
waitboot "$B3" $P3 || { kill $P3 2>/dev/null; exit 4; }
wizard "$B3" tmp-p69/jar3 known69.db false || { kill $P3 2>/dev/null; exit 4; }
sleep 2
E3=$(curl -s -o /dev/null -w "%{http_code} → %{redirect_url}" --max-time 8 "$B3/Employees")
D3=$(curl -s -o /dev/null -w "%{http_code} → %{redirect_url}" --max-time 8 "$B3/Devices")
echo "  /Employees=$E3 (ریدایرکت دوستانه به ModuleDisabled?code=org)"
echo "  /Devices=$D3 (ریدایرکت دوستانه به ModuleDisabled?code=devices)"
H3=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$B3/health")
echo "  health=$H3"
kill $P3 2>/dev/null

echo "== VERDICT =="
ok=1
[ "$T1" -lt 60 ] && { echo "FAIL: جدول‌های کامل کمتر از انتظار ($T1)"; ok=0; }
[ "$(has $T/demo69.db Employees)" != "1" ] && { echo "FAIL: جدول Employees ساخته نشده!"; ok=0; }
[ "$(has $T/noorg69.db Employees)" != "1" ] && { echo "FAIL: جدول پایهٔ Employees بدون پلاگین org نباید حذف می‌شد!"; ok=0; }
[ "$(has $T/noorg69.db Devices)" != "1" ] && { echo "FAIL: جدول پایهٔ Devices نباید حذف می‌شد!"; ok=0; }
[ "$H2" != "200" ] && { echo "FAIL: health در بوت بدون org/devices = $H2"; ok=0; }
[ "$ok" = "1" ] && echo "PHASE-69 VERDICT: PASS" || echo "PHASE-69 VERDICT: FAIL"
