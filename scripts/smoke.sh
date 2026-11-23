#!/bin/bash
# تست دود: ورود ادمین (jar واحد) + پیمایش ۲۶ مسیر اصلی
BASE=http://localhost:3000
JAR=/tmp/hr_smoke.jar
rm -f "$JAR"
HTML=/tmp/hr_login.html

curl -s -c "$JAR" "$BASE/Auth/Login" -o "$HTML"
TOK=$(rg -o 'name="__RequestVerificationToken"[^>]*value="([^"]+)"' -r '$1' "$HTML" | head -1)
[ -z "$TOK" ] && { echo "NO-TOKEN"; exit 1; }

CODE=$(curl -s -b "$JAR" -c "$JAR" -o /dev/null -w "%{http_code}" \
  -d "identifier=admin" -d "secret=Admin@1234" \
  -d "__RequestVerificationToken=$TOK" "$BASE/Auth/Login")
echo "LOGIN: $CODE (expect 302)"
[ "$CODE" = "302" ] || exit 1

ROUTES=("/" "/Setup" "/Employees" "/Devices" "/Attendance" "/Shifts" "/Payroll" \
  "/Visitors" "/Reports" "/Requests" "/Holidays" "/Org" "/Cards" \
  "/Biometrics" "/Users" "/Companies" "/Settings" "/Audit" \
  "/Notifications" "/Access" "/Gateway" "/Parking" "/Parking?tab=live" "/Parking?tab=cameras" \
  "/Devices/LiveView" "/Reports/Parking" "/Export/Employees" "/Ess" "/Self" \
  "/Auth/Denied" "/Home/Error" "/License")
BAD=""
for r in "${ROUTES[@]}"; do
  c=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" --max-time 15 "$BASE$r")
  case "$c" in 200|302) ;; *) BAD="$BAD $r=$c";; esac
done
[ -z "$BAD" ] && echo "ROUTES: ALL-OK (32)" || echo "ROUTES-BAD:$BAD"

# ویزارد راه‌اندازی (فاز ۴۵/۴۷) — سامانهٔ سالم: /Setup باید به داشبورد برگردد (302)
SETUP_R=$(curl -s -b "$JAR" -o /dev/null -w "%{redirect_url}" --max-time 10 "$BASE/Setup")
echo "$SETUP_R" | rg -q "/$" && echo "SETUP-GATE: ok (redirect to /)" || echo "SETUP-GATE: bad ($SETUP_R)"

# مجوز نصب (فاز ۴۶) — صفحهٔ وضعیت + اثر انگشت سخت‌افزار
curl -s -b "$JAR" "$BASE/License" -o /tmp/hr_lic.html
rg -q "مجوز نصب سامانه" /tmp/hr_lic.html && echo "LICENSE-OK: صفحهٔ مجوز" || echo "LICENSE-MISS: صفحهٔ مجوز"
rg -q 'id="licFp">[A-Z2-9]{5}-[A-Z2-9]{5}-[A-Z2-9]{5}-[A-Z2-9]{5}-[A-Z2-9]{5}<' /tmp/hr_lic.html && echo "LICENSE-FP: ok" || echo "LICENSE-FP: bad"

# نشانه‌های نمای زنده حراست (فاز ۴۳)
curl -s -b "$JAR" "$BASE/Devices/LiveView" -o /tmp/hr_lvdev.html
for m in "دیوار دستگاه‌های تردد" "ترددهای زنده" "نوتیفیکیشن"; do
  rg -q "$m" /tmp/hr_lvdev.html && echo "SEC-LIVE-OK: $m" || echo "SEC-LIVE-MISS: $m"
done
# خوراک JSON ترددهای خام
PF=$(curl -s -b "$JAR" "$BASE/Devices/PunchFeed" | head -c 300)
echo "$PF" | rg -q '"chips"' && echo "PUNCHFEED-JSON: ok" || echo "PUNCHFEED-JSON: bad"
# پالت پارسان فعال (فاز ۴۳)
rg -q "14b8a6" /tmp/hr_lvdev.html && echo "PARSAN-PALETTE: ok" || echo "PARSAN-PALETTE: bad"

