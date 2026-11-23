const { chromium } = require('playwright');
const { execSync } = require('child_process');
(async () => {
  const cookieRaw = execSync(`cat /tmp/hrcookie.txt`).toString().trim();
  const [name, ...rest] = cookieRaw.split('=');
  const value = rest.join('=');
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1360, height: 900 } });
  await ctx.addCookies([
    { name, value, domain: 'localhost', path: '/' },
    { name: 'hr_lang', value: 'ar', domain: 'localhost', path: '/' },
  ]);
  const page = await ctx.newPage();
  await page.goto('http://localhost:3000/Devices/Create', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);
  // باز کردن دراپ‌داون نوع دستگاه
  const sel = await page.$('#fKind');
  if (sel) {
    await sel.click().catch(() => {});
  }
  await page.screenshot({ path: '/tmp/render/device_form_ar.png' });
  const opts = await page.$$eval('#fKind option', os => os.map(o => o.textContent.trim()));
  console.log(JSON.stringify(opts, null, 1));
  await browser.close();
})();
