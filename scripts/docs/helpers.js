// helpers.js — مشترک ساخت اسناد Word فارسی RTL (مطابق مهارت docx: جلد R1 تطبیقی، TOC سه‌بخشی، قواعد WPS)
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  Header, Footer, PageNumber, NumberFormat, AlignmentType, HeadingLevel,
  WidthType, BorderStyle, ShadingType, SectionType, TableOfContents,
  TableLayoutType, PageBreak,
} = require("docx");

// ---------- فونت‌ها ----------
const FA = { ascii: "Vazirmatn", hAnsi: "Vazirmatn", cs: "Vazirmatn", eastAsia: "Vazirmatn" };
const CODE = { ascii: "Consolas", hAnsi: "Consolas", cs: "Tahoma", eastAsia: "Tahoma" };

// ---------- پالت‌های جلد (از design-system.md) ----------
const PALETTES = {
  "DM-1": { bg: "162235", accent: "37DCF2", table: { headerBg: "1B6B7A", headerText: "FFFFFF", accentLine: "1B6B7A", innerLine: "C8DDE2", surface: "EDF3F5" },
    cover: { titleColor: "FFFFFF", subtitleColor: "B0B8C0", metaColor: "90989F", footerColor: "687078" }, primary: "0F2A3A", secondary: "5B6B7D" },
  "GO-1": { bg: "1A2330", accent: "D4875A", table: { headerBg: "D4875A", headerText: "FFFFFF", accentLine: "D4875A", innerLine: "DDD0C8", surface: "F8F0EB" },
    cover: { titleColor: "FFFFFF", subtitleColor: "B0B8C0", metaColor: "90989F", footerColor: "687078" }, primary: "3A2A1E", secondary: "68605A" },
  "DS-1": { bg: "0B1C2C", accent: "529286", table: { headerBg: "529286", headerText: "FFFFFF", accentLine: "529286", innerLine: "BECFCC", surface: "E8ECEB" },
    cover: { titleColor: "FFFFFF", subtitleColor: "B0B8C0", metaColor: "90989F", footerColor: "687078" }, primary: "0B2A3A", secondary: "5B6B7D" },
  "WM-1": { bg: "F4F1E9", accent: "FF6A3B", table: { headerBg: "15857A", headerText: "FFFFFF", accentLine: "15857A", innerLine: "D5D0C8", surface: "F0EDE5" },
    cover: { titleColor: "15857A", subtitleColor: "606060", metaColor: "707070", footerColor: "A0A0A0" }, primary: "15857A", secondary: "606060" },
  "CM-2": { bg: "FEFEFE", accent: "FF862F", table: { headerBg: "1284BA", headerText: "FFFFFF", accentLine: "1284BA", innerLine: "D8E4EC", surface: "EDF4F9" },
    cover: { titleColor: "1284BA", subtitleColor: "606060", metaColor: "707070", footerColor: "A0A0A0" }, primary: "0F5A80", secondary: "606060" },
};

const noBorders = {
  top: { style: BorderStyle.NONE }, bottom: { style: BorderStyle.NONE },
  left: { style: BorderStyle.NONE }, right: { style: BorderStyle.NONE },
};
const allNoBorders = { ...noBorders, insideHorizontal: { style: BorderStyle.NONE }, insideVertical: { style: BorderStyle.NONE } };

// ---------- ران‌ها و پاراگراف‌ها ----------
function t(text, o = {}) {
  const rtl = o.rtl !== false && !/^[\x00-\x7F]*$/.test(String(text));
  return new TextRun({
    text: String(text),
    bold: o.bold || false, boldComplexScript: o.bold || false,
    italics: o.italics || false, italicsComplexScript: o.italics || false,
    size: o.size || 24, sizeComplexScript: o.size || 24,
    color: o.color || "000000",
    rightToLeft: rtl,
    font: o.font || FA,
  });
}

// پاراگراف بدنه — راست‌به‌چپ، جاست‌شده، فاصله 1.3x
function p(text, o = {}) {
  return new Paragraph({
    bidirectional: true,
    alignment: o.align || AlignmentType.JUSTIFIED,
    spacing: { line: 312, after: o.after !== undefined ? o.after : 120, before: o.before || 0 },
    children: Array.isArray(text) ? text : [t(text, o)],
    shading: o.fill ? { type: ShadingType.CLEAR, fill: o.fill } : undefined,
    border: o.border,
    keepNext: o.keepNext || false,
  });
}

