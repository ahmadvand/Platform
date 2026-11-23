// run-all.js — تولید هر ۵ سند Word
const H = require("./helpers");

(async () => {
  const docs = [
    require("./doc-handoff"),
    require("./doc-core"),
    require("./doc-setup"),
    require("./doc-user"),
    require("./doc-arch"),
  ];
  for (const d of docs) {
    await H.buildDoc({
      file: d.file, palette: d.palette, cover: d.cover,
      headerTitle: d.headerTitle, bodyChildren: d.bodyChildren,
    });
  }
  console.log("ALL DOCS DONE");
})().catch(e => { console.error("FAIL:", e); process.exit(1); });
