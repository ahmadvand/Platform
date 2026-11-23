/* ==========================================================================
   بستهٔ محافظ صفحهٔ «منظرهٔ چهارفصل» — hr-screensaver-package spec 1 — v2 (فاز ۱۰۷)
   هر فصل متناسب با هوای همان فصل + رخدادهای دوره‌ایِ تصادفی تا صحنه تکراری نشود:
     بهار    = شکوفه‌ریزان + تکه‌پاشی شکوفه از تاج درخت، گل‌های بهاری، زنبور و پروانه که
               «روی گل‌ها» می‌نشینند و وزوز می‌کنند، سبزهٔ نوپا، خورشید ملایم، «باران
               بهاری» دوره‌ای با پاشیدن قطره‌ها روی زمین و «رنگین‌کمان» کوتاه پس از باران،
               رفتگری که شکوفه‌های ریخته را جمع و می‌برد.
     تابستان = باغ میوه‌دار (میوه‌رسانی و افتادن میوه با برگشت)، گل‌های تابستانی + زنبور،
               سنجاقک، دستهٔ پرنده، ابر و خورشید، «بادکنک‌های قاصدک» شناور، لرزش هوای
               گرم نزدیک زمین.
     پاییز   = برگ‌های رنگارنگ با تندباد دوره‌ای، «دستهٔ پرواز پرندگان مهاجر در قالب V»،
               نم‌نم‌باران خاکستری، مه رقیق، رفتگری که برگ‌های ریخته را جمع و می‌برد.
     زمستان  = برف سه‌لایه + مه + برفک‌های درخشان، «آدم‌برفی» با شال و سطل و دستهٔ چوبی،
               گنجشکی که روی زمین لَپ می‌خورد و نوک می‌زند، «خرگوش سفید» که گاهی از صحنه
               می‌گذرد.
   فصل: انتخاب دستی ادمین (تنظیمات سامانه → محافظ صفحه → «فصل منظرهٔ محافظ صفحه») یا
        خودکار از تاریخ شمسی — مقدار از دادهٔ میزبان (hr-ss-data → season) می‌آید.
   عکس‌های پس‌زمینه = تصاویر واقعیِ ساخته‌شده توسط دیگران (اجباری کاربر).
   محیط میزبان: iframe سندباکس — CSP connect-src 'none' — هیچ درخواست شبکه‌ای.
   ========================================================================== */
