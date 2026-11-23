const { chromium } = require('playwright');
const { execSync } = require('child_process');

(async () => {
  const cookieRaw = execSync(`grep hr_at /tmp/c.txt | awk '{print $6"="$7}'`).toString().trim();
  const [name, ...rest] = cookieRaw.split('=');
  const value = rest.join('=');

  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 950 } });
  await ctx.addCookies([{ name, value, domain: 'localhost', path: '/' }]);

  const shots = [];
  for (const lang of ['en', 'fa']) {
    const p2 = await ctx.newPage();
    await ctx.addCookies([{ name: 'hr_lang', value: lang, domain: 'localhost', path: '/' }]);
    await p2.goto('http://localhost:3000/Devices/Create', { waitUntil: 'networkidle' });
    await p2.waitForTimeout(700);
    const f = `/tmp/render/devform_${lang}.png`;
    await p2.screenshot({ path: f });
    shots.push(f);
    await p2.close();
  }
  console.log('shots:', shots.join(', '));
  await browser.close();
})().catch(e => { console.error('ERR', e.message); process.exit(1); });