function h1(text, pal, o = {}) {
  return new Paragraph({
    heading: HeadingLevel.HEADING_1, bidirectional: true, alignment: AlignmentType.RIGHT,
    spacing: { before: 360, after: 160, line: 380, lineRule: "atLeast" },
    children: [t(text, { bold: true, size: 32, color: pal.primary })],
    pageBreakBefore: o.pageBreakBefore || false,
  });
}
function h2(text, pal) {
  return new Paragraph({
    heading: HeadingLevel.HEADING_2, bidirectional: true, alignment: AlignmentType.RIGHT,
    spacing: { before: 260, after: 120, line: 340, lineRule: "atLeast" },
    children: [t(text, { bold: true, size: 28, color: pal.primary })],
  });
}
function h3(text, pal) {
  return new Paragraph({
    heading: HeadingLevel.HEADING_3, bidirectional: true, alignment: AlignmentType.RIGHT,
    spacing: { before: 200, after: 100, line: 312 },
    children: [t(text, { bold: true, size: 24, color: pal.primary })],
  });
}

// بولت دستی (پایدار در RTL)
function bullet(text, o = {}) {
  const runs = Array.isArray(text) ? text : [t(text)];
  return new Paragraph({
    bidirectional: true, alignment: AlignmentType.RIGHT,
    spacing: { line: 312, after: 60 },
    indent: o.sub ? { left: 360 } : undefined,
    children: [t(o.sub ? "– " : "• ", { bold: true, color: o.dotColor || "506070" }), ...runs],
  });
}

// بلاک کد — چپ‌به‌راست، مونو، با پس‌زمینه
function code(lines, o = {}) {
  const out = [];
  const cap = o.caption ? [p(o.caption, { bold: true, size: 21, after: 60, align: AlignmentType.RIGHT, keepNext: true })] : [];
  const arr = Array.isArray(lines) ? lines : String(lines).split("\n");
  arr.forEach((ln, i) => {
    out.push(new Paragraph({
      bidirectional: false, alignment: AlignmentType.LEFT,
      spacing: { line: 264, lineRule: "atLeast", before: i === 0 ? 60 : 0, after: i === arr.length - 1 ? 160 : 0 },
      shading: { type: ShadingType.CLEAR, fill: o.fill || "F4F6F8" },
      indent: { left: 200, right: 200 },
      children: [t(ln.length ? ln : " ", { size: 18, font: CODE, color: o.color || "1A2B40", rtl: false })],
    }));
  });
  return [...cap, ...out];
}

// کال‌اوت نکته — حاشیهٔ سمت راست
function note(text, pal, o = {}) {
  return p(Array.isArray(text) ? text : [t(text, { bold: o.bold || false })], {
    fill: pal.table.surface,
    border: { right: { style: BorderStyle.SINGLE, size: 16, color: pal.table.accentLine, space: 8 } },
    after: 160, align: AlignmentType.RIGHT,
  });
}

// ---------- جدول ----------
function cellPara(content, o) {
  return new Paragraph({
    bidirectional: true, alignment: o.align || AlignmentType.RIGHT,
    spacing: { line: 300, after: 0 },
    children: Array.isArray(content) ? content : [t(content, { size: o.size || 21, bold: o.bold || false, color: o.color })],
  });
}
function tbl(cfg) {
  const pal = cfg.palette;
  const widths = cfg.widths || cfg.headers.map(() => Math.floor(100 / cfg.headers.length));
  const headerRow = new TableRow({
    tableHeader: true, cantSplit: true,
    children: cfg.headers.map((h, i) => new TableCell({
      children: [cellPara(h, { bold: true, color: pal.table.headerText, align: cfg.headerAlign || AlignmentType.RIGHT })],
      shading: { type: ShadingType.CLEAR, fill: pal.table.headerBg },
      margins: { top: 70, bottom: 70, left: 110, right: 110 },
      width: { size: widths[i], type: WidthType.PERCENTAGE },
    })),
  });
  const bodyRows = cfg.rows.map((r, ri) => new TableRow({
    cantSplit: true,
    children: r.map((c, i) => new TableCell({
      children: [cellPara(c, { size: 21 })],
      shading: cfg.zebra !== false && ri % 2 === 1 ? { type: ShadingType.CLEAR, fill: pal.table.surface } : undefined,
      margins: { top: 60, bottom: 60, left: 110, right: 110 },
      width: { size: widths[i], type: WidthType.PERCENTAGE },
    })),
  }));
  return [
    ...(cfg.caption ? [p(cfg.caption, { bold: true, size: 21, after: 60, align: AlignmentType.RIGHT, keepNext: true })] : []),
    new Table({
      visuallyRightToLeft: true,
      width: { size: 100, type: WidthType.PERCENTAGE },
      layout: TableLayoutType.FIXED,
      borders: {
        top: { style: BorderStyle.SINGLE, size: 6, color: pal.table.accentLine },
        bottom: { style: BorderStyle.SINGLE, size: 6, color: pal.table.accentLine },
        left: { style: BorderStyle.NONE }, right: { style: BorderStyle.NONE },
        insideHorizontal: { style: BorderStyle.SINGLE, size: 2, color: pal.table.innerLine },
        insideVertical: { style: BorderStyle.NONE },
      },
      rows: [headerRow, ...bodyRows],
    }),
    p(" ", { size: 8, after: 60 }),
  ];
}

