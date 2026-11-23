#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۱۰۶ — دریافت ۴ عکس فصل از Openverse (تجمیع‌کنندهٔ رسمی CC — منبع آمادهٔ مجوزدار؛
دستور کاربر: «خودت عکس تولید نکن») با فرادادهٔ مجوز کامل (creator/license/version/landing).
مجوزهای پذیرفته: cc0/by/by-sa/pdm. خروجی: scripts/ss-season-assets/{season}.jpg + attributions.json."""
import json, io, os, time, urllib.request, urllib.parse, sys
from PIL import Image

OUT = "/home/z/my-project/scripts/ss-season-assets"
os.makedirs(OUT, exist_ok=True)
UA = "HR-Platform-screensaver-package/2.7.18 (contact: admin@localhost)"
API = "https://api.openverse.org/v1/images/"
OK = {"cc0", "by", "by-sa", "pdm"}

SEASONS = {
    "spring": "cherry blossom tree park",
    "summer": "apple orchard green trees summer",
    "autumn": "autumn park leaves trees colorful",
    "winter": "snowy forest trees winter fog",
}

def get(url, timeout=60):
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "*/*"})
    last = None
    for i in range(4):   # backoff برای 429/504
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.read()
        except Exception as e:
            last = e
            code = getattr(e, "code", 0)
            time.sleep(8 if code in (429, 504) else 5 * (i + 1))
    raise last

def search(q):
    params = urllib.parse.urlencode({
        "q": q, "license": "cc0,by,by-sa,pdm", "page_size": 20,
        "aspect_ratio": "wide", "extension": "jpg",
    })
    data = json.loads(get(API + "?" + params).decode("utf-8"))
    return data.get("results") or []

def pick_best(rows):
    for r in rows:
        lic = (r.get("license") or "").lower()
        if lic not in OK:
            continue
        w, h = r.get("width") or 0, r.get("height") or 0
        if w and w < 900:
            continue   # برای فول‌اسکرین خیلی ریز است
        creator = (r.get("creator") or "").strip()
        title = (r.get("title") or "").strip()
        if "portrait" in title.lower():
            continue
        return {
            "title": title[:120],
            "artist": creator[:120] or "(نامشخص)",
            "license": lic.upper() + ((" " + str(r.get("license_version"))) if r.get("license_version") else ""),
            "page": r.get("foreign_landing_url") or "",
            "url": r.get("url") or "",
            "thumb": r.get("thumbnail") or (f"https://api.openverse.org/v1/images/{r.get('identifier')}/thumb/" if r.get("identifier") else ""),
        }
    return None

def process(raw, path):
    im = Image.open(io.BytesIO(raw)).convert("RGB")
    w, h = im.size
    tw, th = 880, 495
    scale = max(tw / w, th / h)
    im2 = im.resize((max(tw, int(w * scale)), max(th, int(h * scale))), Image.LANCZOS)
    W, H = im2.size
    im2 = im2.crop(((W - tw) // 2, (H - th) // 2, (W - tw) // 2 + tw, (H - th) // 2 + th))
    im2.save(path, "JPEG", quality=52, optimize=True, progressive=True)

meta = {}
_att = os.path.join(OUT, "attributions.json")
if os.path.exists(_att):   # اجرای دوباره — از attributedهای قبلی حفظ شود
    try: meta = json.load(io.open(_att, encoding="utf-8"))
    except Exception: meta = {}

for season, q in SEASONS.items():
    if season in meta and os.path.exists(os.path.join(OUT, f"{season}.jpg")):
        print(f"--- {season}: already saved, skip", flush=True)
        continue
    print(f"--- {season}: {q}", flush=True)
    try:
        rows = search(q)
    except Exception as e:
        print("   search failed:", e, flush=True)
        continue
    print(f"   {len(rows)} results", flush=True)
    m = pick_best(rows)
    if not m:
        print("   !! no acceptable licensed result", flush=True)
        continue
    print("   picked:", m["title"][:50], "|", m["license"], "|", m["artist"][:40], flush=True)
    raw = None
    for cand in (m.pop("url", None), m.pop("thumb", None)):
        if not cand:
            continue
        try:
            raw = get(cand, timeout=90)
            if len(raw) > 12_000_000:
                raw = None; continue
            break
        except Exception as e:
            print("   download fail:", e, flush=True)
    if not raw:
        continue
    try:
        process(raw, os.path.join(OUT, f"{season}.jpg"))
    except Exception as e:
        print("   decode fail:", e, flush=True)
        continue
    m["file"] = os.path.join(OUT, f"{season}.jpg")
    m["bytes"] = os.path.getsize(m["file"])
    meta[season] = m
    print(f"   saved ({m['bytes']} B)", flush=True)
    time.sleep(5)

with io.open(_att, "w", encoding="utf-8") as f:
    json.dump(meta, f, ensure_ascii=False, indent=2)
print("seasons got:", list(meta), "total:", sum(m.get("bytes", 0) for m in meta.values()), "bytes", flush=True)
if len(meta) < 4:
    sys.exit(2)
