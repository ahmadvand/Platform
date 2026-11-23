#!/usr/bin/env python3
# ======================================================================
# فاز ۱۰۶ — بسته‌های محافظ صفحه (نسخهٔ ۲.۱) — assets/screensaver-packages/
#   • شش بسته: آسمان شب، شفق قطبی، امواج آرام، کویر ایران، کهکشان مارپیچ
#     و «باران شعر» (درخواست کاربر — ابیات ماندگار فارسی که مثل باران می‌بارند)
#   • رفع باگ «حروف جدا/مقطع»:
#       ۱) هر عبارت فارسی با «یک فراخوان fillText» روی کل رشته رسم می‌شود تا
#          شکل‌دهی (اتصال حروف) با جداول OpenType وزیرمتن انجام شود.
#       ۲) هیچ‌گاه حرف‌به‌حرف رسم نمی‌شود و letter-spacing به کار نمی‌رود.
#       ۳) فونت وزیرمتن (Regular + Bold) به‌صورت base64 داخل خود بسته embed
#          می‌شود (FontFace API — CSP سندباکس اجازهٔ font-src data: می‌دهد)؛
#          از نسخهٔ ۲.۱ میزبان هم فونت وزیر را به Runtime تزریق می‌کند.
#   • تنظیمات همراه بسته (۴ فیلد محافظ + سرعت/تراکم) — از تب «محافظ صفحه»
#     قابل تغییر کاربر؛ Runtime هم settings را به کد بسته می‌دهد.
#   • سقف‌های درون‌ریزی: فایل ≤۱MB، JS ≤۲۰۰هزار نویسه، CSS ≤۱۰۰هزار نویسه،
#     ممنوع: «</script»، «</style»، «<!--».
# ======================================================================
import base64, json, os, sys

ROOT = "/home/z/my-project"
FONTS = f"{ROOT}/aspnet/src/Platform.Web/wwwroot/fonts"
OUT = f"{ROOT}/assets/screensaver-packages"

FONT_REG_B64 = base64.b64encode(open(f"{FONTS}/Vazirmatn-Regular.woff2", "rb").read()).decode()
FONT_BOLD_B64 = base64.b64encode(open(f"{FONTS}/Vazirmatn-Bold.woff2", "rb").read()).decode()

# ----------------------------------------------------------------------
# سرِ مشترک JS همهٔ بسته‌ها — فونت + قرارداد میزبان + رسم امن متن فارسی
# ----------------------------------------------------------------------
JS_HEAD = r"""(function () {
    'use strict';
    // ======================================================================
    // بستهٔ محافظ صفحه «@@TITLE@@" — قالب hr-screensaver-package (spec 1)
    // نسخهٔ ۲.۱ — رفع باگ «حروف جدا/مقطع»: هر عبارت فارسی همیشه با «یک
    // فراخوان fillText» روی کل رشته رسم می‌شود تا شکل‌دهی (اتصال حروف) با
    // جداول OpenType فونت وزیرمتن انجام شود؛ رسم حرف‌به‌حرف و letter-spacing
    // مطلقاً وجود ندارد. فونت وزیر (Regular + Bold) با FontFace API از دادهٔ
    // base64 embed شده؛ میزبان (نسخهٔ ۲.۱ به بعد) هم همین فونت را تزریق می‌کند.
    // ======================================================================
    var lang = 'fa', pkgName = '', cfg = {};
    try {
        var el = document.getElementById('hr-ss-data');
        if (el) {
            var d = JSON.parse(el.textContent || '{}');
            if (d && d.lang) lang = String(d.lang).toLowerCase();
            if (d && d.name) pkgName = String(d.name);
            if (d && d.settings && typeof d.settings === 'object') cfg = d.settings;
        }
    } catch (e) { }
    function cfgNum(v, dflt, lo, hi) {
        var n = Number(v); if (!isFinite(n)) n = dflt;
        return Math.min(hi, Math.max(lo, n));
    }
    var SPEED = cfgNum(cfg.speed, 1, 0.2, 3);      // تنظیم کاربر: سرعت انیمیشن
    var DENSITY = cfgNum(cfg.density, 1, 0.2, 3);  // تنظیم کاربر: تراکم عناصر

    // ---------- بوم تمام‌صفحه ----------
    var cv = document.createElement('canvas');
    cv.style.cssText = 'position:fixed;inset:0;width:100%;height:100%;display:block';
    document.body.appendChild(cv);
    var ctx = cv.getContext('2d');
    var W = 0, H = 0, raf = 0, t0 = 0, started = false;

    // ---------- رسم عبارت فارسی — کل رشته در یک fillText (رفع باگ اتصال حروف) ----------
    function phrase(text, x, y, px, weight, color, alpha) {
        ctx.save();
        try { ctx.direction = 'rtl'; } catch (e) { }
        ctx.font = (weight || 400) + ' ' + px + 'px Vazirmatn, VazirSS, Vazir';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.globalAlpha = (alpha == null ? 1 : alpha);
        ctx.fillStyle = color;
        ctx.fillText(text, x, y);   // کل رشته — یک‌جا؛ هیچ‌گاه حرف‌به‌حرف رسم نمی‌شود
        ctx.restore();
    }

    function resize() { W = cv.width = window.innerWidth; H = cv.height = window.innerHeight; @@RESET@@ }
    window.addEventListener('resize', resize);

    var TITLE = '@@TITLE@@';
    var SUBTITLE = '@@SUBTITLE@@';

    function go() {
        if (started) return;
        started = true;
        resize();
        t0 = performance.now();
        raf = requestAnimationFrame(frame);
    }

    // ---------- فونت وزیر embed‌شده (base64) — رسم متن فقط پس از آماده‌شدن ----------
    try {
        var f1 = new FontFace('VazirSS', 'url(data:font/woff2;base64,@@FONT_REG@@)', { weight: '400', display: 'block' });
        var f2 = new FontFace('VazirSS', 'url(data:font/woff2;base64,@@FONT_BOLD@@)', { weight: '700', display: 'block' });
        document.fonts.add(f1); document.fonts.add(f2);
        Promise.all([f1.load(), f2.load()]).catch(function () { }).then(go);
    } catch (e) { go(); }
    setTimeout(go, 2500);   // گارد: حتی اگر بارگذاری فونت معطل شد، محافظ حتماً روشن شود

    @@FRAME@@

    // پاک‌سازی اختیاری (قرارداد میزبان سندباکس)
    window.__hrSsGuestCleanup = function () {
        if (raf) cancelAnimationFrame(raf);
        try { cv.remove(); } catch (e) { }
    };
})();
"""

