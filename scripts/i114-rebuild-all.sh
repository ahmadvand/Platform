#!/bin/bash
# فاز ۱۰۴ — بازسازی هر دو بستهٔ مشتری با کد جدید (نظم اتمی zip مثل i99)
LOG=/home/z/my-project/scripts/i114.log
cd /home/z/my-project || exit 1
export PATH="/home/z/.dotnet:$PATH"
zipdir() {
  local out=$1
  ( cd aspnet/publish/customer && python3 - "$out" <<'PY'
import os, sys, zipfile
out = sys.argv[1]
tmp = out + ".tmp"
with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for base, _, files in os.walk("."):
        for f in files:
            p = os.path.join(base, f)
            z.write(p, os.path.relpath(p, "."))
os.replace(tmp, out)
print("zipped ->", out)
PY
  ) >> "$LOG" 2>&1
}
echo "=== i114 start $(date '+%F %T') ===" >> "$LOG"
echo "=== FULL ===" >> "$LOG"
if bash scripts/build-customer.sh "" 2.7.7 >> "$LOG" 2>&1; then
  rm -f download/HR-Platform-2.7.7-customer-FULL.zip
  zipdir /home/z/my-project/download/HR-Platform-2.7.7-customer-FULL.zip
  echo "FULL OK" >> "$LOG"
else
  echo "FULL FAILED" >> "$LOG"
fi
echo "=== MINIMAL ===" >> "$LOG"
if bash scripts/build-customer.sh parking,attendance,access,visitors,payroll,gateway 2.7.7 >> "$LOG" 2>&1; then
  rm -f download/HR-Platform-2.7.7-customer-MINIMAL-base-only.zip
  zipdir /home/z/my-project/download/HR-Platform-2.7.7-customer-MINIMAL-base-only.zip
  echo "MIN OK" >> "$LOG"
else
  echo "MIN FAILED" >> "$LOG"
fi
# سینک مرکز دانلود زنده به sc-run
rsync -a --delete aspnet/src/Platform.Web/wwwroot/dl/ aspnet/sc-run/wwwroot/dl/ >> "$LOG" 2>&1
echo "=== i114 done $(date '+%F %T') ===" >> "$LOG"
