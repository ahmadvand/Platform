const { chromium } = require('playwright');
const { execSync } = require('child_process');

(async () => {
  const cookieRaw = execSync(`grep hr_at /tmp/c.txt | awk '{print $6"="$7}'`).toString().trim();
  const [name, ...rest] = cookieRaw.split('=');
  const value = rest.join('=');

  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1360, height: 900 } });
  await ctx.addCookies([
    { name, value, domain: 'localhost', path: '/' },
    { name: 'hr_lang', value: process.argv[2] || 'fa', domain: 'localhost', path: '/' },
  ]);

  const page = await ctx.newPage();
  await page.goto('http://localhost:3000/', { waitUntil: 'networkidle' });
  await page.waitForTimeout(800);
  await page.screenshot({ path: `/tmp/render/dash_${process.argv[2] || 'fa'}.png` });

  // open the language switcher dropdown
  await page.click('#tbLangBtn').catch(() => {});
  await page.waitForTimeout(400);
  await page.screenshot({ path: `/tmp/render/langmenu_${process.argv[2] || 'fa'}.png` });

  await browser.close();
  console.log('done');
})();
