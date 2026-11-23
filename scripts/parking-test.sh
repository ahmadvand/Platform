#!/bin/bash
# تست عملکردی موتور مجوز پارکینگ: عبور مجاز، آنتی‌پس‌بک، پلاک ناشناس، خروج
BASE=http://localhost:3000
JAR=/tmp/hr_smoke.jar

get_token () {
  curl -s -b "$JAR" "$BASE$1" | rg -o 'name="__RequestVerificationToken"[^>]*value="([^"]+)"' -r '$1' | head -1
}

TOK=$(get_token "/Parking/ScanForm")
[ -z "$TOK" ] && { echo "NO-TOKEN (ScanForm)"; exit 1; }

GATE=$(curl -s -b "$JAR" "$BASE/Parking/ScanForm" | rg -o '<option value="([^"]+)"' -r '$1' | head -1)
echo "GATE: $GATE"

post () { # plate direction
  curl -s -b "$JAR" -H "X-Requested-With: XMLHttpRequest" \
    -d "plate=$1" -d "gateId=$GATE" -d "direction=$2" -d "method=پلاک‌خوان" \
    -d "__RequestVerificationToken=$TOK" "$BASE/Parking/Scan"
}

echo "--- 1) ورود پلاک مجاز (انتظار ok:true)"
post "27د657 ایران 87" "ورود"; echo
echo "--- 2) ورود تکراری همان پلاک (انتظار آنتی‌پس‌بک ok:false)"
post "27د657 ایران 87" "ورود"; echo
echo "--- 3) پلاک ناشناس (انتظار ok:false — فاقد مجوز)"
post "99ف999 ایران 55" "ورود"; echo
echo "--- 4) خروج پلاک مجاز (انتظار ok:true)"
post "27د657 ایران 87" "خروج"; echo
echo "--- 5) جهت ناهم‌خوان روی گیت خروجی اصلی؟ (ورود روی راهبند خروجی — انتظار ok:false)"
OUTGATE=$(curl -s -b "$JAR" "$BASE/Parking/ScanForm" | python3 -c "
import sys, re
h = sys.stdin.read()
opts = re.findall(r'<option value=\"([^\"]+)\">([^<]+)</option>', h)
for oid, label in opts:
    if 'خروجی اصلی' in label: print(oid); break
")
curl -s -b "$JAR" -H "X-Requested-With: XMLHttpRequest" \
  -d "plate=31د295 ایران 11" -d "gateId=$OUTGATE" -d "direction=ورود" -d "method=پلاک‌خوان" \
  -d "__RequestVerificationToken=$TOK" "$BASE/Parking/Scan"; echo
echo "--- COUNTS ---"
python3 -c "
import sqlite3
con = sqlite3.connect('/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db')
for t in ['ParkingLots','ParkingGates','Vehicles','ParkingPermits','ParkingEvents']:
    print(t, con.execute(f'SELECT COUNT(*) FROM {t}').fetchone()[0])
print('alerts:', con.execute(\"SELECT COUNT(*) FROM SecurityAlerts WHERE Source='کنترل پارکینگ'\").fetchone()[0])
"
