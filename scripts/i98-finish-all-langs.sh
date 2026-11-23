#!/bin/bash
# ======================================================================
# i98 — تکمیل نهایی همهٔ بسته‌های زبان تا پوشش ۱۰۰٪ کلیدهای DictEn.
# اولویت (درخواست کاربر 2026-11-07): ازبکی(uz) و دری(prs) → سپس بقیه.
# هر گذر از کش cache2 ادامه می‌دهد؛ گذر بعدی جبران دسته‌های ناموفق را
# انجام می‌دهد و تا «missing=0 برای همهٔ زبان‌ها» تکرار می‌شود.
# مصرف: python3 scripts/daemonize.py bash scripts/i98-finish-all-langs.sh
# ======================================================================
LOG=/home/z/my-project/scripts/i98.log
cd /home/z/my-project || exit 1
echo "=== i98 start $(date '+%F %T') ===" >> "$LOG"
for pass in 1 2 3 4 5 6 7 8 9 10; do
  echo "=== i98 pass $pass A (uz,prs) $(date '+%F %T') ===" >> "$LOG"
  python3 scripts/i95-langpacks.py --only uz,prs >> "$LOG" 2>&1
  echo "=== i98 pass $pass B (ar,ky,ps,ka,tk,tg) $(date '+%F %T') ===" >> "$LOG"
  python3 scripts/i95-langpacks.py --only ar,ky,ps,ka,tk,tg >> "$LOG" 2>&1
  if python3 - <<'PY' >> "$LOG" 2>&1
import re, subprocess, sys
out = subprocess.run(['python3', '/home/z/my-project/scripts/langpacks/audit2.py'],
                     capture_output=True, text=True).stdout
print(out)
nums = [int(m) for m in re.findall(r'missing=\s*(\d+)', out)]
sys.exit(0 if nums and all(n == 0 for n in nums) else 1)
PY
  then
    echo "=== i98 COMPLETE $(date '+%F %T') ===" >> "$LOG"
    break
  fi
  sleep 10
done
echo "=== i98 end $(date '+%F %T') ===" >> "$LOG"
