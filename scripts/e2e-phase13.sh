#!/usr/bin/env bash
# E2E Phase 13: موتور اعلان‌ها — قواعد، تنظیمات، اجرای آزمایشی، لاگ
set -u
BASE="${BASE:-http://localhost:3000}"
JAR=$(mktemp)
DBQ="python3.13 /home/z/my-project/scripts/dbq.py"
PASS=0; FAIL=0
ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); echo "  ✗ $1"; }
q() { $DBQ "$1"; }

echo "== login =="
code=$(curl -s -c "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}' --max-time 10)
[ "$code" = "200" ] && ok "login 200" || bad "login $code"

AF=$(curl -s -b "$JAR" -c "$JAR" "$BASE/Notifications" | rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]+"' -N | head -1 | sed 's/.*value="//;s/"$//')
[ -n "$AF" ] && ok "antiforgery token" || bad "no antiforgery token"

echo "== P13.1 panel renders =="
code=$(curl -s -b "$JAR" -o /tmp/notif.html -w "%{http_code}" "$BASE/Notifications" --max-time 15)
[ "$code" = "200" ] && ok "GET /Notifications 200" || bad "GET /Notifications $code"
for t in "موتور اعلان‌ها" "تنظیمات کانال‌های ارسال" "لاگ اعلان‌ها" "اجرای آزمایشی" "قاعده تعریف‌شده"; do
  rg -q "$t" /tmp/notif.html && ok "panel section: $t" || bad "section missing: $t"
done

echo "== P13.2 create rule (late ≥ 15 min) =="
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Notifications/SaveRule" \
  --data-urlencode "name=تأخیر تست E2E" \
  --data-urlencode "type=late" \
  --data-urlencode "threshold=15" \
  --data-urlencode "recipientEmail=hr-test@example.com" \
  --data-urlencode "viaEmail=true" \
  --data-urlencode "enabled=true" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "POST SaveRule 302" || bad "POST SaveRule $code"
RID=$(q "SELECT Id FROM NotificationRules WHERE Name='تأخیر تست E2E'")
[ -n "$RID" ] && [ "$RID" != "None" ] && ok "rule persisted in DB ($RID)" || bad "rule missing"

echo "== P13.3 invalid type rejected =="
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code} %{redirect_url}" -X POST "$BASE/Notifications/SaveRule" \
  --data-urlencode "name=قاعده نامعتبر" \
  --data-urlencode "type=space_lasers" \
  --data-urlencode "threshold=10" \
  --data-urlencode "__RequestVerificationToken=$AF")
echo "$code" | rg -q "^302 .*/Notifications" && ok "invalid type → back with toast" || bad "invalid type → $code"

echo "== P13.4 settings save =="
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Notifications/Settings" \
  --data-urlencode "EnabledEmail=true" \
  --data-urlencode "SmtpHost=smtp.example.com" \
  --data-urlencode "SmtpPort=587" \
  --data-urlencode "SmtpSsl=true" \
  --data-urlencode "AdminEmails=boss@example.com" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "POST Settings 302" || bad "POST Settings $code"
H=$(q "SELECT SmtpHost FROM NotificationSettings WHERE Id='default'")
[ "$H" = "smtp.example.com" ] && ok "settings persisted (smtp host)" || bad "settings wrong: $H"

echo "== P13.5 run now → simulated email (SMTP unreachable) =="
# خوداتکا: قاعده «تأخیر امروز» به رکورد تأخیر برای تاریخ امروز نیاز دارد
TODAY=$(date +%F)
LATE_EMP=$($DBQ "SELECT Id FROM Employees ORDER BY PersonnelCode LIMIT 1")
LATE_EXISTS=$($DBQ --p "$LATE_EMP" --p "$TODAY" "SELECT COUNT(*) FROM AttendanceRecords WHERE EmployeeId=? AND Date=?")
if [ "$LATE_EXISTS" = "0" ]; then
  $DBQ --exec --p "$LATE_EMP" --p "$TODAY" \
    "INSERT INTO AttendanceRecords (Id, EmployeeId, Date, LateMinutes, EarlyMinutes, OvertimeMinutes, WorkMinutes, Status, CreatedAt, UpdatedAt) VALUES ('e2e-late-' || hex(randomblob(8)), ?, ?, 25, 0, 0, 480, 'تأخیر', datetime('now'), datetime('now'))"
  ok "late record seeded for today ($TODAY)"
