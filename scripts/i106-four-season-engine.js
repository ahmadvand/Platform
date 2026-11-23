/* بستهٔ محافظ صفحهٔ «چهارفصل» — موتور صحنه‌های زنده (فاز ۱۰۶، درخواست کاربر)
 * ---------------------------------------------------------------
 * در iframe سندباکس‌شده اجرا می‌شود (بدون شبکه/کوکی/DOM سامانه).
 * payload (#hr-ss-data): { lang, name, season: auto|spring|summer|autumn|winter, cal: jalali|gregorian|hijri }
 * صحنه‌ها: بهار (شکوفه/زنبور/پروانه/رفتگر باغ)، تابستان (خورشید/ابر/میوهٔ ریزان/پرستو)،
 * پاییز (برگ‌ریزان چندشکلی/تندباد/مه)، زمستان (برف سه‌لایه/مه‌زمینی/یخ‌زدگی/دلیجان).
 * پس‌زمینه: چهار عکس Wikimedia Commons با مجوز آزاد CC BY-SA + ذکر منبع چرخشی در گوشه.
 * ضدتکرار: هر ذره با پارامترهای تصادفی (اندازه/رنگ/سرعت/فاز/شکل) + رخدادهای تصادفی زمان‌بندی‌شده.
 */
(function () {
    'use strict';

    // ---------- payload ----------
    var D = {};
    try {
        var el = document.getElementById('hr-ss-data');
        if (el) D = JSON.parse(el.textContent || '{}');
    } catch (e) { }
    var LANG = String(D.lang || 'fa').toLowerCase();
    var CAL = String(D.cal || 'jalali').toLowerCase();
    var CFG = String(D.season || 'auto').toLowerCase();
    if (CFG !== 'spring' && CFG !== 'summer' && CFG !== 'autumn' && CFG !== 'winter') CFG = 'auto';

    // ---------- بومی‌سازی کوتاه ----------
    var SEASON_NAME = {
        spring: { fa: 'بهار', en: 'Spring', ru: 'Весна', ar: 'الربيع', ico: '🌸' },
        summer: { fa: 'تابستان', en: 'Summer', ru: 'Лето', ar: 'الصيف', ico: '☀️' },
        autumn: { fa: 'پاییز', en: 'Autumn', ru: 'Осень', ar: 'الخريف', ico: '🍂' },
        winter: { fa: 'زمستان', en: 'Winter', ru: 'Зима', ar: 'الشتاء', ico: '❄️' }
    };
    var LBL_CREDIT = { fa: 'عکس:', en: 'Photo:', ru: 'Фото:', ar: 'صورة:' };
    var LBL_AUTO = { fa: 'خودکار', en: 'auto', ru: 'авто', ar: 'تلقائي' };
    var LBL_MANUAL = { fa: 'دستی', en: 'manual', ru: 'вручную', ar: 'يدوي' };
    function T(o) { return o[LANG] || o.fa || o.en; }
    var EAST = { fa: '۰۱۲۳۴۵۶۷۸۹', prs: '۰۱۲۳۴۵۶۷۸۹', ps: '۰۱۲۳۴۵۶۷۸۹', ar: '٠١٢٣٤٥٦٧٨٩' };
    function dg(s) {
        var map = EAST[LANG];
        if (!map) return s;
        return String(s).replace(/[0-9]/g, function (c) { return map[+c]; });
    }

    // ---------- تشخیص فصل (خودکار — بر اساس تاریخ و تقویم سامانه) ----------
    function detectSeason() {
        var m = 0;
        if (CAL === 'jalali') {
            try { m = parseInt(new Intl.DateTimeFormat('en-u-ca-persian', { month: 'numeric' }).format(new Date()), 10); } catch (e) { }
        }
        if (!(m >= 1 && m <= 12)) {
            try { m = new Date().getMonth() + 1; } catch (e) { m = 4; }   // تقویم میلادی/ fallback
        }
        if (CAL === 'jalali') return m <= 3 ? 'spring' : m <= 6 ? 'summer' : m <= 9 ? 'autumn' : 'winter';
        return (m >= 3 && m <= 5) ? 'spring' : (m >= 6 && m <= 8) ? 'summer' : (m >= 9 && m <= 11) ? 'autumn' : 'winter';
    }

    // ---------- عکس‌های پس‌زمینه (جاسازی‌شده — data URI؛ مجوز آزاد + ذکر منبع) ----------
    // PHOTOS_PH توسط سازندهٔ بسته جای‌گذاری می‌شود
    var PHOTOS = /*PHOTOS_PH*/{};

    // ---------- بوم ----------
    var cv = document.createElement('canvas');
    document.body.appendChild(cv);
    var ctx = cv.getContext('2d');
    var W = 0, H = 0, DPR = 1;
    function resize() {
        DPR = Math.min(window.devicePixelRatio || 1, 2);
        W = window.innerWidth; H = window.innerHeight;
        cv.width = Math.round(W * DPR); cv.height = Math.round(H * DPR);
        ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
    }
    window.addEventListener('resize', resize);
    resize();

    // ---------- ابزار تصادفی (ضدتکرار) ----------
    function R(a, b) { return a + Math.random() * (b - a); }
    function RI(a, b) { return Math.floor(R(a, b + 1)); }
    function pick(a) { return a[RI(0, a.length - 1)]; }
    var TAU = Math.PI * 2;

    // ---------- تصاویر ----------
    var imgs = {};
    var loadedCount = 0, totalPhotos = 0;
    for (var k in PHOTOS) {
        totalPhotos++;
        (function (key) {
            var im = new Image();
            im.onload = function () { loadedCount++; };
            im.src = PHOTOS[key].src;
            imgs[key] = im;
        })(k);
    }

    // پوشش «cover» با حرکت آهستهٔ کِن‌برنز + تاریکی ملایم لبه‌ها برای خوانایی
    function drawCover(key, t, alpha) {
        var im = imgs[key];
        if (!im || !im.complete || !im.naturalWidth) return;
        var ph = key.length * 2.1;
        var z = 1.05 + 0.035 * Math.sin(t * 0.02 + ph);
        var iw = im.naturalWidth, ih = im.naturalHeight;
        var s = Math.max(W / iw, H / ih) * z;
        var dw = iw * s, dh = ih * s;
        var dx = (W - dw) / 2 + Math.sin(t * 0.013 + ph) * W * 0.015;
        var dy = (H - dh) / 2 + Math.cos(t * 0.011 + ph) * H * 0.012;
        ctx.globalAlpha = alpha;
        ctx.drawImage(im, dx, dy, dw, dh);
        ctx.globalAlpha = 1;
    }
    function drawVignette() {
        var g = ctx.createRadialGradient(W / 2, H / 2, Math.min(W, H) * 0.42, W / 2, H / 2, Math.max(W, H) * 0.78);
        g.addColorStop(0, 'rgba(0,0,0,0)');
        g.addColorStop(1, 'rgba(0,0,0,0.38)');
        ctx.fillStyle = g;
        ctx.fillRect(0, 0, W, H);
        var b = ctx.createLinearGradient(0, H * 0.72, 0, H);
        b.addColorStop(0, 'rgba(0,0,0,0)');
        b.addColorStop(1, 'rgba(0,0,0,0.30)');
        ctx.fillStyle = b;
        ctx.fillRect(0, H * 0.72, W, H * 0.28);
    }

    // ---------- صحنه‌ها ----------
    var scenes = {};      // key → {parts:[], actors:[], fx:[]}
    var curKey = null, prevKey = null, transT0 = -1;
    var now = 0;

    function makeScene(key) {
        var s = { key: key, parts: [], actors: [], fx: [], pile: [], spawnT: 0 };
        var i;
        if (key === 'spring') {
            for (i = 0; i < 52; i++) s.parts.push(newPetal(true));
            s.actors.push(newBee(), newBee(), newButterfly(), newButterfly(), newSweeper());
        } else if (key === 'summer') {
            for (i = 0; i < 26; i++) s.parts.push(newGlint(true));
            s.actors.push(newSun(), newCloud(true), newCloud(true), newCloud(true), newFruitDrop(), newButterfly(true), newBee(), newSwallow());
        } else if (key === 'autumn') {
            for (i = 0; i < 62; i++) s.parts.push(newLeaf(true));
            s.actors.push(newGust('autumn'), newFogBand(true), newFogBand(true), newSweeper(true));
        } else {
            for (i = 0; i < 126; i++) s.parts.push(newFlake(true));
            s.actors.push(newFogBand(true), newFogBand(true), newFrost(), newSparkle(true), newRobin());
        }
        return s;
    }

    // ===== ذرات — هر بار با پارامترهای تازه (ضدتکرار) =====

    // — گلبرگ بهار: سه شکل بزیه + هیوز شید + نوسان بال‌مانند
    function newPetal(anyY) {
        var shapes = [0, 1, 2];
        return {
            x: R(0, W), y: anyY ? R(-H * 0.2, H) : R(-40, -10),
            s: R(5, 13), hue: RI(330, 352), lit: R(80, 96),
            vy: R(18, 44), vx: R(-14, 14), sw: R(14, 42), ph: R(0, TAU), phv: R(0.6, 1.6),
            rot: R(0, TAU), rv: R(-1.6, 1.6), fl: R(2.2, 4.6), shape: pick(shapes), life: -1
        };
    }
    function drawPetal(p, dt) {
        p.ph += p.phv * dt;
        p.x += (p.vx + Math.sin(p.ph) * p.sw * 0.28) * dt;
        p.y += p.vy * dt;
        p.rot += p.rv * dt;
        var flut = 0.55 + 0.45 * Math.sin(p.ph * p.fl);
        if (p.y > H + 16) { var np = newPetal(false); copyTo(p, np); return; }
        ctx.save();
        ctx.translate(p.x, p.y);
        ctx.rotate(p.rot);
        ctx.scale(1, 0.35 + 0.65 * flut);
        ctx.fillStyle = 'hsl(' + p.hue + ',85%,' + p.lit + '%)';
        ctx.beginPath();
        if (p.shape === 0) { ctx.moveTo(0, -p.s); ctx.bezierCurveTo(p.s, -p.s, p.s, p.s * 0.6, 0, p.s); ctx.bezierCurveTo(-p.s, p.s * 0.6, -p.s, -p.s, 0, -p.s); }
        else if (p.shape === 1) { ctx.moveTo(0, -p.s * 1.3); ctx.quadraticCurveTo(p.s * 0.9, 0, 0, p.s * 1.3); ctx.quadraticCurveTo(-p.s * 0.9, 0, 0, -p.s * 1.3); }
        else { ctx.ellipse(0, 0, p.s * 0.8, p.s * 1.25, 0, 0, TAU); }
        ctx.fill();
        ctx.restore();
    }

    // — برگ پاییز: چهار شکل + پالت رنگی متنوع + واژگش سه‌بعدی‌نما
    var LEAF_COLS = [[196, 62, 44], [214, 92, 24], [232, 132, 32], [244, 180, 60], [150, 96, 30], [188, 48, 60]];
    function newLeaf(anyY) {
        return {
            x: R(0, W), y: anyY ? R(-H * 0.2, H) : R(-40, -10),
            s: R(7, 16), col: pick(LEAF_COLS), jit: RI(-14, 14),
            vy: R(26, 60), vx: R(-18, 18), sw: R(18, 52), ph: R(0, TAU), phv: R(0.5, 1.4),
            rot: R(0, TAU), rv: R(-2.2, 2.2), fx: R(1.1, 2.4), shape: RI(0, 3), life: -1
        };
    }
    function drawLeaf(p, dt, wind) {
        p.ph += p.phv * dt;
        p.x += (p.vx + Math.sin(p.ph) * p.sw * 0.3 + wind * 42) * dt;
        p.y += (p.vy + Math.abs(wind) * 8) * dt;
        p.rot += (p.rv + wind * 1.6) * dt;
        if (p.y > H - 6 && Math.abs(wind) < 0.5 && p.s > 9 && scenes.autumnPile(p.x, p.s)) {
            var d = newLeaf(false); copyTo(p, d); return;
        }
        if (p.y > H + 18) { var np = newLeaf(false); copyTo(p, np); return; }
        var squash = 0.35 + 0.65 * Math.abs(Math.sin(p.ph * p.fx));
        ctx.save();
        ctx.translate(p.x, p.y);
        ctx.rotate(p.rot);
        ctx.scale(squash, 1);
        var c = p.col;
        ctx.fillStyle = 'rgb(' + clamp255(c[0] + p.jit) + ',' + clamp255(c[1] + p.jit) + ',' + clamp255(c[2] + p.jit) + ')';
        ctx.beginPath();
        var s = p.s;
        if (p.shape === 0) {   // شبهٔ افرا
            for (var a = 0; a < 5; a++) {
                var ang = -Math.PI / 2 + a * TAU / 5;
                var ang2 = ang + TAU / 10;
                ctx.lineTo(Math.cos(ang) * s, Math.sin(ang) * s);
                ctx.lineTo(Math.cos(ang2) * s * 0.45, Math.sin(ang2) * s * 0.45);
            }
            ctx.closePath();
        } else if (p.shape === 1) { ctx.moveTo(0, -s * 1.4); ctx.quadraticCurveTo(s, 0, 0, s * 1.4); ctx.quadraticCurveTo(-s, 0, 0, -s * 1.4); }
        else if (p.shape === 2) { ctx.ellipse(0, 0, s * 0.55, s * 1.3, 0, 0, TAU); }
        else { ctx.moveTo(0, -s * 1.2); ctx.bezierCurveTo(s * 1.1, -s * 0.2, s * 0.5, s, 0, s * 1.3); ctx.bezierCurveTo(-s * 0.5, s, -s * 1.1, -s * 0.2, 0, -s * 1.2); }
        ctx.fill();
        ctx.restore();
    }
    function clamp255(v) { return v < 0 ? 0 : v > 255 ? 255 : Math.round(v); }

    // — برف زمستان: سه لایه پارالاکس + نسیم سراسری
    function newFlake(anyY) {
        var layer = RI(0, 2);   // 0 دور، 2 نزدیک
        return {
            x: R(0, W), y: anyY ? R(-H * 0.2, H) : R(-30, -6),
            r: [R(0.8, 1.8), R(1.6, 3.2), R(2.8, 5.4)][layer],
            vy: [R(14, 24), R(28, 46), R(50, 84)][layer],
            sw: R(8, 26), ph: R(0, TAU), phv: R(0.4, 1.2), al: [R(0.35, 0.6), R(0.55, 0.8), R(0.75, 0.95)][layer], life: -1
        };
    }
    function drawFlake(p, dt, wind) {
        p.ph += p.phv * dt;
        p.y += p.vy * dt;
        p.x += (Math.sin(p.ph) * p.sw * 0.35 + wind * 60) * dt;
        if (p.x > W + 8) p.x = -8; else if (p.x < -8) p.x = W + 8;
        if (p.y > H + 8) { var np = newFlake(false); copyTo(p, np); return; }
        ctx.globalAlpha = p.al;
        ctx.fillStyle = '#ffffff';
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, TAU);
        ctx.fill();
        ctx.globalAlpha = 1;
    }

    // — درخشش‌های سطحی تابستان (روی عکس)
    function newGlint(anyY) {
        return { x: R(0, W), y: anyY ? R(0, H) : R(0, H), r: R(1, 2.6), ph: R(0, TAU), phv: R(1.4, 3.2), life: -1 };
    }
    function drawGlint(p, dt) {
        p.ph += p.phv * dt;
        var a = Math.max(0, Math.sin(p.ph));
        if (a > 0.02) {
            ctx.globalAlpha = a * 0.55;
            ctx.fillStyle = '#fff8d9';
            ctx.beginPath();
            ctx.arc(p.x, p.y, p.r, 0, TAU);
            ctx.fill();
            ctx.globalAlpha = 1;
        }
        if (Math.sin(p.ph) < -0.9 && Math.random() < 0.06) { p.x = R(0, W); p.y = R(0, H); }
    }

    // ===== بازیگران =====

    // — زنبور: بدنهٔ راه‌راه + بال‌های لرزان + هدف‌گردی تصادفی
    function newBee() {
        return { kind: 'bee', x: R(W * 0.2, W * 0.8), y: R(H * 0.15, H * 0.6), tx: 0, ty: 0, t0: 0, ph: R(0, TAU), size: R(5, 7.5), life: -1 };
    }
    function drawBee(a, dt, t) {
        if (t >= a.t0) { a.tx = R(W * 0.08, W * 0.92); a.ty = R(H * 0.1, H * 0.68); a.t0 = t + R(1.6, 3.4); }
        var dx = a.tx - a.x, dy = a.ty - a.y, d = Math.sqrt(dx * dx + dy * dy) || 1;
        var v = 42 + 14 * Math.sin(t * 3 + a.ph);
        a.x += dx / d * v * dt + Math.sin(t * 7 + a.ph) * 10 * dt;
        a.y += dy / d * v * dt + Math.cos(t * 6.4 + a.ph) * 12 * dt;
        var s = a.size, wf = Math.sin(t * 26 + a.ph);
        ctx.save();
        ctx.translate(a.x, a.y);
        ctx.rotate(Math.atan2(dy, dx) * 0.35);
        ctx.globalAlpha = 0.5;
        ctx.fillStyle = '#dfe9f2';
        ctx.beginPath();
        ctx.ellipse(-s * 0.2, -s * 0.5, s * 0.75, s * 0.32 + wf * s * 0.22, -0.5, 0, TAU);
        ctx.ellipse(-s * 0.2, s * 0.5, s * 0.75, s * 0.32 - wf * s * 0.22, 0.5, 0, TAU);
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.fillStyle = '#e2a63a';
        ctx.beginPath();
        ctx.ellipse(0, 0, s, s * 0.62, 0, 0, TAU);
        ctx.fill();
        ctx.fillStyle = '#3a2c14';
        ctx.fillRect(-s * 0.15, -s * 0.62, s * 0.3, s * 1.24);
        ctx.fillRect(-s * 0.62, -s * 0.55, s * 0.26, s * 1.1);
        ctx.beginPath();
        ctx.arc(s * 0.92, 0, s * 0.42, 0, TAU);
        ctx.fill();
        ctx.restore();
    }

    // — پروانه: دو بال آینه‌ای + بال‌زدگی + هبوط گاه‌به‌گاه
    function newButterfly(summer) {
        return {
            kind: 'bfly', x: R(W * 0.15, W * 0.85), y: R(H * 0.12, H * 0.6),
            tx: 0, ty: 0, t0: 0, ph: R(0, TAU), s: R(7, 11),
            col: summer ? pick(['#f7f3d8', '#f6d365', '#fbe7a1']) : pick(['#c9a7f5', '#f5a7c3', '#a7c9f5', '#f5c9a7']),
            perchUntil: 0, life: -1
        };
    }
    function drawButterfly(a, dt, t) {
        var wing = Math.sin(t * (a.perchUntil > t ? 1.6 : 9) + a.ph);
        if (a.perchUntil > t) { /* نشسته — فقط بال آرام */ }
        else {
            if (t >= a.t0) {
                if (Math.random() < 0.18) { a.perchUntil = t + R(2, 4); a.tx = a.x; a.ty = a.y; }
                else { a.tx = R(W * 0.08, W * 0.92); a.ty = R(H * 0.08, H * 0.66); a.t0 = t + R(2.4, 5); }
            }
            var dx = a.tx - a.x, dy = a.ty - a.y, d = Math.sqrt(dx * dx + dy * dy) || 1;
            a.x += dx / d * 26 * dt + Math.sin(t * 2.2 + a.ph) * 16 * dt;
            a.y += dy / d * 26 * dt + Math.cos(t * 1.9 + a.ph) * 14 * dt;
        }
        ctx.save();
        ctx.translate(a.x, a.y);
        var s = a.s;
        ctx.fillStyle = a.col;
        ctx.globalAlpha = 0.9;
        ctx.save();
        ctx.scale(Math.max(0.25, Math.abs(wing)), 1);
        ctx.beginPath();
        ctx.ellipse(s * 0.62, -s * 0.3, s * 0.62, s * 0.46, 0.5, 0, TAU);
        ctx.ellipse(s * 0.55, s * 0.42, s * 0.5, s * 0.38, -0.35, 0, TAU);
        ctx.fill();
        ctx.restore();
        ctx.save();
        ctx.scale(Math.max(0.25, Math.abs(wing)), 1);
        ctx.scale(-1, 1);
        ctx.beginPath();
        ctx.ellipse(s * 0.62, -s * 0.3, s * 0.62, s * 0.46, 0.5, 0, TAU);
        ctx.ellipse(s * 0.55, s * 0.42, s * 0.5, s * 0.38, -0.35, 0, TAU);
        ctx.fill();
        ctx.restore();
        ctx.globalAlpha = 1;
        ctx.fillStyle = '#3d2b1f';
        ctx.beginPath();
        ctx.ellipse(0, 0, s * 0.16, s * 0.62, 0, 0, TAU);
        ctx.fill();
        ctx.restore();
    }

    // — رفتگر باغ (بهار) / رفتگر برگ (پاییز): سیلوئت + جاروی متحرک + انبارش/جاروی توده
    function newSweeper(pile) {
        return { kind: 'swp', x: R(W * 0.2, W * 0.8), dir: Math.random() < 0.5 ? 1 : -1, ph: R(0, TAU), pileMode: !!pile, life: -1 };
    }
    function drawSweeper(a, dt, t) {
        var speed = 26;
        a.x += a.dir * speed * dt;
        if (a.x > W * 0.9) a.dir = -1;
        if (a.x < W * 0.1) a.dir = 1;
        var y0 = H - 14;
        var sweep = Math.sin(t * 2.6 + a.ph);
        ctx.save();
        ctx.translate(a.x, y0);
        ctx.scale(a.dir, 1);
        ctx.fillStyle = 'rgba(15,20,26,0.72)';
        // پاها
        ctx.fillRect(-6, -26, 4, 26);
        ctx.fillRect(2, -26, 4, 26);
        // تنه
        ctx.fillRect(-7, -52, 14, 28);
        // سر
        ctx.beginPath();
        ctx.arc(0, -58, 7, 0, TAU);
        ctx.fill();
        // دست + جارو با زاویهٔ متغیر
        ctx.strokeStyle = 'rgba(15,20,26,0.72)';
        ctx.lineWidth = 3.4;
        ctx.beginPath();
        ctx.moveTo(4, -46);
        ctx.lineTo(18, -34 + sweep * 5);
        ctx.stroke();
        ctx.strokeStyle = 'rgba(96,68,38,0.9)';
        ctx.lineWidth = 2.6;
        ctx.beginPath();
        ctx.moveTo(18, -34 + sweep * 5);
        ctx.lineTo(30, -8 + sweep * 12);
        ctx.stroke();
        // دستهٔ جارو (مه‌های ریز)
        ctx.fillStyle = 'rgba(255,255,255,0.35)';
        var i;
        for (i = 0; i < 3; i++) {
            var mx = 30 + i * 3.2, my = -4 + sweep * 12;
            ctx.beginPath();
            ctx.arc(mx, my, 1.6 + Math.sin(t * 9 + i) * 0.7, 0, TAU);
            ctx.fill();
        }
        ctx.restore();
        // جاروی تودهٔ برگ (پاییز): نزدیک رفتگر، برجستگی‌ها کم می‌شوند
        var sc = scenes[curKey];
        if (sc && sc.pile && sc.pile.length) {
            for (i = 0; i < sc.pile.length; i++) {
                var b = sc.pile[i];
                if (Math.abs(b.x - a.x) < 34 && b.h > 1) b.h = Math.max(1, b.h - dt * 9);
            }
        }
    }

    // — خورشید تابستان: قرص درخشان + پرتوهای چرخان
    function newSun() {
        return { kind: 'sun', life: -1 };
    }
    function drawSun(a, dt, t) {
        var cx = W * 0.82, cy = H * 0.18, r = Math.min(W, H) * 0.075;
        ctx.save();
        ctx.translate(cx, cy);
        ctx.rotate(t * 0.05);
        ctx.globalAlpha = 0.24;
        ctx.fillStyle = '#ffd76a';
        var i;
        for (i = 0; i < 12; i++) {
            ctx.rotate(TAU / 12);
            ctx.beginPath();
            ctx.moveTo(r * 1.15, 0);
            ctx.lineTo(r * (2.0 + 0.35 * Math.sin(t * 0.9 + i)), -r * 0.16);
            ctx.lineTo(r * (2.0 + 0.35 * Math.sin(t * 0.9 + i)), r * 0.16);
            ctx.closePath();
            ctx.fill();
        }
        ctx.globalAlpha = 0.9;
        var g = ctx.createRadialGradient(0, 0, r * 0.2, 0, 0, r);
        g.addColorStop(0, '#fff3c4');
        g.addColorStop(0.7, '#ffd76a');
        g.addColorStop(1, 'rgba(255,205,90,0)');
        ctx.fillStyle = g;
        ctx.beginPath();
        ctx.arc(0, 0, r, 0, TAU);
        ctx.fill();
        ctx.restore();
        ctx.globalAlpha = 1;
    }

    // — ابر تابستان: خوشهٔ قوس‌ها با سرعت/مقیاس/شفافیت تصادفی (پارالاکس)
    function newCloud(anyY) {
        return {
            kind: 'cloud', x: R(-W * 0.2, W), y: anyY ? R(H * 0.05, H * 0.45) : R(H * 0.05, H * 0.4),
            s: R(0.6, 1.5), v: R(6, 20), al: R(0.14, 0.32), puffs: RI(4, 7), seed: R(0, TAU), life: -1
        };
    }
    function drawCloud(a, dt, t) {
        a.x -= a.v * dt;
        if (a.x < -W * 0.3) { var nc = newCloud(false); a.x = nc.x + W * 1.25; a.y = nc.y; a.s = nc.s; a.v = nc.v; a.al = nc.al; a.puffs = nc.puffs; a.seed = nc.seed; }
        ctx.save();
        ctx.translate(a.x, a.y);
        ctx.scale(a.s, a.s);
        ctx.fillStyle = 'rgba(255,255,255,' + a.al + ')';
        var i, off = a.seed;
        for (i = 0; i < a.puffs; i++) {
            var px = i * 26 - 60 + Math.sin(off + i * 2.7) * 8;
            var py = Math.sin(off * 1.7 + i * 1.9) * 7;
            var pr = 18 + Math.cos(off + i * 3.1) * 7;
            ctx.beginPath();
            ctx.arc(px, py, pr, 0, TAU);
            ctx.fill();
        }
        ctx.restore();
    }

    // — میوهٔ ریزان تابستان: سقوط با جاذبه + دو جهش + غلت + محو
    function newFruitDrop() {
        return { kind: 'fruit', x: 0, y: 0, v: 0, born: -1, r: R(7, 11), col: pick(['#d84b3a', '#e2673a', '#a8c93a', '#e2b13a']), rot: 0, rv: 0, life: -1 };
    }
    function drawFruit(a, dt, t) {
        if (a.born < 0) {
            if (Math.random() < dt * 0.16) { a.born = t; a.x = R(W * 0.1, W * 0.9); a.y = -20; a.v = R(30, 60); a.rv = R(-2.4, 2.4); }
            return;
        }
        a.v += 240 * dt;
        a.y += a.v * dt;
        a.x += a.rv * 14 * dt;
        a.rot += a.rv * dt;
        var floor = H - 8;
        if (a.y > floor) {
            a.y = floor;
            if (Math.abs(a.v) > 60) { a.v = -a.v * 0.38; a.rv *= 0.6; }
            else { a.v = 0; a.rv *= 0.8; }
        }
        var age = t - a.born;
        var al = age > 7 ? Math.max(0, 1 - (age - 7) / 2) : 1;
        if (al <= 0) { a.born = -1; return; }
        ctx.save();
        ctx.globalAlpha = al;
        ctx.translate(a.x, a.y - a.r);
        ctx.rotate(a.rot);
        ctx.fillStyle = a.col;
        ctx.beginPath();
        ctx.arc(0, 0, a.r, 0, TAU);
        ctx.fill();
        ctx.fillStyle = 'rgba(255,255,255,0.5)';
        ctx.beginPath();
        ctx.arc(-a.r * 0.35, -a.r * 0.35, a.r * 0.22, 0, TAU);
        ctx.fill();
        ctx.fillStyle = '#4e7a2a';
        ctx.beginPath();
        ctx.ellipse(a.r * 0.3, -a.r * 0.9, a.r * 0.42, a.r * 0.2, 0.6, 0, TAU);
        ctx.fill();
        ctx.restore();
        ctx.globalAlpha = 1;
    }

    // — پرستو/پرنده: سیلوئت دو-قوس با بال‌زدگی، عبور تصادفی
    function newSwallow() { return { kind: 'bird', x: -40, y: 0, born: -1, v: R(60, 110), ph: R(0, TAU), s: R(9, 14), life: -1 }; }
    function newRobin() { return { kind: 'bird', x: -40, y: 0, born: -1, v: R(50, 90), ph: R(0, TAU), s: R(8, 11), life: -1 }; }
    function drawBird(a, dt, t) {
        if (a.born < 0) {
            if (Math.random() < dt * 0.05) { a.born = t; a.y = R(H * 0.12, H * 0.4); a.x = -40; }
            return;
        }
        a.x += a.v * dt;
        a.y += Math.sin(t * 0.9 + a.ph) * 14 * dt;
        var flap = Math.sin(t * 9 + a.ph);
        var s = a.s;
        ctx.save();
        ctx.translate(a.x, a.y);
        ctx.fillStyle = 'rgba(20,24,30,0.78)';
        ctx.beginPath();
        ctx.moveTo(-s, 0);
        ctx.quadraticCurveTo(-s * 0.3, -s * (0.55 + flap * 0.5), s * 0.25, -s * 0.08);
        ctx.quadraticCurveTo(-s * 0.3, s * (0.55 + flap * 0.5), -s, 0);
        ctx.fill();
        ctx.beginPath();
        ctx.ellipse(s * 0.28, 0, s * 0.34, s * 0.18, 0, 0, TAU);
        ctx.fill();
        ctx.restore();
        if (a.x > W + 50) { a.born = -1; }
    }

    // — تندباد پاییز: پنجرهٔ سراسری باد برای ذرات
    function newGust() { return { kind: 'gust', v: 0, tNext: R(4, 10), life: -1 }; }
    function drawGust(a, dt, t) {
        if (t > a.tNext && a.v === 0) { a.v = R(0.8, 1.7) * (Math.random() < 0.5 ? 1 : -1); a.tEnd = t + R(2.2, 4.5); a.tNext = t + R(7, 16); }
        if (a.v !== 0) {
            if (t > a.tEnd) a.v = 0;
        }
        a.cur = a.v * (0.75 + 0.25 * Math.sin(t * 2.2));
        return a.cur || 0;
    }

    // — نوارهای مه (پاییز/زمستان)
    function newFogBand(anyY) {
        return { kind: 'fog', y: anyY ? R(H * 0.55, H * 0.95) : R(H * 0.6, H * 0.95), h: R(40, 110), v: R(8, 22) * (Math.random() < 0.5 ? 1 : -1), al: R(0.10, 0.2), ph: R(0, TAU), life: -1 };
    }
    function drawFog(a, dt, t) {
        a.ph += dt * 0.2;
        a.x = (typeof a.x === 'undefined' ? R(0, W) : a.x);
        a.x += a.v * dt;
        if (a.x > W + W * 0.4) a.x = -W * 0.4;
        if (a.x < -W * 0.4) a.x = W + W * 0.4;
        var g = ctx.createLinearGradient(0, a.y - a.h / 2, 0, a.y + a.h / 2);
        g.addColorStop(0, 'rgba(235,240,245,0)');
        g.addColorStop(0.5, 'rgba(235,240,245,' + a.al + ')');
        g.addColorStop(1, 'rgba(235,240,245,0)');
        ctx.fillStyle = g;
        ctx.fillRect(a.x - W * 0.35, a.y - a.h / 2, W * 0.7, a.h);
        ctx.fillRect(a.x + W * 0.35, a.y - a.h / 2, W * 0.7, a.h);
        ctx.fillRect(a.x - W * 1.05, a.y - a.h / 2, W * 0.7, a.h);
    }

    // — یخ‌زدگی لبه‌ها (زمستان)
    function newFrost() { return { kind: 'frost', life: -1 }; }
    function drawFrost(a, dt, t) {
        var a1 = 0.14 + 0.05 * Math.sin(t * 0.4);
        var g;
        g = ctx.createLinearGradient(0, 0, 0, H * 0.2);
        g.addColorStop(0, 'rgba(240,248,255,' + a1 + ')');
        g.addColorStop(1, 'rgba(240,248,255,0)');
        ctx.fillStyle = g; ctx.fillRect(0, 0, W, H * 0.2);
        g = ctx.createLinearGradient(0, H, 0, H * 0.8);
        g.addColorStop(0, 'rgba(240,248,255,' + (a1 + 0.05) + ')');
        g.addColorStop(1, 'rgba(240,248,255,0)');
        ctx.fillStyle = g; ctx.fillRect(0, H * 0.8, W, H * 0.2);
    }

    // — برق برفِ سطح (زمستان)
    function newSparkle(anyY) {
        return { kind: 'spark', x: R(0, W), y: anyY ? R(H * 0.6, H) : R(H * 0.6, H), ph: R(0, TAU), phv: R(1.2, 2.8), s: R(1, 2.4), life: -1 };
    }
    function drawSparkle(a, dt, t) {
        a.ph += a.phv * dt;
        var s = Math.max(0, Math.sin(a.ph));
        if (s > 0.05) {
            ctx.globalAlpha = s * 0.85;
            ctx.strokeStyle = '#ffffff';
            ctx.lineWidth = 1;
            var l = a.s * 2.6 * s;
            ctx.beginPath();
            ctx.moveTo(a.x - l, a.y); ctx.lineTo(a.x + l, a.y);
            ctx.moveTo(a.x, a.y - l); ctx.lineTo(a.x, a.y + l);
            ctx.stroke();
            ctx.globalAlpha = 1;
        }
        if (Math.sin(a.ph) < -0.95 && Math.random() < 0.08) { a.x = R(0, W); a.y = R(H * 0.6, H); }
    }

    // — تودهٔ انباشتهٔ برگ/گلبرگ در کف صحنه
    scenes.autumnPile = function (x, s) {
        var sc = scenes[curKey];
        if (!sc || sc.pile.length > 46) return false;
        sc.pile.push({ x: x, h: s * 0.9, col: null });
        return true;
    };
    function drawPile(sc) {
        if (!sc.pile || !sc.pile.length) return;
        var i, b;
        for (i = 0; i < sc.pile.length; i++) {
            b = sc.pile[i];
            ctx.fillStyle = 'rgba(120,72,32,0.55)';
            ctx.beginPath();
            ctx.ellipse(b.x, H + 2, b.h * 1.5, b.h, 0, Math.PI, TAU);
            ctx.fill();
        }
    }

    function copyTo(dst, src) {
        for (var k2 in src) dst[k2] = src[k2];
    }

    var DRAW = {
        petal: drawPetal, leaf: drawLeaf, flake: drawFlake, glint: drawGlint,
        bee: drawBee, bfly: drawButterfly, swp: drawSweeper, sun: drawSun,
        cloud: drawCloud, fruit: drawFruit, bird: drawBird, gust: drawGust,
        fog: drawFog, frost: drawFrost, spark: drawSparkle
    };

    // ---------- HUD ----------
    var badgeEl = document.createElement('div');
    badgeEl.style.cssText = 'position:fixed;top:14px;left:16px;display:flex;align-items:center;gap:8px;' +
        'background:rgba(8,12,18,0.42);border:1px solid rgba(255,255,255,0.14);border-radius:999px;' +
        'padding:6px 14px;color:#fff;font:600 15px Vazirmatn,Segoe UI,Tahoma,sans-serif;' +
        'text-shadow:0 1px 4px rgba(0,0,0,0.5);user-select:none;pointer-events:none;z-index:5';
    document.body.appendChild(badgeEl);

    var creditEl = document.createElement('div');
    creditEl.style.cssText = 'position:fixed;bottom:12px;right:16px;max-width:46vw;' +
        'background:rgba(8,12,18,0.38);border-radius:8px;padding:4px 10px;color:rgba(255,255,255,0.82);' +
        'font:11px Vazirmatn,Segoe UI,Tahoma,sans-serif;direction:ltr;text-align:right;' +
        'user-select:none;pointer-events:none;z-index:5;transition:opacity .8s';
    document.body.appendChild(creditEl);
    var creditIdx = 0, creditSwap = 0;
    var creditKeys = [];
    for (var ck in PHOTOS) creditKeys.push(ck);

    function renderBadge(key) {
        var s = SEASON_NAME[key];
        var mode = CFG === 'auto' ? T(LBL_AUTO) : T(LBL_MANUAL);
        badgeEl.innerHTML = '<span style="font-size:17px">' + s.ico + '</span><span>' + T(s) +
            '</span><span style="opacity:.55;font-size:11px">· ' + mode + '</span>';
    }
    function renderCredit(t) {
        if (!creditKeys.length) return;
        if (t >= creditSwap) {
            creditIdx = (creditIdx + 1) % creditKeys.length;
            creditSwap = t + 8;
            var key = creditKeys[creditIdx];
            var p = PHOTOS[key];
            creditEl.style.opacity = '0';
            setTimeout(function () {
                creditEl.textContent = (LBL_CREDIT[LANG] || LBL_CREDIT.fa) + ' ' + p.by +
                    ' — ' + p.lic + ' — Wikimedia Commons';
                creditEl.style.opacity = '1';
            }, 500);
        }
    }

    // ---------- ساعت/تاریخ ریز (مکمل گوشهٔ سامانه، داخل بسته ساده‌تر) ----------
    // صحنهٔ سامانه ساعت دارد؛ اینجا فقط برچسب فصل/منبع کافی است — بدون ساعت، تا دوباره‌کاری نشود.

    // ---------- چرخهٔ اصلی ----------
    var last = 0, seasonCheck = 0, t0 = Date.now();
    function frame(ts) {
        if (document.hidden) { last = ts; requestAnimationFrame(frame); return; }
        var t = (Date.now() - t0) / 1000;
        var dt = Math.min(0.05, t - last || 0.016);
        last = t;
        now = t;

        // تشخیص خودکار فصل — هر ۶۰ ثانیه (نیمه‌شب = گذر فصل)
        if (CFG === 'auto' && t > seasonCheck) {
            seasonCheck = t + 60;
            var want = detectSeason();
            if (want !== curKey) switchTo(want);
        }
        if (!curKey) switchTo(CFG === 'auto' ? detectSeason() : CFG);

        // پس‌زمینه + گذار نرم
        ctx.clearRect(0, 0, W, H);
        ctx.fillStyle = '#05080c';
        ctx.fillRect(0, 0, W, H);
        var trans = transT0 < 0 ? 1 : Math.min(1, (t - transT0) / 1.4);
        if (prevKey && trans < 1) drawCover(prevKey, t, 1 - trans);
        drawCover(curKey, t, prevKey && trans < 1 ? trans : 1);
        if (prevKey && trans >= 1) prevKey = null;
        drawVignette();

        var sc = scenes[curKey];
        var wind = 0;
        var i, a;
        if (sc) {
            // باد سراسری زمستان/پاییز (نسیم پایه + تندباد)
            if (curKey === 'autumn') {
                for (i = 0; i < sc.actors.length; i++) if (sc.actors[i].kind === 'gust') { wind = DRAW.gust(sc.actors[i], dt, t); break; }
            } else if (curKey === 'winter') {
                wind = 0.25 * Math.sin(t * 0.35) + 0.15 * Math.sin(t * 0.9);
            }
            if (sc.pile) drawPile(sc);
            for (i = 0; i < sc.actors.length; i++) {
                a = sc.actors[i];
                if (DRAW[a.kind]) DRAW[a.kind](a, dt, t, wind);
            }
            for (i = 0; i < sc.parts.length; i++) {
                var p = sc.parts[i];
                if (p.t === 'leaf') DRAW.leaf(p, dt, wind);
                else if (p.t === 'flake') DRAW.flake(p, dt, wind);
                else if (p.t === 'petal') DRAW.petal(p, dt);
                else if (p.t === 'glint') DRAW.glint(p, dt);
            }
        }

        renderCredit(t);
        requestAnimationFrame(frame);
    }

    // ---------- گذار فصل ----------
    function switchTo(key) {
        if (key === curKey) return;
        prevKey = curKey;
        curKey = key;
        transT0 = now;
        scenes[key] = makeScene(key);
        // برچسب‌گذاری نوع ذره برای مسیر رسم
        var i, arr = scenes[key].parts;
        for (i = 0; i < arr.length; i++) {
            if (key === 'spring') arr[i].t = 'petal';
            else if (key === 'autumn') arr[i].t = 'leaf';
            else if (key === 'winter') arr[i].t = 'flake';
            else arr[i].t = 'glint';
        }
        renderBadge(key);
    }

    requestAnimationFrame(frame);
})();