CSS_TPL = "html,body{margin:0;padding:0;width:100%;height:100%;overflow:hidden;background:@@BG@@}canvas{display:block}"

# ----------------------------------------------------------------------
# پنج تم بصری — هر تم: reset (هنگام resize) + frame (هر فرم)
# ----------------------------------------------------------------------

NIGHT_SKY = r"""var stars = [], shots = [];
function frame(now) {
    var t = (now - t0) / 1000 * SPEED;
    var g = ctx.createLinearGradient(0, 0, 0, H);
    g.addColorStop(0, '#04081a'); g.addColorStop(0.55, '#0b1730'); g.addColorStop(1, '#1d2c55');
    ctx.globalAlpha = 1; ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
    var i, s;
    for (i = 0; i < stars.length; i++) {
        s = stars[i];
        ctx.globalAlpha = 0.3 + 0.7 * (0.5 + 0.5 * Math.sin(t * s.s + s.p));
        ctx.fillStyle = '#d7e6ff';
        ctx.beginPath(); ctx.arc(s.x * W, s.y * H, s.r, 0, 6.283); ctx.fill();
    }
    // ماه — هلال با هالهٔ ملایم
    var mx = W * 0.82, my = H * 0.16, mr = Math.min(W, H) * 0.055 + 14;
    var glow = ctx.createRadialGradient(mx, my, mr * 0.4, mx, my, mr * 3.4);
    glow.addColorStop(0, 'rgba(244,238,214,0.30)'); glow.addColorStop(1, 'rgba(244,238,214,0)');
    ctx.globalAlpha = 1; ctx.fillStyle = glow;
    ctx.beginPath(); ctx.arc(mx, my, mr * 3.4, 0, 6.283); ctx.fill();
    ctx.fillStyle = '#f2ecd4';
    ctx.beginPath(); ctx.arc(mx, my, mr, 0, 6.283); ctx.fill();
    ctx.fillStyle = '#0b1730';
    ctx.beginPath(); ctx.arc(mx - mr * 0.42, my - mr * 0.18, mr * 0.92, 0, 6.283); ctx.fill();
    // شهاب‌های گذرا
    if (Math.random() < 0.005 * SPEED && shots.length < 2)
        shots.push({ x: W * (0.15 + Math.random() * 0.7), y: H * Math.random() * 0.35, vx: -(4 + Math.random() * 4), vy: 2 + Math.random() * 2, born: t });
    for (i = shots.length - 1; i >= 0; i--) {
        var sh = shots[i];
        sh.x += sh.vx * SPEED; sh.y += sh.vy * SPEED;
        var age = t - sh.born, a = Math.max(0, 1 - age / 1.1);
        if (a <= 0 || sh.x < -90 || sh.y > H + 20) { shots.splice(i, 1); continue; }
        var tail = ctx.createLinearGradient(sh.x, sh.y, sh.x + 90, sh.y - 55);
        tail.addColorStop(0, 'rgba(255,255,255,' + (0.85 * a).toFixed(3) + ')');
        tail.addColorStop(1, 'rgba(255,255,255,0)');
        ctx.globalAlpha = 1; ctx.strokeStyle = tail; ctx.lineWidth = 1.6;
        ctx.beginPath(); ctx.moveTo(sh.x, sh.y); ctx.lineTo(sh.x + 90, sh.y - 55); ctx.stroke();
    }
    var ty = H * 0.36 + Math.sin(t * 0.6) * 6;
    var tp = Math.min(64, Math.max(30, W * 0.065));
    phrase(TITLE, W / 2, ty, tp, 700, '#f4f7ff');
    phrase(SUBTITLE, W / 2, ty + tp * 0.95, 19, 400, '#a9bce0', 0.8);
}"""

