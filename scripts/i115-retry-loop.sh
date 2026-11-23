#!/bin/bash
# فاز ۱۰۴ — حلقهٔ بازیابی ترجمه: هر ۱۵ دقیقه یک گذر؛ تا صفر شدن جاافتاده‌ها یا ۸ گذر
LOG=/home/z/my-project/scripts/i115.log
cd /home/z/my-project || exit 1
echo "=== i115 start $(date '+%F %T') ===" >> "$LOG"
for pass in 1 2 3 4 5 6 7 8; do
  echo "=== pass $pass $(date '+%F %T') ===" >> "$LOG"
  python3 scripts/i113-fill-24keys.py >> "$LOG" 2>&1
  LEFT=$(python3 - <<'PY'
import json
AUD = json.load(open('scripts/i110-audit.json', encoding='utf-8'))
BRANDS = {'ZKTeco','Suprema','Anviz','Virdi','Timmy','ZK','SP','AV','VR','TM','GEN','GEN1'}
KEYS = [k for k in AUD['union_missing'] if k not in BRANDS]
total = 0
for code in ['ar','ka','ky','prs','ps','tg','tk','uz','ru']:
    pack = json.load(open(f'aspnet/src/Platform.Web/App_Data/langpacks/hr-lang-{code}.json', encoding='utf-8'))
    total += sum(1 for k in KEYS if k not in pack['entries'])
print(total)
PY
)
  echo "=== pass $pass left=$LEFT $(date '+%F %T') ===" >> "$LOG"
  [ "$LEFT" = "0" ] && break
  sleep 900
done
echo "=== i115 end $(date '+%F %T') ===" >> "$LOG"
