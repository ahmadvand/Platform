// فاز ۱۰۶ — آزمون مرورگری باگ «چند بار باید رمز رو وارد کنی»
// سناریو: دو تب هم‌نشست → هر دو قفل → رمز فقط در تب ۱ وارد می‌شود → هر دو باید بیدار شوند
// + ثبت تصویر صحنهٔ بستهٔ چهارفصل حین قفل + رمز نادرست + تأخیر قفل‌شدن دوباره پس از بیدارسازی
const { chromium } = require('playwright');
const { execSync } = require('child_process');
const fs = require('fs');

const JAR = '/tmp/i106e2e/jar';
const OUT = '/tmp/render';
fs.mkdirSync(OUT, { recursive: true });

function cookiesFromJar() {
    // همهٔ کوکی‌های نشست curl (hr_at + hr_lic_ack + hr_rt + …) — گیت لایسنس کوکی‌محور است
    const raw = execSync(`awk '$6 != "" && $0 !~ /^#/ {print $6"="$7}' ${JAR}`).toString().trim().split('\n');
    const out = [];
    for (const line of raw) {
        const i = line.indexOf('=');
        if (i <= 0) continue;
        out.push({ name: line.slice(0, i), value: line.slice(i + 1), domain: 'localhost', path: '/' });
    }
    return out;
}

