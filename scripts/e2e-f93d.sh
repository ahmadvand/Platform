#!/bin/bash
set -u
export Database__Provider=Sqlite TZ=Asia/Tehran ASPNETCORE_URLS=http://0.0.0.0:3000
cd /home/z/my-project/aspnet/src/Platform.Web
pkill -9 -f "Platform.Web.dll" 2>/dev/null; pkill -9 -f "dotnet run" 2>/dev/null; sleep 1
~/.dotnet/dotnet run -c Release --no-build --no-launch-profile > /tmp/hr-server.log 2>&1 &
SRV=$!
for i in $(seq 1 60); do curl -sf http://localhost:3000/health > /dev/null 2>&1 && break; sleep 1; done
B() { agent-browser --session hr93d "$@"; }
B open http://localhost:3000/ > /dev/null 2>&1
B set viewport 1440 900 > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
B eval "
  const u=document.querySelector('input[name=\"identifier\"]'), p=document.querySelector('input[name=\"secret\"]');
  if(u&&p){u.value='admin';p.value='Admin@1234';
    u.dispatchEvent(new Event('input',{bubbles:true}));p.dispatchEvent(new Event('input',{bubbles:true}));
    document.querySelector('form').requestSubmit();}
" > /dev/null 2>&1 || true
sleep 3
B eval "
  (function(){const b=[...document.querySelectorAll('a.btn,button.btn')].find(x=>/ادامه|سامانه/.test(x.textContent||''));if(b)b.click();return 1;})()
" > /dev/null 2>&1 || true
sleep 1
B open http://localhost:3000/Devices > /dev/null 2>&1
B wait --load networkidle > /dev/null 2>&1 || true
sleep 1
B eval "
  (function(){
    const row=[...document.querySelectorAll('.btn-row')].find(r=>/ویرایش/.test(r.textContent||''));
    if(!row) return 'no-row';
    const cs=getComputedStyle(row);
    const out={rowDisplay:cs.display, rowAlign:cs.alignItems, rowLineHeight:cs.lineHeight, kids:[]};
    [...row.children].forEach(function(k){
      const kcs=getComputedStyle(k);
      const kb=k.getBoundingClientRect();
      const inner=k.querySelector('.btn');
      const ib=inner?inner.getBoundingClientRect():null;
      out.kids.push({
        tag:k.tagName, cls:k.className, disp:kcs.display, align:kcs.alignItems||'',
        box:{t:Math.round(kb.top),h:Math.round(kb.height)},
        btn: ib?{t:Math.round(ib.top),h:Math.round(ib.height)}:null,
        font:kcs.fontSize, lh:kcs.lineHeight, va:kcs.verticalAlign
      });
    });
    return JSON.stringify(out);
  })()
" > /tmp/shots/deep-measure.json 2>&1
B close > /dev/null 2>&1
kill $SRV 2>/dev/null; pkill -9 -f "Platform.Web.dll" 2>/dev/null
echo DONE
