#!/bin/bash
# E2E فاز ۹۳ — دور سوم: سنجش مجدد تراز دکمه‌ها بعد از اصلاح inline-form
set -u
export Database__Provider=Sqlite TZ=Asia/Tehran ASPNETCORE_URLS=http://0.0.0.0:3000
cd /home/z/my-project/aspnet/src/Platform.Web
pkill -9 -f "Platform.Web.dll" 2>/dev/null; pkill -9 -f "dotnet run" 2>/dev/null; sleep 1

~/.dotnet/dotnet run -c Release --no-build --no-launch-profile > /tmp/hr-server.log 2>&1 &
SRV=$!
for i in $(seq 1 60); do curl -sf http://localhost:3000/health > /dev/null 2>&1 && break; sleep 1; done
echo "health=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:3000/health)"

B() { agent-browser --session hr93c "$@"; }

B open http://localhost:3000/ > /dev/null 2>&1
B set viewport 1440 900 > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
B eval "
  const u=document.querySelector('input[name=\"identifier\"]'), p=document.querySelector('input[name=\"secret\"]');
  if(u&&p){u.value='admin';p.value='Admin@1234';
    u.dispatchEvent(new Event('input',{bubbles:true}));p.dispatchEvent(new Event('input',{bubbles:true}));
    document.querySelector('form').requestSubmit();}
  'ok'
" > /dev/null 2>&1 || true
sleep 3
B eval "
  (function(){const b=[...document.querySelectorAll('a.btn,button.btn')].find(x=>/ادامه|سامانه/.test(x.textContent||''));if(b)b.click();return 'ok';})()
" > /dev/null 2>&1 || true
sleep 2

B open http://localhost:3000/Devices > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
sleep 1
B screenshot /tmp/shots/20-devices-fixed.png > /dev/null 2>&1
B eval "
  (function(){
    const out=[];
    document.querySelectorAll('.btn-row').forEach(function(r,i){
      if(i>3) return;
      const items=[...r.querySelectorAll('.btn')].map(function(b){
        const bb=b.getBoundingClientRect();
        return {t:Math.round(bb.top),m:Math.round(bb.top+bb.height/2),txt:(b.textContent||'').trim().slice(0,14)};
      });
      out.push({row:i,items:items});
    });
    return JSON.stringify(out);
  })()
" > /tmp/shots/devices-measure3.json 2>&1

# ردیف جدولی دیگر برای کنترل: کارت‌ها (صفحه پرسنل)
B open http://localhost:3000/Employees > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
sleep 1
B screenshot /tmp/shots/21-employees.png > /dev/null 2>&1

# صفحهٔ گذر مستقل
B open "http://localhost:3000/Ui/Lang?l=en&r=/Devices" > /dev/null 2>&1
sleep 0.35
B screenshot /tmp/shots/22-langswitch.png > /dev/null 2>&1
sleep 2
B open "http://localhost:3000/Ui/Lang?l=fa&r=/Devices" > /dev/null 2>&1
sleep 2.5

B console > /tmp/shots/console3.log 2>&1
B errors > /tmp/shots/pageerrors3.log 2>&1
echo "--- console3 ---"; head -12 /tmp/shots/console3.log
echo "--- pageerrors3 ---"; head -8 /tmp/shots/pageerrors3.log
B close > /dev/null 2>&1
kill $SRV 2>/dev/null
pkill -9 -f "Platform.Web.dll" 2>/dev/null
echo DONE