NIGHT_SKY_RESET = r"""stars = []; var n = Math.round(190 * DENSITY);
    for (var i = 0; i < n; i++) stars.push({ x: Math.random(), y: Math.random() * 0.92, r: 0.4 + Math.random() * 1.3, p: Math.random() * 6.283, s: 0.4 + Math.random() * 1.5 });
    shots = [];"""

AURORA = r"""var stars = [];
function frame(now) {
    var t = (now - t0) / 1000 * SPEED;
    ctx.globalAlpha = 1; ctx.fillStyle = '#02040c'; ctx.fillRect(0, 0, W, H);
    var i;
    for (i = 0; i < stars.length; i++) {
        var s = stars[i];
        ctx.globalAlpha = 0.25 + 0.55 * (0.5 + 0.5 * Math.sin(t * s.s + s.p));
        ctx.fillStyle = '#cfe2ff';
        ctx.beginPath(); ctx.arc(s.x * W, s.y * H, s.r, 0, 6.283); ctx.fill();
    }
    // سه روبان شفق — هر روبان با سه لایهٔ عرض/شفافیت، حس درخشش می‌دهد
    var bands = [
        { hue: '64,224,170', amp: 46, f: 1.0, off: -40, w: 120, a: 0.10 },
        { hue: '110,90,220', amp: 62, f: 0.8, off: 12,  w: 140, a: 0.09 },
        { hue: '40,190,200', amp: 34, f: 1.3, off: -92, w: 90,  a: 0.12 }
    ];
    for (var b = 0; b < bands.length; b++) {
        var bd = bands[b];
        for (var pass = 0; pass < 3; pass++) {
            ctx.beginPath();
            for (var x = -22; x <= W + 22; x += 22) {
                var y = H * 0.32 + bd.off
                    + Math.sin(x * 0.0042 * bd.f + t * 0.55 + b * 1.9) * bd.amp
                    + Math.sin(x * 0.011 * bd.f - t * 0.34 + b) * bd.amp * 0.45;
                if (x === -22) ctx.moveTo(x, y); else ctx.lineTo(x, y);
            }
            ctx.strokeStyle = 'rgba(' + bd.hue + ',' + (pass === 0 ? bd.a : pass === 1 ? bd.a * 1.8 : bd.a * 2.6) + ')';
            ctx.lineWidth = pass === 0 ? bd.w : pass === 1 ? bd.w * 0.45 : bd.w * 0.16;
            ctx.lineJoin = 'round'; ctx.lineCap = 'round';
            ctx.stroke();
        }
    }
    var ty = H * 0.66 + Math.sin(t * 0.5) * 5;
    var tp = Math.min(60, Math.max(30, W * 0.06));
    phrase(TITLE, W / 2, ty, tp, 700, '#eafff5');
    phrase(SUBTITLE, W / 2, ty + tp * 0.95, 19, 400, '#9fd8c8', 0.8);
}"""

AURORA_RESET = r"""stars = []; var n = Math.round(110 * DENSITY);
    for (var i = 0; i < n; i++) stars.push({ x: Math.random(), y: Math.random() * 0.95, r: 0.4 + Math.random() * 1.1, p: Math.random() * 6.283, s: 0.5 + Math.random() * 1.4 });"""

