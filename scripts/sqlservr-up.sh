#!/bin/bash
# ============================================================
# راه‌اندازی SQL Server 2022 بدون root (user-space)
# - باینری پچ‌شده: مسیرهای [Pal.Directories] به /home/z/mssql/mssqldata
# - shim LD_PRELOAD: بازنویسی /var/opt/mssql → /home/z/mssql/mssqldata
# - داده‌ها و لاگ‌ها همگی داخل /home/z/mssql/mssqldata
# ============================================================
MSSQL_BIN=/home/z/mssql/root/opt/mssql/bin/sqlservr-patched
DATA=/home/z/mssql/mssqldata

# اگر در حال اجراست کاری نکن
if ss -ltn 2>/dev/null | grep -q ":1433 "; then
  echo "SQL Server already running on 1433"
  exit 0
fi

[ -x "$MSSQL_BIN" ] || { echo "ERROR: $MSSQL_BIN not found"; exit 1; }
mkdir -p "$DATA/data" "$DATA/log" "$DATA/dump" "$DATA/secrets" "$DATA/backup" "$DATA/.system"

(setsid bash -c "cd $DATA && env \
  ACCEPT_EULA=Y \
  MSSQL_PID=Developer \
  MSSQL_SA_PASSWORD='YourStrong!Passw0rd' \
  MSSQL_MEMORY_LIMIT_MB=1024 \
  MSSQL_DISABLE_SSSD=1 \
  MSSQL_DISABLE_COREDUMP=1 \
  LD_LIBRARY_PATH='/home/z/mssql/root/opt/mssql/lib:/home/z/mssql/root/usr/lib/x86_64-linux-gnu' \
  LD_PRELOAD='/home/z/mssql/shadowpath.so' \
  $MSSQL_BIN" < /dev/null > /home/z/mssql/sqlservr.out 2>&1 &)

# منتظر آماده شدن پورت
for i in $(seq 1 60); do
  if ss -ltn 2>/dev/null | grep -q ":1433 "; then
    echo "SQL Server UP on 1433 after ${i}s"
    exit 0
  fi
  sleep 1
done
echo "FAILED to start SQL Server:"
tail -5 /home/z/mssql/sqlservr.out 2>/dev/null
tail -5 "$DATA/log/errorlog" 2>/dev/null
exit 1
