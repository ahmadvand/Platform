#!/bin/bash
# ======================================================================
# i99 — پس از پایان i98: سینک بسته‌های زبان کامل به sc-run (اجرای زندهٔ
# فعلی) + بازسازی بسته‌های مشتری FULL/MINIMAL با ترجمه‌های کامل.
# zip اتمی: اول tmp سپس mv — هرگز zip قبلی را خراب نمی‌کند.
# مصرف: python3 scripts/daemonize.py /bin/bash scripts/i99-rebuild-packages.sh
# ======================================================================
LOG=/home/z/my-project/scripts/i99.log
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd /home/z/my-project || exit 1
echo "=== i99 start $(date '+%F %T') — waiting for i98 ===" >> "$LOG"
for i in $(seq 1 120); do
  grep -q "i98 COMPLETE\|i98 end" scripts/i98.log 2>/dev/null && break
  sleep 15
done
echo "=== i98 finished — sync packs to sc-run $(date '+%F %T') ===" >> "$LOG"
cp -f aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-*.json aspnet/sc-run/App_Data/langpacks/
echo "packs synced: $(ls aspnet/sc-run/App_Data/langpacks/ | wc -l) files" >> "$LOG"

zipdir() {  # zipdir <out.zip> — از aspnet/publish/customer با نام‌گذاری اتمی
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

echo "=== building FULL package $(date '+%F %T') ===" >> "$LOG"
if bash scripts/build-customer.sh "" 2.7.7 >> "$LOG" 2>&1; then
  rm -f download/HR-Platform-2.7.7-customer-FULL.zip
  zipdir /home/z/my-project/download/HR-Platform-2.7.7-customer-FULL.zip
  echo "FULL OK" >> "$LOG"
else
  echo "FULL BUILD FAILED — قبلی دست‌نخورده ماند" >> "$LOG"
fi

echo "=== building MINIMAL package $(date '+%F %T') ===" >> "$LOG"
if bash scripts/build-customer.sh parking,attendance,access,visitors,payroll,gateway 2.7.7 >> "$LOG" 2>&1; then
  rm -f download/HR-Platform-2.7.7-customer-MINIMAL-base-only.zip
  zipdir /home/z/my-project/download/HR-Platform-2.7.7-customer-MINIMAL-base-only.zip
  echo "MIN OK" >> "$LOG"
else
  echo "MIN BUILD FAILED — قبلی دست‌نخورده ماند" >> "$LOG"
fi
echo "=== i99 done $(date '+%F %T') ===" >> "$LOG"
