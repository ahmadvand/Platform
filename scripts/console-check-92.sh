#!/usr/bin/env bash
# فاز ۹۲ — بررسی کنسول مرورگر روی همهٔ صفحه‌های کلیدی (درخواست کاربر: «خطاهای کنسول فایرفاکس»)
set -u
export PATH="$HOME/.dotnet:$PATH:$PATH"
FRESH=/home/z/my-project/wiztest92
T=/home/z/my-project/wiztest-tmp
cd $FRESH
if ! curl -s -m 2 -o /dev/null http://127.0.0.1:3998/health; then
  setsid dotnet bin/Platform.Web.dll --urls=http://127.0.0.1:3998 >> $FRESH/server.log 2>&1 &
  for i in $(seq 1 60); do curl -s -m 2 -o /dev/null http://127.0.0.1:3998/health && break; sleep 1; done
fi
echo "health: $(curl -s -m 3 -o /dev/null -w '%{http_code}' http://127.0.0.1:3998/health)"

agent-browser close > /dev/null 2>&1 || true
agent-browser open http://127.0.0.1:3998/Auth/Login > /dev/null
sleep 1
SNAP=$(agent-browser snapshot -i --json 2>/dev/null)
U=$(echo "$SNAP" | python3 -c "import json,sys;d=json.load(sys.stdin);els=d.get('elements',d if isinstance(d,list) else []);print(next((e['ref'] for e in els if 'textbox' in str(e.get('type','')).lower() and 'identifier' in str(e.get('name','')+e.get('placeholder','')).lower() or e.get('ref') and 'identifier' in str(e.get('id',''))), ''))" 2>/dev/null)
# fallback: refs by order of interactive elements (identifier, secret, submit)
REFS=$(echo "$SNAP" | python3 -c "
import json,sys
d=json.load(sys.stdin)
els=d.get('elements', d if isinstance(d,list) else [])
tb=[e for e in els if 'textbox' in str(e.get('type','')).lower()]
btn=[e for e in els if 'button' in str(e.get('type','')).lower()]
print((tb[0]['ref'] if len(tb)>0 else '', tb[1]['ref'] if len(tb)>1 else '', btn[0]['ref'] if btn else ''))
" 2>/dev/null)
UR=$(echo $REFS | cut -d"'" -f2 | tr -d "(),'"); PR=$(echo $REFS | awk -F"' " '{print $2}' | tr -d "(),'"); BR=$(echo $REFS | awk -F"'" '{print $4}' | tr -d "(),'")
echo "refs: user=$UR pass=$PR btn=$BR"
agent-browser fill "$UR" admin > /dev/null
agent-browser fill "$PR" "Admin@1234" > /dev/null
agent-browser click "$BR" > /dev/null
agent-browser wait --load networkidle > /dev/null 2>&1 || sleep 2
echo "after login url: $(agent-browser get url)"

: > $T/console92.txt
for P in "/" "/Devices" "/Notifications" "/Access?tab=alerts" "/Settings" "/Updates" "/Payroll" "/Attendance" "/Parking" "/Visitors" "/Org" "/Langs" "/Users"; do
  agent-browser open "http://127.0.0.1:3998$P" > /dev/null 2>&1
  sleep 1
  agent-browser wait --load networkidle > /dev/null 2>&1 || sleep 1
  echo "===== PAGE $P =====" >> $T/console92.txt
  agent-browser console >> $T/console92.txt 2>&1
  echo "----- pageerrors -----" >> $T/console92.txt
  agent-browser errors >> $T/console92.txt 2>&1
done
agent-browser close > /dev/null 2>&1
echo "===== console summary ====="
python3 - <<'EOF'
import re
txt = open('/home/z/my-project/wiztest-tmp/console92.txt', encoding='utf-8', errors='replace').read()
pages = re.split(r'===== PAGE (.+?) =====', txt)
issues = []
for i in range(1, len(pages), 2):
    page, body = pages[i], pages[i+1]
    for line in body.splitlines():
        s = line.strip()
        if not s: continue
        if re.search(r'\b(error|ERR|failed|404|500|Uncaught|SyntaxError|TypeError|ReferenceError)', s, re.I) and 'console --clear' not in s and 'No console' not in s and 'no errors' not in s.lower():
            issues.append((page, s[:180]))
if issues:
    for p, s in issues: print(f"[{p}] {s}")
else:
    print("NO CONSOLE ERRORS on any page")
EOF
pkill -f "Platform.Web.dll --urls=http://127.0.0.1:39" 2>/dev/null
echo "server stopped"
