#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""انتخاب عکس بهار بدون چهرهٔ برجسته — ۶ کاندیدای اول در یک شیت شماره‌گذاری‌شده."""
import json, io, os, time, urllib.request, urllib.parse
from PIL import Image, ImageDraw

OUT = "/home/z/my-project/scripts/ss-season-assets"
UA = "HR-Platform-screensaver-package/2.7.18 (contact: admin@localhost)"
API = "https://api.openverse.org/v1/images/"
OK = {"cc0", "by", "by-sa", "pdm"}

def get(url, timeout=60):
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "*/*"})
    last = None
    for i in range(3):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.read()
        except Exception as e:
            last = e; time.sleep(6 * (i + 1))
    raise last

params = urllib.parse.urlencode({
    "q": "cherry blossom branches spring flowers", "license": "cc0,by,by-sa,pdm",
    "page_size": 20, "aspect_ratio": "wide", "extension": "jpg",
})
rows = json.loads(get(API + "?" + params).decode("utf-8")).get("results") or []
cands = []
for r in rows:
    lic = (r.get("license") or "").lower()
    if lic not in OK: continue
    w = r.get("width") or 0
    if w and w < 900: continue
    if "portrait" in (r.get("title") or "").lower(): continue
    cands.append(r)
    if len(cands) >= 6: break

thumbs = []
for i, r in enumerate(cands):
    try:
        raw = get(r.get("thumbnail") or r.get("url"), timeout=60)
        im = Image.open(io.BytesIO(raw)).convert("RGB")
        thumbs.append((i, r, im))
    except Exception as e:
        print(i, "fail", e)

sheet = Image.new("RGB", (884, 2 * 254 + 10), "white")
d = ImageDraw.Draw(sheet)
for n, (i, r, im) in enumerate(thumbs):
    im = im.resize((440, 248))
    x, y = (n % 2) * 444, (n // 2) * 254
    sheet.paste(im, (x, y))
    d.text((x + 8, y + 6), f"#{i} {(r.get('license') or '').upper()} — {(r.get('creator') or '')[:30]}", fill=(255, 40, 40))
sheet.save("/tmp/spring-cands.png")
for i, r, _ in thumbs:
    print(f"#{i}", (r.get('title') or '')[:60], "|", r.get('license'), r.get('license_version'), "|", (r.get('creator') or '')[:40])
