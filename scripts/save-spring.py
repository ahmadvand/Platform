#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ثبت عکس انتخابی بهار (کاندیدای بدون چهره — #0 در شیت کاندیداها) در assets + به‌روزرسانی attributions.json"""
import json, io, os, time, urllib.request, urllib.parse
from PIL import Image

OUT = "/home/z/my-project/scripts/ss-season-assets"
UA = "HR-Platform-screensaver-package/2.7.18 (contact: admin@localhost)"
API = "https://api.openverse.org/v1/images/"

def get(url, timeout=90):
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "*/*"})
    last = None
    for i in range(4):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.read()
        except Exception as e:
            last = e; time.sleep(6 * (i + 1))
    raise last

q = urllib.parse.urlencode({
    "q": "cherry blossom branches spring flowers", "license": "cc0,by,by-sa,pdm",
    "page_size": 20, "aspect_ratio": "wide", "extension": "jpg",
})
rows = json.loads(get(API + "?" + q).decode("utf-8")).get("results") or []
OK = {"cc0", "by", "by-sa", "pdm"}
cands = []
for r in rows:
    if (r.get("license") or "").lower() not in OK: continue
    w = r.get("width") or 0
    if w and w < 900: continue
    if "portrait" in (r.get("title") or "").lower(): continue
    cands.append(r)
    if len(cands) >= 6: break

r = cands[0]   # #0 — همان انتخاب شیت (بدون چهرهٔ برجسته)
print("chosen:", (r.get("title") or "")[:60], "|", r.get("license"), "|", (r.get("creator") or "")[:40])
raw = None
for cand in (r.get("url"), r.get("thumbnail")):
    if not cand: continue
    try:
        raw = get(cand)
        if len(raw) > 12_000_000: raw = None; continue
        break
    except Exception as e:
        print("dl fail:", e)
im = Image.open(io.BytesIO(raw)).convert("RGB")
w, h = im.size
tw, th = 880, 495
scale = max(tw / w, th / h)
im2 = im.resize((max(tw, int(w * scale)), max(th, int(h * scale))), 1)
W, H = im2.size
im2 = im2.crop(((W - tw) // 2, (H - th) // 2, (W - tw) // 2 + tw, (H - th) // 2 + th))
path = os.path.join(OUT, "spring.jpg")
im2.save(path, "JPEG", quality=52, optimize=True, progressive=True)
print("saved", path, os.path.getsize(path), "B")

att_path = os.path.join(OUT, "attributions.json")
att = json.load(io.open(att_path, encoding="utf-8")) if os.path.exists(att_path) else {}
lic = (r.get("license") or "").lower()
licv = str(r.get("license_version") or "")
licname = {"cc0": "CC0", "pdm": "Public Domain Mark", "by": "CC BY", "by-sa": "CC BY-SA"}[lic] + (" " + licv if licv else "")
att["spring"] = {
    "title": (r.get("title") or "")[:120],
    "artist": (r.get("creator") or "(نامشخص)")[:120],
    "license": licname,
    "page": r.get("foreign_landing_url") or "",
    "file": path, "bytes": os.path.getsize(path),
}
with io.open(att_path, "w", encoding="utf-8") as f:
    json.dump(att, f, ensure_ascii=False, indent=2)
print("attributions:", {k: v["license"] for k, v in att.items()})
