#!/usr/bin/env python3
# فاز ۱۰۶/۱۰۷ — ساخت بستهٔ محافظ صفحهٔ «منظرهٔ چهارفصل» (hr-screensaver-package spec 1)
# خروجی:
#   hr-screensaver-package-seasons-2.0.0.json  (فصل: دستی ادمین یا از تاریخ شمسی — خودکار)
#   test-forced-{spring,summer,autumn,winter}.json  (واریانت تست با فصل اجباری + زمان‌سنج ۵ ثانیه)
#   HR-Screensaver-Package-Seasons-2.0.0.zip   (برای مرکز دانلود)
import base64, json, os, subprocess, zipfile

BASE = "/home/z/my-project/scripts/i106-seasons"
ASSETS = os.path.join(BASE, "assets")
OUT = os.path.join(BASE, "out")
os.makedirs(OUT, exist_ok=True)

TEMPLATE = open(os.path.join(BASE, "template.js"), encoding="utf-8").read()
CSS = "html,body{margin:0;height:100%;overflow:hidden;background:#05070a}" \
      "canvas{position:fixed;inset:0;width:100%;height:100%;display:block}"

SEASONS = ["spring", "summer", "autumn", "winter"]
bgs = []
for s in SEASONS:
    raw = open(os.path.join(ASSETS, f"final-{s}.jpg"), "rb").read()
    bgs.append("data:image/jpeg;base64," + base64.b64encode(raw).decode("ascii"))
bg_js = "[" + ",".join('"' + b + '"' for b in bgs) + "]"

META = {
    "name": "منظرهٔ چهارفصل",
    "author": "پلتفرم یکپارچه منابع انسانی",
    "version": "2.0.0",
    "description": "محافظ صفحهٔ صحنه‌ای با رخدادهای دوره‌ای تصادفی تا تکراری نشود: بهار = شکوفه‌ریزان و تکه‌پاشی، گل‌ها با زنبور و پروانه‌های نشسته روی گل، باران بهاری و رنگین‌کمان، رفتگر شکوفه‌ها؛ تابستان = باغ میوه‌دار، گل و زنبور، قاصدک‌های شناور، لرزش هوای گرم؛ پاییز = برگ‌های رنگارنگ با تندباد، پرندگان مهاجر در قالب V، نم‌نم‌باران، رفتگر برگ‌ها؛ زمستان = برف سه‌لایه، مه، آدم‌برفی، گنجشک لپ‌زن و خرگوش سفید. فصل: دستی از تنظیمات سامانه یا خودکار از تاریخ شمسی. پس‌زمینه‌ها عکس واقعی (ATTRIBUTIONS.txt)",
}

TEST_SETTINGS = {"enabled": True, "requirePassword": True, "minutes": 0, "seconds": 5}
FINAL_SETTINGS = {"enabled": True, "requirePassword": True, "minutes": 1, "seconds": 15}

def banned_ok(txt):
    low = txt.lower()
    for bad in ("</script", "</style", "<!--"):
        if bad in low:
            return bad
    return None

def build(forced, settings, out_name):
    js = TEMPLATE.replace("__FORCED_SEASON__", str(forced)).replace("__BG_DATA__", bg_js)
    bad = banned_ok(js) or banned_ok(CSS)
    assert bad is None, f"banned sequence {bad} in code"
    assert len(js) <= 200_000, f"js too long: {len(js)}"
    assert len(CSS) <= 100_000, f"css too long: {len(CSS)}"
    bundle = {"format": "hr-screensaver-package", "spec": 1, "meta": META,
              "code": {"js": js, "css": CSS}, "settings": settings}
    text = json.dumps(bundle, ensure_ascii=False, separators=(",", ":"))
    assert len(text.encode("utf-8")) <= 1024 * 1024, f"file > 1MB: {len(text.encode('utf-8'))}"
    parsed = json.loads(text)          # گردش کامل JSON
    assert parsed["code"]["js"] == js
    path = os.path.join(OUT, out_name)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    return path, len(js), len(text.encode("utf-8"))

# بررسی نحو JS با node (اگر موجود بود)
node = "/usr/bin/node"
has_node = os.path.exists(node)
if not has_node:
    node = "/usr/local/bin/node"
    has_node = os.path.exists(node)

results = []
for forced, name in [(-1, "hr-screensaver-package-seasons-2.0.0.json")] + \
                    [(i, f"test-forced-{s}.json") for i, s in enumerate(SEASONS)]:
    st = TEST_SETTINGS if forced >= 0 else FINAL_SETTINGS
    p, jslen, size = build(forced, st, name)
    results.append((name, jslen, size))
    if has_node:
        chk = subprocess.run([node, "--check", os.path.join(OUT, f"_chk_{forced}.js")],
                             capture_output=True, text=True)
        # node --check روی js خام (نه JSON) — js را جدا بنویس
    if has_node:
        js_only = open(p, encoding="utf-8").read()
        import json as J
        js_only = J.loads(js_only)["code"]["js"]
        tmp = os.path.join(OUT, f"_chk_{max(forced,0)}_{abs(hash(name))%999}.js")
        open(tmp, "w", encoding="utf-8").write(js_only)
        chk = subprocess.run([node, "--check", tmp], capture_output=True, text=True)
        assert chk.returncode == 0, f"JS syntax error in {name}: {chk.stderr[:600]}"
        os.remove(tmp)

# زیپ برای مرکز دانلود
zpath = os.path.join(OUT, "HR-Screensaver-Package-Seasons-2.0.0.zip")
with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(os.path.join(OUT, "hr-screensaver-package-seasons-2.0.0.json"),
            "hr-screensaver-package-seasons-2.0.0.json")

print("node check:", "done" if has_node else "node not found — skipped")
for name, jslen, size in results:
    print(f"{name}: js={jslen} chars, file={size/1024:.1f}KB")
print("zip:", zpath, os.path.getsize(zpath) // 1024, "KB")
