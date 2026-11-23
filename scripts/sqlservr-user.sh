#!/bin/bash
# راه‌انداز SQL Server 2022 بدون root — داده‌ها در /home/z/mssql/mssqldata
export MSSQL_ROOT=/home/z/mssql/root/opt/mssql
export LD_LIBRARY_PATH="$MSSQL_ROOT/lib:/home/z/mssql/root/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH"

export ACCEPT_EULA=Y
export MSSQL_PID=Developer
export MSSQL_SA_PASSWORD='YourStrong!Passw0rd'
export MSSQL_MEMORY_LIMIT_MB=1024
export MSSQL_DISABLE_SSSD=1
export MSSQL_DISABLE_COREDUMP=1

DATA=/home/z/mssql/mssqldata
mkdir -p "$DATA/data" "$DATA/log" "$DATA/dump" "$DATA/secrets" "$DATA/backup"

export MSSQL_DATA_DIR="$DATA/data"
export MSSQL_LOG_DIR="$DATA/log"
export MSSQL_DUMP_DIR="$DATA/dump"
export MSSQL_BACKUP_DIR="$DATA/backup"
export MSSQL_ERROR_LOG_FILE="$DATA/log/errorlog"
export MSSQL_MACHINE_KEY_FILE="$DATA/secrets/machine-key"

# موتور، SystemDirectory را از working directory می‌سازد (مثل invokesqlservr.sh که cd /var/opt/mssql می‌کند)
cd "$DATA"

exec "$MSSQL_ROOT/bin/sqlservr"
