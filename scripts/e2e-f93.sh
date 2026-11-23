#!/bin/bash
# E2E فاز ۹۳ — سرور + مرورگر در یک فراخوانی (پروسه‌های پس‌زمینه بین فراخوانی‌ها می‌میرند)
set -u
export Database__Provider=Sqlite TZ=Asia/Tehran ASPNETCORE_URLS=http://0.0.0.0:3000
cd /home/z/my-project/aspnet/src/Platform.Web
mkdir -p /tmp/shots
pkill -9 -f "Platform.Web.dll" 2>/dev/null; pkill -9 -f "dotnet run" 2>/dev/null; sleep 1

~/.dotnet/dotnet run -c Release --no-build --no-launch-profile > /tmp/hr-server.log 2>&1 &
SRV=$!
for i in $(seq 1 60); do
  curl -sf http://localhost:3000/health > /dev/null 2>&1 && break
  sleep 1
done
echo "server pid=$SRV health=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:3000/health)"
echo "flags-on-login=$(curl -s http://localhost:3000/Auth/Login | grep -c hr-flag)"

B() { agent-browser --session hr93 "$@"; }

# --- 1) صفحه ورود + پرچم‌ها ---
B open http://localhost:3000/ > /dev/null 2>&1
B set viewport 1440 900 > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
B screenshot /tmp/shots/01-login.png > /dev/null 2>&1

# --- 2) ورود ادمین ---
B eval "
  const u=document.querySelector('input[name=\"identifier\"]'), p=document.querySelector('input[name=\"secret\"]');
  if(u&&p){u.value='admin';p.value='Admin@1234';
    u.dispatchEvent(new Event('input',{bubbles:true}));p.dispatchEvent(new Event('input',{bubbles:true}));}
  document.querySelector('form').requestSubmit();
" > /dev/null 2>&1 || true
sleep 3
B wait --load networkidle > /dev/null 2>&1 || true
# مجوز غایب = صفحهٔ Notice — دکمهٔ «ادامه به سامانه» (کوکی Ack نشست)
B eval "
  (function(){
    const b=[...document.querySelectorAll('a.btn,button.btn')].find(x=>/ادامه|سامانه/.test(x.textContent||''));
    if(b){b.click(); return 'acked';} return 'no-btn';
  })()
" > /dev/null 2>&1 || true
sleep 2
B wait --load networkidle > /dev/null 2>&1 || true
B screenshot /tmp/shots/02-dashboard.png > /dev/null 2>&1
echo "after-login url=$(B get url 2>/dev/null)"

# --- 3) صفحه دستگاه‌ها + اندازه‌گیری تراز دکمه‌ها ---
B open http://localhost:3000/Devices > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
sleep 1
B screenshot /tmp/shots/03-devices.png > /dev/null 2>&1
B eval "
  (function(){
    const out={rows:[]};
    document.querySelectorAll('.btn-row').forEach(function(r,i){
      if(i>3) return;
      const cs=getComputedStyle(r);
      const items=[...r.children].map(function(f){
        const b=f.querySelector('.btn')||f;
        const bb=b.getBoundingClientRect();
        return {t:Math.round(bb.top),h:Math.round(bb.height),txt:(b.textContent||'').trim().slice(0,18)};
      });
      out.rows.push({i:i,disp:cs.display,align:cs.alignItems,gap:cs.gap,items:items});
    });
    const inp=document.querySelector('input[name=pin]');
    if(inp){const cs2=getComputedStyle(inp);out.pinFont=cs2.fontFamily.slice(0,60);}
    return JSON.stringify(out);
  })()
" > /tmp/shots/devices-measure.json 2>&1

# --- 4) دیالوگ قلم حقوق جدید ---
B open http://localhost:3000/Payroll/Items > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
sleep 1
B eval "
  const b=[...document.querySelectorAll('a[data-modal]')].find(a=>/ItemCreate|قلم/.test(a.getAttribute('data-modal')+' '+a.textContent));
  if(b){b.click();} !!b
" > /dev/null 2>&1 || true
sleep 2
B screenshot /tmp/shots/04-item-dialog.png > /dev/null 2>&1

# --- 5) مدیریت زبان‌ها (پرچم‌های جدول) ---
B open http://localhost:3000/Langs > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
sleep 1
B screenshot /tmp/shots/05-langs.png > /dev/null 2>&1

# --- 6) سوییچ زبان: منوی زبان + صفحهٔ گذر ---
B open http://localhost:3000/ > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
B eval "document.getElementById('tbLangBtn').click(); 'ok'" > /dev/null 2>&1 || true
sleep 1
B screenshot /tmp/shots/06-lang-menu.png > /dev/null 2>&1
B open "http://localhost:3000/Ui/Lang?l=en&r=/Auth/Login" > /dev/null 2>&1
sleep 0.4
B screenshot /tmp/shots/07-lang-switch.png > /dev/null 2>&1
sleep 2
B screenshot /tmp/shots/08-login-en.png > /dev/null 2>&1
echo "lang-switch url=$(B get url 2>/dev/null)"
# برگشت به فارسی
B open "http://localhost:3000/Ui/Lang?l=fa&r=/Auth/Login" > /dev/null 2>&1
sleep 2.5

# --- 7) خطاهای کنسول ---
B console > /tmp/shots/console.log 2>&1
B errors > /tmp/shots/pageerrors.log 2>&1
echo "--- console.log ---"; cat /tmp/shots/console.log | head -30
echo "--- pageerrors.log ---"; cat /tmp/shots/pageerrors.log | head -20

B close > /dev/null 2>&1
kill $SRV 2>/dev/null
echo DONE