WAVES = r"""function frame(now) {
    var t = (now - t0) / 1000 * SPEED;
    var g = ctx.createLinearGradient(0, 0, 0, H);
    g.addColorStop(0, '#03102a'); g.addColorStop(0.5, '#06294a'); g.addColorStop(1, '#0a3d6e');
    ctx.globalAlpha = 1; ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
    // مهتاب
    var mx = W * 0.2, my = H * 0.15, mr = Math.min(W, H) * 0.05 + 12;
    var glow = ctx.createRadialGradient(mx, my, mr * 0.4, mx, my, mr * 3.6);
    glow.addColorStop(0, 'rgba(230,240,255,0.26)'); glow.addColorStop(1, 'rgba(230,240,255,0)');
    ctx.fillStyle = glow;
    ctx.beginPath(); ctx.arc(mx, my, mr * 3.6, 0, 6.283); ctx.fill();
    ctx.fillStyle = '#eaf1fb';
    ctx.beginPath(); ctx.arc(mx, my, mr, 0, 6.283); ctx.fill();
    // لایه‌های موج — از دور به نزدیک
    var layers = [
        { y: 0.52, amp: 16, fr: 0.0060, sp: 0.45, c: 'rgba(8,52,100,0.85)' },
        { y: 0.62, amp: 20, fr: 0.0050, sp: 0.60, c: 'rgba(10,63,118,0.88)' },
        { y: 0.72, amp: 24, fr: 0.0042, sp: 0.78, c: 'rgba(13,79,133,0.90)' },
        { y: 0.82, amp: 28, fr: 0.0036, sp: 0.95, c: 'rgba(18,97,156,0.94)' }
    ];
    for (var L = 0; L < layers.length; L++) {
        var ly = layers[L];
        ctx.globalAlpha = 1;
        ctx.beginPath(); ctx.moveTo(0, H);
        for (var x = 0; x <= W + 14; x += 14) {
            var y = H * ly.y + Math.sin(x * ly.fr + t * ly.sp + L * 1.7) * ly.amp
                + Math.sin(x * ly.fr * 2.63 - t * ly.sp * 1.4 + L) * ly.amp * 0.3;
            ctx.lineTo(x, y);
        }
        ctx.lineTo(W + 14, H); ctx.closePath();
        ctx.fillStyle = ly.c; ctx.fill();
        // کف ملایم روی موج جلو
        if (L === layers.length - 1) {
            ctx.globalAlpha = 0.35; ctx.fillStyle = '#cfe8ff';
            for (var x2 = 0; x2 <= W; x2 += 34) {
                var y2 = H * ly.y + Math.sin(x2 * ly.fr + t * ly.sp + L * 1.7) * ly.amp
                    + Math.sin(x2 * ly.fr * 2.63 - t * ly.sp * 1.4 + L) * ly.amp * 0.3;
                if (Math.sin(x2 * 0.9 + t * 2) > 0.55) {
                    ctx.beginPath(); ctx.arc(x2, y2 - 3, 1.4, 0, 6.283); ctx.fill();
                }
            }
        }
    }
    var ty = H * 0.13 + Math.sin(t * 0.5) * 5;
    var tp = Math.min(56, Math.max(28, W * 0.055));
    phrase(TITLE, W / 2, ty, tp, 700, '#eaf2ff');
    phrase(SUBTITLE, W / 2, ty + tp * 1.0, 18, 400, '#9fc3e8', 0.82);
}"""

WAVES_RESET = ";"

DESERT = r"""var stars = [];
function frame(now) {
    var t = (now - t0) / 1000 * SPEED;
    var g = ctx.createLinearGradient(0, 0, 0, H);
    g.addColorStop(0, '#1c0b33'); g.addColorStop(0.42, '#7a2d3a'); g.addColorStop(0.66, '#d97b3f');
    g.addColorStop(0.85, '#f0b26a'); g.addColorStop(1, '#f7cf8e');
    ctx.globalAlpha = 1; ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
    // ستاره‌های نو ظهور در نیمهٔ تاریک آسمان
    for (var i = 0; i < stars.length; i++) {
        var s = stars[i];
        ctx.globalAlpha = (0.25 + 0.5 * (0.5 + 0.5 * Math.sin(t * s.s + s.p))) * (1 - s.y * 2.2);
        ctx.fillStyle = '#ffe9d0';
        ctx.beginPath(); ctx.arc(s.x * W, s.y * H * 0.4, s.r, 0, 6.283); ctx.fill();
    }
    // خورشید در حال غروب
    var sx = W * 0.5, sy = H * 0.6, sr = Math.min(W, H) * 0.06 + 10;
    var glow = ctx.createRadialGradient(sx, sy, sr * 0.4, sx, sy, sr * 3.4);
    glow.addColorStop(0, 'rgba(255,214,150,0.5)'); glow.addColorStop(1, 'rgba(255,214,150,0)');
    ctx.globalAlpha = 1; ctx.fillStyle = glow;
    ctx.beginPath(); ctx.arc(sx, sy, sr * 3.4, 0, 6.283); ctx.fill();
    ctx.fillStyle = '#ffdf9e';
    ctx.beginPath(); ctx.arc(sx, sy, sr, 0, 6.283); ctx.fill();
    // تپه‌های شنی — سه لایه با رانش آرام
    var dunes = [
        { y: 0.66, amp: 18, fr: 0.0032, ph: 0.0, c: '#5f2d24' },
        { y: 0.77, amp: 26, fr: 0.0038, ph: 2.3, c: '#401c20' },
        { y: 0.89, amp: 32, fr: 0.0044, ph: 4.6, c: '#281016' }
    ];
    for (var D = 0; D < dunes.length; D++) {
        var d = dunes[D];
        ctx.globalAlpha = 1;
        ctx.beginPath(); ctx.moveTo(0, H);
        for (var x = 0; x <= W + 18; x += 18) {
            var y = H * d.y + Math.sin(x * d.fr + d.ph + t * 0.04 * (D + 1)) * d.amp
                + Math.sin(x * d.fr * 3.1 - d.ph * 0.7) * d.amp * 0.3;
            ctx.lineTo(x, y);
        }
        ctx.lineTo(W + 18, H); ctx.closePath();
        ctx.fillStyle = d.c; ctx.fill();
    }
    var ty = H * 0.22 + Math.sin(t * 0.5) * 5;
    var tp = Math.min(58, Math.max(28, W * 0.055));
    phrase(TITLE, W / 2, ty, tp, 700, '#ffe9c4');
    phrase(SUBTITLE, W / 2, ty + tp * 0.95, 18, 400, '#f3c98f', 0.85);
}"""

