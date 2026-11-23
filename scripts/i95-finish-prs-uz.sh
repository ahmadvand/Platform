#!/bin/bash
# فاز ۹۵ — حلقهٔ تکمیل prs/uz: هر گذر فقط کلیدهای جاافتاده را ترجمه می‌کند
# (کش پرشده؛ دسته‌های ناموفق در گذر بعدی جبران می‌شوند). تا پر شدن کامل ادامه بده.
LOG=/home/z/my-project/scripts/langpacks/i95.log
for pass in 1 2 3 4 5 6 7 8; do
  echo "=== resume pass $pass $(date '+%T') ===" >> "$LOG"
  python3 /home/z/my-project/scripts/i95-langpacks.py --only prs,uz >> "$LOG" 2>&1
  python3 - <<'PY' >> "$LOG" 2>&1
import json
ok = True
for code in ('prs', 'uz'):
    try:
        n = len(json.load(open(f'/home/z/my-project/aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-{code}.json', encoding='utf-8'))['entries'])
        print(f'[check] {code} entries={n}')
        if n < 2900: ok = False
    except Exception as e:
        print(f'[check] {code} ERR {e}'); ok = False
print('[check] COMPLETE' if ok else '[check] INCOMPLETE')
PY
  grep -q "COMPLETE" "$LOG" && tail -1 "$LOG" | grep -q COMPLETE && break
  sleep 30
done
echo "=== loop end $(date '+%T') ===" >> "$LOG"