// ---------- جلد — R1 تطبیق‌یافتهٔ RTL ----------
function calcTitleLayout(title, maxWidthTwips, preferredPt = 40, minPt = 24) {
  const charWidth = (pt) => pt * 14; // میانگین پهنای نویسهٔ فارسی (محافظه‌کارانه)
  const charsPerLine = (pt) => Math.floor(maxWidthTwips / charWidth(pt));
  let titlePt = preferredPt, lines;
  while (titlePt >= minPt) {
    const cpl = charsPerLine(titlePt);
    if (cpl < 2) { titlePt -= 2; continue; }
    lines = splitTitleLines(title, cpl);
    if (lines.length <= 3) break;
    titlePt -= 2;
  }
  if (!lines || lines.length > 3) { lines = splitTitleLines(title, charsPerLine(minPt)); titlePt = minPt; }
  return { titlePt, titleLines: lines };
}
function splitTitleLines(title, charsPerLine) {
  if (title.length <= charsPerLine) return [title];
  const breakAfter = new Set([..."،؛:!؟ ", ..."-_—–·/"]);
  const lines = []; let remaining = title;
  while (remaining.length > charsPerLine) {
    let breakAt = -1;
    for (let i = charsPerLine; i >= Math.floor(charsPerLine * 0.6); i--) {
      if (i < remaining.length && breakAfter.has(remaining[i - 1])) { breakAt = i; break; }
    }
    // فاز ۹۸ — شکست وسط واژه ممنوع: پیش از برش سخت، آخرین جای شکست پیش از cpl (تا ابتدای رشته)
    if (breakAt === -1) {
      for (let i = charsPerLine - 1; i >= 1; i--) {
        if (breakAfter.has(remaining[i - 1])) { breakAt = i; break; }
      }
    }
    // فاز ۹۸ — وگرنه اولین جای شکست پس از cpl: واژه کمی پهن‌تر می‌شود اما هیچ‌وقت نیمه نمی‌ماند
    if (breakAt === -1) {
      for (let i = charsPerLine + 1; i < remaining.length; i++) {
        if (breakAfter.has(remaining[i - 1])) { breakAt = i; break; }
      }
    }
    if (breakAt === -1) breakAt = charsPerLine;
    lines.push(remaining.slice(0, breakAt).trim());
    remaining = remaining.slice(breakAt).trim();
  }
  if (remaining) lines.push(remaining);
  if (lines.length > 1 && lines[lines.length - 1].length <= 2) {
    // ادغام خط یتیم — فقط وقتی خط قبلی با جای شکست تمام شده (واژهٔ نیم‌بریده هرگز نبود)
    const last = lines.pop();
    lines[lines.length - 1] += " " + last;
  }
  return lines;
}
function calcCoverSpacing(params) {
  const { titleLineCount = 1, titlePt = 36, hasSubtitle = false, hasEnglishLabel = false,
    metaLineCount = 0, fixedHeight = 800, pageHeight = 16838, marginTop = 0, marginBottom = 0 } = params;
  const SAFETY = 1200;
  const usableHeight = pageHeight - marginTop - marginBottom - SAFETY;
  const titleHeight = titleLineCount * (titlePt * 23 + 200);
  const subtitleHeight = hasSubtitle ? (12 * 23 + 600) : 0;
  const englishLabelHeight = hasEnglishLabel ? (9 * 23 + 600) : 0;
  const metaHeight = metaLineCount * (10 * 23 + 100);
  const implicitParaHeight = 3 * 300;
  const contentHeight = titleHeight + subtitleHeight + englishLabelHeight + metaHeight + fixedHeight + implicitParaHeight;
  const remainingSpace = usableHeight - contentHeight;
  const safeRemaining = Math.max(remainingSpace, 400);
  const FOOTER_MIN = 800;
  const rawTop = Math.floor(safeRemaining * 0.45);
  const rawBottom = Math.floor(safeRemaining * 0.45);
  const bottomSpacing = Math.max(rawBottom, FOOTER_MIN);
  const topSpacing = Math.max(rawTop - Math.max(0, FOOTER_MIN - rawBottom), 400);
  const midSpacing = Math.max(safeRemaining - topSpacing - bottomSpacing, 0);
  return { topSpacing, midSpacing, bottomSpacing };
}