fi
BEFORE=$(q "SELECT COUNT(*) FROM NotificationLogs")
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Notifications/RunNow" --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "POST RunNow 302" || bad "RunNow $code"
sleep 2
AFTER=$(q "SELECT COUNT(*) FROM NotificationLogs")
echo "  logs: $BEFORE → $AFTER"
[ "$AFTER" -gt "$BEFORE" ] && ok "notification log row created" || bad "no log row"
CH=$(q "SELECT TOP 1 Channel FROM NotificationLogs ORDER BY CreatedAt DESC")
ST=$(q "SELECT TOP 1 Status FROM NotificationLogs ORDER BY CreatedAt DESC")
echo "  channel=$CH status=$ST"
[ "$ST" = "simulated" ] || [ "$ST" = "sent" ] || [ "$ST" = "failed" ] && ok "log status recorded ($ST)" || bad "bad status $ST"

echo "== P13.6 toggle + edit + delete =="
curl -s -b "$JAR" -o /dev/null -X POST "$BASE/Notifications/ToggleRule" --data-urlencode "id=$RID" --data-urlencode "__RequestVerificationToken=$AF"
E=$(q "SELECT CASE WHEN Enabled=1 THEN '1' ELSE '0' END FROM NotificationRules WHERE Id='$RID'")
[ "$E" = "0" ] && ok "toggle → disabled" || bad "toggle failed: $E"
curl -s -b "$JAR" -o /dev/null -X POST "$BASE/Notifications/ToggleRule" --data-urlencode "id=$RID" --data-urlencode "__RequestVerificationToken=$AF"
E=$(q "SELECT CASE WHEN Enabled=1 THEN '1' ELSE '0' END FROM NotificationRules WHERE Id='$RID'")
[ "$E" = "1" ] && ok "toggle → enabled again" || bad "toggle back failed: $E"
code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/Notifications/SaveRule" \
  --data-urlencode "id=$RID" \
  --data-urlencode "name=تأخیر تست E2E ویرایش" \
  --data-urlencode "type=late" \
  --data-urlencode "threshold=20" \
  --data-urlencode "viaEmail=true" \
  --data-urlencode "enabled=true" \
  --data-urlencode "__RequestVerificationToken=$AF")
[ "$code" = "302" ] && ok "edit rule 302" || bad "edit rule $code"
TH=$(q "SELECT Threshold FROM NotificationRules WHERE Id='$RID'")
[ "$TH" = "20" ] && ok "edit persisted (threshold=20)" || bad "edit not persisted: $TH"
curl -s -b "$JAR" -o /dev/null -X POST "$BASE/Notifications/DeleteRule" --data-urlencode "id=$RID" --data-urlencode "__RequestVerificationToken=$AF"
GONE=$(q "SELECT COUNT(*) FROM NotificationRules WHERE Id='$RID'")
[ "$GONE" = "0" ] && ok "rule deleted" || bad "rule still exists"

echo "== P13.7 regression — pages 200 =="
for p in "/" "/Employees" "/Org" "/Attendance" "/Devices" "/Biometrics" "/Notifications" "/Users" "/Audit"; do
  code=$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" "$BASE$p" --max-time 15)
  [ "$code" = "200" ] && ok "$p 200" || bad "$p → $code"
done

echo ""
echo "RESULT: $PASS passed, $FAIL failed"
# پاک‌سازی لاگ‌های تستی
$DBQ --exec "DELETE FROM NotificationLogs WHERE Recipient LIKE '%example.com%'" >/dev/null
[ "$FAIL" = "0" ]
