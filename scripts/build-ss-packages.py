#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۱۰۷ — ساخت بسته‌های خلاقانهٔ محافظ صفحه با فونت وزیر (دستور کاربر: «چند تا بستهٔ
محافظ صفحهٔ دیگه با خلاقیت خودت بساز — فونت همیشه وزیر باشه»).
چهار خروجی در download/packages/:
  • HR-Screensaver-NightSky-2.7.18.json   آسمان شب — ستاره/شهاب/ماه/سحابی/ماهواره
  • HR-Screensaver-PoetryRain-2.7.18.json باران شعر — ابیات کلاسیک (مالکیت عمومی)
  • HR-Screensaver-Aquarium-2.7.18.json   آکواریوم — ماهی/حباب/جلبک/عروس دریایی
  • HR-Screensaver-FourSeasons-2.7.18.json بازسازی چهارفصل با فونت وزیر
همهٔ بسته‌ها: فونت وزیرمتن زیرمجموعه‌شده به‌صورت data URI در CSS (Runtime فقط font-src data:
اجازه دارد)، ضدتکرار با بازتولید تصادفی ذرات، بدون هیچ درخواست شبکه‌ای (CSP connect-src 'none')."""
import json, io, os, base64, sys

ROOT = "/home/z/my-project"
ASSETS = f"{ROOT}/scripts/ss-season-assets"
SSA = f"{ROOT}/scripts/ss-assets"
OUTDIR = f"{ROOT}/download/packages"
VER = "2.7.19"
os.makedirs(OUTDIR, exist_ok=True)

SEASONS = ["spring", "summer", "autumn", "winter"]

# ---------- فونت وزیر (embed در CSS هر بسته) ----------
FONT_CSS = io.open(f"{SSA}/vazir-embed.css", encoding="utf-8").read()
assert len(FONT_CSS) < 95_000

# ---------- عکس‌ها و اعتبارهای چهارفصل ----------
from PIL import Image
attrib = {}
_ap = f"{ASSETS}/attributions.json"
if os.path.exists(_ap):
    attrib = json.load(io.open(_ap, encoding="utf-8"))

PER_PHOTO_CAP = 45_000   # سقف نویسه‌های base64 هر عکس

def encode_photo(season):
    src = os.path.join(ASSETS, f"{season}.jpg")
    im0 = Image.open(src)
    best = None
    for (tw, th) in ((880, 495), (780, 439), (700, 394), (640, 360), (600, 338)):
        w, h = im0.size
        scale = max(tw / w, th / h)
        im = im0.resize((max(tw, int(w * scale)), max(th, int(h * scale))), Image.LANCZOS)
        W, H = im.size
        im = im.crop(((W - tw) // 2, (H - th) // 2, (W - tw) // 2 + tw, (H - th) // 2 + th))
        for q in (46, 42, 38, 34, 30, 26, 22):
            buf = io.BytesIO()
            im.save(buf, "JPEG", quality=q, optimize=True, progressive=True)
            b64 = base64.b64encode(buf.getvalue()).decode("ascii")
            if len(b64) <= PER_PHOTO_CAP:
                return "data:image/jpeg;base64," + b64, len(b64)
            best = ("data:image/jpeg;base64," + b64, len(b64))
    return best

def esc(s):
    """نویسه‌های امن برای رشتهٔ JS تک‌کوتیشنی + فشرده‌سازی فاصله‌ها"""
    s = (s or "").replace("\\", "").replace("'", "").replace('"', "").strip()
    return " ".join(s.split())

def shell_badge_js():
    """پوستهٔ مشترک: خواندن data، بوم، برچسب — همهٔ متن‌ها با فونت وزیر (CSS بسته)"""
    return r"""
  var el = document.getElementById('hr-ss-data');
  var DATA = {};
  try { DATA = JSON.parse(el.textContent || '{}'); } catch (e) { }
  var LANG = (DATA.lang || 'fa').toString().toLowerCase();
  var RTL = (LANG === 'fa' || LANG === 'prs' || LANG === 'ps' || LANG === 'ar');
  // فاز ۱۰۸ — تنظیمات اختصاصی بسته (opts از Runtime می‌آید؛ ادمین در تب محافظ صفحه ویرایش می‌کند)
  var OPTS = (DATA.opts && typeof DATA.opts === 'object') ? DATA.opts : {};
  function num(k, d, lo, hi) { var v = Number(OPTS[k]); if (!isFinite(v)) return d; v = Math.round(v); return Math.max(lo, Math.min(hi, v)); }
  function flag(k, d) { return typeof OPTS[k] === 'boolean' ? OPTS[k] : d; }
  function one(k, list, d) { var v = String(OPTS[k] || '').toLowerCase(); return list.indexOf(v) >= 0 ? v : d; }
  var cv = document.createElement('canvas');
  cv.className = 'ss-cv';
  document.body.appendChild(cv);
  var ctx = cv.getContext('2d');
  function resize() { cv.width = window.innerWidth; cv.height = window.innerHeight; }
  window.addEventListener('resize', resize);
  resize();
  function rnd(a, b) { return a + Math.random() * (b - a); }
  function pick(arr) { return arr[Math.floor(Math.random() * arr.length)]; }
  function T(fa, en, ru, ar) {
    if (LANG === 'en' || LANG === 'tk' || LANG === 'uz') return en;
    if (LANG === 'ru' || LANG === 'ky' || LANG === 'tg') return ru;
    if (LANG === 'ar') return ar;
    return fa;
  }
"""

def badge_css(right = 'right: 18px'):
    return r"""
