#!/bin/bash
# ============================================================
# E2E فاز ۷۸b — وصال عمومی ستون‌های غایب (خودترمیمی اسکیما)
# سناریوی واقعی کاربر: پایگاه قدیمی SQL Server ستون Users.DepartmentId
# (فاز ۷۴) را ندارد → هنگام ورود «Invalid column name 'DepartmentId'».
# اینجا روی SQLite شبیه‌سازی می‌شود: دو ستون از Users حذف می‌شود؛
# استارتاپ باید هر دو را خودکار برگرداند و ورود باید سالم باشد.
#
# نکات اجرا (درس تجربهٔ ۲۰۲۶-۱۰-۳۱):
#  • نمونهٔ e2e باید PID-محور کشته شود (مسیر دیتابیس در env است نه
#    cmdline — pkill الگویی آن را نمی‌گیرد و نمونهٔ جاگذشته پورت ۳۹۹۹
#    را قفل می‌کند و نمونهٔ بعدی bind نمی‌شود).
#  • راستی‌آزماییِ مستقیم SQLite پس از بسته‌شدن اپ انجام می‌شود؛
#    خواندن فایل WAL-دارِ تحت مالکیت اپِ زنده «disk I/O error» می‌دهد.
# ============================================================
set -u
ROOT=/home/z/my-project
WEB=$ROOT/aspnet/src/HrAttendance.Web
TDIR=$ROOT/scripts/tmp-e2e/heal
LOG=$TDIR/app.log
B=http://localhost:3999
mkdir -p "$TDIR"
export PATH="/home/z/.dotnet:$PATH"

fail() { echo "FAIL: $*"; exit 1; }

APP_PID=""
cleanup() {
  if [ -n "$APP_PID" ]; then
    kill -TERM "$APP_PID" 2>/dev/null
    for _ in $(seq 1 10); do kill -0 "$APP_PID" 2>/dev/null || break; sleep 1; done
    kill -KILL "$APP_PID" 2>/dev/null
    wait "$APP_PID" 2>/dev/null
  fi
}
trap cleanup EXIT

# ---------- ۰) پورت ۳۹۹۹ باید آزاد باشد (نمونهٔ جاگذشتهٔ اجرای قبلی) ----------
if ss -tln 2>/dev/null | grep -q ':3999 '; then
  echo ">> پورت ۳۹۹۹ اشغال است — پاکسازی نمونهٔ جاگذشته"
  if command -v fuser >/dev/null 2>&1; then
    fuser -k 3999/tcp 2>/dev/null || true
  else
    PID=$(ss -tlnp 2>/dev/null | grep ':3999 ' | sed -n 's/.*pid=\([0-9]*\).*/\1/p' | head -1)
    [ -n "$PID" ] && kill -KILL "$PID" 2>/dev/null
  fi
  for _ in $(seq 1 10); do ss -tln 2>/dev/null | grep -q ':3999 ' || break; sleep 1; done
  ss -tln 2>/dev/null | grep -q ':3999 ' && fail "port 3999 still busy"
fi

# ---------- ۱) نسخهٔ امن از پایگاه زنده (sqlite backup API — سازگار با اپ در حال اجرا) ----------
rm -f "$TDIR/hr.db" "$TDIR/hr.db-wal" "$TDIR/hr.db-shm" "$TDIR/cookie.jar"
python3 - "$WEB/hr.db" "$TDIR/hr.db" <<'PY' || fail "backup"
import sqlite3, sys
src = sqlite3.connect(sys.argv[1]); dst = sqlite3.connect(sys.argv[2])
src.backup(dst); src.close(); dst.close()
print("backup OK")
PY

COUNT_BEFORE=$(python3 -c "import sqlite3;print(sqlite3.connect('$TDIR/hr.db').execute('SELECT COUNT(*) FROM Users').fetchone()[0])") || fail "count before"
echo "users rows before: $COUNT_BEFORE"

# ---------- ۲) حذف عمدی دو ستون (شبیه‌سازی پایگاه قدیمی) ----------
python3 - "$TDIR/hr.db" <<'PY' || fail "drop columns"
import sqlite3, sys
db = sqlite3.connect(sys.argv[1])
for col in ("DepartmentId", "PhotoPath"):
    have = {r[1] for r in db.execute("PRAGMA table_info(Users)")}
    assert col in have, f"pre-condition: Users.{col} must exist in fresh copy"
    db.execute(f'ALTER TABLE Users DROP COLUMN "{col}"')
db.commit()
have = {r[1] for r in db.execute("PRAGMA table_info(Users)")}
assert "DepartmentId" not in have and "PhotoPath" not in have
print("dropped Users.DepartmentId + Users.PhotoPath — simulating old schema")
PY

# ---------- ۳) اجرای نمونهٔ دوم روی ۳۹۹۹ با پایگاه معیوب (بدون ساب‌شل — کشتن PID-محور) ----------
cd "$WEB" || fail "cd web"
export Database__Provider=Sqlite
export Database__ConnectionString="Data Source=$TDIR/hr.db"
export ASPNETCORE_URLS=http://localhost:3999
export ASPNETCORE_ENVIRONMENT=Production
export Setup__Configured=true
export SeedDemo=false
export TZ=Asia/Tehran
dotnet bin/Debug/net8.0/HrAttendance.Web.dll > "$LOG" 2>&1 &
APP_PID=$!

code=000
for i in $(seq 1 60); do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 "$B/health" 2>/dev/null || true)
  [ "$code" = "200" ] && break
  kill -0 "$APP_PID" 2>/dev/null || { echo "app process died"; tail -30 "$LOG"; fail "app exited early"; }
  sleep 2
done
[ "$code" = "200" ] || { echo "app did not come up on 3999 (code=$code)"; tail -20 "$LOG"; fail "health"; }
echo "PASS app started on 3999 with broken schema"

# ---------- ۴) ورود باید سالم باشد (کوئری Users = همان نقطهٔ شکست کاربر) ----------
JAR=$TDIR/cookie.jar
lcode=$(curl -s -c "$JAR" -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@1234"}' "$B/api/auth/login" -o /dev/null -w "%{http_code}" --max-time 20) || fail "login curl"
[ "$lcode" = "200" ] || { echo "login on healed DB returned $lcode"; tail -20 "$LOG"; fail "login"; }
echo "PASS login works (Users query OK)"

# ---------- ۵) اپ بسته می‌شود؛ سپس راستی‌آزمایی مستقیم پایگاه ----------
cleanup
sleep 2

python3 - "$TDIR/hr.db" <<'PY' || fail "column check"
import sqlite3, sys
db = sqlite3.connect(sys.argv[1])
have = {r[1] for r in db.execute("PRAGMA table_info(Users)")}
missing = [c for c in ("DepartmentId", "PhotoPath") if c not in have]
assert not missing, f"columns not healed: {missing}"
print("PASS Users.DepartmentId + Users.PhotoPath auto-restored")
PY

COUNT_AFTER=$(python3 -c "import sqlite3;print(sqlite3.connect('$TDIR/hr.db').execute('SELECT COUNT(*) FROM Users').fetchone()[0])") || fail "count after"
if [ "$COUNT_BEFORE" != "$COUNT_AFTER" ]; then fail "data changed ($COUNT_BEFORE → $COUNT_AFTER)"; fi
echo "PASS users data preserved ($COUNT_AFTER rows)"

# ---------- ۶) لاگ باید وصال را نشان دهد ----------
grep -q "وصال عمومی اسکیما" "$LOG" && echo "PASS log reports schema healer" || echo "WARN healer log line not found"

echo "== e2e-schema-heal: ALL PASS =="
