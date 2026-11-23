#!/usr/bin/env node
// bisect-core2.js — دوبخشی خام (بدون تزریق فهرست) برای یافتن بلوک عاملِ حلقهٔ PDF
const H = require("./helpers");
const { execSync } = require("child_process");
const fs = require("fs");
const d = require("./doc-core");
const N = d.bodyChildren.length;
console.log("total blocks:", N);
const OUT = "/tmp/bisect2";
fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(OUT, { recursive: true });

function run(cmd, timeoutMs) {
  try { execSync(cmd, { timeout: timeoutMs, stdio: ["ignore", "pipe", "pipe"] }); return true; }
  catch (e) { return false; }
}
async function test(count) {
  const file = `${OUT}/b-${count}.docx`;
  await H.buildDoc({
    file, palette: d.palette, cover: d.cover, headerTitle: d.headerTitle,
    bodyChildren: d.bodyChildren.slice(0, count),
  });
  run(`python3 /home/z/my-project/scripts/docs/fix-footers.py ${file}`, 30000);
  return run(`timeout 60 soffice -env:UserInstallation=file:///tmp/lo-profile --headless --convert-to pdf ${file} --outdir ${OUT}`, 80000) ? "ok" : "HANG";
}
(async () => {
  let lo = 2, hi = N;
  if (await test(N) !== "HANG") { console.log("full OK?!"); return; }
  while (lo < hi) {
    const mid = Math.floor((lo + hi) / 2);
    const r = await test(mid);
    console.log(`count=${mid} -> ${r}`);
    if (r === "HANG") hi = mid; else lo = mid + 1;
  }
  console.log("SMALLEST HANGING PREFIX:", lo, "/", N);
  const b = d.bodyChildren[lo - 1];
  if (b) {
    const txt = JSON.stringify(b);
    const texts = [...txt.matchAll(/"text":"((?:[^"\\]|\\.)*)"/g)].map(m => m[1]);
    console.log("LAST-BLOCK texts:", texts.slice(0, 6));
  }
})();