DESERT_RESET = r"""stars = []; var n = Math.round(70 * DENSITY);
    for (var i = 0; i < n; i++) stars.push({ x: Math.random(), y: Math.random(), r: 0.4 + Math.random() * 1.0, p: Math.random() * 6.283, s: 0.5 + Math.random() * 1.3 });"""

GALAXY = r"""var pts = [];
function frame(now) {
    var t = (now - t0) / 1000 * SPEED;
    ctx.globalAlpha = 1; ctx.fillStyle = '#030309'; ctx.fillRect(0, 0, W, H);
    var cx = W * 0.5, cy = H * 0.46;
    // سحابی زمینهٔ کم‌رنگ
    var neb = ctx.createRadialGradient(cx, cy, 0, cx, cy, Math.min(W, H) * 0.75);
    neb.addColorStop(0, 'rgba(90,80,200,0.10)'); neb.addColorStop(1, 'rgba(90,80,200,0)');
    ctx.fillStyle = neb; ctx.fillRect(0, 0, W, H);
    // هستهٔ درخشان
    var core = ctx.createRadialGradient(cx, cy, 0, cx, cy, 110);
    core.addColorStop(0, 'rgba(255,225,185,0.30)'); core.addColorStop(1, 'rgba(255,225,185,0)');
    ctx.fillStyle = core;
    ctx.beginPath(); ctx.arc(cx, cy, 110, 0, 6.283); ctx.fill();
    // بازوهای مارپیچ — نقاط نزدیک مرکز تندتر می‌چرخند
    for (var i = 0; i < pts.length; i++) {
        var p = pts[i];
        var th = p.th + t * (150 / (p.r + 90));
        var x = cx + p.r * Math.cos(th + p.arm * Math.PI);
        var y = cy + p.r * Math.sin(th + p.arm * Math.PI) * 0.55;
        ctx.globalAlpha = 0.35 + 0.65 * (0.5 + 0.5 * Math.sin(t * 1.3 + p.th * 3));
        ctx.fillStyle = p.c;
        ctx.beginPath(); ctx.arc(x, y, p.sz, 0, 6.283); ctx.fill();
    }
    var ty = H * 0.84 + Math.sin(t * 0.5) * 5;
    var tp = Math.min(54, Math.max(26, W * 0.05));
    phrase(TITLE, W / 2, ty, tp, 700, '#f2f0ff');
    phrase(SUBTITLE, W / 2, ty + tp * 0.95, 17, 400, '#b8b0e8', 0.82);
}"""

GALAXY_RESET = r"""pts = []; var n = Math.round(300 * DENSITY), lim = Math.min(W, H) * 0.44;
    for (var i = 0; i < n; i++) {
        var r = 26 + Math.pow(Math.random(), 0.65) * lim;
        var c = r < lim * 0.28 ? '#ffdfae' : (r < lim * 0.6 ? '#bcd4ff' : (Math.random() < 0.5 ? '#b9a2ff' : '#8fb8ff'));
        pts.push({ arm: i % 2, r: r, th: Math.random() * 6.283, sz: 0.5 + Math.random() * 1.5, c: c });
    }"""