(function () {
    'use strict';
    // ---------- دادهٔ میزبان (زبان + فصل انتخابی ادمین) ----------
    var host = { lang: 'fa', season: 'auto' };
    try {
        var el = document.getElementById('hr-ss-data');
        if (el) {
            var d0 = JSON.parse(el.textContent || '{}') || {};
            if (d0.lang) host.lang = String(d0.lang).toLowerCase();
            if (d0.season) host.season = String(d0.season).toLowerCase();
        }
    } catch (e) { }

    // ---------- فصل: دستی ادمین، وگرنه از تاریخ شمسی (ماه ۱-۳ بهار، ۴-۶ تابستان، ۷-۹ پاییز، ۱۰-۱۲ زمستان) ----------
    function toJalali(gy, gm, gd) {
        var gdm = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];
        var jy = (gy <= 1600) ? 0 : 979;
        gy -= (gy <= 1600) ? 621 : 1600;
        var gy2 = (gm > 2) ? (gy + 1) : gy;
        var days = (365 * gy) + Math.floor((gy2 + 3) / 4) - Math.floor((gy2 + 99) / 100)
            + Math.floor((gy2 + 399) / 400) - 80 + gd + gdm[gm - 1];
        jy += 33 * Math.floor(days / 12053); days %= 12053;
        jy += 4 * Math.floor(days / 1461); days %= 1461;
        if (days > 365) { jy += Math.floor((days - 1) / 365); days = (days - 1) % 365; }
        var jm = (days < 186) ? 1 + Math.floor(days / 31) : 7 + Math.floor((days - 186) / 30);
        return jm;
    }
    var nd = new Date();
    var FORCED = __FORCED_SEASON__;
    var SEASON = FORCED;
    if (SEASON < 0) {
        if (host.season === 'spring') SEASON = 0;
        else if (host.season === 'summer') SEASON = 1;
        else if (host.season === 'autumn') SEASON = 2;
        else if (host.season === 'winter') SEASON = 3;
        else SEASON = Math.floor((toJalali(nd.getFullYear(), nd.getMonth() + 1, nd.getDate()) - 1) / 3) % 4;
    }

    // ---------- عکس‌های فصل‌ها (data URI — سرویس‌شده از CSS img-src data: میزبان) ----------
    var BGS = __BG_DATA__;
    var imgs = BGS.map(function (u) { var i = new Image(); i.src = u; return i; });

    // ---------- بوم تمام‌صفحه ----------
    var cv = document.createElement('canvas');
    cv.style.cssText = 'position:fixed;inset:0;width:100%;height:100%;display:block';
    document.body.appendChild(cv);
    var ctx = cv.getContext('2d');
    var W = 0, H = 0, DPR = 1;
    var bg = document.createElement('canvas');           // پس‌زمینهٔ ازپیش‌رندرشده
    var bgx = bg.getContext('2d');
    var bgReady = false, t0 = (performance && performance.now) ? performance.now() : Date.now();

    function R(a, b) { return a + Math.random() * (b - a); }
    function RI(a, b) { return Math.floor(R(a, b + 1)); }
    function pick(arr) { return arr[Math.floor(Math.random() * arr.length)]; }

    function resize() {
        DPR = Math.min(2, window.devicePixelRatio || 1);
        W = window.innerWidth; H = window.innerHeight;
        cv.width = W * DPR; cv.height = H * DPR;
        ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
        bg.width = W; bg.height = H;
        buildScene();
    }

    function drawCover(c2, img) {
        var iw = img.naturalWidth || img.width, ih = img.naturalHeight || img.height;
        if (!iw || !ih) return false;
        var s = Math.max(W / iw, H / ih);
        var dw = iw * s, dh = ih * s;
        c2.drawImage(img, (W - dw) / 2, (H - dh) / 2, dw, dh);
        return true;
    }
    function buildBg() {
        var im = imgs[SEASON];
        bgx.fillStyle = '#05070c'; bgx.fillRect(0, 0, W, H);
        if (im.complete && im.naturalWidth) { bgReady = drawCover(bgx, im); }
        else { bgReady = false; im.onload = function () { bgReady = drawCover(bgx, im); }; }
    }

    // ---------- اجزای مشترک ----------
    var groundY = 0, fade = 0, petalsG = [], leavesG = [], fruits = [], grass = [], flowers = [],
        bees = [], flies = [], birds = [], drops = [], snow = [], fogs = [], sparkles = [], clouds = [],
        seeds = [], rain = [], splashes = [], gust = null, sweeper = null, sunA = 0, sunSoft = 1,
        flock = null, robin = null, hare = null, snowman = null,
        burstT = 0, gustT = 0, rainT = 0, rainDur = 0, raining = false, rainbowT = 0,
        flockT = 0, hareT = 0;

    var SPRING_P = ['#ffd9e8', '#ffc2d6', '#ffe6f0', '#f9b8ce', '#fff3f7'];
    var AUTUMN_P = ['#d97706', '#ea580c', '#dc2626', '#f59e0b', '#b45309', '#fbbf24', '#c2410c', '#9a3412'];
    var FRUIT_P = ['#f97316', '#dc2626', '#eab308', '#a3e635', '#fb923c'];
    var FLW_SPRING = ['#f472b6', '#fbbc05', '#e879f9', '#fb7185', '#c084fc'];
    var FLW_SUMMER = ['#fbbf24', '#fb923c', '#f472b6', '#a3e635', '#f59e0b', '#e879f9'];

    function makePetals(n) {
        for (var i = 0; i < n; i++) drops.push({
            x: R(0, W), y: R(-H * 0.6, H), s: R(3.2, 6.5), vy: R(22, 55), vx: R(-12, 12),
            rot: R(0, 6.28), vr: R(-2, 2), ph: R(0, 6.28), c: pick(SPRING_P)
        });
    }
    function makeLeaves(n) {
        for (var i = 0; i < n; i++) drops.push({
            x: R(0, W), y: R(-H * 0.8, H), s: R(5, 10), vy: R(28, 62), vx: R(-14, 14),
            rot: R(0, 6.28), vr: R(-3, 3), ph: R(0, 6.28), sway: R(14, 42), c: pick(AUTUMN_P)
        });
    }
    function makeSnow() {
        snow = [];
        var layers = [[RI(55, 75), 1.1, 2, .45, 14], [RI(38, 48), 2, 3.4, .7, 26], [RI(16, 24), 3.2, 5.5, .92, 46]];
        layers.forEach(function (L, li) {
            for (var i = 0; i < L[0]; i++) snow.push({
                x: R(0, W), y: R(0, H), r: R(L[1], L[2]), a: L[3] * R(.7, 1), vy: L[4] * R(.7, 1.3),
                vx: R(-6, 6), ph: R(0, 6.28), L: li
            });
        });
    }
    function makeGround(n, arr, cFn) {
        for (var i = 0; i < n; i++) arr.push({ x: R(0, W), y: groundY + R(-3, 8), s: R(3.5, 7), a: 1, c: cFn() });
    }
    function makeFlowers(pal) {
        flowers = [];
        var n = RI(9, 15);
        for (var i = 0; i < n; i++) flowers.push({
            x: R(W * .01, W * .99), h: R(16, 40), ph: R(0, 6.28),
            c: pick(pal || FLW_SPRING)
        });
    }
    function makeGrass() {
        grass = [];
        for (var i = 0; i < RI(12, 18); i++) grass.push({ x: R(0, W), h: R(14, 34), ph: R(0, 6.28), w: R(1.5, 3) });
    }
    function makeBees() {
        bees = [];
        for (var i = 0; i < RI(2, 4); i++) bees.push({
            x: R(W * .1, W * .9), y: R(H * .15, H * .6), s: R(5, 8), ph: R(0, 6.28),
            tx: R(0, W), ty: R(H * .1, H * .75), sp: R(30, 60), hover: 0, fi: 0
        });
    }
    function makeFlies() {
        flies = [];
        for (var i = 0; i < RI(2, 3); i++) flies.push({
            x: R(W * .1, W * .9), y: R(H * .2, H * .6), s: R(9, 14), tx: 0, ty: 0, sp: R(120, 210), wait: 0
        });
        flies.forEach(function (f) { f.tx = R(0, W); f.ty = R(H * .15, H * .7); });
    }
    function makeBirds() {
        birds = [];
        var n = RI(2, 5);
        var y0 = R(H * .08, H * .3);
        for (var i = 0; i < n; i++) birds.push({
            x: -R(40, 140) * (i + 1), y: y0 + R(-30, 30), s: R(5, 9), sp: R(45, 75), ph: R(0, 6.28)
        });
    }
    function makeClouds() {
        clouds = [];
        for (var i = 0; i < RI(2, 4); i++) {
            var puffs = [];
            for (var j = 0; j < RI(3, 5); j++) puffs.push({ dx: R(-38, 38), dy: R(-10, 10), r: R(16, 34) });
            clouds.push({ x: R(-100, W), y: R(H * .04, H * .28), v: R(6, 16), a: R(.35, .6), p: puffs });
        }
    }
    function makeFruits() {
        fruits = [];
        for (var i = 0; i < RI(7, 10); i++) fruits.push({
            x: R(W * .06, W * .94), y: R(H * .22, H * .52), s: R(6.5, 10), g: 0, gg: R(1 / 26, 1 / 42),
            c: pick(FRUIT_P), state: 'grow', vy: 0, rot: R(-.4, .4), wait: R(2, 9), fade: 1
        });
    }
    function makeFog(soft) {
        fogs = [];
        for (var i = 0; i < 3; i++) fogs.push({
            y: H * (.4 + i * .16), h: H * R(.16, .24), v: R(7, 20) * (i % 2 ? 1 : -1),
            x: R(0, W), a: R(.07, .13) * (soft || 1)
        });
    }
    function makeSparkles() {
        sparkles = [];
        for (var i = 0; i < RI(22, 30); i++) sparkles.push({ x: R(0, W), y: R(0, H), r: R(.7, 1.8), ph: R(0, 6.28), sp: R(.6, 1.8) });
    }
    function makeSeeds() {
        seeds = [];
        for (var i = 0; i < RI(6, 10); i++) seeds.push({
            x: R(0, W), y: R(H * .15, H * .8), ph: R(0, 6.28), s: R(4, 7),
            vx: R(9, 24), vy: R(-6, 6), rot: R(0, 6.28), vr: R(-.8, .8)
        });
    }
    function makeFlock() {
        var n = RI(5, 8), y0 = R(H * .08, H * .3), dir = Math.random() < .5 ? 1 : -1;
        var m = [];
        for (var i = 0; i < n; i++) {
            var row = Math.ceil(i / 2), side = (i % 2) ? 1 : -1;
            if (i === 0) { row = 0; side = 0; }
            m.push({ dx: -row * 24 * dir, dy: row * 13 * (side === 0 ? 0 : 1) * side, ph: R(0, 6.28), s: R(6, 10) });
        }
        flock = { x: dir > 0 ? -180 : W + 180, y: y0, v: R(40, 62), dir: dir, m: m };
    }
    function newSweeper() {
        var side = Math.random() < .5 ? -1 : 1;
        return {
            x: side < 0 ? -60 : W + 60, dir: side < 0 ? 1 : -1, state: 'walkIn',
            tx: R(W * .25, W * .75), t: 0, pile: [], sweepA: 0, leg: 0,
            pileC: SEASON === 0 ? 'rgba(255,205,225,.9)' : 'rgba(196,123,42,.9)'
        };
    }
    // دسترسی به تودهٔ زمین فصل جاری — رفتگر در بهار روی شکوفه‌ها و در پاییز روی برگ‌ها کار می‌کند
    function gArr() { return SEASON === 0 ? petalsG : leavesG; }
    function setGArr(a) { if (SEASON === 0) petalsG = a; else leavesG = a; }

    function buildScene() {
        groundY = H * .885;
        petalsG = []; leavesG = []; birds = []; flock = null; robin = null; hare = null; snowman = null;
        gust = null; sweeper = null; rain = []; splashes = []; raining = false; rainbowT = 0;
        drops = []; flowers = []; grass = []; bees = []; flies = []; fruits = []; clouds = [];
        sparkles = []; fogs = []; seeds = [];
        makeBg();
        if (SEASON === 0) {          // بهار
            makePetals(RI(28, 42)); makeGround(RI(10, 18), petalsG, function () { return pick(SPRING_P); });
            makeFlowers(FLW_SPRING); makeGrass(); makeBees();
            for (var i = 0; i < RI(1, 3); i++) flies.push({
                x: R(W * .1, W * .9), y: R(H * .2, H * .6), s: R(8, 12), tx: R(0, W), ty: R(H * .2, H * .6),
                sp: R(18, 34), wait: 0, land: 0, fi: 0, c: pick(['#f9a8d4', '#fbbf24', '#a78bfa', '#7dd3fc'])
            });
            makeBirds();
            sunSoft = .55; sunA = R(0, 6.28);
            burstT = R(5, 12); gustT = R(9, 18);
            rainT = R(8, 18);           // باران بهاری دوره‌ای
        } else if (SEASON === 1) {   // تابستان
            makeFruits(); makeGrass(); makeFlies(); makeBirds(); makeClouds(); makeSeeds();
            makeFlowers(FLW_SUMMER); makeBees();
            sunSoft = 1; sunA = R(0, 6.28);
        } else if (SEASON === 2) {   // پاییز
            makeLeaves(RI(34, 52)); makeGround(RI(14, 22), leavesG, function () { return pick(AUTUMN_P); });
            makeBirds(); makeFog(.5);
            gustT = R(5, 12);
            rainT = R(10, 22);          // نم‌نم‌باران پاییزی
            flockT = R(4, 10);          // پرندگان مهاجر دوره‌ای
        } else {                     // زمستان
            makeSnow(); makeFog(); makeSparkles();
            snowman = { x: R(W * .18, W * .82), s: R(.9, 1.15) };
            robin = { x: R(W * .15, W * .85), y: 0, dir: 1, hop: 0, wait: R(.5, 2), peck: 0, ph: 0 };
            hareT = R(4, 10);
        }
        fade = 0;
    }
    function makeBg() { bgReady = false; buildBg(); }

    // هدف‌گیری گل‌ها — زنبور و پروانه «روی گل‌ها» می‌روند (درخواست اصلی کاربر)
    function flowerTarget(fi) {
        if (!flowers.length) return null;
        var f = flowers[fi % flowers.length];
        return { x: f.x + Math.sin(f.ph) * 4, y: groundY - f.h - 7 };
    }

    // ---------- به‌روزرسانی ذرات ----------
    function step(dt, t) {
        if (fade < 1) fade = Math.min(1, fade + dt / 1.4);
        var w = (gust ? Math.sin(Math.min(1, (t - gust.t0) / gust.dur) * Math.PI) * gust.str : 0);
        if ((SEASON === 2 || SEASON === 0) && !gust) {
            gustT -= dt;
            if (gustT <= 0) {
                gust = { t0: t, dur: R(1.2, 2.2), str: SEASON === 0 ? R(40, 90) : R(90, 190) };
                gustT = SEASON === 0 ? R(9, 18) : R(6, 14);
            }
        }
        if (gust && t - gust.t0 > gust.dur) gust = null;

        drops.forEach(function (p) {
            p.ph += dt * 2;
            if (SEASON === 0) { p.y += p.vy * dt; p.x += (p.vx + Math.sin(p.ph) * 16 + w * .4) * dt; p.rot += p.vr * dt; }
            else { p.y += (p.vy + Math.sin(p.ph) * 6) * dt; p.x += (p.vx + Math.sin(p.ph * .7) * p.sway + w) * dt; p.rot += (p.vr + w * .01) * dt; }
            if (p.y > groundY && SEASON === 2) { leavesG.push({ x: p.x, y: groundY + R(-2, 8), s: p.s, a: 1, c: p.c }); p.y = R(-60, -10); p.x = R(0, W); }
            else if (p.y > groundY + 4 && SEASON === 0) { petalsG.push({ x: p.x, y: groundY + R(-2, 6), s: p.s, a: 1, c: p.c }); p.y = R(-60, -10); p.x = R(0, W); }
            else if (p.y > H + 20) { p.y = R(-60, -10); p.x = R(0, W); }
            if (p.x < -30) p.x = W + 20; if (p.x > W + 30) p.x = -20;
        });
        var cap = SEASON === 0 ? 46 : 70;
        var gA = gArr();
        if (gA.length > cap) setGArr(gA.slice(gA.length - cap));

        if (SEASON === 0) {
            // تکه‌پاشی شکوفه از تاج درخت — هر چند ثانیه از نقطه‌ای تصادفی
            burstT -= dt;
            if (burstT <= 0) {
                burstT = R(7, 16);
                var bx = R(W * .15, W * .85), by = R(H * .12, H * .4), n = RI(8, 14);
                for (var i = 0; i < n; i++) drops.push({
                    x: bx + R(-34, 34), y: by + R(-22, 22), s: R(3.2, 6), vy: R(30, 60),
                    vx: R(-25, 25), rot: R(0, 6.28), vr: R(-2.5, 2.5), ph: R(0, 6.28), c: pick(SPRING_P)
                });
            }
            flowers.forEach(function (f) { f.ph += dt * 1.6; });
            grass.forEach(function (g) { g.ph += dt * 1.8; });
            bees.forEach(function (b) {
                b.ph += dt * 34;
                if (b.hover > 0) { b.hover -= dt; }
                else {
                    var tgt = flowerTarget(b.fi) || { x: b.tx, y: b.ty };
                    var dx = tgt.x - b.x, dy = tgt.y - b.y, dist = Math.hypot(dx, dy) || 1;
                    if (dist < 10) {
                        b.hover = R(1, 2.6);                    // روی گل می‌نشیند و وزوز می‌کند
                        b.fi = flowers.length ? RI(0, flowers.length - 1) : 0;
                        b.tx = R(W * .05, W * .95); b.ty = R(H * .1, H * .8);
                    } else { b.x += dx / dist * b.sp * dt; b.y += dy / dist * b.sp * dt; }
                }
                b.x += Math.cos(b.ph) * .7; b.y += Math.sin(b.ph * 1.3) * .7;
            });
            flies.forEach(function (f) {                        // پروانه‌ها روی گل‌ها می‌نشینند
                if (f.land > 0) { f.land -= dt; return; }
                var tgt = flowerTarget(f.fi) || { x: f.tx, y: f.ty };
                var dx = tgt.x - f.x, dy = tgt.y - f.y, dist = Math.hypot(dx, dy) || 1;
                if (dist < 9) {
                    f.land = R(1.5, 4);                         // نشستن روی گل
                    f.fi = flowers.length ? RI(0, flowers.length - 1) : 0;
                    f.tx = R(W * .05, W * .95); f.ty = R(H * .18, groundY - 10);
                } else { f.x += dx / dist * f.sp * dt; f.y += dy / dist * f.sp * dt; }
            });
            birds.forEach(function (b) { b.x += b.sp * dt; b.y += Math.sin(t * 1.3 + b.ph) * 8 * dt; b.ph += dt * 9; if (b.x > W + 60) { b.x = -60 - R(0, 200); b.y = R(H * .06, H * .3); } });
            stepSweeper(dt);
            stepRain(dt);
        }
        if (SEASON === 1) {
            sunA += dt * .18;
            clouds.forEach(function (c) { c.x += c.v * dt; if (c.x > W + 120) { c.x = -160; c.y = R(H * .04, H * .3); } });
            flowers.forEach(function (f) { f.ph += dt * 1.3; });
            grass.forEach(function (g) { g.ph += dt * 2.2; });
            bees.forEach(function (b) {
                b.ph += dt * 34;
                if (b.hover > 0) { b.hover -= dt; }
                else {
                    var tgt = flowerTarget(b.fi) || { x: b.tx, y: b.ty };
                    var dx = tgt.x - b.x, dy = tgt.y - b.y, dist = Math.hypot(dx, dy) || 1;
                    if (dist < 10) {
                        b.hover = R(.8, 2);
                        b.fi = flowers.length ? RI(0, flowers.length - 1) : 0;
                        b.tx = R(W * .05, W * .95); b.ty = R(H * .1, H * .8);
                    } else { b.x += dx / dist * b.sp * dt; b.y += dy / dist * b.sp * dt; }
                }
                b.x += Math.cos(b.ph) * .7; b.y += Math.sin(b.ph * 1.3) * .7;
            });
            flies.forEach(function (f) {
                if (f.wait > 0) { f.wait -= dt; if (f.wait <= 0) { f.tx = R(0, W); f.ty = R(H * .12, H * .7); } }
                else {
                    var dx = f.tx - f.x, dy = f.ty - f.y, dist = Math.hypot(dx, dy) || 1;
                    if (dist < 12) f.wait = R(.4, 1.4);
                    else { f.x += dx / dist * f.sp * dt; f.y += dy / dist * f.sp * dt; f.x += R(-30, 30) * dt; f.y += R(-30, 30) * dt; }
                }
            });
            birds.forEach(function (b) { b.x += b.sp * dt; b.y += Math.sin(t + b.ph) * 6 * dt; b.ph += dt * 10; if (b.x > W + 60) { b.x = -60 - R(0, 260); b.y = R(H * .05, H * .3); } });
            seeds.forEach(function (s) {
                s.ph += dt * 1.4; s.rot += s.vr * dt;
                s.x += (s.vx + Math.sin(s.ph) * 8) * dt;
                s.y += (s.vy + Math.sin(s.ph * .8) * 10) * dt;
                if (s.x > W + 30) { s.x = -20; s.y = R(H * .1, H * .8); }
                if (s.y < -20) s.y = H * .8; if (s.y > H + 20) s.y = H * .15;
            });
            fruits.forEach(function (f) {
                if (f.state === 'grow') { f.g += f.gg; if (f.g >= 1) { f.g = 1; f.state = 'hang'; f.wait = R(2, 10); } }
                else if (f.state === 'hang') { f.wait -= dt; if (f.wait <= 0 && Math.random() < .5) { f.state = 'fall'; f.vy = 0; } else if (f.wait <= 0) f.wait = R(3, 8); }
                else if (f.state === 'fall') {
                    f.vy += 480 * dt; f.y += f.vy * dt;
                    if (f.y >= groundY) { if (f.vy > 60) { f.vy *= -.32; f.y = groundY; } else { f.state = 'rest'; f.wait = R(3, 5); f.y = groundY + 2; } }
                }
                else if (f.state === 'rest') { f.wait -= dt; f.fade -= dt / 2; if (f.wait <= 0) { f.x = R(W * .06, W * .94); f.y = R(H * .22, H * .52); f.g = 0; f.state = 'grow'; f.fade = 1; f.c = pick(FRUIT_P); } }
            });
        }
        if (SEASON === 2) {
            flockT -= dt;
            if (!flock && flockT <= 0) { makeFlock(); }
            if (flock) {
                flock.x += flock.v * dt * flock.dir;
                flock.m.forEach(function (m) { m.ph += dt * 9; });
                if ((flock.dir > 0 && flock.x > W + 240) || (flock.dir < 0 && flock.x < -240)) { flock = null; flockT = R(18, 42); }
            }
            birds.forEach(function (b) { b.x += b.sp * dt; b.y += Math.sin(t + b.ph) * 6 * dt; b.ph += dt * 10; if (b.x > W + 60) { b.x = -60 - R(0, 260); b.y = R(H * .05, H * .3); } });
            stepSweeper(dt);
            stepRain(dt);
        }
        if (SEASON === 3) {
            snow.forEach(function (s) {
                var wind = Math.sin(t * .25 + s.ph) * (10 + s.L * 6) + w * (0.3 + s.L * .35);
                s.y += s.vy * dt; s.x += wind * dt;
                if (s.y > H + 6) { s.y = -8; s.x = R(0, W); }
                if (s.x < -10) s.x = W + 8; if (s.x > W + 10) s.x = -8;
            });
            fogs.forEach(function (f) { f.x += f.v * dt; if (f.x > W + f.h * 2) f.x = -f.h * 2; if (f.x < -f.h * 2) f.x = W + f.h * 2; });
            sparkles.forEach(function (s) { s.ph += dt * s.sp; });
            // گنجشک زمستانی — لَپ‌خوردن و نوک‌زدن روی برف
            if (robin) {
                robin.ph += dt;
                if (robin.peck > 0) { robin.peck -= dt; }
                else {
                    robin.wait -= dt;
                    if (robin.wait <= 0) {
                        if (Math.random() < .35) robin.peck = R(.5, 1.1);
                        else { robin.hop = R(.35, .5); robin.dir = Math.random() < .5 ? -1 : 1; robin.wait = R(.8, 2.4); }
                    }
                    if (robin.hop > 0) {
                        robin.hop -= dt;
                        robin.x += robin.dir * 46 * dt;
                        if (robin.x < W * .06 || robin.x > W * .94) robin.dir *= -1;
                    }
                }
            }
            // خرگوش سفید — گاه‌به‌گاه از صحنه می‌گذرد
            if (!hare) {
                hareT -= dt;
                if (hareT <= 0) {
                    var dir = Math.random() < .5 ? 1 : -1;
                    hare = { x: dir > 0 ? -60 : W + 60, dir: dir, ph: 0 };
                }
            } else {
                hare.ph += dt * 7;
                hare.x += hare.dir * (58 + Math.abs(Math.sin(hare.ph)) * 34) * dt;
                if (hare.x < -80 || hare.x > W + 80) { hare = null; hareT = R(14, 34); }
            }
        }
    }

    // ---------- باران دوره‌ای (بهار/پاییز) + پاشیدن قطره روی زمین + رنگین‌کمان پس از باران بهاری ----------
    function stepRain(dt) {
        if (!raining) {
            rainT -= dt;
            if (rainT <= 0) { raining = true; rainDur = R(5, 9); }
        } else {
            rainDur -= dt;
            if (rainDur <= 0) {
                raining = false; rainT = R(16, 34);
                if (SEASON === 0) rainbowT = R(5, 8);   // رنگین‌کمان فقط پس از باران بهاری
            }
        }
        if (raining) {
            var want = Math.floor(W / 16);
            for (var i = 0; i < want; i++) {
                if (rain.length < 200 && Math.random() < .55) rain.push({
                    x: R(-50, W + 30), y: R(-90, -10), vy: R(540, 760),
                    vx: SEASON === 0 ? R(-35, 8) : R(-70, -12), len: R(9, 16), a: R(.25, .5)
                });
            }
        }
        for (var j = rain.length - 1; j >= 0; j--) {
            var p = rain[j];
            p.y += p.vy * dt; p.x += p.vx * dt;
            if (p.y >= groundY) {
                if (splashes.length < 40 && Math.random() < .3) splashes.push({ x: p.x, y: groundY + R(-2, 6), t: 0 });
                rain.splice(j, 1);
            } else if (p.x < -60) rain.splice(j, 1);
        }
        for (var k = splashes.length - 1; k >= 0; k--) { splashes[k].t += dt; if (splashes[k].t > .35) splashes.splice(k, 1); }
        if (rainbowT > 0) rainbowT -= dt;
    }

    // ---------- رفتگر شکوفه‌ها/برگ‌ها (بهار و پاییز) ----------
    function stepSweeper(dt) {
        if (!sweeper) {
            var arr = gArr();
            if (arr.length >= 12 && Math.random() < dt * .18) sweeper = newSweeper();
            return;
        }
        var s = sweeper;
        s.leg += dt * (s.state === 'sweep' ? 0 : 7);
        if (s.state === 'walkIn') {
            s.x = s.x + Math.sign(s.tx - s.x) * 52 * dt;
            if (Math.abs(s.tx - s.x) < 8) { s.state = 'sweep'; s.t = 0; }
        } else if (s.state === 'sweep') {
            s.t += dt; s.sweepA = Math.sin(s.t * 6) * .5;
            gArr().forEach(function (p) {
                if (Math.abs(p.x - s.x) < 110) { p.x += (s.x - s.dir * 16 - p.x) * dt * 2.4; p.y += (groundY - p.y) * dt * 2; }
            });
            if (s.t > 3) {
                s.state = 'carry';
                var cx = s.x - s.dir * 16;
                s.pile = gArr().filter(function (p) { return Math.abs(p.x - cx) < 90; });
                s.pile.forEach(function (p) { p.a = 0; });
            }
        } else if (s.state === 'carry') {
            s.x -= s.dir * 52 * dt;
            if (s.x < -80 || s.x > W + 80) {
                setGArr(gArr().filter(function (p) { return p.a > 0; }));
                sweeper = null;
            }
        }
    }

    // ---------- رسم ----------
    function drawGroundBits(arr) {
        arr.forEach(function (p) {
            ctx.globalAlpha = p.a;
            ctx.fillStyle = p.c;
            ctx.beginPath(); ctx.ellipse(p.x, p.y, p.s, p.s * .55, 0, 0, 6.29); ctx.fill();
        });
        ctx.globalAlpha = 1;
    }
    function drawFlowers() {
        flowers.forEach(function (f) {
            var sw = Math.sin(f.ph) * 4;
            ctx.strokeStyle = '#2f7d32'; ctx.lineWidth = 1.6;
            ctx.beginPath(); ctx.moveTo(f.x, groundY + 6); ctx.quadraticCurveTo(f.x + sw * .5, groundY - f.h * .5, f.x + sw, groundY - f.h); ctx.stroke();
            ctx.fillStyle = f.c;
            for (var k = 0; k < 5; k++) {
                var a = k * 1.257 + f.ph * .3;
                ctx.beginPath(); ctx.ellipse(f.x + sw + Math.cos(a) * 3.4, groundY - f.h + Math.sin(a) * 3.4, 3.2, 2.1, a, 0, 6.29); ctx.fill();
            }
            ctx.fillStyle = '#fff7cc'; ctx.beginPath(); ctx.arc(f.x + sw, groundY - f.h, 1.8, 0, 6.29); ctx.fill();
        });
    }
    function drawGrass() {
        ctx.strokeStyle = SEASON === 0 ? 'rgba(52,140,60,.7)' : 'rgba(34,120,40,.75)';
        ctx.lineCap = 'round';
        grass.forEach(function (g) {
            var sw = Math.sin(g.ph) * 6;
            ctx.lineWidth = g.w;
            ctx.beginPath(); ctx.moveTo(g.x, groundY + 8); ctx.quadraticCurveTo(g.x + sw * .4, groundY + 8 - g.h * .6, g.x + sw, groundY + 8 - g.h); ctx.stroke();
        });
        ctx.lineCap = 'butt';
    }
    function drawBirds() {
        ctx.strokeStyle = 'rgba(25,30,40,.8)'; ctx.lineWidth = 1.6;
        birds.forEach(function (b) {
            var f = Math.sin(b.ph) * b.s * .6;
            ctx.beginPath();
            ctx.moveTo(b.x - b.s, b.y - f); ctx.quadraticCurveTo(b.x, b.y + 2, b.x + b.s, b.y - f);
            ctx.stroke();
        });
    }
    function drawFlock(t) {
        if (!flock) return;
        ctx.strokeStyle = 'rgba(35,40,50,.85)'; ctx.lineWidth = 1.8;
        flock.m.forEach(function (m) {
            var bx = flock.x + m.dx, by = flock.y + m.dy + Math.sin(t * 1.1 + m.ph) * 3;
            var f = Math.sin(m.ph) * m.s * .6;
            ctx.beginPath();
            ctx.moveTo(bx - m.s, by - f); ctx.quadraticCurveTo(bx, by + 2, bx + m.s, by - f);
            ctx.stroke();
        });
    }
    function drawBees() {
        bees.forEach(function (b) {
            var wing = Math.abs(Math.sin(b.ph)) * .8 + .2;
            ctx.fillStyle = 'rgba(180,210,255,.5)';
            ctx.beginPath(); ctx.ellipse(b.x - 3, b.y - 4, b.s * .8 * wing, b.s * .45, -.5, 0, 6.29); ctx.fill();
            ctx.beginPath(); ctx.ellipse(b.x + 3, b.y - 4, b.s * .8 * (1 - wing * .8 + .2), b.s * .45, .5, 0, 6.29); ctx.fill();
            ctx.fillStyle = '#f59e0b';
            ctx.beginPath(); ctx.ellipse(b.x, b.y, b.s, b.s * .72, 0, 0, 6.29); ctx.fill();
            ctx.fillStyle = 'rgba(40,30,10,.85)';
            ctx.fillRect(b.x - b.s * .45, b.y - b.s * .62, b.s * .3, b.s * 1.2);
            ctx.fillRect(b.x + b.s * .1, b.y - b.s * .6, b.s * .28, b.s * 1.15);
            ctx.fillStyle = 'rgba(30,25,20,.9)';
            ctx.beginPath(); ctx.arc(b.x + b.s * .85, b.y - 1, b.s * .4, 0, 6.29); ctx.fill();
        });
    }
    function drawFlies() {
        var now = performance.now() / 1000;
        flies.forEach(function (f) {
            var col = f.c || '#38bdf8';
            var flap = f.land > 0 ? .25 : Math.abs(Math.sin((f.wait > 0 ? 8 : 22) * now));
            var wing = Math.max(.15, flap);
            ctx.fillStyle = 'rgba(200,230,255,.45)';
            ctx.beginPath(); ctx.ellipse(f.x - 4, f.y - 5, f.s, f.s * .35 * wing, -.6, 0, 6.29); ctx.fill();
            ctx.beginPath(); ctx.ellipse(f.x + 4, f.y - 5, f.s, f.s * .35 * wing, .6, 0, 6.29); ctx.fill();
            ctx.fillStyle = col;
            ctx.beginPath(); ctx.ellipse(f.x, f.y, f.s * .55, f.s * .3, 0, 0, 6.29); ctx.fill();
            ctx.beginPath(); ctx.arc(f.x - f.s * .5, f.y, f.s * .3, 0, 6.29); ctx.fill();
        });
    }
    function drawFruits() {
        fruits.forEach(function (f) {
            if (f.fade <= 0) return;
            ctx.globalAlpha = f.fade;
            var r = f.s * (.35 + .65 * f.g);
            ctx.fillStyle = f.c;
            ctx.beginPath(); ctx.ellipse(f.x, f.y, r, r * .92, f.rot, 0, 6.29); ctx.fill();
            ctx.fillStyle = 'rgba(255,255,255,.35)';
            ctx.beginPath(); ctx.ellipse(f.x - r * .35, f.y - r * .4, r * .28, r * .18, -.6, 0, 6.29); ctx.fill();
            ctx.strokeStyle = 'rgba(60,40,20,.6)'; ctx.lineWidth = 1.2;
            ctx.beginPath(); ctx.moveTo(f.x, f.y - r); ctx.lineTo(f.x + 1.5, f.y - r - 3); ctx.stroke();
            ctx.globalAlpha = 1;
        });
    }
    function drawSeeds() {
        seeds.forEach(function (s) {
            ctx.save(); ctx.translate(s.x, s.y); ctx.rotate(Math.sin(s.rot) * .35);
            ctx.strokeStyle = 'rgba(255,255,255,.75)'; ctx.lineWidth = .8;
            for (var k = 0; k < 8; k++) {
                var a = k * .7854 + s.ph * .2;
                ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(Math.cos(a) * s.s, Math.sin(a) * s.s); ctx.stroke();
            }
            ctx.fillStyle = 'rgba(255,255,255,.85)';
            for (var k2 = 0; k2 < 8; k2++) {
                var a2 = k2 * .7854 + s.ph * .2;
                ctx.beginPath(); ctx.arc(Math.cos(a2) * s.s, Math.sin(a2) * s.s, .8, 0, 6.29); ctx.fill();
            }
            ctx.beginPath(); ctx.arc(0, s.s * 1.15, 1.7, 0, 6.29); ctx.fill();
            ctx.restore();
        });
    }
    function drawShimmer(t) {
        for (var i = 0; i < 3; i++) {
            var y = groundY - 18 - i * 26 + Math.sin(t * 1.6 + i * 2.1) * 4;
            ctx.fillStyle = 'rgba(255,238,190,' + (0.05 - i * 0.012).toFixed(3) + ')';
            ctx.fillRect(0, y, W, 14);
        }
    }
    function drawRain() {
        var col = SEASON === 0 ? '150,190,230' : '140,150,165';
        ctx.strokeStyle = 'rgba(' + col + ',1)'; ctx.lineWidth = 1.1; ctx.lineCap = 'round';
        rain.forEach(function (p) {
            ctx.globalAlpha = p.a * fade;
            ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(p.x - p.vx * .022, p.y - p.len); ctx.stroke();
        });
        ctx.globalAlpha = 1; ctx.lineCap = 'butt';
        splashes.forEach(function (s) {
            var k = s.t / .35;
            ctx.globalAlpha = (1 - k) * .5 * fade;
            ctx.strokeStyle = 'rgba(210,230,250,1)'; ctx.lineWidth = 1;
            ctx.beginPath(); ctx.ellipse(s.x, s.y, 3 + k * 6, (3 + k * 6) * .35, 0, Math.PI, 6.29); ctx.stroke();
        });
        ctx.globalAlpha = 1;
    }
    function drawRainbow() {
        if (rainbowT <= 0) return;
        var env = Math.min(1, rainbowT / 1.5) * fade;
        var cx = W * .5, cy = groundY + 8, r0 = Math.min(W, H) * .4;
        var cols = ['#ff5f5f', '#ffb44d', '#ffe45c', '#7ed957', '#5bc8f5', '#5f7cff', '#b45cff'];
        ctx.save();
        ctx.globalAlpha = .13 * env;
        ctx.lineWidth = 5;
        for (var i = 0; i < cols.length; i++) {
            ctx.strokeStyle = cols[i];
            ctx.beginPath(); ctx.arc(cx, cy, r0 + i * 5, Math.PI, 0); ctx.stroke();
        }
        ctx.restore();
    }
    function drawClouds() {
        clouds.forEach(function (c) {
            ctx.fillStyle = 'rgba(255,255,255,' + c.a + ')';
            c.p.forEach(function (p) { ctx.beginPath(); ctx.arc(c.x + p.dx, c.y + p.dy, p.r, 0, 6.29); ctx.fill(); });
        });
    }
    function drawSun() {
        var sx = W * .84, sy = H * .1, k = sunSoft;
        var g = ctx.createRadialGradient(sx, sy, 4, sx, sy, H * .3);
        g.addColorStop(0, 'rgba(255,236,170,' + (.5 * k) + ')');
        g.addColorStop(.35, 'rgba(255,210,120,' + (.16 * k) + ')');
        g.addColorStop(1, 'rgba(255,200,90,0)');
        ctx.fillStyle = g; ctx.fillRect(sx - H * .32, sy - H * .32, H * .64, H * .64);
        ctx.save(); ctx.translate(sx, sy); ctx.rotate(sunA);
        for (var i = 0; i < 12; i++) {
            ctx.rotate(6.283 / 12);
            ctx.fillStyle = 'rgba(255,225,140,' + (.08 * k) + ')';
            ctx.fillRect(18, -4, H * .24, 8);
        }
        ctx.restore();
        ctx.fillStyle = 'rgba(255,244,200,' + (.9 * k + .1) + ')';
        ctx.beginPath(); ctx.arc(sx, sy, 16, 0, 6.29); ctx.fill();
    }
    function drawSweeper() {
        var s = sweeper; if (!s) return;
        var y = groundY + 4, bob = Math.sin(s.leg) * 1.6;
        ctx.save(); ctx.translate(s.x, y);
        if (s.dir < 0) ctx.scale(-1, 1);
        ctx.strokeStyle = 'rgba(35,30,26,.9)'; ctx.lineWidth = 2.4; ctx.lineCap = 'round';
        var step = Math.sin(s.leg * 1.6) * 5;
        ctx.beginPath(); ctx.moveTo(-3, -12); ctx.lineTo(-5 - step * .4, -1); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(3, -12); ctx.lineTo(5 + step * .4, -1); ctx.stroke();
        ctx.fillStyle = SEASON === 0 ? '#2563eb' : '#7c3aed';   // رفتگر پاییزی با جلیقهٔ متفاوت
        ctx.beginPath(); ctx.roundRect ? ctx.roundRect(-7, -26 + bob, 14, 15, 4) : ctx.rect(-7, -26 + bob, 14, 15); ctx.fill();
        ctx.fillStyle = '#fcd9b0';
        ctx.beginPath(); ctx.arc(0, -33 + bob, 6.5, 0, 6.29); ctx.fill();
        ctx.fillStyle = SEASON === 0 ? '#e11d48' : '#4c1d95';
        ctx.beginPath(); ctx.arc(0, -36 + bob, 8, Math.PI, 0); ctx.fill();
        ctx.fillRect(-9.5, -36.5 + bob, 19, 3);
        var ba = s.state === 'sweep' ? s.sweepA : .35;
        ctx.save(); ctx.translate(9, -18 + bob); ctx.rotate(-.7 + ba);
        ctx.strokeStyle = '#92603a'; ctx.lineWidth = 2.6;
        ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(26, 14); ctx.stroke();
        ctx.fillStyle = '#d9a441';
        ctx.beginPath(); ctx.moveTo(24, 11); ctx.lineTo(34, 15); ctx.lineTo(30, 24); ctx.lineTo(22, 17); ctx.closePath(); ctx.fill();
        ctx.restore();
        if (s.state === 'carry' && s.pile.length) {
            ctx.fillStyle = s.pileC;
            ctx.beginPath(); ctx.ellipse(-14, -12, 13, 8, 0, 0, 6.29); ctx.fill();
        }
        ctx.restore(); ctx.lineCap = 'butt';
    }
    function drawDrops() {
        drops.forEach(function (p) {
            ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.rot);
            ctx.fillStyle = p.c;
            if (SEASON === 0) { ctx.beginPath(); ctx.ellipse(0, 0, p.s, p.s * .55, 0, 0, 6.29); ctx.fill(); }
            else {
                ctx.beginPath();
                ctx.moveTo(0, -p.s);
                ctx.quadraticCurveTo(p.s * .8, -p.s * .3, p.s * .55, p.s * .5);
                ctx.quadraticCurveTo(0, p.s * .95, -p.s * .55, p.s * .5);
                ctx.quadraticCurveTo(-p.s * .8, -p.s * .3, 0, -p.s);
                ctx.fill();
                ctx.strokeStyle = 'rgba(90,45,10,.35)'; ctx.lineWidth = .8;
                ctx.beginPath(); ctx.moveTo(0, -p.s * .7); ctx.lineTo(0, p.s * .7); ctx.stroke();
            }
            ctx.restore();
        });
    }
    function drawSnow() {
        snow.forEach(function (s) {
            ctx.globalAlpha = s.a * fade;
            ctx.fillStyle = '#ffffff';
            ctx.beginPath(); ctx.arc(s.x, s.y, s.r, 0, 6.29); ctx.fill();
        });
        ctx.globalAlpha = 1;
    }
    function drawFog() {
        fogs.forEach(function (f) {
            var g = ctx.createLinearGradient(0, f.y - f.h / 2, 0, f.y + f.h / 2);
            g.addColorStop(0, 'rgba(240,246,255,0)');
            g.addColorStop(.5, 'rgba(240,246,255,' + f.a + ')');
            g.addColorStop(1, 'rgba(240,246,255,0)');
            ctx.fillStyle = g;
            ctx.fillRect(-f.h * 2 + f.x, f.y - f.h / 2, W + f.h * 4, f.h);
        });
    }
    function drawSparkles() {
        sparkles.forEach(function (s) {
            var a = Math.sin(s.ph);
            if (a <= 0) return;
            ctx.globalAlpha = a * .8;
            ctx.fillStyle = '#eaf6ff';
            ctx.beginPath(); ctx.arc(s.x, s.y, s.r, 0, 6.29); ctx.fill();
        });
        ctx.globalAlpha = 1;
    }
    // ---------- آدم‌برفی زمستانی ----------
    function drawSnowman() {
        if (!snowman) return;
        var s = snowman.s, x = snowman.x, y = groundY + 3;
        ctx.fillStyle = 'rgba(40,60,90,.18)';
        ctx.beginPath(); ctx.ellipse(x, y + 2, 26 * s, 6 * s, 0, 0, 6.29); ctx.fill();
        ctx.fillStyle = '#f4f8ff';
        ctx.beginPath(); ctx.arc(x, y - 18 * s, 16 * s, 0, 6.29); ctx.fill();
        ctx.beginPath(); ctx.arc(x, y - 40 * s, 11.5 * s, 0, 6.29); ctx.fill();
        ctx.beginPath(); ctx.arc(x, y - 58 * s, 8 * s, 0, 6.29); ctx.fill();
        ctx.fillStyle = 'rgba(160,190,225,.22)';
        ctx.beginPath(); ctx.arc(x - 5 * s, y - 38 * s, 10 * s, 2.6, 4.4); ctx.fill();
        // دکمه‌ها
        ctx.fillStyle = '#2b2b33';
        for (var i = 0; i < 3; i++) { ctx.beginPath(); ctx.arc(x, y - (22 - i * 5) * s, 1.6 * s, 0, 6.29); ctx.fill(); }
        // چشم‌ها + دماغ هویجی + دکمه‌های صورت
        ctx.beginPath(); ctx.arc(x - 2.7 * s, y - 60 * s, 1.3 * s, 0, 6.29); ctx.fill();
        ctx.beginPath(); ctx.arc(x + 2.7 * s, y - 60 * s, 1.3 * s, 0, 6.29); ctx.fill();
        ctx.fillStyle = '#e8833a';
        ctx.beginPath(); ctx.moveTo(x + 1 * s, y - 58 * s); ctx.lineTo(x + 9.5 * s, y - 56.4 * s); ctx.lineTo(x + 1 * s, y - 56.2 * s); ctx.closePath(); ctx.fill();
        ctx.fillStyle = '#2b2b33';
        for (var m = 0; m < 3; m++) { ctx.beginPath(); ctx.arc(x + (m - 1) * 2.2 * s, y - (53.6 - 0) * s + Math.sin(m * 1.2) * .4, .7 * s, 0, 6.29); ctx.fill(); }
        // شال قرمز
        ctx.fillStyle = '#d64550';
        ctx.fillRect(x - 9 * s, y - 51 * s, 18 * s, 4 * s);
        ctx.fillRect(x + 3 * s, y - 50 * s, 5 * s, 13 * s);
        // دسته‌های چوبی
        ctx.strokeStyle = '#7a5230'; ctx.lineWidth = 2.2; ctx.lineCap = 'round';
        ctx.beginPath(); ctx.moveTo(x - 11 * s, y - 42 * s); ctx.lineTo(x - 24 * s, y - 53 * s); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(x - 19 * s, y - 49 * s); ctx.lineTo(x - 23 * s, y - 44 * s); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(x + 11 * s, y - 42 * s); ctx.lineTo(x + 23 * s, y - 35 * s); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(x + 18 * s, y - 37.5 * s); ctx.lineTo(x + 22 * s, y - 42 * s); ctx.stroke();
        ctx.lineCap = 'butt';
        // سطل روی سر
        ctx.fillStyle = '#3a4250';
        ctx.fillRect(x - 7 * s, y - 70 * s, 14 * s, 8 * s);
        ctx.fillRect(x - 8.6 * s, y - 63.2 * s, 17.2 * s, 2.4 * s);
    }
    // ---------- گنجشک زمستانی (لَپ + نوک‌زدن) ----------
    function drawRobin() {
        if (!robin) return;
        var hop = robin.hop > 0 ? Math.sin((1 - robin.hop / .5) * Math.PI) * 7 : 0;
        var x = robin.x, y = groundY + 2 - hop;
        var peck = robin.peck > 0 ? Math.sin(Math.min(1, robin.peck / 1.1) * Math.PI) : 0;
        ctx.fillStyle = 'rgba(40,60,90,.16)';
        ctx.beginPath(); ctx.ellipse(x, groundY + 4, 7, 2.2, 0, 0, 6.29); ctx.fill();
        // پاها (هنگام لپ مخفی)
        if (hop < 2) {
            ctx.strokeStyle = 'rgba(70,50,30,.9)'; ctx.lineWidth = 1.2;
            ctx.beginPath(); ctx.moveTo(x - 2, y + 4); ctx.lineTo(x - 2, y + 7); ctx.stroke();
            ctx.beginPath(); ctx.moveTo(x + 2, y + 4); ctx.lineTo(x + 2, y + 7); ctx.stroke();
        }
        // دم + بدن
        ctx.fillStyle = '#6b4a2a';
        ctx.beginPath(); ctx.moveTo(x - (robin.dir < 0 ? -7 : 7) * 0 + (robin.dir > 0 ? -8 : 8), y - 1); ctx.lineTo(x + (robin.dir > 0 ? -3 : 3), y + 2); ctx.lineTo(x + (robin.dir > 0 ? -2 : -2), y - 3); ctx.closePath(); ctx.fill();
        ctx.fillStyle = '#8a5a2b';
        ctx.beginPath(); ctx.ellipse(x, y, 6.5, 4.5, 0, 0, 6.29); ctx.fill();
        // سینهٔ سرخ
        ctx.fillStyle = '#e0492f';
        ctx.beginPath(); ctx.ellipse(x + (robin.dir > 0 ? 2.6 : -2.6), y + 1.2, 3, 2.6, 0, 0, 6.29); ctx.fill();
        // سر + نوک + چشم (با نوک‌زدن پایین می‌رود)
        var hx = x + (robin.dir > 0 ? 4.6 : -4.6), hy = y - 3.4 + peck * 3;
        ctx.fillStyle = '#4a3524';
        ctx.beginPath(); ctx.arc(hx, hy, 3, 0, 6.29); ctx.fill();
        ctx.fillStyle = '#e8b23a';
        ctx.beginPath(); ctx.moveTo(hx + (robin.dir > 0 ? 2.6 : -2.6), hy - .6 + peck * 1.5); ctx.lineTo(hx + (robin.dir > 0 ? 5 : -5), hy + .4 + peck * 2); ctx.lineTo(hx + (robin.dir > 0 ? 2.6 : -2.6), hy + 1); ctx.closePath(); ctx.fill();
        ctx.fillStyle = '#14100c';
        ctx.beginPath(); ctx.arc(hx + (robin.dir > 0 ? 1.1 : -1.1), hy - 1, .7, 0, 6.29); ctx.fill();
    }
    // ---------- خرگوش سفید زمستانی ----------
    function drawHare() {
        if (!hare) return;
        var hopY = -Math.abs(Math.sin(hare.ph)) * 12;
        var x = hare.x, y = groundY - 7 + hopY, d = hare.dir;
        ctx.fillStyle = 'rgba(40,60,90,.14)';
        ctx.beginPath(); ctx.ellipse(x, groundY + 3, 11, 2.6, 0, 0, 6.29); ctx.fill();
        ctx.fillStyle = '#f6f9ff';
        ctx.beginPath(); ctx.ellipse(x, y, 12, 8, 0, 0, 6.29); ctx.fill();
        ctx.beginPath(); ctx.arc(x + d * 9, y - 5, 5, 0, 6.29); ctx.fill();
        // گوش‌ها
        ctx.beginPath(); ctx.ellipse(x + d * 8, y - 13, 2.2, 6.5, d * .25, 0, 6.29); ctx.fill();
        ctx.beginPath(); ctx.ellipse(x + d * 11.5, y - 12.5, 2.2, 6, d * .45, 0, 6.29); ctx.fill();
        // دم پنبه‌ای
        ctx.fillStyle = '#ffffff';
        ctx.beginPath(); ctx.arc(x - d * 11, y - 2, 3, 0, 6.29); ctx.fill();
        // چشم + بینی
        ctx.fillStyle = '#1a1a22';
        ctx.beginPath(); ctx.arc(x + d * 11, y - 6, .9, 0, 6.29); ctx.fill();
        ctx.fillStyle = '#e8a0a0';
        ctx.beginPath(); ctx.arc(x + d * 13.6, y - 4.6, .8, 0, 6.29); ctx.fill();
    }
    function tint(color) {
        ctx.fillStyle = color; ctx.fillRect(0, 0, W, H);
    }
    function vignette() {
        var g = ctx.createRadialGradient(W / 2, H / 2, Math.min(W, H) * .42, W / 2, H / 2, Math.max(W, H) * .75);
        g.addColorStop(0, 'rgba(0,0,0,0)'); g.addColorStop(1, 'rgba(0,0,10,.22)');
        ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
    }
    function streaks() {
        if (!gust) return;
        ctx.strokeStyle = 'rgba(255,235,200,.09)'; ctx.lineWidth = 1.4;
        for (var i = 0; i < 7; i++) {
            var y = R(H * .2, H * .8), len = R(60, 160), x = (Date.now() * .35 + i * 297) % (W + len) - len;
            ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x + len, y + R(-4, 4)); ctx.stroke();
        }
    }

    function render(t) {
        ctx.clearRect(0, 0, W, H);
        if (bgReady) ctx.drawImage(bg, 0, 0, W, H);
        else { ctx.fillStyle = '#05070c'; ctx.fillRect(0, 0, W, H); }
        if (!bgReady) return;
        if (SEASON === 0) {
            tint('rgba(255,183,214,.07)');
            drawSun();
            drawGroundBits(petalsG); drawGrass(); drawFlowers();
            drawDrops(); drawRain(); drawBirds(); drawFlies(); drawBees(); drawSweeper();
            drawRainbow();
        } else if (SEASON === 1) {
            tint('rgba(255,196,90,.06)');
            drawClouds(); drawSun(); drawGrass(); drawFlowers(); drawFruits();
            drawSeeds(); drawShimmer(t); drawBirds(); drawFlies(); drawBees();
        } else if (SEASON === 2) {
            tint('rgba(216,110,20,.1)');
            drawFog();
            drawGroundBits(leavesG); drawDrops(); drawRain(); drawBirds(); drawFlock(t); drawSweeper();
            streaks();
        } else {
            tint('rgba(120,160,220,.12)');
            drawSnow(); drawSnowman(); drawRobin(); drawHare();
            drawFog(); drawSparkles();
        }
        vignette();
        if (fade < 1) { ctx.fillStyle = 'rgba(0,0,0,' + (1 - fade) + ')'; ctx.fillRect(0, 0, W, H); }
    }

    // ---------- حلقهٔ اصلی ----------
    var raf = 0, last = t0;
    function frame(now) {
        var dt = Math.min(.05, (now - last) / 1000);
        last = now;
        step(dt, (now - t0) / 1000);
        render((now - t0) / 1000);
        raf = requestAnimationFrame(frame);
    }
    window.addEventListener('resize', resize);
    window.__hrSsGuestCleanup = function () {
        if (raf) cancelAnimationFrame(raf);
        try { cv.remove(); } catch (e) { }
    };
    resize();
    raf = requestAnimationFrame(frame);
})();
