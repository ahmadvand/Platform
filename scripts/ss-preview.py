#!/usr/bin/env python3
# ======================================================================
# فاز ۱۰۶ — پیش‌نمایش مستقل بستهٔ محافظ صفحه با ساختار «دقیقاً» همان
# /Settings/ScreensaverRuntime (CSP + style + hr-ss-data + script) تا
# اتصال حروف فارسی (رفع باگ «آ س م ا ن ش ب» / متن مقطع) بصری راستی‌آزمایی شود.
# مصرف: python3 scripts/ss-preview.py <slug> [out.html]
# ======================================================================
import json, sys, os

ROOT = "/home/z/my-project"
SSPKGS = f"{ROOT}/assets/screensaver-packages"

def main():
    slug = sys.argv[1] if len(sys.argv) > 1 else "hr-ss-poem-rain"
    fp = f"{SSPKGS}/{slug}.json"
    out = sys.argv[2] if len(sys.argv) > 2 else f"{ROOT}/scripts/ss-preview/preview-{slug}.html"
    b = json.load(open(fp, encoding="utf-8"))
    name = b["meta"]["name"]
    css, js = b["code"]["css"], b["code"]["js"]
    data = json.dumps({"lang": "fa", "name": name, "settings": b.get("settings")}, ensure_ascii=False)
    html = f"""<!doctype html>
<html lang="fa" dir="rtl">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="referrer" content="no-referrer">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data:; font-src data:; connect-src 'none'; form-action 'none'; base-uri 'none'">
<title>{name}</title>
<style>html,body{{margin:0;height:100%;overflow:hidden;background:#03070a}}{css}</style>
</head>
<body>
<script id="hr-ss-data" type="application/json">{data}</script>
<script>{js}</script>
</body>
</html>
"""
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        f.write(html)
    print("preview written:", out)

if __name__ == "__main__":
    main()
