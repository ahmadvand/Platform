#!/usr/bin/env bash
# ============================================================
# فاز ۱۹ — بازیابی بکاپ SQLite (فایل .db.gz از scripts/backup-db.sh)
# استفاده: bash scripts/restore-db.sh backups/hr-sqlite-XXXX.db.gz [--check]
#   --check : فقط تست صحت/سالم‌بودن فایل بدون جایگزینی
# ============================================================
set -u
cd "$(dirname "$0")/.."
F="${1:-}"; CHECK="${2:-}"
[ -f "$F" ] || { echo "✗ فایل بکاپ یافت نشد: $F"; exit 1; }
DB="aspnet/src/HrAttendance.Web/hr.db"

TMP=$(mktemp -d)
gunzip -c "$F" > "$TMP/restore.db"
python3 - "$TMP/restore.db" <<'PYEOF'
import sqlite3, sys
con = sqlite3.connect(sys.argv[1])
ok = con.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
tables = con.execute("SELECT COUNT(*) FROM sqlite_master WHERE type='table'").fetchone()[0]
print(f"integrity={ok} tables={tables}")
sys.exit(0 if ok else 1)
PYEOF
[ "$?" = "0" ] || { echo "✗ فایل بکاپ خراب است"; rm -rf "$TMP"; exit 1; }
echo "✓ فایل بکاپ سالم است"

if [ "$CHECK" = "--check" ]; then rm -rf "$TMP"; echo "(حالت --check — جایگزینی انجام نشد)"; exit 0; fi

# جایگزینی امن: بکاپ از نسخه فعلی + جایگزینی
cp "$DB" "$DB.bak-$(date +%Y%m%d-%H%M%S)"
pkill -f "HrAttendance.Web.dll" 2>/dev/null; sleep 2
cp "$TMP/restore.db" "$DB"
rm -rf "$TMP"
echo "✓ بازیابی انجام شد — نسخه قبلی با پسوند .bak-* نگه داشته شد"
echo "  برای شروع مجدد: bash scripts/up.sh"
