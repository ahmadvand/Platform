#!/bin/bash
# راستی‌آزمایی نهایی ۸۷ب — همه‌چیز در یک invocation
cd /home/z/my-project
pkill -f start-web.sh 2>/dev/null; pkill -f Platform.Web.dll 2>/dev/null; sleep 3
setsid nohup bash scripts/start-web.sh </dev/null >>dev.log 2>&1 & disown
code=000
for i in $(seq 1 15); do code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 http://localhost:3000/health 2>/dev/null); [ "$code" = "200" ] && break; sleep 3; done
echo "health=$code"

J=/tmp/jar2.txt; rm -f $J
TOKEN=$(curl -s -c $J --max-time 8 http://localhost:3000/Auth/Login | rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
# احراز با فرم (identifier/secret)
curl -s -b $J -c $J -o /dev/null -w "login_post=%{http_code}\n" --max-time 8 -X POST http://localhost:3000/Auth/Login \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOKEN"
curl -s -b $J -c $J -o /dev/null -w "home=%{http_code}\n" --max-time 8 http://localhost:3000/
# پذیرش هشدار مجوز (حالت Strict) — کوکی تأیید
T2=$(curl -s -b $J -c $J --max-time 8 http://localhost:3000/License/Notice | rg -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
curl -s -b $J -c $J -o /dev/null -w "ack=%{http_code}\n" --max-time 8 -X POST http://localhost:3000/License/Ack --data-urlencode "__RequestVerificationToken=$T2"

echo "=== صفحات ==="
for p in /Users /Users/Roles /Settings /Langs /Parking /Employees /Payroll /Attendance /Visitors /Devices /Gateway /Audit /Reports /License /dl/index.html; do
  echo "  $p -> $(curl -s -b $J -c $J -o /dev/null -w '%{http_code}' --max-time 8 http://localhost:3000$p)"
done

echo "=== مرکز دانلود ==="
curl -s -b $J --max-time 8 http://localhost:3000/dl/index.html | rg -o "HR-Platform-2.7.4[^\"<]*|LicenseStudio.zip|TranslationStudio.zip" | sort -u | head -6

echo "=== زبان‌ها روی صفحه ورود ==="
curl -s --max-time 6 -H "Cookie: hr_lang=ps" http://localhost:3000/Auth/Login | rg -c "سیسټم ته ننوتل"
curl -s --max-time 6 -H "Cookie: hr_lang=ka" http://localhost:3000/Auth/Login | rg -c "&#x10E8;&#x10D4;&#x10E1;&#x10D5;&#x10DA;" || true

echo "=== GUID + دادهٔ زنده ==="
python3 - <<'PY'
import sqlite3
con = sqlite3.connect('/home/z/my-project/aspnet/src/Platform.Web/hr.db')
tables = [r[0] for r in con.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")]
bad = []
for t in tables:
    for c in con.execute(f'PRAGMA table_info("{t}")'):
        if c[5] > 0 and c[2].upper() != 'TEXT':
            bad.append((t, c[1], c[2]))
print("tables:", len(tables), "| non-TEXT PKs:", bad if bad else "NONE ✓")
print("langs:", con.execute("SELECT COUNT(*) FROM HrLangDefs WHERE IsActive=1").fetchone()[0])
print("audit rows:", con.execute("SELECT COUNT(*) FROM AuditLogs").fetchone()[0])
print("latest audit:", con.execute("SELECT Action, At FROM AuditLogs ORDER BY At DESC LIMIT 1").fetchone())
con.close()
PY
