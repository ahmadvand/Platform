#!/usr/bin/env python3
# فاز ۱۰۶ — فشرده‌سازی عکس‌های فصل‌ها برای گنجایش در بستهٔ hr-screensaver-package
# سقف‌ها: فایل کل ≤۱MB؛ code.js ≤۲۰۰هزار نویسه؛ هر عکس باید ~≤۲۶KB شود (base64 ×1.333)
from PIL import Image
import os, io

ASSETS = "/home/z/my-project/scripts/i106-seasons/assets"
PICKS = [          # (منبع، انتخاب) — عکس‌های ساخته‌شده توسط دیگران (کاربر: عکس تولید نکن)
    ("spring", "spring-a.jpg"),
    ("summer", "summer-b.jpg"),
    ("autumn", "autumn-a.jpg"),
    ("winter", "winter-a.jpg"),
]
TARGET_W = 700     # عرض یکنواخت
TARGET_KB = 25     # سقف بایت هر JPEG نهایی

for name, src in PICKS:
    im = Image.open(os.path.join(ASSETS, src)).convert("RGB")
    w, h = im.size
    nh = round(h * TARGET_W / w)
    im = im.resize((TARGET_W, nh), Image.LANCZOS)
    best = None
    for q in range(58, 23, -2):
        buf = io.BytesIO()
        im.save(buf, "JPEG", quality=q, optimize=True, progressive=True)
        data = buf.getvalue()
        best = (q, data)
        if len(data) <= TARGET_KB * 1024:
            break
    q, data = best
    out = os.path.join(ASSETS, f"final-{name}.jpg")
    with open(out, "wb") as f:
        f.write(data)
    kb = len(data) / 1024
    b64 = len(data) * 4 / 3 / 1024
    flag = "OK" if kb <= TARGET_KB else "OVER"
    print(f"{name}: q={q} {TARGET_W}x{nh} {kb:.1f}KB (b64≈{b64:.1f}K chars) {flag}")