# ----------------------------------------------------------------------
# «باران شعر» (فاز ۱۰۶ — درخواست کاربر) — ابیات ماندگار فارسی مثل قطره‌های
# باران آرام می‌بارند؛ هر بیت همیشه یک fillText کامل است (اتصال حروف تضمینی)
# + باران ریز خط‌های نوری در پس‌زمینه.
# ابیات: مشهور و عمومی (حافظ غزل ۱، گلستان سعدی، خیام، شاهنامه، وحشی بافقی)
# ----------------------------------------------------------------------
POEM_RAIN = r"""var drops = [], streaks = [];
// ابیات ماندگار — هر قطره یک بیت کامل (hemistichها با « … » از هم جدا می‌شوند؛
// هر بیت همیشه با یک fillText روی کل رشته رسم می‌شود)
var verses = [
    'الا یا ایها الساقی ادر کأسا و ناولها … که عشق آسان نمود اول ولی افتاد مشکل‌ها',
    'بنی‌آدم اعضای یکدیگرند … که در آفرینش ز یک گوهرند',
    'اسرار ازل را نه تو دانی و نه من … و در راه رمش نه تو دانی و نه من',
    'به نام خداوند جان و خرد',
    'هرگز نمیرد آن که دلش زنده شد به عشق'
];
function frame(now) {
    var t = (now - t0) / 1000 * SPEED;
    var g = ctx.createLinearGradient(0, 0, 0, H);
    g.addColorStop(0, '#070a16'); g.addColorStop(0.6, '#0c1024'); g.addColorStop(1, '#131a36');
    ctx.globalAlpha = 1; ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
    // هالهٔ ملایم مرکز — مثل چراغی پشت مه
    var cx = W * 0.5, cy = H * 0.38;
    var halo = ctx.createRadialGradient(cx, cy, 0, cx, cy, Math.min(W, H) * 0.55);
    halo.addColorStop(0, 'rgba(120,140,220,0.10)'); halo.addColorStop(1, 'rgba(120,140,220,0)');
    ctx.fillStyle = halo; ctx.fillRect(0, 0, W, H);
    // باران ریز خط‌های نوری پس‌زمینه
    for (var i = 0; i < streaks.length; i++) {
        var st = streaks[i];
        st.y += st.v * SPEED;
        if (st.y > H + 40) { st.y = -40 - Math.random() * 120; st.x = Math.random() * W; st.v = 260 + Math.random() * 260; }
        var sg = ctx.createLinearGradient(st.x, st.y, st.x, st.y + st.len);
        sg.addColorStop(0, 'rgba(158,178,235,0)'); sg.addColorStop(0.5, 'rgba(158,178,235,' + st.a + ')'); sg.addColorStop(1, 'rgba(158,178,235,0)');
        ctx.globalAlpha = 1; ctx.strokeStyle = sg; ctx.lineWidth = 1.1;
        ctx.beginPath(); ctx.moveTo(st.x, st.y); ctx.lineTo(st.x, st.y + st.len); ctx.stroke();
    }
    // قطره‌های شعر — هر بیت آرام می‌بارد، از بالا پیدا و پایین محو می‌شود
    for (var d = 0; d < drops.length; d++) {
        var p = drops[d];
        p.y += p.vy * SPEED;
        p.x += Math.sin(t * 0.7 + p.ph) * 0.25;
        var fade = 1;
        if (p.y < p.enter) fade = Math.max(0.05, p.y / p.enter);           // ورود از بالا
        if (p.y > H - 90) fade = Math.max(0, (H - 90 - p.y) / 90) * 0.9;   // محو در پایین
        if (p.y > H - 30) { p.y = -30 - Math.random() * 160; p.x = W * (lanes[Math.floor(Math.random() * 3)] + (Math.random() - 0.5) * 0.12); p.vy = 30 + Math.random() * 25; continue; }
        phrase(p.text, p.x, p.y, p.px, 400, '#f3ead8', Math.min(p.a, fade));
    }
    // عنوان و زیرنویس
    var ty = H * 0.075 + Math.sin(t * 0.5) * 4;
    var tp = Math.min(54, Math.max(28, W * 0.052));
    phrase(TITLE, W / 2, ty, tp, 700, '#f7f1e3');
    phrase(SUBTITLE, W / 2, ty + tp * 0.95, 17, 400, '#b9b4d8', 0.85);
}"""

POEM_RAIN_RESET = r"""drops = []; var n = Math.round(9 * DENSITY), i;
    var lanes = [0.2, 0.5, 0.8];
    for (i = 0; i < n; i++) {
        var vt = verses[i % verses.length];
        drops.push({
            text: vt,
            x: W * (lanes[i % 3] + (Math.random() - 0.5) * 0.12),
            y: i * (H * 0.9 / n),
            vy: 30 + Math.random() * 25,
            px: Math.min(30, Math.max(15, Math.min(W * 0.027, 780 / Math.max(14, vt.length)))),
            ph: Math.random() * 6.283,
            a: 0.55 + Math.random() * 0.4,
            enter: 50
        });
    }
    streaks = []; var m = Math.round(90 * DENSITY);
    for (i = 0; i < m; i++) streaks.push({ x: Math.random() * W, y: Math.random() * H, v: 260 + Math.random() * 260, len: 22 + Math.random() * 46, a: 0.05 + Math.random() * 0.12 });"""

# ----------------------------------------------------------------------
# تعریف بسته‌ها
# ----------------------------------------------------------------------
AUTHOR = "پلتفرم یکپارچه منابع انسانی"
VER = "2.1.0"

