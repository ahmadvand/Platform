#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۱۰۶ — ساخت بستهٔ محافظ صفحهٔ «چهارفصل» (hr-screensaver-package / spec 1).
عکس‌های پس‌زمینه از Openverse (تجمیع‌کنندهٔ رسمی تصاویر CC — منبع آمادهٔ مجوزدار؛ دستور کاربر: «خودت عکس تولید نکن»)
به‌صورت data URI در بسته embed می‌شوند (CSP سندباکس: connect-src 'none' → هیچ منبع خارجی مجاز نیست).
موتور JS بسته: صحنهٔ هر فصل = عکس با Ken-Burns + عناصر متحرک ضدتکرار (شکوفه/زنبور/پروانه، شب‌تاب،
برگ‌ریزان با تلکهٔ زمینی، برف پارالاکس + مه) + رویداد gust تصادفی + خط منبع عکس روی صفحه.
خروجی: download/packages/HR-Screensaver-FourSeasons-2.7.17.json (≤۱MB، js ≤200k، css ≤100k نویسه)."""
import json, io, os, base64, sys

ROOT = "/home/z/my-project"
ASSETS = f"{ROOT}/scripts/ss-season-assets"
OUTDIR = f"{ROOT}/download/packages"
VER = "2.7.17"
os.makedirs(OUTDIR, exist_ok=True)

SEASONS = ["spring", "summer", "autumn", "winter"]
PHOTO_BUDGET_B64 = 180_000   # سقف نویسه‌های base64 برای کل ۴ عکس (JS ≤ 200k نویسه با موتور ~۱۳k)

# ---------- ۱) بارگذاری عکس‌ها + کاهش کیفیت/رزولوشن تطبیقی تا خوردن در بودجه ----------
from PIL import Image

attrib = {}
p = os.path.join(ASSETS, "attributions.json")
if os.path.exists(p):
    attrib = json.load(io.open(p, encoding="utf-8"))

PER_PHOTO_CAP = PHOTO_BUDGET_B64 // 4   # ~۴۵هزار نویسه برای هر عکس

def encode_photo(season):
    """عکس فصل را با رزولوشن/کیفیت تطبیقی encode می‌کند تا زیر سقف نویسه‌های هر عکس برود."""
    src = os.path.join(ASSETS, f"{season}.jpg")
    if not os.path.exists(src):
        return None, 0
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
    return best   # کم‌ترین رزولوشن/کیفیت ممکن — هرچقدر که شد

photos, photo_chars = {}, {}
for s in SEASONS:
    photos[s], photo_chars[s] = encode_photo(s)
total_chars = sum(photo_chars.values())
print("photo base64 chars:", photo_chars, "total:", total_chars)
missing = [s for s in SEASONS if photos[s] is None]
if missing:
    print("!! عکس‌ها هنوز آماده نیستند:", missing)
    sys.exit(1)

# ---------- ۲) موتور بسته ----------
ENGINE_JS = r"""
(function () {
  'use strict';
  var el = document.getElementById('hr-ss-data');
  var DATA = {};
  try { DATA = JSON.parse(el.textContent || '{}'); } catch (e) { }
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

  // عکس‌های embed‌شده (منبع: Openverse — مجوزدار CC؛ منبع/پدیدآور/مجوز روی صفحه ذکر می‌شود)
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

  // ---------- بوم ----------
  var cv = document.createElement('canvas');
  cv.className = 'ss-cv';
  document.body.appendChild(cv);
  var ctx = cv.getContext('2d');
  function resize() { cv.width = window.innerWidth; cv.height = window.innerHeight; }
  window.addEventListener('resize', resize);
  resize();

  // ---------- بارگذاری عکس فصل (data URI) با پس‌زمینهٔ گرادیان محض به‌عنوان پشتیبان ----------
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

  // Ken-Burns آرام — عکس ثابتِ خشک نمی‌ماند (ضدتکرار بصری)
  function drawPhoto(t) {
    if (!BG.ok) { drawBgFallback(); return; }
    var k = 1.06 + Math.sin(t / 17000) * 0.035 + Math.sin(t / 5100) * 0.012;
    var iw = BG.img.width, ih = BG.img.height;
    var sc = Math.max(cv.width / iw, cv.height / ih) * k;
    var w = iw * sc, h = ih * sc;
    var px = (cv.width - w) / 2 + Math.sin(t / 23000) * cv.width * 0.02;
    var py = (cv.height - h) / 2 + Math.cos(t / 29000) * cv.height * 0.018;
    ctx.drawImage(BG.img, px, py, w, h);
    // رنگ‌مایهٔ فصل + تیرگی برای خوانایی ذرات
    var tint = { spring: 'rgba(23,90,60,0.20)', summer: 'rgba(22,101,52,0.18)', autumn: 'rgba(90,40,10,0.22)', winter: 'rgba(20,40,70,0.25)' }[SEASON];
    ctx.fillStyle = tint; ctx.fillRect(0, 0, cv.width, cv.height);
    var vg = ctx.createRadialGradient(cv.width / 2, cv.height / 2, cv.height * 0.35, cv.width / 2, cv.height / 2, cv.height * 0.95);
    vg.addColorStop(0, 'rgba(0,0,0,0)'); vg.addColorStop(1, 'rgba(0,0,0,0.45)');
    ctx.fillStyle = vg; ctx.fillRect(0, 0, cv.width, cv.height);
  }

  // ---------- باد + رویداد gust تصادفی (ضدتکرار) ----------
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

  // ---------- ذرات فصل ----------
  var parts = [], pile = [];
  var PALS = {
    spring: ['#fbcfe8', '#f9a8d4', '#f472b6', '#fda4af', '#fde68a', '#fecdd3', '#fef3c7'],
    summer: ['#fde047', '#fef08a', '#bbf7d0', '#a7f3d0', '#fcd34d'],
    autumn: ['#f97316', '#eab308', '#dc2626', '#b45309', '#f59e0b', '#ea580c', '#d97706', '#a3e635'],
    winter: ['#ffffff', '#e0f2fe', '#f8fafc', '#cffafe']
  };
  function counts() {
    switch (SEASON) {
      case 'spring': return { a: 68, aKind: 'petal', b: 2, bKind: 'butterfly', c: 2, cKind: 'bee' };
      case 'summer': return { a: 34, aKind: 'mote', b: 14, bKind: 'fly', c: 2, cKind: 'butterfly' };
      case 'autumn': return { a: 74, aKind: 'leaf', b: 0, bKind: null, c: 0, cKind: null };
      default:       return { a: 104, aKind: 'snow', b: 0, bKind: null, c: 0, cKind: null };
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
    if (kind === 'snow') p.vy *= (0.45 + p.layer);          // پارالاکس برف
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

  // تلکهٔ زمینی — برگ/شکوفه/برفِ فرودآمده (سقف‌دار، با محوشدگی؛ ضدتکرار)
  function land(p) {
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
    if (SEASON === 'winter') {   // پوشش برف زمین
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
        if (p.kind === 'leaf' && p.r > 6) {   // رگبرگ برگ پاییز
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

  // مه غلتان زمستان — سه باند کم‌رنگ با جهت‌های متفاوت (ضدتکرار)
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

  // خورشید تابستانی — هالهٔ گرم تپنده + پرتوهای نوری
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

  // ---------- برچسب فصل + منبع عکس (CC BY/CC BY-SA نیازمند ذکر منبع است) ----------
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

ENGINE_CSS = r"""
html, body { margin: 0; height: 100%; overflow: hidden; background: #05100a; }
.ss-cv { position: fixed; inset: 0; width: 100%; height: 100%; display: block; }
.ss-badge {
  position: fixed; bottom: 14px; right: 18px;
  color: rgba(255,255,255,0.85); font: 600 15px/1.4 Tahoma, "Segoe UI", sans-serif;
  text-shadow: 0 1px 4px rgba(0,0,0,0.8); pointer-events: none; letter-spacing: 0.3px;
}
.ss-credit {
  position: fixed; bottom: 12px; left: 50%; transform: translateX(-50%); max-width: 44vw; overflow: hidden;
  text-overflow: ellipsis; white-space: nowrap;
  color: rgba(255,255,255,0.45); font: 11px/1.4 Tahoma, "Segoe UI", sans-serif;
  text-shadow: 0 1px 3px rgba(0,0,0,0.8); pointer-events: none; direction: ltr;
}
"""

# ---------- ۳) درج عکس‌ها و اعتبارها ----------
def esc(s):
    """نویسه‌های امن برای درون‌ریزی در رشتهٔ JS تک‌کوتیشنی + پالایش دنباله‌های ممنوع + فشرده‌سازی فاصله‌ها"""
    s = (s or "").replace("\\", "").replace("'", "").replace('"', "").strip()
    return " ".join(s.split())

credits = {}
for s in SEASONS:
    m = attrib.get(s, {})
    credits[s] = { k: esc(m.get(k, "")) for k in ("artist", "license", "page") }

js = ENGINE_JS
for s in SEASONS:
    js = js.replace("__" + s.upper() + "_DATA__", photos[s])
    js = js.replace("__" + s.upper() + "_ARTIST__", credits[s]["artist"])
    js = js.replace("__" + s.upper() + "_LICENSE__", credits[s]["license"])
    js = js.replace("__" + s.upper() + "_PAGE__", credits[s]["page"])

# پالایش دنباله‌های ممنوع (همان قاعدهٔ سرور — درون‌ریزی نمی‌پذیرد)
for bad in ("</script", "</style", "<!--"):
    if bad.lower() in js.lower() or bad.lower() in ENGINE_CSS.lower():
        print("!! forbidden sequence:", bad); sys.exit(1)

# ---------- ۴) متا و توضیح (سقف ۵۰۰ نویسه) ----------
def credit_line(s):
    m = attrib.get(s, {})
    return f"{m.get('artist','?')} ({m.get('license','?')})"

desc = ("محافظ صفحهٔ چهارفصل: بهار (شکوفه، زنبور، پروانه)، تابستان (باغ سرسبز با خورشید و شب‌تاب)، "
        "پاییز (برگ‌ریزان رنگارنگ با تلکهٔ زمینی)، زمستان (برف پارالاکس، مه، درختان برفی). "
        "انتخاب فصل: خودکار از تاریخ یا دستی از تنظیمات سامانه (فصل صحنه). "
        "عناصر متحرک با بازتولید تصادفی (ضدتکرار). عکس‌ها از Openverse: "
        + credit_line("spring") + "، " + credit_line("summer") + "، "
        + credit_line("autumn") + "، " + credit_line("winter"))
if len(desc) > 500:
    desc = desc[:497] + "…"
print("meta.description chars:", len(desc))

pkg = {
    "format": "hr-screensaver-package",
    "spec": 1,
    "exportedAt": __import__("datetime").datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
    "meta": {
        "name": "محافظ صفحهٔ چهارفصل",
        "author": "HR Platform — تصاویر: Openverse (CC)",
        "version": VER,
        "description": desc,
    },
    "settings": {
        "enabled": True,
        "minutes": 1,
        "seconds": 15,
        "requirePassword": True,
        "season": "auto",
    },
    "code": {"js": js, "css": ENGINE_CSS},
}

out = os.path.join(OUTDIR, f"HR-Screensaver-FourSeasons-{VER}.json")
with io.open(out, "w", encoding="utf-8") as f:
    json.dump(pkg, f, ensure_ascii=False)
size = os.path.getsize(out)
print(f"js chars: {len(js)}, css chars: {len(ENGINE_CSS)}")
print(f"OK -> {out} ({size} bytes)")
if size > 1024 * 1024: print("!! فایل بزرگ‌تر از ۱MB است"); sys.exit(1)
if len(js) > 200_000: print("!! JS بلندتر از سقف است"); sys.exit(1)
if len(ENGINE_CSS) > 100_000: print("!! CSS بلندتر از سقف است"); sys.exit(1)