(async () => {
    const browser = await chromium.launch();
    const ctx = await browser.newContext({ viewport: { width: 1360, height: 860 } });

    // ===== ورود واقعی در مرورگر (نشست تازه — بدون دست‌کاری کوکی) =====
    const p0 = await ctx.newPage();
    await p0.goto('http://localhost:3000/Auth/Login', { waitUntil: 'networkidle' });
    await p0.fill('#identifier', 'admin');
    await p0.fill('#secret', 'Admin@1234');
    await p0.click('button[type="submit"]');
    await p0.waitForLoadState('networkidle');
    if (p0.url().includes('/License/Notice')) {
        console.error('license notice — clicking ack');
        await p0.click('form[action="/License/Ack"] button');
        await p0.waitForLoadState('networkidle');
    }
    console.error('login landed: ' + p0.url());
    if (!/localhost:3000\/?$|\/$/.test(p0.url()) || p0.url().includes('Login')) throw new Error('login failed: ' + p0.url());
    await p0.close();

    const p1 = await ctx.newPage();
    const p2 = await ctx.newPage();
    // لاگ‌پایا در sessionStorage — از ناوبری عبور می‌کند
    await ctx.addInitScript(() => {
        try {
            const log = (m) => {
                try {
                    const a = JSON.parse(sessionStorage.p106 || '[]');
                    a.push(Date.now() % 1000000 + ' ' + m);
                    sessionStorage.p106 = JSON.stringify(a.slice(-60));
                } catch (e) { }
            };
            const of = window.fetch;
            window.fetch = function (u, o) {
                log('FETCH ' + (o && o.method || 'GET') + ' ' + String(u) + ' XRW=' + ((o && o.headers && o.headers['X-Requested-With']) || (o && o.headers && o.headers['X-Pjax']) || ''));
                return of.apply(this, arguments);
            };
            const xo = XMLHttpRequest.prototype.open;
            XMLHttpRequest.prototype.open = function (m, u) { log('XHR ' + m + ' ' + String(u)); return xo.apply(this, arguments); };
            window.addEventListener('beforeunload', () => log('BEFOREUNLOAD ' + location.href));
            window.addEventListener('error', (e) => log('ERR ' + e.message));
            window.addEventListener('unhandledrejection', (e) => log('REJ ' + (e.reason && e.reason.message || e.reason)));
            setTimeout(() => log('BOOT ' + location.href), 0);
        } catch (e) { }
    });
    const errs = [];
    for (const [tag, pg] of [['T1', p1], ['T2', p2]]) {
        pg.on('pageerror', e => errs.push(`[${tag}] ${e.message}`));
    }
    await p1.goto('http://localhost:3000/', { waitUntil: 'networkidle' });
    await p2.goto('http://localhost:3000/', { waitUntil: 'networkidle' });
    console.error('T1 url=' + p1.url() + '  T2 url=' + p2.url());
    const ssAttr = await p1.evaluate(() => JSON.stringify({
        start: document.documentElement.getAttribute('data-ss-start-locked'),
        min: document.documentElement.getAttribute('data-ss-minutes'),
        sec: document.documentElement.getAttribute('data-ss-seconds'),
        pass: document.documentElement.getAttribute('data-ss-require-password'),
    }));
    console.error('T1 ss attrs: ' + ssAttr);

    const isLocked = (pg) => pg.evaluate(() => !!document.querySelector('.hr-ss') && document.body.classList.contains('hr-ss-lock'));
    const hasPwBox = (pg) => pg.evaluate(() => { const f = document.querySelector('.hr-ss-pw'); return !!f && !f.hidden; });

    // ===== انتظار برای قفل خودکار هر دو تب (آستانهٔ ۱۰ ثانیه + حاشیه) =====
    console.error('waiting ~14s for idle lock in both tabs…');
    await p1.waitForTimeout(14000);
    const l1 = await isLocked(p1), l2 = await isLocked(p2);
    console.error('locked: T1=' + l1 + ' T2=' + l2);
    if (!l1 || !l2) throw new Error('both tabs must be locked');

    // صحنهٔ بستهٔ چهارفصل در تب ۱ (iframe سندباکس) — برچسب فصل از داخل iframe
    const badge = await p1.evaluate(() => {
        const fr = document.querySelector('.hr-ss-frame');
        if (!fr) return 'NO-IFRAME(internal?)';
        try {
            const bd = fr.contentDocument && fr.contentDocument.getElementById && null;
            return 'IFRAME-PRESENT(opaque-origin: contentDocument=' + String(fr.contentDocument === null) + ')';
        } catch (e) { return 'IFRAME-OPAQUE(OK: ' + e.message.slice(0, 40) + ')'; }
    });
    console.error('screensaver mode:', badge);
    await p1.screenshot({ path: OUT + '/i106_locked.png' });

    // ===== نمایش پنل رمز با حرکت واقعی (رخداد trusted) در تب ۱ =====
    await p1.mouse.move(680, 430);
    await p1.waitForTimeout(600);
    if (!(await hasPwBox(p1))) throw new Error('password box must be visible in T1 after real movement (requirePassword=true)');
    await p1.fill('.hr-ss-pw input', 'WrongPass!23');
    // ابزار دقیق: نشانهٔ زنده‌بودن صفحه + رهگیری ناوبری و درخواست‌ها
    await p1.evaluate(() => {
        window.__marker = 42;
        window.__pj = [];
        if (window.pjax && window.pjax.load) {
            const ol = window.pjax.load;
            window.pjax.load = function (u, p) {
                window.__pj.push(u + ' @ ' + new Error().stack.split('\n')[2].trim().slice(0, 80));
                return ol.call(this, u, p);
            };
        }
        window.addEventListener('beforeunload', () => { window.__pj.push('BEFOREUNLOAD'); });
    });
    p1.on('framenavigated', f => console.error('NAVIGATED: ' + f.url()));
    p1.on('request', r => { if (r.method() === 'POST') console.error('POST: ' + r.url()); });
    await p1.click('.hr-ss-pw button');
    await p1.waitForTimeout(600);
    const nav = await p1.evaluate(() => ({
        markerAlive: window.__marker === 42,
        url: location.href, boxHidden: document.querySelector('.hr-ss-pw').hidden,
        val: document.querySelector('.hr-ss-pw input').value,
        msgHidden: document.querySelector('.hr-ss-msg').hidden
    }));
    console.error('DIAG after wrong-pass click: ' + JSON.stringify(nav));
    const slog = await p1.evaluate(() => sessionStorage.p106 || '[]');
    console.error('SESSION LOG: ' + slog);
    const stillLocked = await isLocked(p1);
    const msg = await p1.evaluate(() => {
        const m = document.querySelector('.hr-ss-msg');
        return m && !m.hidden ? m.textContent.trim() : '';
    });
    console.error('wrong pass: stillLocked=' + stillLocked + ' msg="' + msg + '"');
    if (!stillLocked) throw new Error('wrong password must NOT unlock');
    if (!msg) throw new Error('error message must show');

    // ===== رمز درست — فقط یک‌بار، فقط در تب ۱ =====
    await p1.mouse.move(680, 430);   // پنل رمز تا ۱۰ ثانیهٔ بی‌حرکتی خودش را مخفی می‌کند — دوباره بازش کن
    await p1.waitForTimeout(300);
    await p1.fill('.hr-ss-pw input', 'Admin@1234');
    await p1.click('.hr-ss-pw button');
    await p1.waitForTimeout(700);
    const unlocked1 = !(await isLocked(p1));
    console.error('T1 unlocked after ONE correct password: ' + unlocked1);
    if (!unlocked1) throw new Error('one correct password must unlock T1');

    // پنجرهٔ 423 دیررس: تا ۳ ثانیه پس از بیدارسازی نباید قفل دوباره بیاید
    await p1.waitForTimeout(3200);
    const rel1 = await isLocked(p1);
    console.error('T1 stayed unlocked 3.2s after wake (late-423 grace): ' + !rel1);
    if (rel1) throw new Error('T1 re-locked right after unlock — late-423 grace failed');

    // ===== تب ۲ باید بدون رمز بیدار شده باشد (سیگنال بین‌تبی) =====
    const unlocked2 = !(await isLocked(p2));
    const pwShown2 = await hasPwBox(p2);
    console.error('T2 unlocked WITHOUT password (cross-tab signal): ' + unlocked2 + ' (pw box shown: ' + pwShown2 + ')');
    if (!unlocked2) throw new Error('T2 must auto-unlock via storage signal — bug persists');
    if (pwShown2) throw new Error('T2 must not show a password prompt after remote unlock');

    await p1.screenshot({ path: OUT + '/i106_unlocked.png' });
    if (errs.length) { console.error('PAGE ERRORS:'); errs.forEach(e => console.error(' -', e)); throw new Error('page errors'); }
    await browser.close();
    console.error('BUG-FIX-E2E-OK: one password unlocks the whole session');
})().catch(e => { console.error('FAILED: ' + e.message); process.exit(1); });