html, body { margin: 0; height: 100%; overflow: hidden; background: #050a14; }
.ss-cv { position: fixed; inset: 0; width: 100%; height: 100%; display: block; }
.ss-badge {
  position: fixed; bottom: 14px; """ + right + """;
  color: rgba(255,255,255,0.85); font-family: Vazirmatn, Tahoma, sans-serif;
  font-size: 15px; font-weight: 700; letter-spacing: 0.3px;
  text-shadow: 0 1px 4px rgba(0,0,0,0.8); pointer-events: none;
}
"""

def compose(name, author, desc, js, css, settings=None, opts=None):
    desc = desc[:497] + "…" if len(desc) > 500 else desc
    css = FONT_CSS + css   # فونت وزیر در CSS هر بسته (دستور کاربر: «فونت همیشه وزیر باشه»)
    pkg = {
        "format": "hr-screensaver-package",
        "spec": 1,
        "exportedAt": __import__("datetime").datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "meta": {"name": name, "author": author, "version": VER, "description": desc},
        "settings": settings or {"enabled": True, "minutes": 1, "seconds": 15, "requirePassword": True, "season": "auto"},
        "options": opts if opts is not None else {},   # فاز ۱۰۸ — پیش‌فرض‌های قابل‌ویرایش ادمین
        "code": {"js": js, "css": css},
    }
    # پالایش دنباله‌های ممنوع (همان قاعدهٔ سرور)
    for bad in ("</script", "</style", "<!--"):
        if bad.lower() in js.lower() or bad.lower() in css.lower():
            print("!! forbidden sequence:", bad, "in", name); sys.exit(1)
    if len(js) > 200_000: print("!! js too long:", name, len(js)); sys.exit(1)
    if len(css) > 100_000: print("!! css too long:", name, len(css)); sys.exit(1)
    return pkg

def save_pkg(pkg, fname):
    out = os.path.join(OUTDIR, fname)
    with io.open(out, "w", encoding="utf-8") as f:
        json.dump(pkg, f, ensure_ascii=False)
    size = os.path.getsize(out)
    print(f"OK -> {fname} ({size} B) | js={len(pkg['code']['js'])} css={len(pkg['code']['css'])} | desc={len(pkg['meta']['description'])}")
    if size > 1024 * 1024: print("!! file > 1MB"); sys.exit(1)
    return out

# =====================================================================
# بستهٔ ۱ — آسمان شب (NightSky)
# =====================================================================
NIGHTSKY_JS = r"""
(function () {
  'use strict';
  """ + shell_badge_js() + r"""
  // ---------- آسمان شب: ستاره‌ها/شهاب/ماه/سحابی/ماهواره — ضدتکرار ----------
  var stars = [], shots = [], nebs = [], sat = null, moonA = 0;
  function newStar(first) {
    return { x: rnd(0, cv.width), y: first ? rnd(0, cv.height) : rnd(0, cv.height * 0.96),
             r: rnd(0.4, 1.7), ph: rnd(0, 6.283), sp: rnd(0.0006, 0.0025),
             a: rnd(0.25, 1), hue: pick(['255,255,255', '214,226,255', '255,241,214', '255,224,224']) };
  }
  function newShot(t) {
    return { x: rnd(cv.width * 0.15, cv.width * 0.9), y: rnd(0, cv.height * 0.45),
             ang: rnd(0.45, 1.15) * (Math.random() < 0.5 ? 1 : -1),
             sp: rnd(9, 16), len: rnd(90, 220), born: t, life: rnd(700, 1300) };
  }
  function newNeb(first) {
    return { x: rnd(0, cv.width), y: rnd(0, cv.height * 0.8), r: rnd(cv.width * 0.12, cv.width * 0.3),
             c: pick(['99,102,241', '139,92,246', '20,184,166', '236,72,153']),
             vx: rnd(-0.06, 0.06), vy: rnd(-0.03, 0.03), a: rnd(0.05, 0.11) };
  }
  function init() { stars = []; nebs = []; shots = []; sat = null;
    var NS = num('starCount', 300, 80, 600);   // opts — تعداد ستاره‌ها
    for (var i = 0; i < NS; i++) stars.push(newStar(true));
    for (var k = 0; k < 3; k++) nebs.push(newNeb(true));
  }
  init();
  var SHOT_P = 1 / (60 * num('shootingEverySec', 8, 3, 30));   // opts — هر چند ثانیه یک شهاب

  function drawSky() {
    var g = ctx.createLinearGradient(0, 0, 0, cv.height);
    g.addColorStop(0, '#020617'); g.addColorStop(0.55, '#0b1026'); g.addColorStop(1, '#101a3a');
    ctx.fillStyle = g; ctx.fillRect(0, 0, cv.width, cv.height);
  }
  function drawNeb(t) {
    if (!flag('nebula', true)) return;   // opts — سحابی خاموش/روشن
    ctx.save(); ctx.globalCompositeOperation = 'lighter';
    for (var i = 0; i < nebs.length; i++) {
      var n = nebs[i];
      n.x += n.vx; n.y += n.vy;
      if (n.x < -n.r || n.x > cv.width + n.r || n.y < -n.r || n.y > cv.height + n.r) nebs[i] = newNeb(false);
      var g = ctx.createRadialGradient(n.x, n.y, 0, n.x, n.y, n.r);
      g.addColorStop(0, 'rgba(' + n.c + ',' + n.a + ')');
      g.addColorStop(1, 'rgba(' + n.c + ',0)');
      ctx.fillStyle = g;
      ctx.beginPath(); ctx.arc(n.x, n.y, n.r, 0, 6.283); ctx.fill();
    }
    ctx.restore();
  }
  function drawStars(t) {
    for (var i = 0; i < stars.length; i++) {
      var s = stars[i];
      var tw = 0.55 + 0.45 * Math.sin(t * s.sp + s.ph);
      ctx.fillStyle = 'rgba(' + s.hue + ',' + (s.a * tw).toFixed(3) + ')';
      ctx.beginPath(); ctx.arc(s.x, s.y, s.r, 0, 6.283); ctx.fill();
      if (s.r > 1.3) {   // هالهٔ ستاره‌های درشت
        ctx.strokeStyle = 'rgba(' + s.hue + ',' + (s.a * tw * 0.25).toFixed(3) + ')';
        ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(s.x - s.r * 3, s.y); ctx.lineTo(s.x + s.r * 3, s.y);
        ctx.moveTo(s.x, s.y - s.r * 3); ctx.lineTo(s.x, s.y + s.r * 3); ctx.stroke();
      }
    }
  }
  function drawShots(t) {
    if (!shots.length && Math.random() < SHOT_P) shots.push(newShot(t));
    for (var i = shots.length - 1; i >= 0; i--) {
      var s = shots[i];
      var age = t - s.born;
      if (age > s.life) { shots.splice(i, 1); continue; }
      var prog = age / s.life;
      var dx = Math.cos(s.ang) * s.sp * age, dy = Math.sin(s.ang) * s.sp * age;
      var x = s.x + dx, y = s.y + dy;
      var fade = Math.sin(prog * 3.14159);
      var tx = x - Math.cos(s.ang) * s.len, ty = y - Math.sin(s.ang) * s.len;
      var g = ctx.createLinearGradient(x, y, tx, ty);
      g.addColorStop(0, 'rgba(255,255,255,' + (0.95 * fade).toFixed(3) + ')');
      g.addColorStop(1, 'rgba(165,180,252,0)');
      ctx.strokeStyle = g; ctx.lineWidth = 2.2; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(tx, ty); ctx.stroke();
      ctx.fillStyle = 'rgba(255,255,255,' + fade.toFixed(3) + ')';
      ctx.beginPath(); ctx.arc(x, y, 2.1, 0, 6.283); ctx.fill();
    }
  }
  function drawMoon(t) {
    if (!flag('moon', true)) return;   // opts — ماه خاموش/روشن
    var mx = cv.width * 0.82 + Math.sin(t / 60000) * cv.width * 0.01;
    var my = cv.height * 0.16;
    var mr = Math.min(cv.width, cv.height) * 0.055;
    var glow = ctx.createRadialGradient(mx, my, 0, mx, my, mr * 3.2);
    glow.addColorStop(0, 'rgba(254,249,195,0.30)');
    glow.addColorStop(1, 'rgba(254,249,195,0)');
    ctx.fillStyle = glow;
    ctx.beginPath(); ctx.arc(mx, my, mr * 3.2, 0, 6.283); ctx.fill();
    ctx.fillStyle = '#fefce8';
    ctx.beginPath(); ctx.arc(mx, my, mr, 0, 6.283); ctx.fill();
    ctx.fillStyle = 'rgba(11,16,38,0.92)';   // هلال — دایرهٔ تیرهٔ روی‌سو
    ctx.beginPath(); ctx.arc(mx - mr * 0.42, my - mr * 0.18, mr * 0.86, 0, 6.283); ctx.fill();
  }
  function drawSat(t) {
    if (!sat && Math.random() < 0.004) {
      sat = { x: rnd(0, cv.width * 0.4), y: rnd(0, cv.height * 0.5),
              vx: rnd(0.5, 1.0), vy: rnd(-0.25, 0.45), blink: rnd(0.004, 0.010) };
    }
    if (!sat) return;
    sat.x += sat.vx; sat.y += sat.vy;
    if (sat.x > cv.width + 30 || sat.y < -30 || sat.y > cv.height + 30) { sat = null; return; }
    var b = 0.5 + 0.5 * Math.sin(t * sat.blink);
    ctx.fillStyle = 'rgba(226,232,240,' + (0.5 + 0.4 * b).toFixed(3) + ')';
    ctx.beginPath(); ctx.arc(sat.x, sat.y, 1.6, 0, 6.283); ctx.fill();
  }
  function frame() {
    var t = Date.now();
    drawSky();
    drawNeb(t);
    drawStars(t);
    drawMoon(t);
    drawShots(t);
    drawSat(t);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
  var badge = document.createElement('div');
  badge.className = 'ss-badge';
  badge.textContent = T('آسمان شب', 'Night Sky', 'Ночное небо', 'سماء الليل');
  document.body.appendChild(badge);
})();
"""

NIGHTSKY_CSS = badge_css() + r"""
  .ss-badge { opacity: .8; }
"""

save_pkg(compose(
    "محافظ صفحهٔ آسمان شب", "HR Platform",
    "بستهٔ محافظ صفحهٔ «آسمان شب»: ستاره‌های چشمک‌زن با رنگ و تپش تصادفی، شهاب با زاویه و طول تصادفی، ماه هلالی با هالهٔ گرم، سحابی‌های رنگی غلتان و ماهوارهٔ چشمک‌زن عبوری. ضدتکرار: هر بازتولید ذره پارامترهای تازه می‌گیرد و مسیر شهاب‌ها هرگز تکراری نیست. تنظیمات کاربر (opts): starCount=تعداد ستاره (۸۰–۶۰۰)، shootingEverySec=فاصلهٔ شهاب ثانیه (۳–۳۰)، moon (true/false)، nebula (true/false). کاملاً رویه‌ای روی بوم — بدون هیچ عکس یا درخواست شبکه‌ای. همهٔ متن‌ها با فونت وزیر (embed در بسته).",
    NIGHTSKY_JS, NIGHTSKY_CSS,
    opts={"starCount": 300, "shootingEverySec": 8, "moon": True, "nebula": True}),
    f"HR-Screensaver-NightSky-{VER}.json")

# =====================================================================
# بستهٔ ۲ — باران شعر (PoetryRain) — اشعار کلاسیک مالکیت عمومی
# =====================================================================
POETRY_JS = r"""
(function () {
  'use strict';
  """ + shell_badge_js() + r"""
  // ---------- باران شعر: ابیات کلاسیک + واژه‌باران — ضدتکرار (کیسهٔ تصادفی) ----------
  var VERSES = [
    { a: 'حافظ', l1: 'الا یا ایها الساقی ادر کاساً و ناولها', l2: 'که عشق آسان نمود اول ولی افتاد مشکل‌ها' },
    { a: 'حافظ', l1: 'یوسف گمگشته بازآید به کنعان غم مخور', l2: 'کلبهٔ احزان شود روزی گلستان غم مخور' },
    { a: 'حافظ', l1: 'رسید مژده که ایام غم نخواهد ماند', l2: 'چنان نماند چنین نیز هم نخواهد ماند' },
    { a: 'حافظ', l1: 'دوش دیدم که ملایک در میخانه زدند', l2: 'گل آدم بستانند و به پیمانه کشتند' },
    { a: 'سعدی', l1: 'بنی‌آدم اعضای یک پیکرند', l2: 'که در آفرینش ز یک گوهرند' },
    { a: 'سعدی', l1: 'درخت دوستی بنشان که کام دل به بار آرد', l2: 'نهال دشمنی برکن که رنج بی‌شمار آرد' },
    { a: 'مولانا', l1: 'بشنو این نی چون شکایت می‌کند', l2: 'از جدایی‌ها حکایت می‌کند' },
    { a: 'مولانا', l1: 'از محبت خارها گل می‌شود', l2: 'وز محبت سرکه‌ها مُل می‌شود' },
    { a: 'فردوسی', l1: 'توانا بود هر که دانا بود', l2: 'ز دانش دل پیر برنا بود' },
    { a: 'خیام', l1: 'اسرار ازل را نه تو دانی و نه من', l2: 'وین حرف معما نه تو خوانی و نه من' },
    { a: 'خیام', l1: 'این کوزه چو من عاشق زاری بوده است', l2: 'در بند سر زلف نگاری بوده است' },
    { a: 'نظامی', l1: 'ای نام تو بهترین سرآغاز', l2: 'بی‌نام تو نامه کی کنم باز' },
  ];
  var bag = [];
  function nextVerse() {   // کیسهٔ تصادفی — هیچ بیت تا تمام‌شدن دوباره نمی‌آید (ضدتکرار)
    if (!bag.length) {
      bag = VERSES.map(function (v, i) { return i; });
      for (var i = bag.length - 1; i > 0; i--) { var j = Math.floor(Math.random() * (i + 1)); var t = bag[i]; bag[i] = bag[j]; bag[j] = t; }
    }
    return VERSES[bag.pop()];
  }
  var ALLWORDS = [];
  VERSES.forEach(function (v) { ALLWORDS = ALLWORDS.concat((v.l1 + ' ' + v.l2).split(' ')); });
  ALLWORDS = ALLWORDS.filter(function (w) { return w.length > 1; });

  var box = document.createElement('div');
  box.className = 'ss-poem';
  var l1 = document.createElement('div'); l1.className = 'ss-l1';
  var l2 = document.createElement('div'); l2.className = 'ss-l2';
  var au = document.createElement('div'); au.className = 'ss-au';
  box.appendChild(l1); box.appendChild(l2); box.appendChild(au);
  document.body.appendChild(box);
  var HOLD = num('holdSec', 9, 4, 20) * 1000;   // opts — مدت ماندن هر بیت
  function showVerse() {
    var v = nextVerse();
    l1.textContent = v.l1;
    l2.textContent = v.l2;
    au.textContent = '— ' + v.a;
    box.classList.remove('show'); void box.offsetWidth; box.classList.add('show');
    setTimeout(showVerse, HOLD + Math.random() * HOLD * 0.3);
  }
  setTimeout(showVerse, 600);

  var words = [];
  function newWord(first) {
    return { x: rnd(0, cv.width), y: first ? rnd(-cv.height, cv.height) : rnd(-140, -30),
             w: pick(ALLWORDS), s: rnd(12, 17), vy: rnd(0.35, 1.05),
             amp: rnd(10, 34), ph: rnd(0, 6.283), fr: rnd(0.0015, 0.004),
             a: rnd(-0.4, 0.4), va: rnd(-0.006, 0.006), alpha: rnd(0.18, 0.6),
             gold: flag('goldWords', true) && Math.random() < 0.22 };
  }
  var WC = num('wordCount', 42, 0, 120);   // opts — تعداد واژه‌های باران
  for (var i = 0; i < WC; i++) words.push(newWord(true));
  function drawWords(t) {
    ctx.textAlign = 'center';
    for (var i = 0; i < words.length; i++) {
      var p = words[i];
      p.ph += p.fr; p.a += p.va; p.y += p.vy;
      p.x += Math.sin(p.ph) * 0.5;
      if (p.y > cv.height + 30) words[i] = newWord(false);
      ctx.save();
      ctx.globalAlpha = p.alpha;
      ctx.translate(p.x + Math.sin(p.ph) * p.amp * 0.3, p.y);
      ctx.rotate(p.a);
      ctx.font = '600 ' + p.s.toFixed(1) + 'px Vazirmatn, Tahoma';
      ctx.fillStyle = p.gold ? 'rgba(253,230,138,0.9)' : 'rgba(203,213,225,0.9)';
      ctx.fillText(p.w, 0, 0);
      ctx.restore();
    }
  }
  function drawBg() {
    var g = ctx.createRadialGradient(cv.width / 2, cv.height * 0.42, cv.height * 0.1, cv.width / 2, cv.height / 2, Math.max(cv.width, cv.height) * 0.75);
    g.addColorStop(0, '#141b3d'); g.addColorStop(0.6, '#0c1230'); g.addColorStop(1, '#050816');
    ctx.fillStyle = g; ctx.fillRect(0, 0, cv.width, cv.height);
  }
  function drawMotes(t) {
    for (var i = 0; i < 24; i++) {
      var x = ((i * 137.5 + t * 0.008) % cv.width);
      var y = ((i * 91.3 + t * 0.012) % cv.height);
      var a = 0.05 + 0.05 * Math.sin(t / 900 + i);
      ctx.fillStyle = 'rgba(253,230,138,' + a.toFixed(3) + ')';
      ctx.beginPath(); ctx.arc(x, y, 1.2, 0, 6.283); ctx.fill();
    }
  }
  function frame() {
    var t = Date.now();
    drawBg();
    drawMotes(t);
    drawWords(t);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
  var badge = document.createElement('div');
  badge.className = 'ss-badge';
  badge.textContent = T('باران شعر', 'Poetry Rain', 'Поэтический дождь', 'مطر الشعر');
  document.body.appendChild(badge);
})();
"""

POETRY_CSS = badge_css() + r"""
  .ss-poem {
    position: fixed; top: 38%; left: 50%; transform: translate(-50%,-50%);
    width: min(86vw, 900px); text-align: center; pointer-events: none;
    opacity: 0; transition: opacity 1.6s ease;
    text-shadow: 0 2px 14px rgba(0,0,0,0.75);
  }
  .ss-poem.show { opacity: 1; }
  .ss-l1, .ss-l2 {
    font-family: Vazirmatn, Tahoma, sans-serif; font-weight: 700;
    font-size: clamp(24px, 4.6vw, 42px); line-height: 1.9; color: #f8fafc;
  }
  .ss-l2 { color: #e2e8f0; }
  .ss-au { margin-top: 14px; font-family: Vazirmatn, Tahoma, sans-serif;
    font-size: clamp(15px, 2vw, 21px); color: #fbbf24; font-weight: 700; }
  .ss-badge { opacity: .8; }
"""

save_pkg(compose(
    "محافظ صفحهٔ باران شعر", "HR Platform — اشعار: حافظ، سعدی، مولانا، فردوسی، خیام، نظامی",
    "بستهٔ محافظ صفحهٔ «باران شعر»: ابیات ماندگار شعرای کلاسیک فارسی (حافظ، سعدی، مولانا، فردوسی، خیام، نظامی — مالکیت عمومی) با فونت وزیر بزرگ و طلایی در مرکز صفحه آرام ظاهر و محو می‌شود؛ واژه‌های ابیات مثل باران با تاب و چرخش می‌ریزند و غبار طلایی معلق است. ضدتکرار: ابیات در «کیسهٔ تصادفی» می‌چرخند تا هیچ بیتی دوباره تکرار نشود. تنظیمات کاربر (opts): holdSec=مدت نمایش هر بیت ثانیه (۴–۲۰)، wordCount=تعداد واژه‌های باران (۰–۱۲۰)، goldWords (true/false). بدون هیچ درخواست شبکه‌ای — فونت وزیر داخل بسته embed شده است.",
    POETRY_JS, POETRY_CSS,
    opts={"holdSec": 9, "wordCount": 42, "goldWords": True}),
    f"HR-Screensaver-PoetryRain-{VER}.json")

# =====================================================================
# بستهٔ ۳ — آکواریوم (Aquarium)
# =====================================================================
AQUARIUM_JS = r"""
(function () {
  'use strict';
  """ + shell_badge_js() + r"""
  // ---------- آکواریوم: ماهی/حباب/جلبک/عروس دریایی/پرتوهای نور — ضدتکرار ----------
  var PALS = [
    ['#f97316', '#fb923c', '#ffedd5'], ['#38bdf8', '#7dd3fc', '#e0f2fe'],
    ['#f43f5e', '#fb7185', '#ffe4e6'], ['#a3e635', '#bef264', '#ecfccb'],
    ['#c084fc', '#d8b4fe', '#f3e8ff'], ['#fbbf24', '#fcd34d', '#fef9c3'],
  ];
  var fishes = [], bubbles = [], weeds = [], rocks = [], jellies = [], nextJelly = 0;
  function newFish(first) {
    var pal = pick(PALS);
    var dir = Math.random() < 0.5 ? 1 : -1;
    return { x: first ? rnd(0, cv.width) : (dir > 0 ? -60 : cv.width + 60),
             y: rnd(cv.height * 0.12, cv.height * 0.82),
             s: rnd(16, 40), dir: dir, sp: rnd(0.5, 1.5) * dir,
             wob: rnd(0.004, 0.011), ph: rnd(0, 6.283), amp: rnd(8, 26),
             flap: rnd(0.10, 0.22), body: pal[0], tail: pal[1], fin: pal[2] };
  }
  function newBubble(first) {
    return { x: rnd(0, cv.width), y: first ? rnd(0, cv.height) : cv.height + rnd(0, 40),
             r: rnd(2, 6.5), vy: rnd(0.5, 1.4), ph: rnd(0, 6.283), fr: rnd(0.004, 0.012), a: rnd(0.25, 0.6) };
  }
  function newWeed(i) {
    return { x: cv.width * (0.06 + 0.9 * i / 5) + rnd(-20, 20), h: rnd(cv.height * 0.14, cv.height * 0.30),
             w: rnd(5, 11), ph: rnd(0, 6.283), fr: rnd(0.0008, 0.002),
             c: pick(['#166534', '#15803d', '#047857', '#3f6212']) };
  }
  function init() {
    fishes = []; bubbles = []; weeds = []; rocks = []; jellies = []; nextJelly = Date.now() + 8000;
    var NF = num('fishCount', 9, 1, 30);   // opts — تعداد ماهی‌ها
    for (var i = 0; i < NF; i++) fishes.push(newFish(true));
    if (flag('bubbles', true))   // opts — حباب خاموش/روشن
        for (var j = 0; j < 30; j++) bubbles.push(newBubble(true));
    for (var k = 0; k < 6; k++) weeds.push(newWeed(k));
    for (var m = 0; m < 5; m++) rocks.push({ x: rnd(0, cv.width), r: rnd(10, 30), c: pick(['#334155', '#3f3f46', '#475569']) });
  }
  init();
  function drawWater() {
    var g = ctx.createLinearGradient(0, 0, 0, cv.height);
    g.addColorStop(0, '#0e7490'); g.addColorStop(0.45, '#155e75'); g.addColorStop(1, '#083344');
    ctx.fillStyle = g; ctx.fillRect(0, 0, cv.width, cv.height);
  }
  function drawRays(t) {
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    for (var i = 0; i < 4; i++) {
      var bx = cv.width * (0.15 + 0.22 * i) + Math.sin(t / 5200 + i * 2) * 40;
      var top = cv.height * 0.1, bw = cv.width * 0.05;
      var g = ctx.createLinearGradient(bx, 0, bx + cv.width * 0.12, top + cv.height * 0.75);
      g.addColorStop(0, 'rgba(186,230,253,0.11)');
      g.addColorStop(1, 'rgba(186,230,253,0)');
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.moveTo(bx - bw / 2, 0); ctx.lineTo(bx + bw / 2, 0);
      ctx.lineTo(bx + cv.width * 0.14, cv.height); ctx.lineTo(bx + cv.width * 0.02, cv.height);
      ctx.closePath(); ctx.fill();
    }
    ctx.restore();
  }
  function drawSand() {
    var g = ctx.createLinearGradient(0, cv.height - 46, 0, cv.height);
    g.addColorStop(0, 'rgba(253,230,138,0)'); g.addColorStop(1, 'rgba(253,230,138,0.30)');
    ctx.fillStyle = g; ctx.fillRect(0, cv.height - 46, cv.width, 46);
    for (var i = 0; i < rocks.length; i++) {
      var r = rocks[i];
      ctx.fillStyle = r.c;
      ctx.beginPath(); ctx.ellipse(r.x, cv.height - 8, r.r, r.r * 0.5, 0, 3.14, 6.283); ctx.fill();
    }
  }
  function drawWeeds(t) {
    for (var i = 0; i < weeds.length; i++) {
      var w = weeds[i];
      var sway = Math.sin(t * w.fr + w.ph) * 18;
      ctx.strokeStyle = w.c; ctx.lineWidth = w.w; ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(w.x, cv.height - 4);
      ctx.quadraticCurveTo(w.x + sway * 0.5, cv.height - w.h * 0.55, w.x + sway, cv.height - w.h);
      ctx.stroke();
      ctx.lineWidth = w.w * 0.55;
      ctx.beginPath();
      ctx.moveTo(w.x + 8, cv.height - 4);
      ctx.quadraticCurveTo(w.x + 8 - sway * 0.4, cv.height - w.h * 0.4, w.x + 8 - sway * 0.7, cv.height - w.h * 0.7);
      ctx.stroke();
    }
  }
  function drawFish(t) {
    for (var i = 0; i < fishes.length; i++) {
      var f = fishes[i];
      f.ph += f.wob; f.x += f.sp;
      var y = f.y + Math.sin(f.ph) * f.amp;
      if ((f.dir > 0 && f.x > cv.width + 70) || (f.dir < 0 && f.x < -70)) { fishes[i] = newFish(false); continue; }
      var flap = Math.sin(t * f.flap * 16) * 0.6;
      ctx.save();
      ctx.translate(f.x, y);
      ctx.scale(f.dir, 1);
      ctx.fillStyle = f.tail;   // دم — مثلث بال‌زن
      ctx.beginPath();
      ctx.moveTo(-f.s * 0.45, 0);
      ctx.lineTo(-f.s * 0.95, -f.s * 0.34 * (1 + flap * 0.5));
      ctx.lineTo(-f.s * 0.95, f.s * 0.34 * (1 - flap * 0.5));
      ctx.closePath(); ctx.fill();
      ctx.fillStyle = f.body;   // بدن
      ctx.beginPath(); ctx.ellipse(0, 0, f.s * 0.55, f.s * 0.3, 0, 0, 6.283); ctx.fill();
      ctx.fillStyle = f.fin;    // بالهٔ پشتی
      ctx.beginPath();
      ctx.moveTo(-f.s * 0.1, -f.s * 0.26);
      ctx.quadraticCurveTo(f.s * 0.12, -f.s * 0.55, f.s * 0.3, -f.s * 0.2);
      ctx.closePath(); ctx.fill();
      ctx.fillStyle = 'rgba(255,255,255,0.95)';   // چشم
      ctx.beginPath(); ctx.arc(f.s * 0.36, -f.s * 0.07, f.s * 0.09, 0, 6.283); ctx.fill();
      ctx.fillStyle = '#0f172a';
      ctx.beginPath(); ctx.arc(f.s * 0.39, -f.s * 0.07, f.s * 0.045, 0, 6.283); ctx.fill();
      ctx.restore();
    }
  }
  function drawBubbles(t) {
    if (!flag('bubbles', true)) return;   // opts — حباب خاموش/روشن
    for (var i = 0; i < bubbles.length; i++) {
      var b = bubbles[i];
      b.ph += b.fr; b.y -= b.vy;
      if (b.y < -12) bubbles[i] = newBubble(false);
      ctx.strokeStyle = 'rgba(224,242,254,' + b.a.toFixed(3) + ')';
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      ctx.arc(b.x + Math.sin(b.ph) * 7, b.y, b.r, 0, 6.283);
      ctx.stroke();
    }
  }
  function drawJelly(t) {
    if (!flag('jellyfish', true)) return;   // opts — عروس دریایی خاموش/روشن
    if (!jellies.length && t > nextJelly) {
      jellies.push({ x: rnd(cv.width * 0.2, cv.width * 0.8), y: cv.height + 90,
                     vx: rnd(-0.25, 0.25), s: rnd(26, 44), ph: rnd(0, 6.283) });
      nextJelly = t + 18000 + Math.random() * 22000;
    }
    for (var i = jellies.length - 1; i >= 0; i--) {
      var j = jellies[i];
      j.ph += 0.02;
      j.y -= 0.45 + Math.sin(j.ph) * 0.3;
      j.x += j.vx + Math.sin(j.ph * 0.7) * 0.3;
      if (j.y < -100) { jellies.splice(i, 1); continue; }
      var pulse = 1 + Math.sin(j.ph) * 0.12;
      ctx.save();
      ctx.translate(j.x, j.y);
      ctx.globalAlpha = 0.4;
      ctx.fillStyle = 'rgba(216,180,254,0.85)';
      ctx.beginPath();
      ctx.ellipse(0, 0, j.s * pulse, j.s * 0.7 * pulse, 0, 3.14, 6.283);
      ctx.fill();
      ctx.strokeStyle = 'rgba(216,180,254,0.75)'; ctx.lineWidth = 2; ctx.lineCap = 'round';
      for (var k = 0; k < 6; k++) {
        var tx = (k - 2.5) * j.s * 0.22;
        var sw = Math.sin(j.ph * 1.4 + k) * 10;
        ctx.beginPath();
        ctx.moveTo(tx, 0);
        ctx.quadraticCurveTo(tx + sw, j.s * 0.6, tx + sw * 1.6, j.s * 1.15);
        ctx.stroke();
      }
      ctx.restore();
    }
  }
  function frame() {
    var t = Date.now();
    drawWater();
    drawRays(t);
    drawSand();
    drawWeeds(t);
    drawBubbles(t);
    drawFish(t);
    drawJelly(t);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
  var badge = document.createElement('div');
  badge.className = 'ss-badge';
  badge.textContent = T('آکواریوم', 'Aquarium', 'Аквариум', 'حوض الماء');
  document.body.appendChild(badge);
})();
"""

AQUARIUM_CSS = badge_css() + r"""
  .ss-badge { opacity: .8; }
"""

save_pkg(compose(
    "محافظ صفحهٔ آکواریوم", "HR Platform",
    "بستهٔ محافظ صفحهٔ «آکواریوم»: ماهی‌های رویه‌ای با پالت‌های رنگی متنوع، دم و بالهٔ بال‌زنده و مسیر سینوسی، حباب‌های لرزان، جلبک‌های موج‌دار، سنگ و شن طلایی، پرتوهای نور از سطح آب و عروس دریایی بنفش که هر ۱۸ تا ۴۰ ثانیه عبور می‌کند. ضدتکرار: هر ماهی/حباب در بازتولید رنگ، اندازه و سرعت تازه می‌گیرد و مسیرها هرگز ثابت نیست. تنظیمات کاربر (opts): fishCount=تعداد ماهی (۱–۳۰)، bubbles (true/false)، jellyfish (true/false). کاملاً رویه‌ای روی بوم — بدون هیچ عکس یا درخواست شبکه‌ای. همهٔ متن‌ها با فونت وزیر (embed در بسته).",
    AQUARIUM_JS, AQUARIUM_CSS,
    opts={"fishCount": 9, "bubbles": True, "jellyfish": True}),
    f"HR-Screensaver-Aquarium-{VER}.json")

# =====================================================================
# بستهٔ ۴ — بازسازی چهارفصل با فونت وزیر (عکس‌های مجوزدار Openverse)
# =====================================================================
photos, photo_chars = {}, {}
for s in SEASONS:
    photos[s], photo_chars[s] = encode_photo(s)
print("photo base64 chars:", photo_chars, "total:", sum(photo_chars.values()))

credits = {}
for s in SEASONS:
    m = attrib.get(s, {})
    credits[s] = { k: esc(m.get(k, "")) for k in ("artist", "license", "page") }

FS_JS = r"""
(function () {
  'use strict';
  var el = document.getElementById('hr-ss-data');
  var DATA = {};
  try { DATA = JSON.parse(el.textContent || '{}'); } catch (e) { }
  // فاز ۱۰۸ — تنظیمات اختصاصی بسته (opts) + کمک‌سازهای خواندن با پیش‌فرض و بازهٔ امن
  var OPTS = (DATA.opts && typeof DATA.opts === 'object') ? DATA.opts : {};
  function num(k, d, lo, hi) { var v = Number(OPTS[k]); if (!isFinite(v)) return d; v = Math.round(v); return Math.max(lo, Math.min(hi, v)); }
  function flag(k, d) { return typeof OPTS[k] === 'boolean' ? OPTS[k] : d; }
  var LANG = (DATA.lang || 'fa').toString().toLowerCase();
  var SEASON_IN = (DATA.season || 'auto').toString().toLowerCase();
  if (SEASON_IN !== 'spring' && SEASON_IN !== 'summer' && SEASON_IN !== 'autumn' && SEASON_IN !== 'winter') SEASON_IN = 'auto';
  // «خودکار» = تشخیص از تاریخ (نیم‌کرهٔ شمالی — محدودهٔ کاربران سامانه)
  var M = new Date().getMonth() + 1;
  var AUTO = (M >= 3 && M <= 5) ? 'spring' : (M >= 6 && M <= 8) ? 'summer' : (M >= 9 && M <= 11) ? 'autumn' : 'winter';
  var SEASON = SEASON_IN === 'auto' ? AUTO : SEASON_IN;
  var NAMES = {
    spring: { fa: 'بهار', en: 'Spring', ru: 'Весна', ar: 'الربيع' },
    summer: { fa: 'تابستان', en: 'Summer', ru: 'Лето', ar: 'الصيف' },
    autumn: { fa: 'پاییز', en: 'Autumn', ru: 'Осень', ar: 'الخريف' },
    winter: { fa: 'زمستان', en: 'Winter', ru: 'Зима', ar: 'الشتاء' }
  };
  function L(season) { var n = NAMES[season] || NAMES.spring; return n[LANG] || n.en; }
  var RTL = (LANG === 'fa' || LANG === 'prs' || LANG === 'ps' || LANG === 'ar');
  var PHOTOS = {
    spring: '__SPRING_DATA__',
    summer: '__SUMMER_DATA__',
    autumn: '__AUTUMN_DATA__',
    winter: '__WINTER_DATA__'
  };
  var CREDITS = {
    spring: { artist: '__SPRING_ARTIST__', license: '__SPRING_LICENSE__', page: '__SPRING_PAGE__' },
    summer: { artist: '__SUMMER_ARTIST__', license: '__SUMMER_LICENSE__', page: '__SUMMER_PAGE__' },
    autumn: { artist: '__AUTUMN_ARTIST__', license: '__AUTUMN_LICENSE__', page: '__AUTUMN_PAGE__' },
    winter: { artist: '__WINTER_ARTIST__', license: '__WINTER_LICENSE__', page: '__WINTER_PAGE__' }
  };
  var cv = document.createElement('canvas');
  cv.className = 'ss-cv';
  document.body.appendChild(cv);
  var ctx = cv.getContext('2d');
  function resize() { cv.width = window.innerWidth; cv.height = window.innerHeight; }
  window.addEventListener('resize', resize);
  resize();
  var BG = { ok: false, img: null };
  var TINTS = {
    spring: ['#134e2f', '#0f3d2e', '#1b4332'],
    summer: ['#14532d', '#166534', '#052e16'],
    autumn: ['#431407', '#571c0a', '#3d1a05'],
    winter: ['#0c1a2e', '#0e2233', '#101f33']
  };
  function drawBgFallback() {
    var g = ctx.createLinearGradient(0, 0, 0, cv.height);
    var c = TINTS[SEASON];
    g.addColorStop(0, c[2]); g.addColorStop(0.6, c[1]); g.addColorStop(1, c[0]);
    ctx.fillStyle = g; ctx.fillRect(0, 0, cv.width, cv.height);
  }
  var img = new Image();
  img.onload = function () { BG.ok = true; BG.img = img; };
  img.onerror = function () { BG.ok = false; };
  img.src = PHOTOS[SEASON];
  function drawPhoto(t) {
    if (!BG.ok) { drawBgFallback(); return; }
    var k = 1.06 + Math.sin(t / 17000) * 0.035 + Math.sin(t / 5100) * 0.012;
    var iw = BG.img.width, ih = BG.img.height;
    var sc = Math.max(cv.width / iw, cv.height / ih) * k;
    var w = iw * sc, h = ih * sc;
    var px = (cv.width - w) / 2 + Math.sin(t / 23000) * cv.width * 0.02;
    var py = (cv.height - h) / 2 + Math.cos(t / 29000) * cv.height * 0.018;
    ctx.drawImage(BG.img, px, py, w, h);
    var tint = { spring: 'rgba(23,90,60,0.20)', summer: 'rgba(22,101,52,0.18)', autumn: 'rgba(90,40,10,0.22)', winter: 'rgba(20,40,70,0.25)' }[SEASON];
    ctx.fillStyle = tint; ctx.fillRect(0, 0, cv.width, cv.height);
    var vg = ctx.createRadialGradient(cv.width / 2, cv.height / 2, cv.height * 0.35, cv.width / 2, cv.height / 2, cv.height * 0.95);
    vg.addColorStop(0, 'rgba(0,0,0,0)'); vg.addColorStop(1, 'rgba(0,0,0,0.45)');
    ctx.fillStyle = vg; ctx.fillRect(0, 0, cv.width, cv.height);
  }
  var gust = { until: 0, amp: 0 };
  function nextGust(t) {
    gust.until = t + 1200 + Math.random() * 2400;
    gust.amp = (1.2 + Math.random() * 2.2) * (Math.random() < 0.5 ? -1 : 1);
    gust.next = t + 6000 + Math.random() * 14000;
  }
  nextGust(0);
  function windOf(t) {
    if (t > gust.until && t > (gust.next || 0)) nextGust(t);
    var g = (t < gust.until ? gust.amp : 0);
    return Math.sin(t / 4700) * 0.6 + Math.sin(t / 1300) * 0.22 + g;
  }
  function rnd(a, b) { return a + Math.random() * (b - a); }
  function pick(arr) { return arr[Math.floor(Math.random() * arr.length)]; }
  var parts = [], pile = [];
  var PALS = {
    spring: ['#fbcfe8', '#f9a8d4', '#f472b6', '#fda4af', '#fde68a', '#fecdd3', '#fef3c7'],
    summer: ['#fde047', '#fef08a', '#bbf7d0', '#a7f3d0', '#fcd34d'],
    autumn: ['#f97316', '#eab308', '#dc2626', '#b45309', '#f59e0b', '#ea580c', '#d97706', '#a3e635'],
    winter: ['#ffffff', '#e0f2fe', '#f8fafc', '#cffafe']
  };
  function counts() {
    var DEN = num('density', 1, 0.3, 2);   // opts — تراکم ذرات (۰.۳ تا ۲ برابر)
    function n(v) { return Math.max(0, Math.round(v * DEN)); }
    switch (SEASON) {
      case 'spring': return { a: n(68), aKind: 'petal', b: n(2), bKind: 'butterfly', c: n(2), cKind: 'bee' };
      case 'summer': return { a: n(34), aKind: 'mote', b: n(14), bKind: 'fly', c: n(2), cKind: 'butterfly' };
      case 'autumn': return { a: n(74), aKind: 'leaf', b: 0, bKind: null, c: 0, cKind: null };
      default:       return { a: n(104), aKind: 'snow', b: 0, bKind: null, c: 0, cKind: null };
    }
  }
  function part(kind, first) {
    var p = {
      kind: kind,
      x: rnd(-40, cv.width + 40),
      y: first ? rnd(-cv.height, cv.height) : rnd(-cv.height * 0.5, -30),
      r: kind === 'snow' ? rnd(1.2, 4.4) : kind === 'mote' ? rnd(1, 2.6) : rnd(4.5, 11),
      vy: kind === 'mote' ? rnd(-0.15, 0.15) : kind === 'snow' ? rnd(0.5, 2.2) : rnd(0.6, 1.8),
      vx2: rnd(-0.35, 0.35),
      amp: rnd(14, 60), ph: rnd(0, 6.283), fr: rnd(0.004, 0.012),
      a: rnd(0, 6.283), va: rnd(-0.04, 0.04),
      c: pick(PALS[SEASON]),
      alpha: kind === 'fly' ? rnd(0.35, 0.95) : rnd(0.6, 1),
      pulse: rnd(0.02, 0.07), flap: rnd(0.12, 0.3),
      layer: Math.random()
    };
    if (kind === 'snow') p.vy *= (0.45 + p.layer);
    if (kind === 'bee') { p.r = rnd(5, 7.5); p.vy = rnd(-0.25, 0.25); }
    if (kind === 'butterfly') { p.r = rnd(7, 12); p.vy = rnd(-0.3, 0.3); }
    return p;
  }
  function initParts() {
    var c = counts(); parts = []; pile = [];
    var i;
    for (i = 0; i < c.a; i++) parts.push(part(c.aKind, true));
    for (i = 0; i < c.b; i++) parts.push(part(c.bKind, true));
    for (i = 0; i < c.c; i++) parts.push(part(c.cKind, true));
  }
  initParts();
  function land(p) {
    if (!flag('pile', true)) return;   // opts — تلکهٔ زمینی خاموش/روشن
    if (pile.length > 150) pile.shift();
    pile.push({ x: p.x, r: p.r * rnd(0.8, 1.25), c: p.c, a: rnd(-0.5, 0.5), alpha: 0 });
  }
  function drawPile(dt) {
    for (var i = 0; i < pile.length; i++) {
      var q = pile[i];
      q.alpha = Math.min(0.9, q.alpha + 0.01);
      var baseY = cv.height - 8 - (i % 3) * 7 - q.r * 0.4;
      ctx.save(); ctx.globalAlpha = q.alpha * 0.8; ctx.translate(q.x, baseY); ctx.rotate(q.a);
      ctx.fillStyle = q.c;
      ctx.beginPath(); ctx.ellipse(0, 0, q.r, q.r * (SEASON === 'winter' ? 0.5 : 0.55), 0, 0, 6.283); ctx.fill();
      ctx.restore();
    }
    if (SEASON === 'winter') {
      var g = ctx.createLinearGradient(0, cv.height - 70, 0, cv.height);
      g.addColorStop(0, 'rgba(241,245,249,0)'); g.addColorStop(1, 'rgba(241,245,249,0.5)');
      ctx.fillStyle = g; ctx.fillRect(0, cv.height - 70, cv.width, 70);
    }
  }
  function agentWings(p, t, colA, colB) {
    var wing = Math.sin(t * p.flap) * 0.75;
    ctx.fillStyle = colA;
    ctx.beginPath(); ctx.ellipse(-p.r * 0.5, 0, p.r * 0.58, p.r * 0.36, wing, 0, 6.283); ctx.fill();
    ctx.beginPath(); ctx.ellipse(p.r * 0.5, 0, p.r * 0.58, p.r * 0.36, -wing, 0, 6.283); ctx.fill();
    ctx.fillStyle = colB;
    ctx.beginPath(); ctx.ellipse(0, 0, p.r * 0.14, p.r * 0.3, 0, 0, 6.283); ctx.fill();
  }
  function frame() {
    var t = Date.now();
    var dt = Math.min(48, t - (frame.t || t)); frame.t = t;
    drawPhoto(t);
    var wind = windOf(t);
    var i, p;
    for (i = 0; i < parts.length; i++) {
      p = parts[i];
      p.ph += p.fr * dt; p.a += p.va * (dt / 16);
      var sway = Math.sin(p.ph) * p.amp;
      var drift = wind * (p.kind === 'snow' ? 1.2 : p.kind === 'leaf' ? 1 : 0.3);
      p.x += drift * (dt / 16) * 0.6 + Math.cos(p.ph) * 0.5 + p.vx2;
      p.y += p.vy * (dt / 16);
      if (p.kind !== 'fly' && p.kind !== 'butterfly' && p.kind !== 'bee' && p.y > cv.height - 6) {
        if (p.kind !== 'mote') land(p);
        parts[i] = part(p.kind, false); continue;
      }
      if (p.y > cv.height + 30 || p.x < -70 || p.x > cv.width + 70 || p.y < -cv.height) {
        parts[i] = part(p.kind, false); continue;
      }
      ctx.save();
      ctx.globalAlpha = p.kind === 'fly' ? Math.max(0.08, p.alpha + Math.sin(t * p.pulse) * 0.4) : p.alpha;
      ctx.translate(p.x + sway, p.y);
      ctx.rotate(p.a);
      if (p.kind === 'snow') {
        ctx.fillStyle = p.c;
        ctx.beginPath(); ctx.arc(0, 0, p.r, 0, 6.283); ctx.fill();
      } else if (p.kind === 'petal' || p.kind === 'leaf' || p.kind === 'mote') {
        ctx.fillStyle = p.c;
        ctx.beginPath(); ctx.ellipse(0, 0, p.r, p.r * 0.55, 0, 0, 6.283); ctx.fill();
        if (p.kind === 'leaf' && p.r > 6) {
          ctx.strokeStyle = 'rgba(60,30,5,0.35)'; ctx.lineWidth = 1;
          ctx.beginPath(); ctx.moveTo(-p.r * 0.7, 0); ctx.lineTo(p.r * 0.7, 0); ctx.stroke();
        }
      } else if (p.kind === 'fly') {
        ctx.fillStyle = p.c; ctx.shadowColor = p.c; ctx.shadowBlur = 10;
        ctx.beginPath(); ctx.arc(0, 0, p.r * 0.5, 0, 6.283); ctx.fill();
        ctx.shadowBlur = 0;
      } else if (p.kind === 'bee') {
        agentWings(p, t, 'rgba(255,255,255,0.8)', '#facc15');
        ctx.strokeStyle = '#1c1917'; ctx.lineWidth = 1.4;
        ctx.beginPath(); ctx.moveTo(-p.r * 0.35, 0); ctx.lineTo(p.r * 0.35, 0); ctx.stroke();
      } else if (p.kind === 'butterfly') {
        agentWings(p, t, p.c, 'rgba(30,27,75,0.9)');
      }
      ctx.restore();
    }
    drawPile(dt);
    if (SEASON === 'winter') fog(t);
    if (SEASON === 'summer') sun(t);
    requestAnimationFrame(frame);
  }
  function fog(t) {
    var bands = [[0.07, t / 90, 150], [0.055, t / 130 + cv.width / 3, 120], [0.04, t / 170 + cv.width * 0.7, 180]];
    for (var i = 0; i < 3; i++) {
      var b = bands[i];
      var fy = (b[1] % (cv.height + 340)) - 170;
      var g = ctx.createLinearGradient(0, fy - b[2], 0, fy + b[2]);
      g.addColorStop(0, 'rgba(226,232,240,0)');
      g.addColorStop(0.5, 'rgba(226,232,240,' + b[0] + ')');
      g.addColorStop(1, 'rgba(226,232,240,0)');
      ctx.fillStyle = g; ctx.fillRect(0, fy - b[2] - 20, cv.width, b[2] * 2 + 40);
    }
  }
  function sun(t) {
    var sx = cv.width * 0.82, sy = cv.height * 0.16;
    var rr = cv.height * (0.16 + Math.sin(t / 3600) * 0.02);
    var g = ctx.createRadialGradient(sx, sy, 0, sx, sy, rr);
    g.addColorStop(0, 'rgba(253,230,138,0.5)');
    g.addColorStop(0.45, 'rgba(253,186,116,0.22)');
    g.addColorStop(1, 'rgba(253,186,116,0)');
    ctx.fillStyle = g;
    ctx.beginPath(); ctx.arc(sx, sy, rr, 0, 6.283); ctx.fill();
  }
  requestAnimationFrame(frame);
  var badge = document.createElement('div');
  badge.className = 'ss-badge';
  badge.textContent = L(SEASON) + (SEASON_IN === 'auto' ? '' : ' (' + (RTL ? 'انتخاب دستی' : 'manual') + ')');
  document.body.appendChild(badge);
  var cr = CREDITS[SEASON] || {};
  var credit = document.createElement('div');
  credit.className = 'ss-credit';
  var srcTxt = '';
  if (cr.artist) srcTxt += (RTL ? 'عکس: ' : 'Photo: ') + cr.artist;
  if (cr.license) srcTxt += (srcTxt ? ' — ' : '') + cr.license;
  srcTxt += (srcTxt ? ' — ' : '') + 'Openverse';
  credit.textContent = srcTxt;
  if (cr.page) credit.title = cr.page;
  document.body.appendChild(credit);
})();
"""

FS_CSS = badge_css() + r"""
  .ss-credit {
    position: fixed; bottom: 12px; left: 50%; transform: translateX(-50%); max-width: 44vw; overflow: hidden;
    text-overflow: ellipsis; white-space: nowrap;
    color: rgba(255,255,255,0.45); font: 11px Vazirmatn, Tahoma, sans-serif;
    text-shadow: 0 1px 3px rgba(0,0,0,0.8); pointer-events: none; direction: ltr;
  }
"""

js = FS_JS
for s in SEASONS:
    js = js.replace("__" + s.upper() + "_DATA__", photos[s])
    js = js.replace("__" + s.upper() + "_ARTIST__", credits[s]["artist"])
    js = js.replace("__" + s.upper() + "_LICENSE__", credits[s]["license"])
    js = js.replace("__" + s.upper() + "_PAGE__", credits[s]["page"])

def credit_line(s):
    m = attrib.get(s, {})
    return f"{m.get('artist','?')} ({m.get('license','?')})"

desc = ("بستهٔ محافظ صفحهٔ چهارفصل: بهار (شکوفه، زنبور، پروانه)، تابستان (باغ سرسبز با خورشید و شب‌تاب)، "
        "پاییز (برگ‌ریزان رنگارنگ با تلکهٔ زمینی)، زمستان (برف پارالاکس، مه، درختان برفی). "
        "انتخاب فصل: خودکار از تاریخ یا دستی از تنظیمات سامانه (فصل صحنه). "
        "تنظیمات کاربر (opts): density=تراکم ذرات (۰.۳–۲)، pile=تلکهٔ زمینی (true/false). "
        "متن‌ها با فونت وزیر (embed در بسته) و عناصر متحرک با بازتولید تصادفی (ضدتکرار). "
        "عکس‌های آمادهٔ مجوزدار از Openverse: "
        + credit_line("spring") + "، " + credit_line("summer") + "، "
        + credit_line("autumn") + "، " + credit_line("winter"))
save_pkg(compose("محافظ صفحهٔ چهارفصل", "HR Platform — تصاویر: Openverse (CC)", desc, js, FS_CSS,
                 opts={"density": 1, "pile": True}),
         f"HR-Screensaver-FourSeasons-{VER}.json")

# =====================================================================
# بستهٔ ۵ — آتش‌بازی (Fireworks) — جدیدترین بسته؛ همهٔ صحنه با تنظیمات کاربر
# =====================================================================
FIREWORKS_JS = r"""
(function () {
  'use strict';
  """ + shell_badge_js() + r"""
  // ---------- آتش‌بازی: موشک/انفجار/شهر شبانه — ضدتکرار و کاملاً قابل‌تنظیم (opts) ----------
  var BURSTS = num('burstsPerMin', 8, 2, 30);          // opts — انفجار در دقیقه
  var SPEED = num('particleSpeed', 10, 3, 20) / 10;    // opts — ضریب سرعت ذرات (۰.۳ تا ۲)
  var PAL = one('palette', ['full', 'gold', 'rose', 'ocean', 'violet'], 'full');
  var CITY = flag('city', true);
  var TRAIL = flag('trail', true);
  var HUES = { full: null, gold: [36, 42, 48], rose: [330, 345, 355], ocean: [185, 200, 215], violet: [265, 280, 295] };
  function burstHue() {
    var hs = HUES[PAL];
    return hs ? pick(hs) : rnd(0, 360);
  }
  var rockets = [], sparks = [], stars = [], buildings = [];
  function buildStars() {
    stars = [];
    for (var i = 0; i < 140; i++) stars.push({ x: rnd(0, cv.width), y: rnd(0, cv.height * 0.72),
      r: rnd(0.3, 1.2), a: rnd(0.15, 0.8), ph: rnd(0, 6.283), sp: rnd(0.0008, 0.003) });
  }
  function buildCity() {   // خط آسمان شهر — با پنجره‌های روشن تصادفی (هر تغییر اندازه از نو)
    buildings = [];
    var x = 6;
    while (x < cv.width - 30) {
      var w = rnd(42, 110), h = rnd(cv.height * 0.10, cv.height * 0.28);
      var cols = Math.max(1, Math.floor(w / 18)), rows = Math.max(1, Math.floor(h / 22));
      var wins = [];
      for (var r = 0; r < rows; r++) for (var c = 0; c < cols; c++)
        if (Math.random() < 0.3) wins.push([c, r]);
      buildings.push({ x: x, w: w, h: h, wins: wins, dark: pick(['#0b1220', '#0d1526', '#101a2e']) });
      x += w + rnd(6, 26);
    }
  }
  function rebuild() { buildStars(); buildCity(); }
  rebuild();
  window.addEventListener('resize', rebuild);
  function spawnRocket(t) {
    rockets.push({ x: rnd(cv.width * 0.12, cv.width * 0.88), y: cv.height + 8,
      vx: rnd(-0.7, 0.7), vy: -(cv.height * rnd(0.0058, 0.0088)) * SPEED,
      burstAt: rnd(cv.height * 0.16, cv.height * 0.5), born: t, hue: burstHue() });
  }
  function explode(x, y, t, hue) {
    var n = Math.floor(rnd(70, 135));
    var pat = pick(['ring', 'palm', 'willow']);   // هر انفجار الگوی خودش — ضدتکرار
    for (var i = 0; i < n; i++) {
      var ang = (i / n) * 6.283 + rnd(-0.05, 0.05);
      var sp = (pat === 'ring' ? rnd(1.7, 2.1) : pat === 'palm' ? rnd(0.5, 2.3) : rnd(1.0, 2.5)) * SPEED;
      sparks.push({ x: x, y: y, px: x, py: y,
        vx: Math.cos(ang) * sp, vy: Math.sin(ang) * sp,
        life: rnd(900, 2200), born: t,
        hue: (hue + rnd(-16, 16) + 360) % 360,
        g: pat === 'willow' ? 0.028 : 0.05,
        drag: pat === 'willow' ? 0.988 : 0.972,
        flick: Math.random() < 0.2 });
    }
  }
  function drawBg(t) {
    var g = ctx.createLinearGradient(0, 0, 0, cv.height);
    g.addColorStop(0, '#04060f'); g.addColorStop(0.6, '#0a1024'); g.addColorStop(1, '#111a33');
    ctx.fillStyle = g; ctx.fillRect(0, 0, cv.width, cv.height);
    for (var i = 0; i < stars.length; i++) {   // ستاره‌های زمینه
      var s = stars[i];
      ctx.fillStyle = 'rgba(226,232,240,' + (s.a * (0.5 + 0.5 * Math.sin(t * s.sp + s.ph))).toFixed(3) + ')';
      ctx.beginPath(); ctx.arc(s.x, s.y, s.r, 0, 6.283); ctx.fill();
    }
    if (!CITY) return;
    for (var j = 0; j < buildings.length; j++) {   // شهر شبانه
      var b = buildings[j], by = cv.height - b.h;
      ctx.fillStyle = b.dark;
      ctx.fillRect(b.x, by, b.w, b.h);
      for (var k = 0; k < b.wins.length; k++) {
        var wn = b.wins[k];
        var lit = Math.random() < 0.995;   // چشمک خیلی نادر
        if (!lit) continue;
        ctx.fillStyle = 'rgba(253,224,71,0.55)';
        ctx.fillRect(b.x + 6 + wn[0] * 18, by + 8 + wn[1] * 22, 7, 10);
      }
    }
  }
  function frame() {
    var t = Date.now();
    drawBg(t);
    if (rockets.length < 4 && Math.random() < BURSTS / 3600) spawnRocket(t);
    for (var i = rockets.length - 1; i >= 0; i--) {   // موشک‌ها
      var r = rockets[i];
      r.px = r.x; r.py = r.y;
      r.vy += 0.045 * SPEED;
      r.x += r.vx; r.y += r.vy;
      ctx.strokeStyle = 'rgba(254,243,199,0.85)'; ctx.lineWidth = 1.8; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(r.px, r.py); ctx.lineTo(r.x, r.y); ctx.stroke();
      sparks.push({ x: r.x, y: r.y, px: r.x, py: r.y, vx: rnd(-0.25, 0.25), vy: rnd(0.2, 0.7),
        life: rnd(250, 550), born: t, hue: 45, g: 0.02, drag: 0.99, flick: false });
      if (r.y <= r.burstAt || r.vy >= -0.8) { explode(r.x, r.y, t, r.hue); rockets.splice(i, 1); }
    }
    for (var s = sparks.length - 1; s >= 0; s--) {   // جرقه‌ها
      var p = sparks[s];
      var age = t - p.born;
      if (age > p.life) { sparks.splice(s, 1); continue; }
      p.px = p.x; p.py = p.y;
      p.vx *= p.drag; p.vy = p.vy * p.drag + p.g;
      p.x += p.vx; p.y += p.vy;
      var a = 1 - age / p.life;
      if (p.flick && Math.random() < 0.25) continue;   // چشمک جرقه
      var col = TRAIL ? 'hsla(' + p.hue.toFixed(0) + ',95%,' + (58 + 22 * a).toFixed(0) + '%,' + a.toFixed(3) + ')'
                      : 'hsla(' + p.hue.toFixed(0) + ',95%,' + (58 + 22 * a).toFixed(0) + '%,' + (a * 0.85).toFixed(3) + ')';
      ctx.strokeStyle = col; ctx.lineWidth = TRAIL ? 2 : 2.6;
      ctx.beginPath();
      if (TRAIL) { ctx.moveTo(p.px, p.py); ctx.lineTo(p.x, p.y); }
      else { ctx.arc(p.x, p.y, 1.6, 0, 6.283); }
      ctx.stroke();
    }
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
  var badge = document.createElement('div');
  badge.className = 'ss-badge';
  badge.textContent = T('آتش‌بازی', 'Fireworks', 'Фейерверк', 'ألعاب نارية');
  document.body.appendChild(badge);
})();
"""

FIREWORKS_CSS = badge_css() + r"""
  .ss-badge { opacity: .8; }
"""

save_pkg(compose(
    "محافظ صفحهٔ آتش‌بازی", "HR Platform",
    "بستهٔ محافظ صفحهٔ «آتش‌بازی»: موشک‌هایی که از پایین اوج می‌گیرند و با الگوهای متنوع (حلقه/نخل/بید) می‌ترکند، جرقه‌ها با دنبالهٔ رنگی و گرانش واقعی فرومی‌ریزند و خط آسمان شهر شبانه با پنجره‌های روشن در پایین می‌درخشد. ضدتکرار: رنگ/الگو/تعداد جرقه/نقطهٔ انفجار هر بار تصادفی است. تنظیمات کاربر (opts): burstsPerMin=انفجار در دقیقه (۲–۳۰)، particleSpeed=سرعت ذرات (۰.۳–۲)، palette=پالت (full/gold/rose/ocean/violet)، city=نمایش شهر (true/false)، trail=دنبالهٔ جرقه (true/false). کاملاً رویه‌ای — بدون عکس و بدون درخواست شبکه‌ای. همهٔ متن‌ها با فونت وزیر (embed در بسته).",
    FIREWORKS_JS, FIREWORKS_CSS,
    opts={"burstsPerMin": 8, "particleSpeed": 1, "palette": "full", "city": True, "trail": True}),
    f"HR-Screensaver-Fireworks-{VER}.json")

print("ALL PACKAGES DONE")

