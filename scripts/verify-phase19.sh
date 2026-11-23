#!/usr/bin/env bash
# ============================================================
# فاز ۱۹ — تکمیل سخت‌سازی زیرساخت: Serilog + HSTS + بکاپ/بازیابی + Docker
# اجرا: bash scripts/verify-phase19.sh
# ============================================================
set -u
cd "$(dirname "$0")/.."
ROOT="$(pwd)"
WEB="$ROOT/aspnet/src/HrAttendance.Web"
PASS=0; FAIL=0
ck() { if [ "$1" = "1" ]; then PASS=$((PASS+1)); echo "  ✓ $2"; else FAIL=$((FAIL+1)); echo "  ✗ $2"; fi }
export PATH="/home/z/.dotnet:$PATH"

echo "── ۱) health endpoint (از بخش قبلی فاز ۱۹)"
h=$(curl -s http://localhost:3000/health --max-time 8)
echo "$h" | grep -q '"status":"healthy"' && ck 1 "/health → healthy" || ck 0 "/health: $h"
echo "$h" | grep -q '"db"' && ck 1 "/health شامل نام پروایدر دیتابیس" || ck 0 "/health بدون db"

echo "── ۲) Serilog"
ck "$(grep -q "UseSerilog" "$WEB/Program.cs" && echo 1 || echo 0)" "Program.cs ← UseSerilog (کنسول + فایل چرخشی)"
ck "$([ -d "$WEB/logs" ] && ls "$WEB"/logs/log-*.txt >/dev/null 2>&1 && echo 1 || echo 0)" "فایل لاگ روزانه ساخته شد (logs/log-*.txt)"
ck "$(grep -q "retainedFileCountLimit" "$WEB/Program.cs" && echo 1 || echo 0)" "نگهداری ۳۱ روز فایل لاگ"
head -1 "$WEB"/logs/log-*.txt 2>/dev/null | grep -qE "\[(19|20)[0-9]{2}-" && ck 1 "قالب لاگ فایل (Timestamp + Level + Message + Properties)" || ck 0 "قالب لاگ فایل"

echo "── ۳) HSTS (کلید پیکربندی Security:EnableHsts)"
ck "$(grep -q "UseHsts" "$WEB/Program.cs" && echo 1 || echo 0)" "Program.cs ← UseHsts شرطی"
ck "$(python3 -c "import json;print(1 if json.load(open('$WEB/appsettings.json'))['Security']['EnableHsts'] is False else 0)" 2>/dev/null)" "appsettings.json ← Security:EnableHsts=false (سندباکس) — پروداکشن true کنید"
ck "$(grep -q "EnableHsts" "$ROOT/aspnet/Dockerfile" && echo 1 || echo 0)" "Dockerfile یادآوری EnableHsts"

echo "── ۴) بکاپ خودکار"
before=$(ls "$ROOT"/backups/hr-sqlite-*.db.gz 2>/dev/null | wc -l)
bash "$ROOT/scripts/backup-db.sh" --sqlite > /tmp/p19-backup.log 2>&1
after=$(ls "$ROOT"/backups/hr-sqlite-*.db.gz 2>/dev/null | wc -l)
ck "$([ "$after" -gt "$before" ] && echo 1 || echo 0)" "backup-db.sh فایل جدید ساخت ($after نسخه)"
f=$(ls -1t "$ROOT"/backups/hr-sqlite-*.db.gz 2>/dev/null | head -1)
bash "$ROOT/scripts/restore-db.sh" "$f" --check > /tmp/p19-restore.log 2>&1
grep -q "سالم است" /tmp/p19-restore.log && ck 1 "بازیابی (حالت --check): integrity_check سالم" || ck 0 "بازیابی: $(tail -2 /tmp/p19-restore.log)"
ck "$(grep -q "Task Scheduler\|cron" "$ROOT/scripts/backup-db.sh" && echo 1 || echo 0)" "راهنمای زمان‌بندی در اسکریپت"

echo "── ۵) Docker"
ck "$([ -f "$ROOT/aspnet/Dockerfile" ] && echo 1 || echo 0)" "aspnet/Dockerfile (multi-stage)"
ck "$([ -f "$ROOT/aspnet/docker-compose.yml" ] && echo 1 || echo 0)" "aspnet/docker-compose.yml (وب + SQL Server 2022)"
grep -q "mcr.microsoft.com/dotnet/sdk:8.0" "$ROOT/aspnet/Dockerfile" && ck 1 "استفاده از ایمیج رسمی .NET 8" || ck 0 "ایمیج SDK"
grep -q "healthcheck" "$ROOT/aspnet/docker-compose.yml" && ck 1 "healthcheck دیتابیس در compose" || ck 0 "healthcheck"

echo "── ۶) رگرسیون سریع وب"
code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:3000/Auth/Login)
ck "$([ "$code" = "200" ] && echo 1 || echo 0)" "صفحه ورود → 200"
r=$(curl -s -c /tmp/p19.txt -X POST http://localhost:3000/api/auth/login -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@1234"}')
echo "$r" | grep -q '"user"' && echo "$r" | grep -q '"isSuperadmin":true' && ck 1 "لاگین API سالم" || ck 0 "لاگین API: ${r:0:80}"
grep -q "Login" "$WEB"/logs/log-*.txt 2>/dev/null && ck 1 "رویداد لاگین در فایل لاگ Serilog" || ck 0 "لاگ لاگین"

echo ""
echo "═══════ نتیجه فاز ۱۹: $PASS سبز، $FAIL قرمز ═══════"
[ "$FAIL" = "0" ]
