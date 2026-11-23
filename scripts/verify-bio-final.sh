#!/bin/bash
# تست نهایی چرخه کامل هویتی با شناسه‌های واقعی دیتابیس
B=http://localhost:3000
J=/tmp/hr-c2.txt
UA="Mozilla/5.0"
rm -f $J

curl -s -c $J -b $J -X POST "$B/api/auth/login" -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@1234"}' -o /dev/null

# شناسه‌های واقعی
DEVS=$(python3 -c "
import sqlite3
c=sqlite3.connect('/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db')
rows=[r[0] for r in c.execute('''SELECT d.Id FROM Devices d
 LEFT JOIN (SELECT DeviceId,COUNT(*) n FROM DeviceUsers GROUP BY DeviceId) du ON du.DeviceId=d.Id
 WHERE IFNULL(du.n,0)=0 LIMIT 1''')]
empty=rows[0] if rows else [r[0] for r in c.execute('SELECT Id FROM Devices LIMIT 1')][0]
anydev=[r[0] for r in c.execute('SELECT Id FROM Devices LIMIT 1')][0]
print(empty, anydev)")
EMPTY=$(echo $DEVS | cut -d' ' -f1); ANY=$(echo $DEVS | cut -d' ' -f2)
echo "دستگاه خالی: $EMPTY | دستگاه نمونه: $ANY"

PAGE=$(curl -s -c $J -b $J -A "$UA" "$B/Biometrics")
TOKEN=$(echo "$PAGE" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')

echo "== 1) شبیه‌ساز: دریافت پرسنل از دستگاه خالی =="
code=$(curl -s -o /tmp/s1.html -w "%{http_code}" -b $J -c $J -A "$UA" -X POST "$B/Biometrics/SimulateFetch" \
  -d "deviceId=$EMPTY&mode=users&__RequestVerificationToken=$TOKEN")
echo "HTTP $code (302=PRG موفق)"
curl -s -b $J -c $J -A "$UA" "$B/Biometrics" -o /tmp/p1.html
python3 -c "
import html,re
t=html.unescape(open('/tmp/p1.html').read())
newpins=re.findall(r'۹۹۰[۰-۹]',t)
print('  پین‌های ۹۹۰x در صفحه:', sorted(set(newpins)))
print('  پیام موفقیت:', 'شبیه‌سازی دریافت پرسنل' in t)
"

echo "== 2) شبیه‌ساز: دریافت قالب‌های هویتی از همان دستگاه =="
code=$(curl -s -o /dev/null -w "%{http_code}" -b $J -c $J -A "$UA" -X POST "$B/Biometrics/SimulateFetch" \
  -d "deviceId=$EMPTY&mode=bios&__RequestVerificationToken=$TOKEN")
echo "HTTP $code"
curl -s -b $J -c $J -A "$UA" "$B/Biometrics" -o /tmp/p2.html
python3 -c "
import sqlite3
c=sqlite3.connect('/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db')
print('  DeviceUsers کل:', c.execute('SELECT COUNT(*) FROM DeviceUsers').fetchone()[0])
print('  DeviceBiometrics کل:', c.execute('SELECT COUNT(*) FROM DeviceBiometrics').fetchone()[0])
print('  EmployeeBiometrics (مرجع):', c.execute('SELECT COUNT(*) FROM EmployeeBiometrics').fetchone()[0])
"

echo "== 3) انتقال ۲ پرسنل + قالب‌ها به دستگاه نمونه =="
EIDS=$(python3 -c "
import sqlite3
c=sqlite3.connect('/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db')
ids=[r[0] for r in c.execute('''SELECT DISTINCT e.Id FROM Employees e JOIN EmployeeBiometrics b ON b.EmployeeId=e.Id LIMIT 2''')]
print('\n'.join(ids))")
ARGS="toDeviceIds=$ANY&includeBios=true&__RequestVerificationToken=$TOKEN"
for e in $EIDS; do ARGS="$ARGS&employeeIds=$e"; done
code=$(curl -s -o /dev/null -w "%{http_code}" -b $J -c $J -A "$UA" -X POST "$B/Biometrics/PushToDevices" -d "$ARGS")
echo "HTTP $code"
curl -s -b $J -c $J -A "$UA" "$B/Biometrics" -o /tmp/p3.html
python3 -c "
import html
t=html.unescape(open('/tmp/p3.html').read())
print('  پیام انتقال:', 'دستور انتقال' in t and 'در صف' in t)
"
echo "== 4) لاگ دستورهای هویتی =="
python3 -c "
import sqlite3
c=sqlite3.connect('/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db')
for r in c.execute('''SELECT Type,Status,substr(CreatedAt,1,16) FROM DeviceCommands
 WHERE Type IN ('get_users','get_user_bios','push_user_bios') ORDER BY CreatedAt DESC LIMIT 5'''):
  print(' ',r)
"