PKGS = [
    {
        "slug": "hr-ss-poem-rain",
        "name": "باران شعر",
        "subtitle": "بارانی از واژه‌های ماندگار",
        "bg": "#070a16",
        "reset": POEM_RAIN_RESET,
        "frame": POEM_RAIN,
        "desc": "ابیات ماندگار فارسی (حافظ، سعدی، خیام، فردوسی، وحشی بافقی) مثل قطره‌های باران آرام می‌بارند — نسخهٔ ۲.۱: رفع کامل «مقطع بودن نوشته‌ها» — هر بیت همیشه یک‌جا و با اتصال کامل حروف رسم می‌شود + فونت وزیرمتن embed در خود بسته + میزبان هم فونت را تزریق می‌کند. تنظیمات قابل تغییر: وضعیت، زمان، بیدارسازی با رمز، سرعت و تراکم.",
    },
    {
        "slug": "hr-ss-night-sky",
        "name": "آسمان شب",
        "subtitle": "ستاره‌ها و شهاب‌های گذرا",
        "bg": "#04081a",
        "reset": NIGHT_SKY_RESET,
        "frame": NIGHT_SKY,
        "desc": "آسمان پرستاره با پلاکیدن ستاره‌ها، هلال ماه و شهاب‌های گذرا. نسخهٔ ۲.۱: فونت وزیر embed + رفع باگ «حروف جدا از هم» — عبارت فارسی یک‌جا و با اتصال کامل حروف رسم می‌شود. تنظیمات قابل تغییر: وضعیت، زمان، بیدارسازی با رمز، سرعت و تراکم.",
    },
    {
        "slug": "hr-ss-aurora",
        "name": "شفق قطبی",
        "subtitle": "رقص نور در آسمان شمال",
        "bg": "#02040c",
        "reset": AURORA_RESET,
        "frame": AURORA,
        "desc": "سه روبان نور شفق قطبی که آرام بر فراز آسمان تاریک موج می‌زنند. نسخهٔ ۲.۱: فونت وزیر embed + رفع باگ اتصال حروف فارسی. تنظیمات قابل تغییر: وضعیت، زمان، بیدارسازی با رمز، سرعت و تراکم.",
    },
    {
        "slug": "hr-ss-calm-waves",
        "name": "امواج آرام",
        "subtitle": "دریای آرام زیر مهتاب",
        "bg": "#03102a",
        "reset": WAVES_RESET,
        "frame": WAVES,
        "desc": "چهار لایه موج آرام با مهتاب و کف‌های ملایم روی موج جلو. نسخهٔ ۲.۱: فونت وزیر embed + رفع باگ اتصال حروف فارسی. تنظیمات قابل تغییر: وضعیت، زمان، بیدارسازی با رمز، سرعت و تراکم.",
    },
    {
        "slug": "hr-ss-persian-desert",
        "name": "کویر ایران",
        "subtitle": "غروب بر تپه‌های شنی",
        "bg": "#7a2d3a",
        "reset": DESERT_RESET,
        "frame": DESERT,
        "desc": "غروب کویری با ستاره‌های نو ظهور و سه لایه تپهٔ شنی با رانش آرام. نسخهٔ ۲.۱: فونت وزیر embed + رفع باگ اتصال حروف فارسی. تنظیمات قابل تغییر: وضعیت، زمان، بیدارسازی با رمز، سرعت و تراکم.",
    },
    {
        "slug": "hr-ss-spiral-galaxy",
        "name": "کهکشان مارپیچ",
        "subtitle": "چرخش ستارگان در گرداب نور",
        "bg": "#030309",
        "reset": GALAXY_RESET,
        "frame": GALAXY,
        "desc": "کهکشان مارپیچ با هستهٔ درخشان و چرخش تفکیکی بازوها. نسخهٔ ۲.۱: فونت وزیر embed + رفع باگ اتصال حروف فارسی. تنظیمات قابل تغییر: وضعیت، زمان، بیدارسازی با رمز، سرعت و تراکم.",
    },
]

# ----------------------------------------------------------------------
# ساخت + اعتبارسنجی سفت (همان قواعد درون‌ریزی سامانه)
# ----------------------------------------------------------------------
def build_js(p):
    js = (JS_HEAD
          .replace("@@FONT_REG@@", FONT_REG_B64)
          .replace("@@FONT_BOLD@@", FONT_BOLD_B64)
          .replace("@@TITLE@@", p["name"])
          .replace("@@SUBTITLE@@", p["subtitle"])
          .replace("@@RESET@@", p["reset"])
          .replace("@@FRAME@@", p["frame"]))
    return js

def build_css(p):
    return CSS_TPL.replace("@@BG@@", p["bg"])

