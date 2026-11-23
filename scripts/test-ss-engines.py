#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۱۰۸ — ساخت صفحهٔ تست هر بسته (HTML با data JSON + CSS/JS استخراج‌شده از خود بسته)
برای آزمایش بصری موتورها در مرورگر headless — دقیقاً همان چیزی که Runtime سرو می‌کند.
دو واریانت برای آتش‌بازی: پیش‌فرض + با opts سفارشی (برای تست مسیر تنظیمات کاربر)."""
import json, io, os

ROOT = "/home/z/my-project"
OUT = "/tmp/sstest"
os.makedirs(OUT, exist_ok=True)

VER = "2.7.19"
PACKS = ["NightSky", "PoetryRain", "Aquarium", "Fireworks", "FourSeasons"]

def page(fname, d, data_json, out):
    html = f"""<!doctype html>
<html lang="fa" dir="rtl">
<head>
<meta charset="utf-8">
<title>{fname} test</title>
<style>html,body{{margin:0;height:100%;overflow:hidden}}</style>
<style>{d['code']['css']}</style>
</head>
<body>
<script id="hr-ss-data" type="application/json">{data_json}</script>
<script>{d['code']['js']}</script>
</body>
</html>"""
    io.open(f"{OUT}/{out}.html", "w", encoding="utf-8").write(html)
    print("test page:", out)

for name in PACKS:
    d = json.load(io.open(f"{ROOT}/download/packages/HR-Screensaver-{name}-{VER}.json", encoding="utf-8"))
    page(name, d,
         json.dumps({"lang": "fa", "name": d["meta"]["name"], "season": "auto"}, ensure_ascii=False),
         name)

# واریانت با opts سفارشی — آتش‌بازی طلایی کم‌شهر با انفجار زیاد (تست مسیر opts)
d = json.load(io.open(f"{ROOT}/download/packages/HR-Screensaver-Fireworks-{VER}.json", encoding="utf-8"))
page("Fireworks", d,
     json.dumps({"lang": "fa", "name": d["meta"]["name"], "season": "auto",
                 "opts": {"burstsPerMin": 30, "particleSpeed": 0.6, "palette": "gold", "city": False, "trail": True}},
                ensure_ascii=False),
     "FireworksGold")

# واریانت opts سفارشی — آسمان شب با ۸۰ ستاره و بدون سحابی (تست clamp/flag)
d = json.load(io.open(f"{ROOT}/download/packages/HR-Screensaver-NightSky-{VER}.json", encoding="utf-8"))
page("NightSky", d,
     json.dumps({"lang": "fa", "name": d["meta"]["name"], "season": "auto",
                 "opts": {"starCount": 80, "shootingEverySec": 3, "moon": False, "nebula": False}},
                ensure_ascii=False),
     "NightSkySparse")
