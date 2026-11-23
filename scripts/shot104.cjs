const { chromium } = require('playwright');
const { execSync } = require('child_process');

(async () => {
  const cookieRaw = execSync(`cat /tmp/hrcookie.txt`).toString().trim();
  const [name, ...rest] = cookieRaw.split('=');
  const value = rest.join('=');
  const lang = process.argv[2] || 'ar';
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1360, height: 900 } });
  await ctx.addCookies([
    { name, value, domain: 'localhost', path: '/' },
    { name: 'hr_lang', value: lang, domain: 'localhost', path: '/' },
  ]);
  const page = await ctx.newPage();
  await page.goto('http://localhost:3000/Biometrics#transfer', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1200);
  await page.screenshot({ path: `/tmp/render/bio_transfer_${lang}.png` });
  await page.click('.btab[data-tab="users"]').catch(() => {});
  await page.waitForTimeout(500);
  await page.screenshot({ path: `/tmp/render/bio_users_${lang}.png` });
  await browser.close();
  console.log('shots done');
})();
