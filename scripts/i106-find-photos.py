#!/usr/bin/env python3
# فاز ۱۰۶ — یافتن چهار عکس مجوزدار از Wikimedia Commons برای بستهٔ چهارفصل
# فقط عکس‌های با مجوز آزاد (CC0 / CC BY / CC BY-SA / Public domain) + ثبت اطلاعات ارجاع
import json, urllib.request, urllib.parse, sys, io

UA = {'User-Agent': 'HR-Platform-Screensaver/1.0 (Wikimedia Commons fetch for free-licensed backdrop)'}

SEARCHES = {
    'spring': 'cherry blossoms close-up tree',
    'summer': 'apple orchard trees green summer',
    'autumn': 'autumn forest colorful leaves',
    'winter': 'winter forest snow covered trees',
}
OK_LIC = ('CC0', 'CC BY', 'Public domain', 'публичное достояние')

def api(params):
    import subprocess
    url = 'https://commons.wikimedia.org/w/api.php?' + urllib.parse.urlencode(params)
    r = subprocess.run(['curl', '-s', '--max-time', '40', '-A',
                        'HR-Platform-Screensaver/1.0 (Wikimedia Commons fetch; free-licensed backdrop)', url],
                       capture_output=True, text=True, timeout=60)
    if r.returncode != 0 or not r.stdout.strip():
        raise RuntimeError('curl failed: ' + r.stderr[:200])
    return json.loads(r.stdout)

def strip_html(s):
    import re
    s = re.sub(r'<[^>]+>', '', s or '')
    return s.replace('&amp;', '&').strip()

def find(season, query):
    d = api({
        'action': 'query', 'format': 'json',
        'generator': 'search', 'gsrsearch': 'filetype:bitmap ' + query,
        'gsrnamespace': 6, 'gsrlimit': 14,
        'prop': 'imageinfo', 'iiprop': 'url|extmetadata|mime|size',
        'iiurlwidth': 640,
    })
    pages = d.get('query', {}).get('pages', {})
    out = []
    for p in pages.values():
        ii = (p.get('imageinfo') or [{}])[0]
        if ii.get('mime') not in ('image/jpeg',):
            continue
        w, h = ii.get('width', 0), ii.get('height', 0)
        if not (w >= 800 and h >= 500):
            continue
        em = ii.get('extmetadata', {}) or {}
        lic = strip_html((em.get('LicenseShortName') or {}).get('value', ''))
        artist = strip_html((em.get('Artist') or {}).get('value', ''))[:80]
        if not any(lic.startswith(x) or x in lic for x in OK_LIC):
            continue
        out.append({
            'title': p.get('title'), 'license': lic, 'artist': artist,
            'page': 'https://commons.wikimedia.org/wiki/' + urllib.parse.quote(p.get('title', '').replace(' ', '_')),
            'thumb': ii.get('thumburl') or ii.get('url'),
            'w': w, 'h': h,
        })
    return out

res = {}
for season, q in SEARCHES.items():
    cands = find(season, q)
    res[season] = cands[:4]
    print(f'== {season}: {len(cands)} candidates')
    for c in res[season]:
        print('  -', c['license'], '|', c['artist'][:40], '|', c['title'][:60])
        print('    ', c['page'])
io.open('/home/z/my-project/scripts/i106-season-candidates.json', 'w', encoding='utf-8').write(
    json.dumps(res, ensure_ascii=False, indent=1))
print('saved candidates json')
