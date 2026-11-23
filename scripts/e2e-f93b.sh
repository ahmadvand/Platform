#!/bin/bash
# E2E فاز ۹۳ — دور دوم: ساخت دستگاه + سنجش تراز دکمه‌ها + صفحهٔ گذر مستقل
set -u
export Database__Provider=Sqlite TZ=Asia/Tehran ASPNETCORE_URLS=http://0.0.0.0:3000
cd /home/z/my-project/aspnet/src/Platform.Web
mkdir -p /tmp/shots
pkill -9 -f "Platform.Web.dll" 2>/dev/null; pkill -9 -f "dotnet run" 2>/dev/null; sleep 1

~/.dotnet/dotnet run -c Release --no-build --no-launch-profile > /tmp/hr-server.log 2>&1 &
SRV=$!
for i in $(seq 1 60); do curl -sf http://localhost:3000/health > /dev/null 2>&1 && break; sleep 1; done
echo "health=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:3000/health)"

B() { agent-browser --session hr93b "$@"; }

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
echo "url=$(B get url 2>/dev/null)"

# --- ۱) ساخت دستگاه آزمایشی ---
B open http://localhost:3000/Devices > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
B eval "
  (function(){
    const b=[...document.querySelectorAll('a[data-modal],a.btn')].find(a=>/تعریف دستگاه جدید/.test(a.textContent||''));
    if(b){b.click();return 'clicked';} return 'not-found';
  })()
" > /dev/null 2>&1 || true
sleep 2
B eval "
  (function(){
    const m=document.querySelector('.modal');
    if(!m) return 'no-modal';
    m.querySelector('input[name=name]').value='ترمینال ورودی اصلی';
    m.querySelector('input[name=serial]').value='TST-93-001';
    m.querySelector('input[name=name]').dispatchEvent(new Event('input',{bubbles:true}));
    m.querySelector('input[name=serial]').dispatchEvent(new Event('input',{bubbles:true}));
    const f=m.querySelector('form');
    f.requestSubmit();
    return 'submitted';
  })()
" > /tmp/shots/dev-create.log 2>&1 || true
sleep 3
B wait --load networkidle > /dev/null 2>&1 || true
B open http://localhost:3000/Devices > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
sleep 1
B screenshot /tmp/shots/10-devices-with-card.png > /dev/null 2>&1

# --- ۲) اندازه‌گیری تراز دکمه‌های اکشن ---
B eval "
  (function(){
    const out={rows:[],pin:null};
    document.querySelectorAll('.btn-row').forEach(function(r,i){
      if(i>5) return;
      const items=[...r.querySelectorAll('.btn')].map(function(b){
        const bb=b.getBoundingClientRect();
        return {t:Math.round(bb.top),m:Math.round(bb.top+bb.height/2),h:Math.round(bb.height),txt:(b.textContent||'').trim().slice(0,16)};
      });
      out.rows.push({i:i,items:items});
    });
    const inp=document.querySelector('input[name=pin]');
    if(inp) out.pin=getComputedStyle(inp).fontFamily.slice(0,50);
    return JSON.stringify(out);
  })()
" > /tmp/shots/devices-measure2.json 2>&1

# --- ۳) پرسنل: ردیف اکشن جدول ---
B open http://localhost:3000/Employees > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
sleep 1
B screenshot /tmp/shots/11-employees.png > /dev/null 2>&1
B eval "
  (function(){
    const out=[];
    document.querySelectorAll('td .btn-row').forEach(function(r,i){
      if(i>2) return;
      const items=[...r.querySelectorAll('.btn')].map(function(b){
        const bb=b.getBoundingClientRect();
        return {t:Math.round(bb.top),m:Math.round(bb.top+bb.height/2),h:Math.round(bb.height),txt:(b.textContent||'').trim().slice(0,14)};
      });
      out.push({row:i,items:items});
    });
    return JSON.stringify(out);
  })()
" > /tmp/shots/emp-measure.json 2>&1

# --- ۴) صفحهٔ گذر زبان (مستقل) ---
B open "http://localhost:3000/Ui/Lang?l=en&r=/Devices" > /dev/null 2>&1
sleep 0.3
B screenshot /tmp/shots/12-langswitch.png > /dev/null 2>&1
sleep 2
B eval "document.documentElement.lang" > /dev/null 2>&1 || true
B open "http://localhost:3000/Ui/Lang?l=fa&r=/Devices" > /dev/null 2>&1
sleep 2.5

# --- ۵) کنسول ---
B console > /tmp/shots/console2.log 2>&1
B errors > /tmp/shots/pageerrors2.log 2>&1
echo "--- console2 ---"; head -20 /tmp/shots/console2.log
echo "--- pageerrors2 ---"; head -10 /tmp/shots/pageerrors2.log
B close > /dev/null 2>&1
kill $SRV 2>/dev/null
pkill -9 -f "Platform.Web.dll" 2>/dev/null
echo DONE
