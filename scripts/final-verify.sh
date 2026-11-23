#!/bin/bash
# راستی‌آزمایی نهایی در یک invocation: بوت + زبان‌ها + صفحات + GUID
cd /home/z/my-project
pkill -f start-web.sh 2>/dev/null; pkill -f Platform.Web.dll 2>/dev/null; sleep 3
setsid nohup bash scripts/start-web.sh </dev/null >>dev.log 2>&1 & disown
code=000
for i in $(seq 1 15); do code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 http://localhost:3000/health 2>/dev/null); [ "$code" = "200" ] && break; sleep 3; done
echo "health=$code"

echo "=== زبان گرجی (ka) روی صفحه ورود ==="
curl -s --max-time 6 -H "Cookie: hr_lang=ka" http://localhost:3000/Auth/Login > /tmp/l-ka.html
rg -o "შესვლა|მომხმარებლის სახელი|პაროლი|نام کاربری" /tmp/l-ka.html | sort | uniq -c
rg -o '<html[^>]*dir="[a-z]+"' /tmp/l-ka.html | head -1

echo "=== پشتو (ps) ==="
curl -s --max-time 6 -H "Cookie: hr_lang=ps" http://localhost:3000/Auth/Login > /tmp/l-ps.html
rg -o "سیسټم ته ننوتل|کارن نوم|پټنوم" /tmp/l-ps.html | sort | uniq -c
rg -o '<html[^>]*dir="[a-z]+"' /tmp/l-ps.html | head -1

echo "=== دری (prs) — dir ==="
curl -s --max-time 6 -H "Cookie: hr_lang=prs" http://localhost:3000/Auth/Login > /tmp/l-prs.html
rg -o '<html[^>]*dir="[a-z]+"' /tmp/l-prs.html | head -1

echo "=== سوئیچر زبان در صفحه (۹ زبان) ==="
curl -s --max-time 6 http://localhost:3000/Auth/Login > /tmp/l-def.html
for c in prs ps ka ky uz tg tk en fa; do
  n=$(rg -c "lang/$c|hr_lang=$c|data-lang=\"$c\"" /tmp/l-def.html 2>/dev/null || echo 0)
  echo "  $c: $n"
done

echo "=== صفحات کلیدی (باید 200 یا 302) ==="
for p in /health /Auth/Login /dl/index.html; do
  echo "  $p -> $(curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://localhost:3000$p)"
done
