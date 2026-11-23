// فاز ۱۰۶ — آزمون بصری «انتخاب دستی فصل»: تنظیم season=spring → قفل → برچسب باید «بهار · دستی» باشد
const { chromium } = require('playwright');
const fs = require('fs');
const OUT = '/tmp/render';
fs.mkdirSync(OUT, { recursive: true });

(async () => {
    const browser = await chromium.launch();
    const ctx = await browser.newContext({ viewport: { width: 1360, height: 860 } });
    const p0 = await ctx.newPage();
    await p0.goto('http://localhost:3000/Auth/Login', { waitUntil: 'networkidle' });
    await p0.fill('#identifier', 'admin');
    await p0.fill('#secret', 'Admin@1234');
    await p0.click('button[type="submit"]');
    await p0.waitForLoadState('networkidle');
    if (p0.url().includes('/License/Notice')) {
        await p0.click('form[action="/License/Ack"] button');
        await p0.waitForLoadState('networkidle');
    }

    // فصل دستی = بهار از مسیر UI واقعی (تب محافظ صفحه)
    await p0.goto('http://localhost:3000/Settings#tab-screensaver', { waitUntil: 'networkidle' });
    await p0.selectOption('select[name="ssSeason"]', 'spring');
    await p0.click('#tab-screensaver button.btn-primary');
    await p0.waitForLoadState('networkidle');
    await p0.waitForTimeout(800);

    const p1 = await ctx.newPage();
    await p1.goto('http://localhost:3000/', { waitUntil: 'networkidle' });
    const isLocked = (pg) => pg.evaluate(() => !!document.querySelector('.hr-ss') && document.body.classList.contains('hr-ss-lock'));
    console.error('waiting ~14s for idle lock…');
    await p1.waitForTimeout(14000);
    if (!(await isLocked(p1))) throw new Error('not locked');
    await p1.waitForTimeout(1500);   // فرصت رندر صحنه
    await p1.screenshot({ path: OUT + '/i106_spring_manual.png' });

    // برچسب فصل داخل iframe سندباکس خواندنی نیست (opaque) — ولی payload سرویشده را چک می‌کنیم
    const resp = await p1.request.get('http://localhost:3000/Settings/ScreensaverRuntime');
    const html = await resp.text();
    const m = html.match(/id="hr-ss-data" type="application\/json">(.*?)</);
    const payload = JSON.parse(m[1]);
    console.error('payload: ' + JSON.stringify({ season: payload.season, cal: payload.cal }));
    if (payload.season !== 'spring') throw new Error('manual season not applied: ' + payload.season);
    await browser.close();
    console.error('MANUAL-SEASON-OK: payload=spring (badge در اسکرین‌شات باید «بهار · دستی» باشد)');
})().catch(e => { console.error('FAILED: ' + e.message); process.exit(1); });
