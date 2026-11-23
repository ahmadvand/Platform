#!/bin/bash
# تست زندهٔ پیش‌نمایش پالت با مرورگر headless — همه‌چیز در یک invocation
export PATH="/home/z/.dotnet:$PATH"
cd /home/z/my-project

pkill -f start-web.sh 2>/dev/null; pkill -f Platform.Web.dll 2>/dev/null; sleep 3
setsid nohup bash scripts/start-web.sh </dev/null >>dev.log 2>&1 & disown

code=000
for i in $(seq 1 20); do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 http://localhost:3000/health 2>/dev/null)
  [ "$code" = "200" ] && break; sleep 3
done
echo "health=$code"
[ "$code" != "200" ] && exit 1

agent-browser open http://localhost:3000/Auth/Login >/dev/null 2>&1
sleep 1
agent-browser eval "
var u=document.querySelector('input[name=identifier]'), p=document.querySelector('input[name=secret]');
u.value='admin'; p.value='Admin@1234'; u.closest('form').submit(); 'ok'
" >/dev/null 2>&1
sleep 4
URL=$(agent-browser get url 2>/dev/null | tail -1)
echo "after-login-url=$URL"

# اگر صفحهٔ هشدار مجوز بود، تایید کن
agent-browser eval "
var b = Array.from(document.querySelectorAll('button,a,form button')).find(function(x){ return /فهمیدم/.test(x.innerText||''); });
if (b) { b.click(); 'notice-ok' } else 'no-notice'
" 2>&1 | tail -1
sleep 2

# به صفحه تنظیمات
agent-browser open http://localhost:3000/Settings >/dev/null 2>&1
sleep 2
agent-browser get url 2>&1 | tail -1
agent-browser eval "document.querySelector('.pal-acts [data-modal]') ? 'settings-ok' : document.body.innerText.slice(0,150)" 2>&1 | tail -1
# دکمهٔ ویرایش اولین پالت (data-modal)
EDIT_HREF=$(agent-browser eval "
var b=document.querySelector('.pal-acts [data-modal]');
b ? b.getAttribute('data-modal') : 'none'
" 2>/dev/null | tail -1 | tr -d '"')
echo "edit-modal-url=$EDIT_HREF"

# باز کردن مودال با کلیک واقعی
agent-browser eval "
var b=document.querySelector('.pal-acts [data-modal]');
if (b) { b.click(); 'clicked' } else 'no-btn'
" 2>&1 | tail -1
sleep 2

# حالا ورودی رنگ اصلی را تغییر بده و ببین پیش‌نمایش عوض می‌شود؟
agent-browser eval "
var inp = document.querySelector('.modal-overlay input[data-pal][name=primaryColor]');
if (!inp) JSON.stringify({err:'no-input'});
else {
  var box = document.getElementById('palPreviewBox');
  var before = box ? box.getAttribute('style') : null;
  inp.value = '#ff0000';
  inp.dispatchEvent(new Event('input', {bubbles:true}));
  var after = box ? box.getAttribute('style') : null;
  JSON.stringify({before: before, after: after, changed: before !== after})
}
" 2>&1 | tail -2

echo "=== console errors ==="
echo "(skipped)"
