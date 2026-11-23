#!/bin/bash
# ======================================================================
# بازتولید E2E خطای گزارش‌شده (فاز ۷۸):
#   نصب با رشتهٔ اتصال SQL «غیرقابل‌دسترس» + کوکی hr_at معتبرِ اجرای قبلی
#   → GET /Setup باید ویزارد را نشان دهد، نه SqlException.
# پیش‌از-fix: 500 با SqlException | پس‌از-fix: 200 ویزارد
# مصرف:  bash scripts/repro-setup-sql.sh   (فایل میانه‌افزار را از وضعیت فعلی می‌خواند)
# ======================================================================
set -uo pipefail
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
ROOT=/home/z/my-project/aspnet
WEB=$ROOT/src/HrAttendance.Web
PORT=5099
LOG=/tmp/hr-repro.log

# توکن JWT معتبر با همان Secret/Issuer/Audience پیش‌فرض (کوکی اجرای قبلی را شبیه‌سازی می‌کند)
TOKEN=$(python3 - <<'PY'
import hmac, hashlib, base64, json, time
def b64(b): return base64.urlsafe_b64encode(b).rstrip(b"=")
secret = b"CHANGE-ME-in-production-hr-attendance-256bit-secret-key-0123456789"
h = b64(json.dumps({"alg":"HS256","typ":"JWT"}, separators=(",",":")).encode())
now = int(time.time())
p = b64(json.dumps({"sub":"1","iss":"hr-attendance","aud":"hr-attendance-clients",
                    "exp":now+3600,"iat":now}, separators=(",",":")).encode())
sig = b64(hmac.new(secret, h+b"."+p, hashlib.sha256).digest())
print(h.decode()+"."+p.decode()+"."+sig.decode())
PY
)

start_app() {
  cd "$WEB"
  ASPNETCORE_URLS="http://127.0.0.1:$PORT" ASPNETCORE_ENVIRONMENT=Development \
  Setup__Configured=true \
  Database__Provider=SqlServer \
  Database__ConnectionString="Data Source=127.0.0.1,1444;User Id=sa;Password=x;TrustServerCertificate=True;Connect Timeout=3" \
  dotnet bin/Release/net8.0/HrAttendance.Web.dll > "$LOG" 2>&1 &
  echo $!
}

wait_health() {  # تا بالاآمدن پروسه (حتی با MarkFailure، سرور بالا می‌آید) — max 60s
  for i in $(seq 1 60); do
    code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 "http://127.0.0.1:$PORT/health" 2>/dev/null || true)
    [ "$code" != "000" ] && return 0
    sleep 1
  done
  return 1
}

echo "== build =="
dotnet build "$WEB/HrAttendance.Web.csproj" -c Release --nologo -v q -p:WarningLevel=0 2>&1 | tail -2

PID=$(start_app)
wait_health || { echo "app did not start"; tail -20 "$LOG"; kill $PID 2>/dev/null; exit 1; }
echo "== app up (pid $PID) =="

echo "-- GET /Setup با کوکی معتبر:"
curl -s -o /tmp/repro-body.html -w "HTTP %{http_code}\n" \
  -H "Cookie: hr_at=$TOKEN" "http://127.0.0.1:$PORT/Setup"

echo "-- بدنهٔ پاسخ (نمونهٔ خطا یا ویزارد):"
grep -o -m1 "SqlException[^<]*" /tmp/repro-body.html | head -1 || true
grep -c "ویزارد راه‌اندازی" /tmp/repro-body.html >/dev/null 2>&1 && echo "(ویزارد راه‌اندازی: یافت شد)" || \
  grep -o -m1 "<title>[^<]*</title>" /tmp/repro-body.html || true

echo "-- لاگ سرور (خطای مرتبط):"
grep -E "SqlException|IsSuperadminAsync|RbacRefresh|fail" "$LOG" | head -4 || true

kill $PID 2>/dev/null
wait $PID 2>/dev/null
echo "== done =="
