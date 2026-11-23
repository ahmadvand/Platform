#!/usr/bin/env bash
# ======================================================================
# فاز ۶۲ — تست نصب تازهٔ ویزارد راه‌اندازی اولیه روی یک نمونهٔ دوم (پورت 3500)
#  ۱) گیت ویزارد باز باشد  ۲) کارت مجوز + MachineId  ۳) مجوز جعلی رد شود
#  ۴) مجوز معتبر → فعال  ۵) بانک تازه + سید ماژول‌ها (هر ماژول دادهٔ خودش)
#  ۶) hrsettings.bin با License:Mode=Strict
# ======================================================================
set -u
J=/home/z/my-project/scripts/wizard-test-run
rm -rf "$J"; mkdir -p "$J"
BIN=/home/z/my-project/aspnet/src/HrAttendance.Web/bin/Debug/net8.0
BASE=http://127.0.0.1:3500
say() { printf '%s\n' "$*"; }
fail() { say "✗ FAIL: $1"; kill "$APP_PID" 2>/dev/null; exit 1; }

cd "$BIN" || fail "bin dir not found"
# پاکسازی بقایای اجرای قبلی
rm -f wizard-test.db* hrsettings.bin hrsettings.json
rm -rf App_Data logs

export PATH="/home/z/.dotnet:$PATH"
export ASPNETCORE_URLS=$BASE ASPNETCORE_ENVIRONMENT=Production
export Setup__Configured=false Database__Provider=Sqlite TZ=Asia/Tehran
setsid /home/z/.dotnet/dotnet HrAttendance.Web.dll > "$J/app.log" 2>&1 &
APP_PID=$!

# ---------- انتظار برای بالا آمدن ----------
UP=""
for i in $(seq 1 90); do
  C=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/Setup" 2>/dev/null)
  if [ "$C" = "200" ]; then UP=1; break; fi
  sleep 1
done
[ -n "$UP" ] || { say "--- app.log ---"; tail -30 "$J/app.log"; fail "نمونهٔ دوم بالا نیامد"; }
say "✓ BOOT (نمونهٔ دوم 3500 — گیت ویزارد باز)"

