#!/bin/bash
# فاز ۱۰۵ — تست جامع بسته‌های محافظ صفحه (curl)
set -uo pipefail
R=/home/z/my-project
T=/tmp/i105t; rm -rf "$T"; mkdir -p "$T"
B=http://localhost:3000
J="$T/jar"

TOK=$(curl -s -c "$J" "$B/Auth/Login" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Auth/Login" \
  --data-urlencode "identifier=admin" --data-urlencode "secret=Admin@1234" \
  --data-urlencode "__RequestVerificationToken=$TOK")
echo "login POST -> $CODE"; [ "$CODE" = "302" ] || { echo LOGIN_FAILED; exit 1; }

# گیت مجوز (در صورت نیاز «فهمیدم» بزن)
REDIR=$(curl -s -b "$J" -o /dev/null -w "%{redirect_url}" "$B/Settings")
if [[ "$REDIR" == *"/License/Notice"* ]]; then
  ATOK=$(curl -s -b "$J" -c "$J" "$B/License/Notice" | python3 -c "
import re,sys; m=re.search(r'name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"',sys.stdin.read()); print(m.group(1))")
  curl -s -b "$J" -c "$J" -o /dev/null -X POST "$B/License/Ack" --data-urlencode "__RequestVerificationToken=$ATOK"
  echo "license ack done"
fi

echo "== 1) صفحهٔ تنظیمات: بلوک بسته‌ها =="
curl -s -b "$J" -c "$J" "$B/Settings" -o "$T/settings.html"
for pat in "بسته‌های محافظ صفحه — انتقال خودِ اسکرین‌سیور" "باران ماتریکس (محافظ داخلی)" "درون‌ریزی بستهٔ محافظ صفحه" "ssPkgImportForm" "2.7.16"; do
  if grep -q "$pat" "$T/settings.html"; then echo "  OK: $pat"; else echo "  MISSING: $pat"; fi
done

echo "== 2) برون‌ریزی بستهٔ داخلی =="
CS=$(curl -s -b "$J" -c "$J" "$B/Settings" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
curl -s -b "$J" -c "$J" -D "$T/exp-h.txt" "$B/Settings/ExportScreensaverPackage" -o "$T/builtin.json"
python3 - "$T/builtin.json" <<'PY'
import json, sys
d = json.load(open(sys.argv[1], encoding='utf-8'))
assert d['format'] == 'hr-screensaver-package' and d['spec'] == 1, d.get('format')
assert d['meta']['name'], 'meta.name empty'
assert 'CHARSETS' in d['code']['js'] and 'Vazirmatn' in d['code']['js'], 'js payload wrong'
assert len(d['code']['js']) > 1000 and d['settings']['minutes'] >= 0
print('  OK: builtin bundle valid — name=%s, js=%d chars, css=%d chars, settings=%s' % (
    d['meta']['name'], len(d['code']['js']), len(d['code']['css']), d['settings']))
PY

echo "== 3) درون‌ریزی بستهٔ نمایشی سفارشی (آبی) =="
python3 - "$T/demo.json" <<'PY'
import json, sys
rain = """// باران دیجیتال آبی — بستهٔ نمایشی فاز ۱۰۵
(function(){
 'use strict';
 var lang='fa';
 try{var el=document.getElementById('hr-ss-data'); if(el){var d=JSON.parse(el.textContent||'{}'); if(d&&d.lang) lang=String(d.lang).toLowerCase();}}catch(e){}
 var SETS={fa:'اآبپتثجچحخدذرزژسشصضطظعغفقکگلمنوهی۰۱۲۳۴۵۶۷۸۹',ru:'абвгдежзийклмнопрстуфхцчшщъыьэюяё0123456789',en:'ABCDEF0123456789'};
 var CH=(SETS[lang]||SETS.en).split(''), fs=16;
 var cv=document.createElement('canvas');
 cv.style.cssText='position:fixed;inset:0;width:100%;height:100%;display:block';
 document.body.appendChild(cv);
 var ctx=cv.getContext('2d'), cols=[],drops=[],raf=0;
 function resize(){cv.width=innerWidth;cv.height=innerHeight;cols=Math.floor(cv.width/fs);drops=[];for(var i=0;i<cols;i++)drops[i]=Math.floor(Math.random()*-100);}
 function frame(){
  ctx.fillStyle='rgba(2,6,12,0.09)';ctx.fillRect(0,0,cv.width,cv.height);
  ctx.font=fs+'px Vazirmatn, monospace';
  for(var i=0;i<cols;i++){var ch=CH[Math.floor(Math.random()*CH.length)];var y=drops[i]*fs;
   ctx.fillStyle=Math.random()<0.05?'#bae6fd':'#38bdf8';ctx.fillText(ch,i*fs,y);
   if(y>cv.height&&Math.random()>0.977)drops[i]=0;drops[i]++;}
  raf=requestAnimationFrame(frame);
 }
 addEventListener('resize',resize);resize();raf=requestAnimationFrame(frame);
})();"""
css = "html,body{margin:0;height:100%;overflow:hidden;background:#02060c}canvas{position:fixed;inset:0}"
bundle = {
    "format": "hr-screensaver-package", "spec": 1,
    "meta": {"name": "باران دیجیتال آبی (نمونه)", "author": "تست فاز ۱۰۵", "version": "1.0",
             "description": "بستهٔ نمایشی برای راستی‌آزمایی درون‌ریزی/فعال‌سازی/سندباکس"},
    "settings": {"enabled": True, "minutes": 0, "seconds": 8, "requirePassword": True},
    "code": {"js": rain, "css": css},
}
open(sys.argv[1], 'w', encoding='utf-8').write(json.dumps(bundle, ensure_ascii=False))
print('  demo bundle written:', len(rain), 'chars js')
PY
TOK2=$(curl -s -b "$J" -c "$J" "$B/Settings" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/ImportScreensaverPackage" \
  -F "file=@$T/demo.json;type=application/json" -F "applySettings=true" \
  -F "__RequestVerificationToken=$TOK2")
echo "  import POST -> $CODE"; [ "$CODE" = "302" ] || { echo IMPORT_FAILED; exit 1; }
curl -s -b "$J" -c "$J" "$B/Settings" -o "$T/settings2.html"
grep -q "باران دیجیتال آبی (نمونه)" "$T/settings2.html" && echo "  OK: package row rendered" || echo "  MISSING row"
grep -q "✅ @HrT\|فعال" "$T/settings2.html" >/dev/null && echo "  OK: active badge present"

echo "== 4) Runtime سندباکس =="
curl -s -b "$J" -c "$J" -D "$T/rt-h.txt" "$B/Settings/ScreensaverRuntime" -o "$T/rt.html"
grep -qi "^content-security-policy: sandbox allow-scripts" "$T/rt-h.txt" && echo "  OK: CSP sandbox header" || grep -i "security-policy" "$T/rt-h.txt"
grep -q "hr-ss-data" "$T/rt.html" && grep -q "باران دیجیتال آبی" "$T/rt.html" && echo "  OK: runtime serves active package (data + code)"
grep -q "connect-src 'none'" "$T/rt.html" && echo "  OK: meta CSP connect-src none"
grep -q "پارس آسیا\|localStorage\|document.cookie" "$T/rt.html" && echo "  WARN: suspicious content" || echo "  OK: no host secrets in runtime"

echo "== 5) تست‌های منفی درون‌ریزی =="
python3 - "$T/bad1.json" "$T/bad2.json" "$T/bad3.json" <<'PY'
import json, sys
json.dump({"format":"other","spec":1,"meta":{"name":"x"},"code":{"js":"a"}}, open(sys.argv[1],'w'))
json.dump({"format":"hr-screensaver-package","spec":1,"meta":{},"code":{"js":"a"}}, open(sys.argv[2],'w'))
json.dump({"format":"hr-screensaver-package","spec":1,"meta":{"name":"x"},"code":{"js":"var a=1; document.write('</scr'+'ipt>')"}}, open(sys.argv[3],'w'))
PY
TOK3=$(curl -s -b "$J" -c "$J" "$B/Settings" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
for i in 1 2 3; do
  CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/ImportScreensaverPackage" \
    -F "file=@$T/bad$i.json;type=application/json" -F "__RequestVerificationToken=$TOK3")
  LOC=$(curl -s -b "$J" -o /dev/null -w "%{redirect_url}" "$B/Settings")
  echo "  bad#$i POST -> $CODE"
done
curl -s -b "$J" "$B/Settings" -o "$T/settings3.html"
python3 - <<'PY'
import io,re
h=io.open('/tmp/i105t/settings3.html',encoding='utf-8').read()
ok1='قالب' in h or 'هست' in h
rows=len(re.findall(r'باران دیجیتال آبی', h))
print('  OK: after 3 bad imports, demo row count =', rows, '(must be 1 — nothing new imported)')
assert rows == 1
PY

echo "== 6) زبان و پرچم بسته در صفحات عادی =="
curl -s -b "$J" "$B/" -o "$T/home.html"
grep -o 'data-ss-package="true"' "$T/home.html" | head -1 && echo "  OK: data-ss-package=true rendered"
grep -o 'data-ss-company="[^"]*"' "$T/home.html" | head -1

echo "== 7) غیرفعال‌سازی (بازگشت به داخلی) و حذف بسته =="
TOK4=$(curl -s -b "$J" -c "$J" "$B/Settings" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
PID=$(python3 -c "
import io,re
h=io.open('/tmp/i105t/settings3.html',encoding='utf-8').read()
m=re.search(r'ssPkgAct_(\d+)', h); print(m.group(1) if m else '0')")
echo "  package id = $PID"
CODE=$(curl -s -b "$J" -c "$J" -o /dev/null -w "%{http_code}" -X POST "$B/Settings/ActivateScreensaverPackage" \
  --data-urlencode "id=0" --data-urlencode "__RequestVerificationToken=$TOK4")
echo "  activate-builtin POST -> $CODE"
curl -s -b "$J" "$B/Settings/ScreensaverRuntime" -o /dev/null -w "  runtime after builtin-active -> %{http_code} (must be 404)\n"

echo "== DONE (curl part) =="
echo "jar: $J"
