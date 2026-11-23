#!/usr/bin/env python3
# فاز ۱۰۶ — دانلود ۴ عکس منتخب (مجوز آزاد CC BY-SA) و فشرده‌سازی برای جاسازی data-URI در بسته
import json, subprocess, os, base64, sys

CAND = json.load(open('/home/z/my-project/scripts/i106-season-candidates.json', encoding='utf-8'))
OUT = '/home/z/my-project/scripts/i106-photos'
os.makedirs(OUT, exist_ok=True)

PICK = {
    'spring': "File:Cherry blossom close up, Ravenscourt Park - geograph.org.uk - 8007734.jpg",
    'summer': "File:Schenna (IT), Apfelplantage, Apfelernte -- 2026 -- 4095.jpg",
    'autumn': "File:Aerial View of Autumn Forest Colors.jpg",
    'winter': "File:Beech forest Mátra in winter.jpg",
}

def api_thumburl(title):
    import urllib.parse
    q = urllib.parse.urlencode({
        'action': 'query', 'format': 'json', 'prop': 'imageinfo',
        'iiprop': 'url|extmetadata', 'iiurlwidth': 800, 'titles': title,
    })
    url = 'https://commons.wikimedia.org/w/api.php?' + q
    r = subprocess.run(['curl', '-s', '--max-time', '40', '-A',
                        'HR-Platform-Screensaver/1.0 (free-licensed backdrop fetch)', url],
                       capture_output=True, text=True, timeout=60)
    d = json.loads(r.stdout)
    for p in d['query']['pages'].values():
        ii = p['imageinfo'][0]
        em = ii.get('extmetadata', {})
        return ii.get('thumburl') or ii.get('url'), em
    raise RuntimeError('no imageinfo for ' + title)

meta = {}
try:
    from PIL import Image
except ImportError:
    sys.exit('PIL missing')

for season, title in PICK.items():
    turl, em = api_thumburl(title)
    raw = os.path.join(OUT, season + '-orig.jpg')
    subprocess.run(['curl', '-sL', '--max-time', '60', '-A',
                    'HR-Platform-Screensaver/1.0 (free-licensed backdrop fetch)', '-o', raw, turl],
                   timeout=90, check=True)
    im = Image.open(raw).convert('RGB')
    w, h = im.size
    tw = 480
    th = int(h * tw / w)
    im2 = im.resize((tw, th), Image.LANCZOS)
    # برش به نسبت ۱۶:۹ برای پس‌زمینهٔ یکنواخت
    target_h = int(tw * 9 / 16)
    if th > target_h:
        top = (th - target_h) // 2
        im2 = im2.crop((0, top, tw, top + target_h))
    q = 44
    while True:
        small = os.path.join(OUT, season + '.jpg')
        im2.save(small, 'JPEG', quality=q, optimize=True, progressive=True)
        sz = os.path.getsize(small)
        if sz <= 17000 or q <= 26:
            break
        q -= 4
    lic = em.get('LicenseShortName', {}).get('value', '')
    artist = em.get('Artist', {}).get('value', '')
    import re
    artist = re.sub(r'<[^>]+>', '', artist).strip()
    b64 = base64.b64encode(open(small, 'rb').read()).decode()
    meta[season] = {
        'title': title, 'license': lic, 'artist': artist,
        'page': 'https://commons.wikimedia.org/wiki/' + title.replace(' ', '_'),
        'jpg_bytes': sz, 'b64_chars': len(b64), 'quality': q,
    }
    print(f'{season}: {sz}B q{q} b64={len(b64)} — {lic} — {artist[:50]}')

json.dump(meta, open(OUT + '/meta.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
total = sum(m['b64_chars'] for m in meta.values())
print(f'TOTAL b64 chars: {total}  (js cap 200000)')