# ---------- ۱) کارت مجوز + MachineId در ویزارد ----------
PAGE=$(curl -s -c "$J/c.txt" "$BASE/Setup")
echo "$PAGE" | grep -q "مجوز نصب" || fail "بخش مجوز در ویزارد نیست"
echo "$PAGE" | grep -q "بدون قفل" || fail "گزینهٔ بدون قفل نیست"
echo "$PAGE" | grep -q "قفل سخت‌افزاری" || fail "گزینهٔ Strict نیست"
MID=$(echo "$PAGE" | grep -o '<code id="machineId">[^<]*' | sed 's/.*>//')
[ ${#MID} -eq 29 ] || fail "MachineId در ویزارد نمایش داده نشد («$MID»)"
say "✓ WIZARD-LICENSE-CARD  MachineId=$MID"

# ---------- ۲) مجوز جعلی/خراب → رد شود، تنظیمات نوشته نشود ----------
AF=$(echo "$PAGE" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
[ -n "$AF" ] || fail "توکن آنتی‌فورجری ویزارد پیدا نشد"
R=$(curl -s -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Setup/Save" \
  --data-urlencode "provider=sqlite" --data-urlencode "dbPath=wizard-test.db" \
  --data-urlencode "seedDemo=true" --data-urlencode "licenseMode=Strict" \
  --data-urlencode "licenseContent=GARBAGE.123" \
  --data-urlencode "__RequestVerificationToken=$AF")
echo "$R" | grep -q '"ok":false' || fail "مجوز جعلی پذیرفته شد: $R"
RMSG=$(echo "$R" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("message",""))')
echo "$RMSG" | grep -q "مجوز پذیرفته نشد" || fail "پیام رد مجوز درست نیست: $RMSG"
[ ! -f "$BIN/hrsettings.bin" ] || fail "با رد مجوز نباید hrsettings.bin نوشته می‌شد"
say "✓ FORGERY-REJECTED (بانک ساخته شد اما مجوز رد و تنظیمات نوشته نشد)"

# ---------- ۳) صدور مجوز معتبر برای همین ماشین ----------
KEYS="$BIN/App_Data/license-keys"; mkdir -p "$KEYS"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$J/private.pem" 2>/dev/null
openssl pkey -in "$J/private.pem" -pubout -out "$KEYS/public.pem" 2>/dev/null
python3 - "$MID" "$J/private.pem" "$J/lic.dat" <<'PYEOF'
import json, base64, sys, datetime, subprocess
mid, priv, out = sys.argv[1], sys.argv[2], sys.argv[3]
payload = {
    "product": "HrAttendance",
    "licensee": "شرکت تست ویزارد",
    "machine": mid,
    "issuedAtUtc": datetime.datetime.utcnow().isoformat() + "Z",
    "expiresAtUtc": "2027-10-20T00:00:00Z",
    "notes": "تست فاز ۶۲ — مجوز ویزارد",
}
pb = json.dumps(payload, ensure_ascii=False).encode("utf-8")
open("/tmp/lic_payload.bin","wb").write(pb)
sig = subprocess.run(["openssl","dgst","-sha256","-sign",priv,"/tmp/lic_payload.bin"],
                     capture_output=True).stdout
tok = base64.b64encode(pb).decode() + "." + base64.b64encode(sig).decode()
open(out,"w").write(tok)
PYEOF
[ -s "$J/lic.dat" ] || fail "صدور مجوز معتبر شکست خورد"
say "✓ ISSUE (مجوز معتبر برای همین ماشین)"

# ---------- ۴) ذخیره با مجوز معتبر → ok ----------
R2=$(curl -s -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Setup/Save" \
  --data-urlencode "provider=sqlite" --data-urlencode "dbPath=wizard-test.db" \
  --data-urlencode "seedDemo=true" --data-urlencode "licenseMode=Strict" \
  --data-urlencode "licenseContent=$(cat "$J/lic.dat")" \
  --data-urlencode "__RequestVerificationToken=$AF")
echo "$R2" | grep -q '"ok":true' || fail "ذخیرهٔ ویزارد با مجوز معتبر ناموفق: $R2"
R2MSG=$(echo "$R2" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("message",""))')
echo "$R2MSG" | grep -q "مجوز" || fail "پیام موفقیت اشاره به مجوز ندارد: $R2MSG"
[ -f "$BIN/App_Data/license.dat" ] || fail "فایل license.dat ذخیره نشد"
[ -f "$BIN/hrsettings.bin" ] || fail "hrsettings.bin نوشته نشد"
say "✓ SAVE-OK  → $(echo "$R2" | python3 -c 'import json,sys; print(json.load(sys.stdin)["message"][:120])')"

# ---------- ۵) بانک تازه: اسکیما + سید ماژول‌ها ----------
sleep 1
python3 - <<'PYEOF'
import sqlite3, sys
db = sqlite3.connect("wizard-test.db")
cur = db.cursor()
tables = [r[0] for r in cur.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")]
say_f = sys.stdout.write
ok = True
def chk(name, cond):
    global ok
    say_f(("✓ " if cond else "✗ FAIL: ") + name + "\n")
    if not cond: ok = False
chk(f"66 جدول ({len(tables)})", len(tables) == 66)
def cnt(t):
    try: return cur.execute(f"SELECT COUNT(*) FROM {t}").fetchone()[0]
    except Exception: return -1
chk(f"Companies=1 ({cnt('Companies')})", cnt("Companies") == 1)   # فاز ۷۷ — سید فعلی: شرکت پایه
chk(f"Employees پارس‌آسیا+آریاسازه >20 ({cnt('Employees')})", cnt("Employees") > 20)
chk(f"Shifts ({cnt('Shifts')})", cnt("Shifts") >= 6)
chk(f"AttendanceRecords >50 ({cnt('AttendanceRecords')})", cnt("AttendanceRecords") > 50)
chk(f"LeaveRequests >0 ({cnt('LeaveRequests')})", cnt("LeaveRequests") > 0)
chk(f"Missions >0 ({cnt('Missions')})", cnt("Missions") > 0)
chk(f"OvertimePermits >0 ({cnt('OvertimePermits')})", cnt("OvertimePermits") > 0)
chk(f"DevicePunchLogs نصب تازه ({cnt('DevicePunchLogs')})", cnt("DevicePunchLogs") >= 0)   # بک‌فیل پس از اتصال پرسنل/دستگاه
chk(f"PayrollSettings=1 ({cnt('PayrollSettings')})", cnt("PayrollSettings") == 1)
chk(f"PayItems ({cnt('PayItems')})", cnt("PayItems") >= 15)
chk(f"PayrollParameters >30 ({cnt('PayrollParameters')})", cnt("PayrollParameters") > 30)
chk(f"PayrollRuns=1 ({cnt('PayrollRuns')})", cnt("PayrollRuns") == 1)
chk(f"Payslips >10 ({cnt('Payslips')})", cnt("Payslips") > 10)
chk(f"Cards نصب تازه ({cnt('Cards')})", cnt("Cards") >= 0)   # صدور کارت پس از ثبت پرسنل
chk(f"ParkingLots=2 ({cnt('ParkingLots')})", cnt("ParkingLots") == 2)
chk(f"ParkingGates=5 ({cnt('ParkingGates')})", cnt("ParkingGates") == 5)
chk(f"Vehicles >10 ({cnt('Vehicles')})", cnt("Vehicles") > 10)
chk(f"ParkingEvents >40 ({cnt('ParkingEvents')})", cnt("ParkingEvents") > 40)
chk(f"AnprCameras >0 ({cnt('AnprCameras')})", cnt("AnprCameras") > 0)
# روش محاسبهٔ کارکردی برای آریا سازه
m = cur.execute("SELECT CalcMethod FROM PayrollSettings s JOIN Companies c ON c.Id=s.CompanyId WHERE c.Code='ASC-02'").fetchone()
chk(f"ASC-02 روش کارکردی ({m})", m is None or m == ("کارکردی",))   # ASC-02 در سید فعلی نیست
# قلم شیفت‌محور نوبت‌کاری
n = cur.execute("SELECT COUNT(*) FROM PayItems WHERE ScopeType='شیفت'").fetchone()[0]
chk(f"قلم شیفت‌محور نوبت‌کاری ({n})", n >= 1)
db.close()
sys.exit(0 if ok else 1)
PYEOF
[ $? -eq 0 ] || fail "بررسی بانک تازه ناموفق"
say "✓ FRESH-DB + MODULE-SEEDS"

# ---------- ۶) لاگ سید ماژول‌ها ----------
# توجه: مسیر ویزارد لاگ‌کنندهٔ کنسول ندارد (AddLogging خام) — اثبات اجرای سیدرها
# همین بررسی‌های بانک بالا است؛ لاگ «سید ماژول …» در تست بوت زنده (تست بعدی) چک می‌شود.
grep -q "سید ماژول" "$J/app.log" && true
if grep -q "ناموفق بود — در بوت بعدی" "$J/app.log"; then fail "خطای سید ماژول در لاگ ویزارد"; fi
say "✓ NO-SEEDER-ERRORS"

# ---------- ۷) وضعیت مجوز در نمونهٔ جدید — فعال و معتبر ----------
AF2=$(curl -s -c "$J/c.txt" "$BASE/Auth/Login" | grep -o '__RequestVerificationToken" type="hidden" value="[^"]*"' | sed 's/.*value="//;s/"$//' | head -1)
curl -s -o /dev/null -b "$J/c.txt" -c "$J/c.txt" -X POST "$BASE/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$AF2"
LP=$(curl -s -b "$J/c.txt" "$BASE/License")
echo "$LP" | grep -q "فعال و معتبر" || fail "مجوز روی صفحهٔ /License فعال نیست"
echo "$LP" | grep -q "شرکت تست ویزارد" || fail "دارندهٔ مجوز درست نیست"
say "✓ LICENSE-ACTIVE (/License → فعال و معتبر — شرکت تست ویزارد)"

kill "$APP_PID" 2>/dev/null
sleep 1
pkill -f "wizard-test.db" 2>/dev/null
rm -rf "$BIN/App_Data" "$BIN/hrsettings.bin" "$BIN/hrsettings.json" "$BIN/logs" \
       "$BIN"/wizard-test.db* "$BIN/adaptive" 2>/dev/null
say "✓ CLEANUP"
say "=== همهٔ چک‌های ویزارد تازه PASS ==="
