#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۱۰۷ — زیرمجموعه‌سازی فونت وزیرمتن برای embed در بسته‌های محافظ صفحه (دستور کاربر:
«فونت همیشه وزیر باشه»). Runtime سندباکس فقط font-src data: دارد → فونت باید data URI باشد.
زیرمجموعه: لاتین پایه + بلوک عربی/فارسی + ZWNJ + نقطه‌گذاری — تا CSS بسته (سقف ۱۰۰هزار نویسه)
هم فونت بگذرد هم استایل‌ها. خروجی: scripts/ss-assets/Vazir-{Regular,Bold}-sub.woff2 + فایل CSS آماده."""
import os, subprocess, base64, io

ROOT = "/home/z/my-project"
FONTS = f"{ROOT}/aspnet/src/Platform.Web/wwwroot/fonts"
TTFS = f"{FONTS}/ttf"
OUT = f"{ROOT}/scripts/ss-assets"
os.makedirs(OUT, exist_ok=True)

UNICODES = "U+0020-007E,U+00A0,U+00AB,U+00BB,U+0600-06FF,U+200C-200F,U+2010-2027"
# فاز ۱۰۸ (رفع اشکال کاربر: حروف فارسی جدا از هم بود — «آسمان شب» مثل «آ س م ا ن ش ب»)
# علت: محدودکردن --layout-features، ویژگی‌های شکل‌دهی عربی init/medi/fina/isol را حذف
# می‌کرد و هر حرف در «شکل ایزوله» می‌ماند. اکنون هیچ محدودیتی نمی‌گذاریم تا pyftsubset
# همهٔ ویژگی‌های چیدمان (شکل‌دهی + کشش + نقطه‌گذاری) را نگه دارد؛ در پایان هم assert داریم.
MUST_HAVE = {
    "GSUB": {"init", "medi", "fina", "rlig", "ccmp", "calt"},
    "GPOS": {"kern", "mark", "mkmk"},
}
# نکته: وزیرمتن ویژگی «isol» ندارد و نیازی هم نیست — شکل پایهٔ هر حرف همان ایزوله است
# و پیوستن با init/medi/fina انجام می‌شود؛ mark/mkmk هم ویژگی GPOS هستند نه GSUB.

def subset(src, dst):
    subprocess.run([
        "pyftsubset", src,
        f"--output-file={dst}", "--flavor=woff2",
        f"--unicodes={UNICODES}",
        "--no-hinting", "--desubroutinize",
    ], check=True)
    return os.path.getsize(dst)

def assert_shaping(dst):
    from fontTools.ttLib import TTFont
    f = TTFont(dst)
    report = {}
    for t, need in MUST_HAVE.items():
        have = {fr.FeatureTag for fr in f[t].table.FeatureList.FeatureRecord} if t in f else set()
        missing = need - have
        assert not missing, f"{os.path.basename(dst)}: {t} جا افتاده: {sorted(missing)}"
        report[t] = sorted(have)
    return report

def css_face(family, weight, path):
    b64 = base64.b64encode(open(path, "rb").read()).decode("ascii")
    return (f"@font-face{{font-family:'{family}';font-style:normal;font-weight:{weight};"
            f"font-display:swap;src:url(data:font/woff2;base64,{b64}) format('woff2');}}")

res = {}
for name in ("Regular", "Bold"):
    src = f"{TTFS}/Vazirmatn-{name}.ttf"
    dst = f"{OUT}/Vazir-{name}-sub.woff2"
    res[name] = subset(src, dst)
    feats = assert_shaping(dst)
    print(f"Vazirmatn-{name}: {os.path.getsize(src)} → {res[name]} B (b64 chars: {len(base64.b64encode(open(dst,'rb').read()))})")
    print(f"  GSUB: {feats['GSUB']}")
    print(f"  GPOS: {feats['GPOS']}")

css = css_face("Vazirmatn", 400, f"{OUT}/Vazir-Regular-sub.woff2") + \
      css_face("Vazirmatn", 700, f"{OUT}/Vazir-Bold-sub.woff2")
with io.open(f"{OUT}/vazir-embed.css", "w", encoding="utf-8") as f:
    f.write(css)
print("css chars:", len(css), "| css file:", f"{OUT}/vazir-embed.css")
assert len(css) < 100_000, "CSS با فونت از سقف ۱۰۰هزار نویسه می‌گذرد"
print("OK — زیر سقف CSS بسته‌ها است")
