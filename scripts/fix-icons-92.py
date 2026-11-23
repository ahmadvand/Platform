# -*- coding: utf-8 -*-
# فاز ۹۲ — اصلاح آیکن‌ها (درخواست کاربر: «آیکن‌ها بعضاً درست نمایش داده نمی‌شوند»)
# ۱) گلیف‌های بدون پوشش فونت در ویندوز/فایرفاکس جایگزین می‌شوند:
#    🫆 (U+1FAC6 — یونیکد ۱۶، سگو ویندوز ۱۰ ندارد) → 🧬 (U+1F9EC، یونیکد ۱۱)
#    🪪 (U+1FAAA — یونیکد ۱۳، ویندوز ۱۰ ندارد)   → 🎫 (U+1F39F، یونیکد ۷)
#    ⬑ (U+2B11 نادر)                             → ↳ (U+21B3، BMP امن)
# ۲) ایموجی‌های بدون VS16 حالت رنگی می‌گیرند (پیوستن U+FE0F) تا رندر مونوکروم/تخته‌سفید نشوند؛
#    کلیدهای ترجمه در HrLang.cs همزمان به‌روز می‌شوند تا تطابق کلید حفظ شود.
# ۳) پرچم‌های 🇮🇷/🇬🇧 (هرگز در فایرفاکس/ویندوز رندر نمی‌شوند) با برچسب متنی «فا/EN» عوض می‌شوند.
import os, re, io

SRC = "/home/z/my-project/aspnet/src"
VS = '\uFE0F'

def iter_files():
    for root, dirs, files in os.walk(SRC):
        dirs[:] = [d for d in dirs if d not in ('bin', 'obj', 'node_modules', 'fonts', 'dl', 'langpacks')]
        for f in files:
            if f.endswith(('.cshtml', '.cs')):
                yield os.path.join(root, f)

# (pattern, replacement, description) — regex روی کل فایل‌ها
RULES = [
    ('\U0001FAC6', '\U0001F9EC', 'fingerprint -> DNA'),
    ('\U0001FAAA', '\U0001F39F', 'idcard -> ticket'),
    ('\u2B11', '\u21B3', 'rare arrow -> hooked arrow'),
    ('🖥(?!%s)' % VS, '🖥%s' % VS, 'desktop VS16'),
    ('🅿(?!%s)' % VS, '🅿%s' % VS, 'parking VS16'),
    ('🛡(?!%s)' % VS, '🛡%s' % VS, 'shield VS16'),
    ('🎛(?!%s)' % VS, '🎛%s' % VS, 'knobs VS16'),
    ('⏱(?!%s)' % VS, '⏱%s' % VS, 'stopwatch VS16'),
    ('⚙(?!%s)' % VS, '⚙%s' % VS, 'gear VS16'),
    ('🗑(?!%s)' % VS, '🗑%s' % VS, 'waste VS16'),
]

changed = {}
for p in iter_files():
    try:
        s = open(p, encoding='utf-8').read()
    except Exception:
        continue
    orig = s
    for pat, rep, _ in RULES:
        s = re.sub(pat, rep, s)
    if s != orig:
        open(p, 'w', encoding='utf-8', newline='').write(s)
        changed[p] = sum(1 for a, b in zip(orig.split('\n'), s.split('\n')) if a != b)
print("=== changed files ===")
for p in sorted(changed):
    print(p.replace(SRC, ''), changed[p])

# ---- پرچم‌ها → برچسب متنی ----
def patch(path, pairs):
    p = os.path.join(SRC, path)
    s = open(p, encoding='utf-8').read()
    for a, b in pairs:
        if a not in s:
            print('!! NOT FOUND in', path, '->', a[:40])
        s = s.replace(a, b)
    open(p, 'w', encoding='utf-8', newline='').write(s)
    print('patched', path)

patch('Platform.Web/Views/Setup/Index.cshtml', [
    ('<span class="lang-flag">🇮🇷</span>', '<span class="lang-flag">فا</span>'),
    ('<span class="lang-flag">🇬🇧</span>', '<span class="lang-flag">EN</span>'),
])
patch('Platform.Web/Views/Shared/_Layout.cshtml', [
    ('@(L.Code == "fa" ? "🇮🇷" : L.Code == "en" ? "🇬🇧" : "🌐")', '@(L.Code == "fa" ? "فا" : L.Code == "en" ? "EN" : "🌐")'),
])
patch('Platform.Web/Views/Auth/Login.cshtml', [
    ('🇮🇷 فارسی', 'فا — فارسی'),
    ('🇬🇧 English', 'EN — English'),
])

# ---- CSS: فونت پشتیبان ایموجی + font-variant-emoji ----
css_path = os.path.join(SRC, 'Platform.Web/wwwroot/css/site.css')
css = open(css_path, encoding='utf-8').read()
css = css.replace('Vazirmatn, Tahoma, sans-serif',
                  'Vazirmatn, Tahoma, "Segoe UI Emoji", "Noto Color Emoji", "Apple Color Emoji", sans-serif')
anchor = '@font-face { font-family: Vazirmatn; src: url(../fonts/Vazirmatn-Bold.woff2) format(woff2); font-weight: 700; font-display: swap; }'
if anchor in css and 'font-variant-emoji' not in css:
    css = css.replace(anchor, anchor + '\n'
        '/* فاز ۹۲ — ایموجی‌های رابط (آیکن‌های منو/دکمه‌ها) همیشه با فونت ایموجی رندر شوند؛\n'
        '   بدون این، فایرفاکس/ویندوز برخی نمادها را تخته‌سفید یا مونوکروم نشان می‌داد.\n'
        '   پشتیبان‌ها: ویندوز=Segoe UI Emoji، لینوکس=Noto Color Emoji، مک=Apple Color Emoji */\n'
        'html { font-variant-emoji: emoji; }')
open(css_path, 'w', encoding='utf-8', newline='').write(css)
print('site.css patched:', css.count('Segoe UI Emoji'), 'stacks + font-variant-emoji')

ltr = os.path.join(SRC, 'Platform.Web/wwwroot/css/site-ltr.css')
if os.path.exists(ltr):
    s = open(ltr, encoding='utf-8').read()
    s2 = s.replace('Vazirmatn, Tahoma, sans-serif',
                   'Vazirmatn, Tahoma, "Segoe UI Emoji", "Noto Color Emoji", "Apple Color Emoji", sans-serif')
    if s2 != s:
        open(ltr, 'w', encoding='utf-8', newline='').write(s2)
        print('site-ltr.css patched')

# ---- favicon.ico (سکوت‌کنندهٔ 404 کنسول فایرفاکس) ----
def make_ico():
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        return False
    imgs = []
    for size in (16, 32):
        im = Image.new('RGBA', (size, size), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        # زمینهٔ گرد بنفش برند (◈)
        d.rounded_rectangle([0, 0, size - 1, size - 1], radius=max(3, size // 4), fill=(88, 80, 236, 255))
        # لوزی ◈ وسط
        cx = cy = size / 2
        r = size * 0.32
        d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=(255, 255, 255, 255))
        d.polygon([(cx, cy - r * 0.5), (cx + r * 0.5, cy), (cx, cy + r * 0.5), (cx - r * 0.5, cy)], fill=(88, 80, 236, 255))
        imgs.append(im)
    out = os.path.join(SRC, 'Platform.Web/wwwroot/favicon.ico')
    imgs[0].save(out, format='ICO', sizes=[(16, 16), (32, 32)], append_images=[imgs[1]])
    print('favicon.ico written:', os.path.getsize(out), 'bytes')
    return True

if not make_ico():
    print('PIL not available — favicon skipped')