# نشانه‌های نمای زنده پارکینگ (فاز ۴۲)
curl -s -b "$JAR" "$BASE/Parking?tab=live" -o /tmp/hr_live.html
for m in "دیوار دوربین‌های پلاک‌خوان" "خوانش‌های زنده" "داخل پارکینگ"; do
  rg -q "$m" /tmp/hr_live.html && echo "LIVE-OK: $m" || echo "LIVE-MISS: $m"
done
# خوراک JSON نمای زنده
LV=$(curl -s -b "$JAR" "$BASE/Parking/LiveData" | head -c 200)
echo "$LV" | rg -q '"ok" *: *true' && echo "LIVEDATA-JSON: ok" || echo "LIVEDATA-JSON: bad"

# نشانه‌های گزارش ورود/خروج خودروها (فاز ۴۲)
curl -s -b "$JAR" "$BASE/Reports/Parking" -o /tmp/hr_rpark.html
for m in "گزارش ورود/خروج خودروها" "خوانش خودکار پلاک" "پلاک‌خوان \(ثبت خودکار دوربین\)"; do
  rg -q "$m" /tmp/hr_rpark.html && echo "RPARK-OK: $m" || echo "RPARK-MISS: $m"
done
# خروجی‌ها
C1=$(curl -s -b "$JAR" -o /tmp/hr_rpark.xlsx -w "%{http_code}" "$BASE/Reports/ParkingXlsx")
C2=$(curl -s -b "$JAR" -o /tmp/hr_rpark.pdf -w "%{http_code}" "$BASE/Reports/ParkingPdf")
[ "$C1" = "200" ] && head -c 4 /tmp/hr_rpark.xlsx | rg -q "PK" && echo "RPARK-XLSX: ok" || echo "RPARK-XLSX: bad ($C1)"
[ "$C2" = "200" ] && head -c 5 /tmp/hr_rpark.pdf | rg -q "%PDF" && echo "RPARK-PDF: ok" || echo "RPARK-PDF: bad ($C2)"

# نشانه‌های ماژول پارکینگ
curl -s -b "$JAR" "$BASE/Parking" -o /tmp/hr_park.html
for m in "تابلوی پارکینگ" "اشغال پارکینگ" "خودروها"; do
  rg -q "$m" /tmp/hr_park.html && echo "PARK-OK: $m" || echo "PARK-MISS: $m"
done

# نشانه‌های دوربین‌های پلاک‌خوان (فاز ۴۱)
curl -s -b "$JAR" "$BASE/Parking?tab=cameras" -o /tmp/hr_cams.html
for m in "دوربین‌های پلاک‌خوان \(ANPR\)" "آستانه اطمینان" "api/anpr/webhook"; do
  rg -q "$m" /tmp/hr_cams.html && echo "ANPR-OK: $m" || echo "ANPR-MISS: $m"
done
# پل دوربین: احراز بدون توکن باید رد شود (401) و با توکن باید ضربان بدهد
read -r C_SN C_TOK < <(python3 - <<'PY'
import sqlite3
c = sqlite3.connect('/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db')
row = c.execute("SELECT Serial, WebhookToken FROM AnprCameras WHERE Active=1 LIMIT 1").fetchone()
print((row[0] + ' ' + row[1]) if row else '- -')
PY
)
if [ -n "$C_SN" ] && [ "$C_SN" != "-" ]; then
  A=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/anpr/webhook?SN=$C_SN")
  B=$(curl -s "$BASE/api/anpr/webhook?SN=$C_SN" -H "X-Device-Token: $C_TOK" | head -c 12)
  [ "$A" = "401" ] && echo "ANPR-AUTH: ok" || echo "ANPR-AUTH: bad ($A)"
  echo "$B" | rg -q '"ok": *true' && echo "ANPR-PING: ok" || echo "ANPR-PING: bad ($B)"
else
  echo "ANPR-SKIP: no camera in db"
fi

# نشانه‌های کتابخانه پالت در تنظیمات
curl -s -b "$JAR" "$BASE/Settings" -o /tmp/hr_set.html
for m in "پالت" "کتابخانه" "ApplyPalette"; do
  rg -q "$m" /tmp/hr_set.html && echo "MARK-OK: $m" || echo "MARK-MISS: $m"
done
# صفحه ورود: برند و نسخه
rg -q "نسخه" "$HTML" && echo "LOGIN-VER: ok" || echo "LOGIN-VER: missing"
[ -z "$BAD" ] || exit 1
