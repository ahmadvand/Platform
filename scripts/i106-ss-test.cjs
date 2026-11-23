// فاز ۱۰۶ — تست رندر مستقل بستهٔ چهارفصل: هر ۴ فصل + خطاهای کنسول + اسکرین‌شات
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const PKG = '/home/z/my-project/download/HR-Screensaver-FourSeasons-Package-1.0.0.json';
const OUTDIR = '/tmp/render';
fs.mkdirSync(OUTDIR, { recursive: true });

(async () => {
    const pkg = JSON.parse(fs.readFileSync(PKG, 'utf8'));
    const browser = await chromium.launch();
    const errors = [];
    for (const season of ['spring', 'summer', 'autumn', 'winter']) {
        const payload = JSON.stringify({ lang: 'fa', name: 'چهارفصل', season, cal: 'jalali' });
        const html = `<!doctype html>
<html lang="fa" dir="rtl"><head><meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data:; font-src data:; connect-src 'none'; form-action 'none'; base-uri 'none'">
<style>${pkg.code.css}</style></head>
<body>
<script id="hr-ss-data" type="application/json">${payload}<\/script>
<script>${pkg.code.js}<\/script>
</body></html>`;
        const f = path.join(OUTDIR, `ss_${season}.html`);
        fs.writeFileSync(f, html);
        const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
        page.on('pageerror', e => errors.push(`[${season}] pageerror: ${e.message}`));
        page.on('console', m => { if (m.type() === 'error') errors.push(`[${season}] console: ${m.text()}`); });
        await page.goto('file://' + f, { waitUntil: 'load' });
        await page.waitForTimeout(3200);
        await page.screenshot({ path: path.join(OUTDIR, `ss_${season}.png`) });
        // یک صحنهٔ تعاملی: تغییر فصل در حالت auto از راه payload — فقط رندر ثابت کافی است
        await page.close();
    }
    await browser.close();
    if (errors.length) {
        console.log('ERRORS:');
        errors.slice(0, 12).forEach(e => console.log(' -', e));
        process.exit(1);
    }
    console.log('all four seasons rendered without errors');
})();
