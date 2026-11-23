#!/usr/bin/env python3
# فاز ۱۰۶ — ساخت بستهٔ hr-screensaver-package «چهارفصل» با همان قواعد سخت‌گیرانهٔ درون‌ریزی سامانه
import json, io, os, subprocess, sys, re

ROOT = '/home/z/my-project'
ENG = ROOT + '/scripts/i106-four-season-engine.js'
PHOTOS = ROOT + '/scripts/i106-photos/meta.json'
OUT = ROOT + '/download/HR-Screensaver-FourSeasons-Package-1.0.0.json'

meta_photos = json.load(open(PHOTOS, encoding='utf-8'))
js = io.open(ENG, encoding='utf-8').read()

# جای‌گذاری عکس‌ها به‌جای /*PHOTOS_PH*/{}
photos_obj = {}
for season, m in meta_photos.items():
    jpg_path = f'{ROOT}/scripts/i106-photos/{season}.jpg'
    import base64
    b64 = base64.b64encode(open(jpg_path, 'rb').read()).decode()
    artist = m['artist']
    lic = m['license']
    photos_obj[season] = {
        'src': 'data:image/jpeg;base64,' + b64,
        'by': artist,
        'lic': lic,
        'page': m['page'],
    }
photos_json = json.dumps(photos_obj, ensure_ascii=False, separators=(',', ':'))
marker = '/*PHOTOS_PH*/{}'
if marker not in js:
    sys.exit('PHOTOS_PH marker not found in engine')
js = js.replace(marker, photos_json, 1)

CSS = ('html,body{margin:0;height:100%;overflow:hidden;background:#05080c}' +
       'canvas{position:fixed;inset:0;width:100vw;height:100vh;display:block}')

DESC = ('محافظ صفحهٔ زندهٔ چهارفصل: با «خودکار» فصل از تاریخ سامانه و تقویم شمسی/میلادی تشخیص داده می‌شود '
        'و از تنظیمات سامانه («فصل صحنهٔ محافظ صفحه») می‌توان فصل را دستی قفل کرد. '
        'بهار: گلبرگ‌ریزان، زنبور، پروانه و رفتگر باغ؛ تابستان: خورشید، ابر، میوهٔ ریزان و پرستو؛ '
        'پاییز: برگ‌ریزان چندشکلی با تندباد و مه؛ زمستان: برف سه‌لایه، مه‌زمینی، یخ‌زدگی و دلیجان. '
        'پس‌زمینه: عکس‌های Wikimedia Commons با مجوز CC BY-SA؛ ارجاع هر عکس چرخشی در گوشهٔ صفحه.')

bundle = {
    'format': 'hr-screensaver-package',
    'spec': 1,
    'meta': {
        'name': 'چهارفصل — صحنه‌های زندهٔ محافظ صفحه',
        'author': 'سامانه یکپارچه منابع انسانی (Platform HR)',
        'version': '1.0.0',
        'description': DESC,
    },
    'settings': {
        'enabled': True,
        'minutes': 0,
        'seconds': 10,
        'requirePassword': True,
        'season': 'auto',
    },
    'code': {'js': js, 'css': CSS},
}

# ---------- اعتبارسنجی با همان قواعد SettingsController ----------
errors = []
if len(bundle['meta']['name']) > 100: errors.append('name > 100')
if len(bundle['meta']['author']) > 100: errors.append('author > 100')
if len(bundle['meta']['version']) > 40: errors.append('version > 40')
if len(bundle['meta']['description']) > 500: errors.append(f'description {len(bundle["meta"]["description"])} > 500')
if len(js) > 200_000: errors.append(f'js {len(js)} > 200000')
if len(CSS) > 100_000: errors.append(f'css {len(CSS)} > 100000')
for bad in ('</script', '</style', '<!--'):
    if bad in js.lower() or bad in CSS.lower():
        errors.append('forbidden sequence: ' + bad)
raw = json.dumps(bundle, ensure_ascii=False, separators=(',', ':'))
if len(raw.encode('utf-8')) > 1024 * 1024:
    errors.append(f'file {len(raw.encode("utf-8"))} > 1MB')

# سینتکس جاوااسکریپت
tmp_js = '/tmp/i106-engine-check.js'
io.open(tmp_js, 'w', encoding='utf-8').write(js)
r = subprocess.run(['node', '--check', tmp_js], capture_output=True, text=True)
if r.returncode != 0:
    errors.append('JS SYNTAX: ' + r.stderr[:400])

if errors:
    print('VALIDATION FAILED:')
    for e in errors:
        print(' -', e)
    sys.exit(1)

io.open(OUT, 'w', encoding='utf-8').write(json.dumps(bundle, ensure_ascii=False, indent=1))
size = os.path.getsize(OUT)
import hashlib
sha = hashlib.sha256(open(OUT, 'rb').read()).hexdigest()
print(f'OK  {OUT}')
print(f'size={size}B  js={len(js)}  css={len(CSS)}  desc={len(DESC)}')
print(f'sha256={sha[:16]}...')
