#!/bin/bash
# ============================================================
# تست فاز ۶۷ — لایهٔ دیتای مشروط ماژول‌ها
# تست A: بوت کامل (همهٔ DLLها حاضر) → همهٔ جداول ساخته شود
# تست B: حذف HrModules.Access*.dll → بوت سالم + بدون جداول Access
# هر تست با DB تازهٔ SQLite در پوشهٔ جدا و پورت ایزوله.
# ============================================================
set -u
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export TZ=Asia/Tehran

ROOT=/home/z/my-project/scripts/p67verify/aspnet/src/HrAttendance.Web
PUBLISH=$ROOT/bin/Release/net8.0/publish

# ---------- آماده‌سازی خروجی publish (یک بار) ----------
if [ ! -f "$PUBLISH/HrAttendance.Web.dll" ]; then
  cd "$ROOT" && dotnet publish -c Release -o "$PUBLISH" --nologo -v q 2>&1 | tail -2
fi

run_test () {
  local NAME="$1" PORT="$2"
  local TDIR=/home/z/my-project/scripts/p67verify/test-$NAME
  rm -rf "$TDIR" && mkdir -p "$TDIR"
  # کپی از خروجی publish (بدون DB — DB تازه می‌سازیم)
  cp -r "$PUBLISH"/. "$TDIR"/
  # پیکربندی تست
  export ASPNETCORE_URLS="http://127.0.0.1:$PORT"
  export ASPNETCORE_ENVIRONMENT=Production
  export Database__Provider=Sqlite
  export Setup__Configured=true
  export ConnectionStrings__Sqlite="Data Source=$TDIR/test.db"
  export ContentRoot=""
  cd "$TDIR"
  setsid dotnet HrAttendance.Web.dll > "$TDIR/app.log" 2>&1 &
  local PID=$!
  # تا 60 ثانیه صبر برای سلامت
  local CODE=000 i=0
  for i in $(seq 1 60); do
    sleep 1
    CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 "http://127.0.0.1:$PORT/health" || true)
    [ "$CODE" = "200" ] && break
  done
  echo "=== TEST $NAME — /health: $CODE (بعد از ${i}s) ==="
  echo "$PID" > "$TDIR/pid"
}

count_tables () {
  local DB="$1"; shift
  sqlite3 "$DB" "SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ($1);" 2>/dev/null || echo "sqlite3-missing"
}

case "${1:-all}" in
  a)
    run_test full 3111
    ;;
  b)
    run_test noaccess 3112
    ;;
  *)
    echo "usage: p67-test.sh a|b"
    ;;
esac
