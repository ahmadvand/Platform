#!/bin/bash
# فاز ۱۱۳ — بازسازی همهٔ zipهای تحویلی با نسخهٔ ۲.۷.۹ (الگوی i114/regen-dl)
LOG=/home/z/my-project/scripts/i137.log
cd /home/z/my-project || exit 1
export PATH="/home/z/.dotnet:$PATH"
VER=2.7.9
PUB=aspnet/publish/customer

zipdir() {
  local out=$1
  ( cd "$PUB" && python3 - "$out" <<'PY'
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

echo "=== i137 start $(date '+%F %T') ===" >> "$LOG"

# حذف zipهای ۲.۷.۷ کهنه
rm -f download/HR-Platform-2.7.7-customer-FULL.zip download/HR-Platform-2.7.7-customer-MINIMAL-base-only.zip \
      download/HR-Platform-2.7.7-source.zip download/HR-Platform-Windows-Tools-2.7.7.zip \
      download/HR-Langpack-Russian-2.7.7.zip download/HR-Platform-2.7.7-Docs-FA.zip

echo "=== FULL ===" >> "$LOG"
if bash scripts/build-customer.sh "" "$VER" >> "$LOG" 2>&1; then
  zipdir /home/z/my-project/download/HR-Platform-$VER-customer-FULL.zip
  echo "FULL OK" >> "$LOG"
else
  echo "FULL FAILED" >> "$LOG"
fi

echo "=== MINIMAL ===" >> "$LOG"
if bash scripts/build-customer.sh parking,attendance,access,visitors,payroll,gateway "$VER" >> "$LOG" 2>&1; then
  zipdir /home/z/my-project/download/HR-Platform-$VER-customer-MINIMAL-base-only.zip
  echo "MIN OK" >> "$LOG"
else
  echo "MIN FAILED" >> "$LOG"
fi

# Windows-Tools 2.7.9 (بیلد سبک)
python3 scripts/make-windows-tools.py >> "$LOG" 2>&1 && echo "WIN-TOOLS OK" >> "$LOG" || echo "WIN-TOOLS FAILED" >> "$LOG"
# بستهٔ روسی 2.7.9 (۳۱۶۳ کلید)
python3 scripts/i137-ru-zip.py >> "$LOG" 2>&1 && echo "RU-ZIP OK" >> "$LOG" || echo "RU-ZIP FAILED" >> "$LOG"
# سورس‌زیپ از کامیتهٔ فاز ۱۱۳
git -C /home/z/my-project archive --format=zip -o download/HR-Platform-$VER-source.zip HEAD >> "$LOG" 2>&1 && echo "SOURCE OK" >> "$LOG" || echo "SOURCE FAILED" >> "$LOG"
echo "=== i137 done $(date '+%F %T') ===" >> "$LOG"
ls -la download/*.zip