function coverR1RTL(config) {
  const P = config.palette;
  const padR = 1200, padL = 800; // در RTL پدینگ از سمت راست
  const availableWidth = 11906 - padR - padL - 300;
  const { titlePt, titleLines } = calcTitleLayout(config.title, availableWidth, 40, 24);
  const titleSize = titlePt * 2;
  const spacing = calcCoverSpacing({
    titleLineCount: titleLines.length, titlePt,
    hasSubtitle: !!config.subtitle, hasEnglishLabel: !!config.englishLabel,
    metaLineCount: (config.metaLines || []).length, fixedHeight: 400,
  });
  const accentRight = { style: BorderStyle.SINGLE, size: 8, color: P.accent, space: 12 };
  const children = [];
  children.push(new Paragraph({ spacing: { before: spacing.topSpacing } }));
  if (config.englishLabel) {
    children.push(new Paragraph({
      bidirectional: false, alignment: AlignmentType.RIGHT,
      indent: { right: padR, left: padL }, spacing: { after: 500 },
      border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: P.accent, space: 8 } },
      children: [new TextRun({ text: config.englishLabel.split("").join(" "), size: 18, color: P.accent, font: { ascii: "Calibri", hAnsi: "Calibri" }, characterSpacing: 40 })],
    }));
  }
  for (let i = 0; i < titleLines.length; i++) {
    children.push(new Paragraph({
      bidirectional: true, alignment: AlignmentType.RIGHT,
      indent: { right: padR, left: padL },
      spacing: { after: i < titleLines.length - 1 ? 100 : 300, line: Math.ceil(titlePt * 23), lineRule: "atLeast" },
      children: [t(titleLines[i], { size: titleSize, bold: true, color: P.cover.titleColor })],
    }));
  }
  if (config.subtitle) {
    children.push(new Paragraph({
      bidirectional: true, alignment: AlignmentType.RIGHT,
      indent: { right: padR, left: padL }, spacing: { after: 800, line: 340, lineRule: "atLeast" },
      children: [t(config.subtitle, { size: 24, color: P.cover.subtitleColor })],
    }));
  }
  for (const line of (config.metaLines || [])) {
    children.push(new Paragraph({
      bidirectional: true, alignment: AlignmentType.RIGHT,
      indent: { right: padR + 200, left: padL }, spacing: { after: 80, line: 300, lineRule: "atLeast" },
      border: { right: accentRight },
      children: [t(line, { size: 24, color: P.cover.metaColor })],
    }));
  }
  children.push(new Paragraph({ spacing: { before: spacing.bottomSpacing } }));
  children.push(new Paragraph({
    bidirectional: true, alignment: AlignmentType.RIGHT,
    indent: { right: padR, left: padL },
    border: { top: { style: BorderStyle.SINGLE, size: 2, color: P.accent, space: 8 } },
    spacing: { before: 200 },
    children: [
      t(config.footerRight || "", { size: 16, color: P.cover.footerColor }),
      new TextRun({ text: "                                        ", size: 16 }),
      t(config.footerLeft || "", { size: 16, color: P.cover.footerColor }),
    ],
  }));
  return [new Table({
    width: { size: 100, type: WidthType.PERCENTAGE },
    layout: TableLayoutType.FIXED,
    borders: allNoBorders,
    rows: [new TableRow({
      height: { value: 16838, rule: "exact" },
      children: [new TableCell({
        shading: { type: ShadingType.CLEAR, fill: P.bg }, borders: noBorders,
        children,
      })],
    })],
  })];
}

