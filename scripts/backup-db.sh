#!/usr/bin/env bash
# ============================================================
# فاز ۱۹ — بکاپ خودکار دیتابیس (SQLite / SQL Server)
# SQLite : VACUUM INTO (فایل سازگار و فشرده‌شده)
# SQL Server : BACKUP DATABASE به مسیر DISK
# نگهداری ۱۴ نسخه اخیر — برای زمان‌بندی: cron یا Windows Task Scheduler
# اجرا: bash scripts/backup-db.sh            (پروایدر فعال)
#       bash scripts/backup-db.sh --sqlite    (اجباری SQLite)
#       bash scripts/backup-db.sh --sqlserver (اجباری SQL Server)
# ============================================================
set -u
cd "$(dirname "$0")/.."
ROOT="$(pwd)"
MODE="${1:-auto}"
KEEP=14
OUT_DIR="$ROOT/backups"
mkdir -p "$OUT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"

provider() {
  if [ -f aspnet/src/HrAttendance.Web/appsettings.Production.json ]; then
    python3 -c "import json;print(json.load(open('aspnet/src/HrAttendance.Web/appsettings.Production.json')).get('Database',{}).get('Provider',''))" 2>/dev/null
  fi
}

read_conn() {
  python3 -c "import json;print(json.load(open('aspnet/src/HrAttendance.Web/appsettings.json')).get('ConnectionStrings',{}).get('$1',''))" 2>/dev/null
}

do_sqlite() {
  local db="$ROOT/aspnet/src/HrAttendance.Web/hr.db"
  [ -f "$db" ] || { echo "✗ SQLite db یافت نشد: $db"; return 1; }
  local target="$OUT_DIR/hr-sqlite-$STAMP.db"
  python3 - "$db" "$target" <<'PYEOF'
import sqlite3, sys
src, dst = sys.argv[1], sys.argv[2]
con = sqlite3.connect(src)
con.execute(f"VACUUM INTO '{dst}'")
con.close()
print("ok")
PYEOF
  [ "$(python3 -c "print('ok')" 2>/dev/null)" = "ok" ] || true
  if [ -f "$target" ]; then
    gzip -f "$target"
    echo "✓ بکاپ SQLite: ${target}.gz ($(du -h "${target}.gz" | cut -f1))"
    return 0
  fi
  echo "✗ VACUUM INTO ناموفق بود"; return 1
}

do_sqlserver() {
  local cs conn_str
  conn_str=$(read_conn SqlServer)
  [ -n "$conn_str" ] || { echo "✗ ConnectionStrings:SqlServer خالی است"; return 1; }
  local srv db user pass
  srv=$(python3 - "$conn_str" <<'PYEOF'
import sys, re
cs = sys.argv[1]
m = re.search(r"(?:Server|Data Source)=([^;]+)", cs, re.I)
print(m.group(1).split(',')[0].strip() if m else "")
PYEOF
)
  db=$(python3 - "$conn_str" <<'PYEOF'
import sys, re
cs = sys.argv[1]
m = re.search(r"(?:Database|Initial Catalog)=([^;]+)", cs, re.I)
print(m.group(1).strip() if m else "")
PYEOF
)
  user=$(python3 - "$conn_str" <<'PYEOF'
import sys, re
cs = sys.argv[1]
m = re.search(r"(?:User ID|Uid)=([^;]+)", cs, re.I)
print(m.group(1).strip() if m else "sa")
PYEOF
)
  pass=$(python3 - "$conn_str" <<'PYEOF'
import sys, re
cs = sys.argv[1]
m = re.search(r"(?:Password|Pwd)=([^;]+)", cs, re.I)
print(m.group(1) if m else "")
PYEOF
)
  [ -n "$srv" ] && [ -n "$db" ] || { echo "✗ تحلیل ConnectionString ناموفق"; return 1; }
  echo "→ BACKUP DATABASE $db روی $srv …"
  python3 - "$srv" "$db" "$user" "$pass" "$STAMP" <<'PYEOF'
import sys
import pymssql
srv, db, user, pwd, stamp = sys.argv[1:6]
con = pymssql.connect(server=srv, user=user, password=pwd, database="master", login_timeout=8)
cur = con.cursor()
# بکاپ در مسیر داخل سرور SQL Server — برای بازیابی همین مسیر لازم است
cur.execute(f"BACKUP DATABASE [{db}] TO DISK = N'/var/opt/mssql/backup/{db}-{stamp}.bak' WITH INIT, COMPRESSION, STATS = 25")
while cur.nextset():
    pass
con.close()
print("ok")
PYEOF
  [ "$?" = "0" ] && { echo "✓ بکاپ SQL Server: /var/opt/mssql/backup/$db-$STAMP.bak (روی سرور دیتابیس)"; return 0; }
  echo "✗ بکاپ SQL Server ناموفق"; return 1
}

retention() {
  ls -1t "$OUT_DIR"/hr-sqlite-*.db.gz 2>/dev/null | tail -n +$((KEEP + 1)) | while read -r f; do
    rm -f "$f"; echo "  ↺ نگهداری ۱۴ نسخه — حذف $f"
  done
}

rc=0
case "$MODE" in
  --sqlite)    do_sqlite || rc=1 ;;
  --sqlserver) do_sqlserver || rc=1 ;;
  *)
    p="$(provider)"
    if [ "$p" = "SqlServer" ]; then
      do_sqlserver || do_sqlite || rc=1
    else
      do_sqlite || rc=1
    fi
    ;;
esac
retention
echo "بکاپ‌ها: $OUT_DIR"
exit $rc
