#!/usr/bin/env node
// bisect-core.js — پیدا کردن بلوک بدنهٔ سند هسته که خروجی PDF لو را حلقه می‌کند
const H = require("./helpers");
const { execSync } = require("child_process");
const fs = require("fs");
const d = require("./doc-core");
const N = d.bodyChildren.length;
console.log("total blocks:", N);

const OUT = "/tmp/bisect";
fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(OUT, { recursive: true });

function run(cmd, timeoutMs) {
  try {
    execSync(cmd, { timeout: timeoutMs, stdio: ["ignore", "pipe", "pipe"] });
    return true;
  } catch (e) { return false; }
}

async function test(count, inject) {
  const file = `${OUT}/t-${count}-${inject ? "inj" : "raw"}.docx`;
  await H.buildDoc({
    file, palette: d.palette, cover: d.cover, headerTitle: d.headerTitle,
    bodyChildren: d.bodyChildren.slice(0, count),
  });
  run(`python3 /home/z/my-project/scripts/docs/fix-footers.py ${file}`, 30000);
  if (inject) {
    if (!run(`timeout 60 soffice -env:UserInstallation=file:///tmp/lo-profile --headless --convert-to pdf ${file} --outdir ${OUT}`, 70000)) return "hangA";
    if (!run(`python3 /home/z/my-project/scripts/docs/toc-pages.py ${file} ${OUT}/t-${count}-raw.pdf`, 60000)) return "injFail";
  }
  const ok = run(`timeout 70 soffice -env:UserInstallation=file:///tmp/lo-profile --headless --convert-to pdf ${file} --outdir ${OUT}/out`, 80000);
  return ok ? "ok" : "HANG";
}

(async () => {
  console.log("control raw full:", await test(N, false));
  let lo = 1, hi = N; // smallest count that hangs (with injection)
  console.log("mid inject:", await test(Math.floor(N / 2), true));
  // binary search
  while (lo < hi) {
    const mid = Math.floor((lo + hi) / 2);
    const r = await test(mid, true);
    console.log(`count=${mid} -> ${r}`);
    if (r === "HANG") hi = mid; else lo = mid + 1;
    if (r === "hangA" || r === "injFail") { console.log("pipeline issue at", mid); break; }
  }
  console.log("SMALLEST HANGING PREFIX:", lo, "block:", JSON.stringify(String(d.bodyChildren[lo - 1] && d.bodyChildren[lo - 1].constructor.name)));
  const b = d.bodyChildren[lo - 1];
  if (b && b.root) {
    try { console.log(JSON.stringify(b).slice(0, 600)); } catch (e) { console.log("block dump failed"); }
  }
})();
