// inspect-blocks.js
const d = require("./doc-core");
for (let i = 45; i < 56; i++) {
  const b = d.bodyChildren[i];
  let s = "";
  try { s = JSON.stringify(b); } catch (e) { s = "ERR"; }
  const strs = [...s.matchAll(/"root":\s*\[\s*"((?:[^"\\]|\\.)*)"/g)].map(m => m[1]).filter(x => x.trim());
  console.log(i + 1, b.constructor.name, "|", strs.slice(0, 6).join(" § ").slice(0, 220));
}