def main():
    os.makedirs(OUT, exist_ok=True)
    ok = True
    print(f"{'slug':24} {'file KB':>8} {'js chars':>9} {'css chars':>9}  checks")
    for p in PKGS:
        js, css = build_js(p), build_css(p)
        bundle = {
            "format": "hr-screensaver-package",
            "spec": 1,
            "meta": {
                "name": p["name"],
                "author": AUTHOR,
                "version": VER,
                "description": p["desc"],
            },
            "code": {"js": js, "css": css},
            "settings": {
                "enabled": True,
                "requirePassword": True,
                "minutes": 1,
                "seconds": 30,
                "speed": 1,
                "density": 1,
            },
        }
        raw = json.dumps(bundle, ensure_ascii=False, indent=1)
        data = raw.encode("utf-8")
        checks = []
        if len(data) <= 1024 * 1024: checks.append("size<=1MB")
        else: ok = False; checks.append("SIZE>1MB!")
        if len(js) <= 200_000: checks.append("js<=200k")
        else: ok = False; checks.append("JS>200k!")
        if len(css) <= 100_000: checks.append("css<=100k")
        else: ok = False; checks.append("CSS>100k!")
        bad = [b for b in ("</script", "</style", "<!--") if b in js.lower() or b in css.lower()]
        if not bad: checks.append("no-bad-seq")
        else: ok = False; checks.append(f"BAD-SEQ:{bad}")
        parsed = json.loads(raw)
        if parsed["format"] == "hr-screensaver-package" and parsed["spec"] == 1 and parsed["meta"]["name"]:
            checks.append("format/spec")
        else:
            ok = False; checks.append("FORMAT!")
        for k in ("enabled", "requirePassword", "minutes", "seconds"):
            if k not in parsed["settings"]:
                ok = False; checks.append(f"MISSING-SETTING:{k}")
        fp = f"{OUT}/{p['slug']}.json"
        with open(fp, "w", encoding="utf-8") as f:
            f.write(raw)
        print(f"{p['slug']:24} {len(data)/1024:8.1f} {len(js):9d} {len(css):9d}  {', '.join(checks)}")
    readme = f"""# بسته‌های محافظ صفحه — قالب hr-screensaver-package (spec 1)

این پوشه بخشی از سورس است و با کامیت ماندگار می‌ماند (درخواست کاربر).
هر فایل `.json` یک بستهٔ کامل محافظ صفحه است: متادیتا + کد JS/CSS + تنظیمات.

## نسخهٔ ۲.۱ — رفع کامل «حروف جدا/مقطع» + بستهٔ «باران شعر»

اگر متن فارسیِ محافظ به شکل «آ س م ا ن ش ب» یا مقطع دیده می‌شد، علت رسم
حرف‌به‌حرف یا نبودن فونت فارسی در سندباکس بود. در این نسخه:

1. هر عبارت فارسی همیشه با **یک فراخوان `fillText` روی کل رشته** رسم می‌شود؛
   اتصال حروف با جداول OpenType فونت انجام می‌شود.
2. رسم حرف‌به‌حرف و `letter-spacing` در هیچ‌کجای کد بسته وجود ندارد.
3. **فونت وزیرمتن (Regular + Bold) داخل خود بسته embed شده** (base64 + FontFace API).
4. از نسخهٔ سامانهٔ 2.7.18 به بعد، **میزبان هم فونت وزیر را به Runtime تزریق می‌کند** —
   پس بسته‌های قدیمی‌تر که با ارجاع به `Vazirmatn` رسم می‌کنند هم اصلاح می‌شوند.
5. بستهٔ جدید **«باران شعر»**: ابیات ماندگار فارسی مثل قطره‌های باران.

## بسته‌ها

| فایل | تم |
| --- | --- |
| hr-ss-poem-rain.json | باران شعر — ابیات ماندگار فارسی |
| hr-ss-night-sky.json | آسمان شب — ستاره‌ها، هلال ماه، شهاب |
| hr-ss-aurora.json | شفق قطبی — سه روبان نور |
| hr-ss-calm-waves.json | امواج آرام — دریای مهتابی |
| hr-ss-persian-desert.json | کویر ایران — غروب و تپه‌های شنی |
| hr-ss-spiral-galaxy.json | کهکشان مارپیچ — چرخش تفکیکی |

## درون‌ریزی

تنظیمات ← تب «محافظ صفحه» ← بخش «بسته‌های محافظ صفحه» ← «درون‌ریزی بسته»
(نیازمند مجوز settings.manage). با چک‌باکس «اعمال تنظیمات همراه بسته»، چهار فیلد
محافظ (وضعیت/دقیقه/ثانیه/بیدارسازی با رمز) هم از بسته اعمال می‌شود؛ سرعت و تراکم
درون تنظیمات بسته می‌مانند و کد بسته با مقادیر پیش‌فرض یا مقادیر ذخیره‌شده اجرا می‌شود.

امنیت: کد بسته فقط در iframe سندباکس‌شدهٔ `/Settings/ScreensaverRuntime` اجرا می‌شود —
بدون allow-same-origin، با CSP «connect-src 'none'» و پالایش دنباله‌های تگ در درون‌ریزی.
"""
    with open(f"{OUT}/README.md", "w", encoding="utf-8") as f:
        f.write(readme)
    print("README.md written")
    if not ok:
        print("VALIDATION FAILED"); sys.exit(1)
    print("ALL PACKAGES OK")

if __name__ == "__main__":
    main()
