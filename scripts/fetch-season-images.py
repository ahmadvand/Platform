#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۱۰۶ — دریافت ۴ عکس فصل از Wikimedia Commons (منبع آمادهٔ مجوزدار — دستور کاربر:
«خودت عکس تولید نکن») + برش ۱۶:۹ و فشرده‌سازی برای embed در بستهٔ اسکرین‌سیور (CSP فقط data:).
مجوزهای پذیرفته: CC0 / CC BY / CC BY-SA / Public domain. اسنپ‌شات اطلاعات منبع در JSON ذخیره می‌شود."""
import json, io, os, sys, urllib.request, urllib.parse
from PIL import Image

OUT = "/home/z/my-project/scripts/ss-season-assets"
os.makedirs(OUT, exist_ok=True)
UA = "HR-Platform-screensaver-package/2.7.17 (contact: admin@localhost)"
API = "https://commons.wikimedia.org/w/api.php"
OK_LICENSES = ("cc0", "public domain", "cc by-sa", "cc by", "pd", "copyrighted free use")

SEASONS = {
    "spring": "cherry blossom tree park",
    "summer": "green orchard apple trees summer",
    "autumn": "autumn park fallen leaves trees",
    "winter": "winter forest snow trees fog",
}

def api(params):
    params = dict(params, format="json")
    url = API + "?" + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    import time
    last = None
    for i in range(5):   # backoff — ویکی‌مدیا نرخ را محدود می‌کند
        try:
            with urllib.request.urlopen(req, timeout=45) as r:
                return json.load(r)
        except Exception as e:
            last = e
            time.sleep(12 * (i + 1))
    raise last

def grab(season, query):
    data = api({
        "action": "query", "generator": "search",
        "gsrsearch": f"filetype:bitmap {query}", "gsrnamespace": 6, "gsrlimit": 12,
        "prop": "imageinfo", "iiprop": "url|size|mime|extmetadata", "iiurlwidth": 960,
    })
    pages = (data.get("query") or {}).get("pages") or {}
    cands = sorted(pages.values(), key=lambda p: p.get("index", 99))
    for p in cands:
        ii = (p.get("imageinfo") or [{}])[0]
        if ii.get("mime") != "image/jpeg":
            continue
        if ii.get("width", 0) < 1100:
            continue
        em = ii.get("extmetadata") or {}
        def emv(k):
            v = em.get(k, {}).get("value") or ""
            return v
        lic = emv("LicenseShortName").strip()
        lic_l = lic.lower()
        if not any(l in lic_l for l in OK_LICENSES):
            continue
        # از valueهای HTML میدان Artist تگ‌ها را می‌گیریم
        artist = re.sub(r"<[^>]+>", "", emv("Artist")).strip()
        thumb = ii.get("thumburl") or ii.get("url")
        return {
            "title": p.get("title", ""),
            "license": lic,
            "artist": artist[:200],
            "page": ii.get("descriptionurl", ""),
            "thumb": thumb,
        }
    return None

import re, time

meta = {}
for si, (season, q) in enumerate(SEASONS.items()):
    if si:
        time.sleep(15)   # pacing — درخواست‌های پشت‌سرهم 403 می‌گیرند
    print(f"--- searching {season}: {q}")
    m = None
    for attempt in range(2):
        try:
            m = grab(season, q)
            break
        except Exception as e:
            print("   api error:", e)
    if not m:
        print(f"   !! no licensed candidate for {season}")
        continue
    print("   picked:", m["title"], "|", m["license"], "|", m["artist"][:60])
    req = urllib.request.Request(m["thumb"], headers={"User-Agent": UA})
    raw = urllib.request.urlopen(req, timeout=60).read()
    im = Image.open(io.BytesIO(raw)).convert("RGB")
    w, h = im.size
    # برش مرکزی ۱۶:۹
    tw, th = (880, 495)
    scale = max(tw / w, th / h)
    im2 = im.resize((max(tw, int(w * scale)), max(th, int(h * scale))), Image.LANCZOS)
    W, H = im2.size
    left, top = (W - tw) // 2, (H - th) // 2
    im2 = im2.crop((left, top, left + tw, top + th))
    path = os.path.join(OUT, f"{season}.jpg")
    im2.save(path, "JPEG", quality=52, optimize=True, progressive=True)
    m["file"] = path
    m["bytes"] = os.path.getsize(path)
    meta[season] = m
    print(f"   saved {path} ({m['bytes']} B)")

with io.open(os.path.join(OUT, "attributions.json"), "w", encoding="utf-8") as f:
    json.dump(meta, f, ensure_ascii=False, indent=2)
print("total:", sum(m.get("bytes", 0) for m in meta.values()), "bytes")