// ---------- اسکلت سند: جلد → فهرست (رومی) → بدنه (عربی از ۱) ----------
function pageNumFooter() {
  return new Footer({
    children: [new Paragraph({
      alignment: AlignmentType.CENTER,
      children: [new TextRun({ children: [PageNumber.CURRENT], size: 18, font: FA, color: "687078" })],
    })],
  });
}
function docHeader(titleText) {
  return new Header({
    children: [new Paragraph({
      bidirectional: true, alignment: AlignmentType.RIGHT,
      border: { bottom: { style: BorderStyle.SINGLE, size: 2, color: "C8D0DA", space: 4 } },
      spacing: { after: 0 },
      children: [t(titleText, { size: 17, color: "8A93A6" })],
    })],
  });
}

function buildDoc(cfg) {
  // cfg: { file, cover:{title,subtitle,englishLabel,metaLines,footerRight,footerLeft}, palette, headerTitle, frontChildren, bodyChildren }
  const pal = cfg.palette;
  const pgSize = { width: 11906, height: 16838 };
  const pgMargin = { top: 1440, bottom: 1440, left: 1417, right: 1560 };
  const frontChildren = [
    new Paragraph({
      bidirectional: true, alignment: AlignmentType.CENTER,
      spacing: { before: 480, after: 360, line: 420, lineRule: "atLeast" },
      children: [t("فهرست مطالب", { bold: true, size: 32, color: pal.primary })],
    }),
    new TableOfContents("Table of Contents", { hyperlink: true, headingStyleRange: "1-3" }),
    new Paragraph({
      bidirectional: true, alignment: AlignmentType.RIGHT, spacing: { before: 200 },
      children: [t("یادآوری: این فهرست با کد فیلد ساخته شده است؛ پس از ویرایش سند در Word، روی فهرست راست‌کلیک و «Update Field» را بزنید تا شماره صفحه‌ها به‌روز شود.", { italics: true, size: 18, color: "888888" })],
    }),
    ...(cfg.frontChildren || []),
  ];
  const doc = new Document({
    creator: cfg.author || "Z.ai",
    title: cfg.cover.title,
    description: cfg.cover.subtitle || "",
    styles: {
      default: {
        document: {
          run: { font: FA, size: 24, color: "000000" },
          paragraph: { spacing: { line: 312 } },
        },
        heading1: { run: { font: FA, size: 32, bold: true, color: pal.primary }, paragraph: { spacing: { before: 360, after: 160, line: 380 }, outlineLevel: 0 } },
        heading2: { run: { font: FA, size: 28, bold: true, color: pal.primary }, paragraph: { spacing: { before: 260, after: 120, line: 340 }, outlineLevel: 1 } },
        heading3: { run: { font: FA, size: 24, bold: true, color: pal.primary }, paragraph: { spacing: { before: 200, after: 100, line: 312 }, outlineLevel: 2 } },
      },
    },
    sections: [
      { // جلد — حاشیه صفر، بدون شماره صفحه
        properties: { page: { size: pgSize, margin: { top: 0, bottom: 0, left: 0, right: 0 } } },
        children: coverR1RTL({ ...cfg.cover, palette: pal }),
      },
      { // فهرست — رومی
        properties: {
          type: SectionType.NEXT_PAGE,
          page: { size: pgSize, margin: pgMargin, pageNumbers: { start: 1, formatType: NumberFormat.UPPER_ROMAN } },
        },
        headers: { default: docHeader(cfg.headerTitle) },
        footers: { default: pageNumFooter() },
        children: frontChildren,
      },
      { // بدنه — عربی از ۱
        properties: {
          type: SectionType.NEXT_PAGE,
          page: { size: pgSize, margin: pgMargin, pageNumbers: { start: 1, formatType: NumberFormat.DECIMAL } },
        },
        headers: { default: docHeader(cfg.headerTitle) },
        footers: { default: pageNumFooter() },
        children: cfg.bodyChildren,
      },
    ],
  });
  return Packer.toBuffer(doc).then(buf => {
    require("fs").writeFileSync(cfg.file, buf);
    console.log("WROTE", cfg.file, buf.length, "bytes");
  });
}

module.exports = { PALETTES, FA, CODE, t, p, h1, h2, h3, bullet, code, note, tbl, buildDoc, AlignmentType };
