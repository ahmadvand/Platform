// ============================================================
// گزارش انطباق OWASP Top 10:2025 — سند Word فارسی (RTL)
// docx-js — طبق اسکیل docx: Recipe R1 (RTL-mirrored) + پالت DM-1 + سه سکشن + TOC
// ============================================================
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  Header, Footer, PageNumber, NumberFormat, AlignmentType, HeadingLevel,
  WidthType, BorderStyle, ShadingType, TableLayoutType, SectionType,
  TableOfContents, PageBreak,
} = require("docx");
const fs = require("fs");

// ---------- پالت DM-1 (Deep Cyan — فناوری) از design-system ----------
const P = {
  bg: "162235", accent: "37DCF2",
  cover: { titleColor: "FFFFFF", subtitleColor: "B0B8C0", metaColor: "90989F", footerColor: "687078" },
  table: { headerBg: "1B6B7A", headerText: "FFFFFF", accentLine: "1B6B7A", innerLine: "C8DDE2", surface: "EDF3F5" },
};
const BODY_COLOR = "000000"; // Profile A — متن بدنه سیاه خالص
const HEAD_COLOR = "134B57"; // سرتیترها — نسخهٔ تیرهٔ accent برای خوانایی روی سفید

// ---------- فونت فارسی (Complex Script) ----------
const FA = { ascii: "Tahoma", hAnsi: "Tahoma", cs: "Tahoma", eastAsia: "Tahoma" };

// ---------- کمک‌سازهای RTL ----------
function safeText(v, ph) {
  if (v === undefined || v === null || v === "" || String(v) === "NaN" || String(v) === "undefined")
    return ph || "【تکمیل شود】";
  return String(v);
}
function fr(text, o = {}) { // فارسی TextRun
  return new TextRun({
    text: safeText(text), rightToLeft: true, font: FA,
    size: o.size || 24, sizeComplexScript: o.size || 24,
    bold: o.bold || false, boldComplexScript: o.bold || false,
    italics: o.italics || false, color: o.color || BODY_COLOR,
  });
}
function en(text, o = {}) { // متن لاتین درون سند فارسی
  return new TextRun({
    text: safeText(text), font: FA,
    size: o.size || 22, sizeComplexScript: o.size || 22,
    bold: o.bold || false, color: o.color || BODY_COLOR,
  });
}
function faH1(text) {
  return new Paragraph({
    heading: HeadingLevel.HEADING_1, bidirectional: true, alignment: AlignmentType.RIGHT,
    spacing: { before: 360, after: 160, line: 380, lineRule: "atLeast" },
    children: [fr(text, { size: 32, bold: true, color: HEAD_COLOR })],
  });
}
function faH2(text) {
  return new Paragraph({
    heading: HeadingLevel.HEADING_2, bidirectional: true, alignment: AlignmentType.RIGHT,
    spacing: { before: 240, after: 120, line: 340, lineRule: "atLeast" },
    children: [fr(text, { size: 28, bold: true, color: HEAD_COLOR })],
  });
}
function faP(runsOrText, o = {}) { // پاراگراف بدنه — راست‌چین/تراز دوطرفه
  const children = Array.isArray(runsOrText) ? runsOrText : [fr(runsOrText)];
  return new Paragraph({
    bidirectional: true, alignment: o.align || AlignmentType.JUSTIFIED,
    spacing: { after: 120, line: 312 }, children,
  });
}
function bullet(runsOrText, o = {}) {
  const children = Array.isArray(runsOrText) ? runsOrText : [fr(runsOrText)];
  return new Paragraph({
    bidirectional: true, alignment: AlignmentType.RIGHT, bullet: { level: 0 },
    spacing: { after: 60, line: 312 }, children,
  });
}
function tableTitle(text) {
  return new Paragraph({
    bidirectional: true, alignment: AlignmentType.RIGHT, keepNext: true,
    spacing: { before: 160, after: 80 },
    children: [fr(text, { size: 21, bold: true, color: HEAD_COLOR })],
  });
}

// ---------- جدول RTL استاندارد ----------
function cellPara(content, o = {}) {
  const children = Array.isArray(content) ? content : [fr(content, { size: o.size || 21, bold: o.bold, color: o.color })];
  return new Paragraph({ bidirectional: true, alignment: o.align || AlignmentType.RIGHT, spacing: { line: 312 }, children });
}
function rtlTable(headers, rows, widths) {
  const mk = (texts, isHead) => new TableRow({
    tableHeader: isHead, cantSplit: true,
    children: texts.map((t, i) => new TableCell({
      children: [cellPara(t, isHead ? { bold: true, color: P.table.headerText, align: AlignmentType.CENTER } : {})],
      shading: { type: ShadingType.CLEAR, fill: isHead ? P.table.headerBg : "FFFFFF" },
      margins: { top: 60, bottom: 60, left: 120, right: 120 },
      width: { size: widths[i], type: WidthType.PERCENTAGE },
    })),
  });
  return new Table({
    visuallyRightToLeft: true,
    width: { size: 100, type: WidthType.PERCENTAGE },
    borders: {
      top: { style: BorderStyle.SINGLE, size: 4, color: P.table.accentLine },
      bottom: { style: BorderStyle.SINGLE, size: 4, color: P.table.accentLine },
      left: { style: BorderStyle.NONE }, right: { style: BorderStyle.NONE },
      insideHorizontal: { style: BorderStyle.SINGLE, size: 2, color: P.table.innerLine },
      insideVertical: { style: BorderStyle.SINGLE, size: 2, color: P.table.innerLine },
    },
    rows: [mk(headers, true), ...rows.map(r => mk(r, false))],
  });
}

// ---------- جلد — Recipe R1 (آینهٔ RTL؛ ساختار/wrapper/spacing طبق دستور) ----------
const NB = { style: BorderStyle.NONE, size: 0, color: "FFFFFF" };
const noBorders = { top: NB, bottom: NB, left: NB, right: NB };
const allNoBorders = { top: NB, bottom: NB, left: NB, right: NB, insideHorizontal: NB, insideVertical: NB };

function calcTitleLayout(title, maxWidthTwips, preferredPt = 40, minPt = 24) {
  const charWidth = (pt) => pt * 20;
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
  const breakAfter = new Set([..."،,؛:!؟?؛", ..."-_—–·/", ..." \t"]);
  const lines = []; let remaining = title;
  while (remaining.length > charsPerLine) {
    let breakAt = -1;
    for (let i = charsPerLine; i >= Math.floor(charsPerLine * 0.6); i--)
      if (i < remaining.length && breakAfter.has(remaining[i - 1])) { breakAt = i; break; }
    if (breakAt === -1) {
      const limit = Math.min(remaining.length, Math.ceil(charsPerLine * 1.3));
      for (let i = charsPerLine + 1; i < limit; i++)
        if (breakAfter.has(remaining[i - 1])) { breakAt = i; break; }
    }
    if (breakAt === -1) breakAt = charsPerLine;
    lines.push(remaining.slice(0, breakAt).trim());
    remaining = remaining.slice(breakAt).trim();
  }
  if (remaining) lines.push(remaining);
  if (lines.length > 1 && lines[lines.length - 1].length <= 2) {
    const last = lines.pop(); lines[lines.length - 1] += " " + last;
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
  const safeRemaining = Math.max(usableHeight - contentHeight, 400);
  const FOOTER_MIN = 800;
  const rawTop = Math.floor(safeRemaining * 0.45);
  const rawBottom = Math.floor(safeRemaining * 0.45);
  const bottomSpacing = Math.max(rawBottom, FOOTER_MIN);
  const topSpacing = Math.max(rawTop - Math.max(0, FOOTER_MIN - rawBottom), 400);
  const midSpacing = Math.max(safeRemaining - topSpacing - bottomSpacing, 0);
  return { topSpacing, midSpacing, bottomSpacing };
}

function buildCoverR1RTL(config) {
  const PC = config.palette;
  const padR = 1200, padL = 800; // آینهٔ RTL: پد اصلی سمت راست
  const availableWidth = 11906 - padR - padL - 300;
  const { titlePt, titleLines } = calcTitleLayout(config.title, availableWidth, 40, 24);
  const titleSize = titlePt * 2;
  const spacing = calcCoverSpacing({
    titleLineCount: titleLines.length, titlePt,
    hasSubtitle: !!config.subtitle, hasEnglishLabel: !!config.englishLabel,
    metaLineCount: (config.metaLines || []).length, fixedHeight: 400,
  });
  const accentRight = { style: BorderStyle.SINGLE, size: 8, color: PC.accent, space: 12 };
  const children = [];
  children.push(new Paragraph({ spacing: { before: spacing.topSpacing } }));
  if (config.englishLabel) {
    children.push(new Paragraph({
      indent: { left: padL, right: padR }, spacing: { after: 500 }, alignment: AlignmentType.RIGHT,
      border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: PC.accent, space: 8 } },
      children: [new TextRun({ text: config.englishLabel.split("").join("  "), size: 18, color: PC.accent, font: FA, characterSpacing: 40 })],
    }));
  }
  for (let i = 0; i < titleLines.length; i++) {
    children.push(new Paragraph({
      bidirectional: true, alignment: AlignmentType.RIGHT, indent: { right: padR },
      spacing: { after: i < titleLines.length - 1 ? 100 : 300, line: Math.ceil(titlePt * 23), lineRule: "atLeast" },
      children: [fr(titleLines[i], { size: titleSize, bold: true, color: PC.cover.titleColor })],
    }));
  }
  if (config.subtitle) {
    children.push(new Paragraph({
      bidirectional: true, alignment: AlignmentType.RIGHT, indent: { right: padR }, spacing: { after: 800, line: 340, lineRule: "atLeast" },
      children: [fr(config.subtitle, { size: 24, color: PC.cover.subtitleColor })],
    }));
  }
  for (const line of (config.metaLines || [])) {
    children.push(new Paragraph({
      bidirectional: true, alignment: AlignmentType.RIGHT, indent: { right: padR + 200 }, spacing: { after: 80, line: 320, lineRule: "atLeast" },
      border: { right: accentRight },
      children: [fr(line, { size: 24, color: PC.cover.metaColor })],
    }));
  }
  children.push(new Paragraph({ spacing: { before: spacing.bottomSpacing } }));
  children.push(new Paragraph({
    bidirectional: true, alignment: AlignmentType.RIGHT, indent: { left: padL, right: padR },
    border: { top: { style: BorderStyle.SINGLE, size: 2, color: PC.accent, space: 8 } },
    spacing: { before: 200 },
    children: [
      fr(config.footerRight || "", { size: 16, color: PC.cover.footerColor }),
      new TextRun({ text: "                                        ", size: 16 }),
      fr(config.footerLeft || "", { size: 16, color: PC.cover.footerColor }),
    ],
  }));
  return [new Table({
    width: { size: 100, type: WidthType.PERCENTAGE },
    layout: TableLayoutType.FIXED, borders: allNoBorders,
    rows: [new TableRow({
      height: { value: 16838, rule: "exact" },
      children: [new TableCell({
        shading: { type: ShadingType.CLEAR, fill: PC.bg }, borders: noBorders,
        verticalAlign: "top", children,
      })],
    })],
  })];
}

// ---------- سرصفحه/پاصفحه ----------
const pageHeader = () => new Header({
  children: [new Paragraph({
    bidirectional: true, alignment: AlignmentType.CENTER,
    border: { bottom: { style: BorderStyle.SINGLE, size: 2, color: P.table.innerLine, space: 4 } },
    children: [fr("گزارش انطباق امنیتی OWASP Top 10:2025 — سامانهٔ حضور و غیاب ماژولار", { size: 18, color: "888888" })],
  })],
});
const pageNumFooter = () => new Footer({
  children: [new Paragraph({
    alignment: AlignmentType.CENTER,
    children: [new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "808080", font: FA })],
  })],
});

// ============================================================
// محتوا
// ============================================================
const coverConfig = {
  title: "گزارش انطباق امنیتی با OWASP Top 10:2025",
  subtitle: "سامانهٔ حضور و غیاب و مدیریت نیروی انسانی ماژولار — پلتفرم وب ASP.NET Core 8",
  englishLabel: "OWASP TOP 10:2025 COMPLIANCE",
  metaLines: [
    "نوع سند: گزارش انطباق امنیتی برای ممیزی گواهی",
    "دامنهٔ ارزیابی: راه‌حل Platform.sln (هاست وب + ماژول‌های افزونه)",
    "مرجع استاندارد: OWASP Top 10:2025 (انتشار رسمی نوامبر ۲۰۲۵)",
    "تاریخ گزارش: ۱۲ آبان ۱۴۰۵ (۳ نوامبر ۲۰۲۶) — نسخهٔ ۱٫۰",
  ],
  footerLeft: "محرمانه — ویژهٔ ممیزی امنیتی",
  footerRight: "سامانهٔ حضور و غیاب ماژولار",
  palette: P,
};

// ---------- فصل ۱: خلاصه مدیریتی ----------
const ch1 = [
  faH1("۱. خلاصه مدیریتی"),
  faP("سامانهٔ حضور و غیاب ماژولار (پلتفرم وب) در تاریخ ۱۲ آبان ۱۴۰۵ در برابر ویرایش ششم استاندارد OWASP Top 10:2025 — آخرین نسخهٔ رسمی این استاندارد که در نوامبر ۲۰۲۵ منتشر شده است — ارزیابی و سخت‌سازی شد. این استاندارد ده ریسک بحرانی نرم‌افزارهای وب را رتبه‌بندی می‌کند و مبنای متداول گواهی‌های امنیتی ممیزان مستقل است. ارزیابی شامل بازبینی کد لایه‌های احراز هویت، رمزنگاری، دسترسی و پایگاه داده، و سپس راستی‌آزمایی زندهٔ رفتار سامانه در محیط اجرا بود."),
  faP([fr("خروجی این کار: "), fr("هر ده دستهٔ ریسک استاندارد اکنون با کنترل‌های لایه‌ای پوشش داده شده‌اند", { bold: true }), fr("؛ از جمله میان‌افزار تازهٔ هدرهای امنیتی HTTP با سیاست امنیت محتوا (CSP)، حذف هویت سرور از پاسخ‌ها، چرخش خودکار رمز امضای توکن‌ها به‌جای مقدار پیش‌فرض قالب، محدودسازی نرخ سراسری دو لایه در برابر حملهٔ سیلابی و حدس رمز، وصلهٔ سه بستهٔ وابستهٔ آسیب‌پذیر، و بستن نشت جزئیات خطای بانک داده از نقطهٔ سلامت سرویس. اسکن وابستگی‌ها پس از وصله، هیچ بستهٔ آسیب‌پذیری نشان نمی‌دهد و ساخت کامل راه‌حل با صفر خطا انجام شد.")]),
  faP([fr("تمامی تغییرات «افزودنی و پیکربندی‌پذیر» طراحی شدند: هیچ مسیر، صفحه، رابط برنامه‌نویسی یا قابلیتی حذف یا محدود نشد و رفتار پیش‌فرض سازگار با استقرارهای فعلی باقی مانده است. پروژه‌های ابزاری استودیوها (LicenseStudio و TranslationStudio) طبق دستور کارفرما هیچ تغییری نکردند. جداول فصل پنجم همین سند، نگاشت یک‌به‌یک هر ریسک به کنترل‌های فعال و شواهد آزمون را برای ارائه به ممیز ارائه می‌کند.")]),
];

// ---------- فصل ۲: محدوده، مبنا و روش‌شناسی ----------
const ch2 = [
  faH1("۲. محدوده، مبنا و روش‌شناسی"),
  faH2("۲.۱. محدودهٔ ارزیابی"),
  faP("محدودهٔ این انطباق، نرم‌افزار وب پلتفرم است: راه‌حل Platform.sln شامل هاست وب (HrAttendance.Web)، هستهٔ پلتفرم (HrPlatform.Core و HrPlatform.Contracts) و ماژول‌های افزونه (حضور و غیاب، سازمان و پرسنل، دستگاه‌ها، دروازهٔ دستگاه‌ها، کنترل تردد، پارکینگ، حقوق و دستمزد، میهمانان). ورودی دستگاه‌های حضورسنج و دوربین‌های پلاک‌خوان از مسیر گیت‌وی درون همین نرم‌افزار وب سرو می‌شود و در ارزیابی پوشش داده شده است. ابزارهای دسکتاپ استودیوها خارج از دامنهٔ این سند و به‌درخواست کارفرما دست‌نخورده‌اند."),
  faH2("۲.۲. مرجع استاندارد"),
  faP("مرجع ارزیابی، فهرست OWASP Top 10:2025 است که در نوامبر ۲۰۲۵ منتشر شد و جایگزین ویرایش ۲۰۲۱ است. در این ویرایش، کنترل دسترسی معیوب رتبهٔ اول را حفظ کرده، پیکربندی نادرست امنیتی به رتبهٔ دوم ارتقا یافته، «شکست‌های زنجیرهٔ تأمین نرم‌افزار» دستهٔ مستقل جدید است و «مدیریت نادرست شرایط استثنایی» به‌عنوان ریسک دهم افزوده شده است."),
  tableTitle("جدول ۱: دسته‌های ده‌گانهٔ OWASP Top 10:2025"),
  rtlTable(["کد", "عنوان ریسک"],
    [
      ["A01:2025", "کنترل دسترسی معیوب (Broken Access Control)"],
      ["A02:2025", "پیکربندی نادرست امنیتی (Security Misconfiguration)"],
      ["A03:2025", "شکست‌های زنجیرهٔ تأمین نرم‌افزار (Software Supply Chain Failures)"],
      ["A04:2025", "شکست‌های رمزنگاری (Cryptographic Failures)"],
      ["A05:2025", "تزریق (Injection)"],
      ["A06:2025", "طراحی ناامن (Insecure Design)"],
      ["A07:2025", "شکست‌های احراز هویت (Authentication Failures)"],
      ["A08:2025", "شکست‌های یکپارچگی نرم‌افزار یا داده (Software or Data Integrity Failures)"],
      ["A09:2025", "شکست‌های ثبت رویداد و هشداردهی (Security Logging and Alerting Failures)"],
      ["A10:2025", "مدیریت نادرست شرایط استثنایی (Mishandling of Exceptional Conditions)"],
    ], [18, 82]),
  faH2("۲.۳. روش‌شناسی"),
  faP("روش کار سه‌مرحله‌ای بود. نخست، بازبینی کد منبع لایه‌های حساس (احراز هویت، نشست، رمزنگاری، کنترل دسترسی، دستورات بانک داده، مدیریت استثناها) برای شناسایی کنترل‌های موجود و شکاف‌ها؛ دوم، پیاده‌سازی اصلاحات فاز ۸۵ در قالب میان‌افزارها و پیکربندی‌های افزودنی؛ و سوم، راستی‌آزمایی زندهٔ سیاه‌جعبه روی سرویس در حال اجرا: بررسی هدرهای پاسخ، اجرای چرخهٔ کامل ورود با توکن ضدجعل، فشار نرخ روی مسیر ورود، و اسکن وابستگی‌ها با ابزار رسمی dotnet list package. نتیجهٔ هر آزمون در فصل ششم ثبت شده است."),
];

// ---------- فصل ۳: کنترل‌های پایهٔ موجود ----------
const ch3 = [
  faH1("۳. کنترل‌های پایهٔ موجود (پیش از فاز ۸۵)"),
  faP("پلتفرم از مراحل پیشین توسعه، کنترل‌های داخلی قوی داشت که در این ارزیابی صحه گذاشته شد و هیچ‌کدام تغییر نکردند. این کنترل‌ها ستون فقرات پوشش ریسک‌های A01، A05، A07، A08 و A09 هستند و در جدول زیر خلاصه شده‌اند."),
  tableTitle("جدول ۲: کنترل‌های امنیتی از پیش فعال در پلتفرم"),
  rtlTable(["حوزه", "کنترل فعال", "ریسک پوشش‌داده"],
    [
      ["رمز عبور", "درهم‌سازی PBKDF2-SHA256 با ۱۰۰٬۰۰۰ تکرار، نمک ۱۶ بایتی و مقایسهٔ زمان‌ثابت", "A04، A07"],
      ["قفل حساب", "پس از ۵ تلاش ناموفق، قفل ۱۵ دقیقه‌ای به‌ازای هر حساب و ثبت رویداد آن", "A07"],
      ["سقف تلاش ورود", "۱۰ تلاش در پنجرهٔ ۵ دقیقه‌ای به‌ازای هر جفت آی‌پی و نام کاربری", "A06، A07"],
      ["توکن نشست", "JWT با امضای HS256، اعتبارسنجی کامل صادرکننده/مخاطب/عمر، فهرست مجاز الگوریتم و انحراف زمانی صفر", "A07"],
      ["کوکی‌های احراز", "HttpOnly و Secure و SameSite=None همراه ویژگی Partitioned (استاندارد CHIPS)", "A07"],
      ["کنترل دسترسی", "فیلتر مجوز بر پایهٔ کاتالوگ دسترسی‌ها، نقش‌های سلسله‌مراتبی با ارث‌بری، دامنهٔ سطر (Row-Level) و گیت فعال‌بودن ماژول‌ها", "A01"],
      ["تزریق", "پرس‌وجوهای پارامتری EF Core در کل سامانه؛ SQL خام فقط برای وصال اسکیما از روی مدل (بدون ورودی کاربر)؛ انکودینگ خودکار Razor + انکودر سازگار با فارسی", "A05"],
      ["ضد جعل", "توکن Antiforgery روی عملیات‌های POST حساس فرم‌ها", "A08"],
      ["امضای مجوز", "اعتبارسنجی امضای RSA فایل مجوز نصب (فازهای ۴۶ تا ۷۷)", "A08"],
      ["ثبت رویداد", "Serilog (کنسول + فایل چرخشی ۳۱ روزه) و دفتر حسابرسی AuditLog با بازیگر، عملیات، موجودیت، جزئیات و آی‌پی؛ ثبت ورود، قفل حساب و رویدادهای عملیاتی", "A09"],
      ["مدیریت استثنا", "صفحهٔ خطای سراسری در محیط Production به‌جای نمایش ردپای پشته؛ حالت تعمیر امن در خرابی اتصال بانک", "A10"],
      ["کلیدهای DataProtection", "ماندگاری کلیدها در مسیر ثابت نصب برای پایداری توکن‌ها و کوکی‌ها", "A04"],
    ], [18, 62, 20]),
];

// ---------- فصل ۴: اقدامات فاز ۸۵ ----------
const ch4 = [
  faH1("۴. اقدامات سخت‌سازی فاز ۸۵"),
  faH2("۴.۱. هدرهای امنیتی HTTP و پنهان‌سازی سرور (A02:2025)"),
  faP("میان‌افزار جدید SecurityHeadersMiddleware روی تمام پاسخ‌ها — شامل فایل‌های استاتیک — هدرهای استاندارد دفاع‌در-عمق اضافه می‌کند: X-Content-Type-Options: nosniff برای جلوگیری از حدس نوع محتوا، Referrer-Policy برای جلوگیری از افشای نشانی‌های داخلی به سایت‌های بیرونی، Permissions-Policy برای سلب دسترسی اسکریپت‌ها به دوربین، میکروفون و موقعیت مکانی (سامانه هیچ استفاده‌ای از این APIهای مرورگر ندارد)، X-Permitted-Cross-Domain-Policies، Cross-Origin-Opener-Policy برای ایزوله‌سازی پنجره‌ها، و Content-Security-Policy با سیاست پیش‌فرض فقط-منابع-خودی همراه مجوز inline برای اسکریپت‌ها و استایل‌های موجود صفحات. جاسازی سامانه در iframe پنل پیش‌نمایش طبق رفتار قبلی باقی مانده است، بنابراین هیچ قابلیتی محدود نشد. علاوه بر این، هدر Server سرور Kestrel که نسخه و فناوری سرور را افشا می‌کرد، از همهٔ پاسخ‌ها حذف شد."),
  faP("همهٔ هدرها از بخش Security:Headers فایل پیکربندی قابل تغییر یا خاموش‌کردن‌اند (کلید Enabled)، تا در استقرارهای خاص هیچ گره‌ای ایجاد نشود."),
  faH2("۴.۲. چرخش خودکار رمز امضای توکن‌ها (A04:2025)"),
  faP("رمز امضای JWT در قالب پیکربندی، مقدار پیش‌فرض متنی «CHANGE-ME» داشت؛ نصبی که آن را عوض نکرده باشد در معرض جعل توکن توسط هر کسی است که سورس را دیده باشد. اکنون در راه‌اندازی، اگر این مقدار خالی یا پیش‌فرض باشد، یک کلید تصادفی ۳۸۴ بیتی تولید و در مسیر App_Data/jwt-secret.key با دسترسی فایل ۶۰۰ (فقط مالک) ماندگار می‌شود؛ ماندگاری فایل باعث می‌شود با هر ری‌استارت، نشست‌های معتبر نشکنند. اولویت همیشه با مقدار صریح پیکربندی اپراتور است و حذف فایل به‌سادگی چرخش کلید و خروج همهٔ نشست‌ها را ممکن می‌کند."),
  faH2("۴.۳. محدودسازی نرخ سراسری (A06:2025 و A07:2025)"),
  faP("با موتور داخلی .NET 8، محدودکنندهٔ نرخ دو لایه‌ای روی همهٔ ترافیک داینامیک فعال شد. مسیرهای ورود (فرم وب ورود یکپارچه، مسیر سازگار ورود کارکنان و ورود و تازه‌سازی توکن در API دستگاه‌ها) در پنجرهٔ ثابت حداکثر ۶۰ درخواست در دقیقه به‌ازای هر آی‌پی پذیرفته می‌شوند؛ این لایه مستقل از سقف قبلی هر حساب کار می‌کند و حدس رمز توزیع‌شده را هم پرهزینه می‌سازد. سایر مسیرها با توکن‌باکت سخاوتمندانه (انفجار ۶۰۰ درخواستی و پرشدن ۲۰۰ درخواست در ثانیه) محافظت می‌شوند که ترافیک طبیعی کاربران و آزمون‌های خودکار را هرگز لمس نمی‌کند و فقط سیلاب خام را با پاسخ 429 و هدر Retry-After می‌بندد. جایگاه این میان‌افزار پس از سرو فایل‌های استاتیک و مرکز دانلود است تا دانلودهای حجیم شمرده نشوند و فرکانس واقعی گیت‌وی دستگاه‌ها بی‌ضرر جا شود. کلید پیکربندی Security:RateLimit:TrustForwardedIp نیز تعبیه شد تا در استقرار مستقیم روی Kestrel (بدون پراکسی)، هدر جعل‌پذیر X-Forwarded-For نتواند پارتیشن‌بندی آی‌پی را دور بزند."),
  faH2("۴.۴. وصلهٔ شکست‌های زنجیرهٔ تأمین نرم‌افزار (A03:2025)"),
  faP("اسکن کامل راه‌حل با ابزار رسمی dotnet list package (شامل وابستگی‌های ترانزیتیو) سه بستهٔ آسیب‌پذیر نشان داد: کتابخانهٔ بومی SQLitePCLRaw.lib.e_sqlite3 نسخهٔ ۲٫۱٫۶ با درجهٔ High، و دو بستهٔ رمزنگاری System.Security.Cryptography.Pkcs نسخهٔ ۸٫۰٫۱ و System.Security.Cryptography.Xml نسخهٔ ۸٫۰٫۲ با مجموعاً چهارده مشاورهٔ امنیتی GHSA (از جمله خانوادهٔ حملهٔ پیچش امضای XML). رفع با فایل مرکزی Directory.Build.props در ریشهٔ سورس پلتفرم انجام شد که نسخه‌های امن ۲٫۱٫۱۴، ۸٫۰٫۲ و ۸٫۰٫۵ را برای همهٔ پروژه‌های راه‌حل پین می‌کند؛ تنها وصلهٔ امنیتی کتابخانهٔ زیرین است، هیچ API تغییر نکرده و پروژه‌های بیرون از پلتفرم را شامل نمی‌شود. اسکن مجدد پس از بازسازی، در تمامی پروژه‌ها صفر بستهٔ آسیب‌پذیر گزارش کرد."),
  faH2("۴.۵. بهداشت شرایط استثنایی (A10:2025)"),
  faP("نقطهٔ پایان سلامت (/health) که توسط مانیتورینگ و لودبالانسر پایش می‌شود، پیش‌تر متن استثنای بانک داده را در پاسخ برمی‌گرداند که می‌تواند نشانه‌هایی از ساختار زیرساخت افشا کند. اکنون وضعیت degraded با کد ۵۰۳ بدون جزئیات بیرون می‌رود و شرح کامل خطا فقط در لاگ Serilog ثبت می‌شود؛ کارکرد مانیتورینگ بدون تغییر مانده است. سایر مسیرهای خطا از قبل از صفحهٔ خطای سراسری استفاده می‌کردند و در محیط Production ردپای پشته نمایش داده نمی‌شود."),
];

// ---------- فصل ۵: نگاشت انطباق ----------
const ch5 = [
  faH1("۵. نگاشت انطباق ده ریسک استاندارد"),
  faP("جدول زیر وضعیت نهایی هر دستهٔ ریسک را پس از فاز ۸۵ نشان می‌دهد. ترکیب کنترل‌های پایهٔ فصل سوم و اقدامات فصل چهارم، هر ده دسته را پوشش می‌دهد."),
  tableTitle("جدول ۳: نگاشت ریسک به کنترل‌های فعال و وضعیت انطباق"),
  rtlTable(["ریسک", "کنترل‌های فعال در پلتفرم", "وضعیت"],
    [
      ["A01:2025", "فیلتر مجوز مرکزی روی کنترلرها با کاتالوگ دسترسی، نقش‌های سلسله‌مراتبی با ارث‌بری و اعمال فوری تغییرات نشست، دامنهٔ دسترسی سطر به سطر، گیت ماژول‌ها و گیت ویزارد راه‌اندازی، ممنوعیت fallback از مسیر کاربر به پرسنل در ورود یکپارچه", "منطبق"],
      ["A02:2025", "میان‌افزار هدرهای امنیتی (nosniff، Referrer-Policy، Permissions-Policy، COOP، X-Permitted-Cross-Domain-Policies، CSP)، حذف هدر Server، پیکربندی‌پذیری کامل، صفحهٔ خطای Production، HSTS پیکربندی‌پذیر، پنهان‌سازی کلیدهای نصب در فایل باینری رمزشدهٔ hrsettings.bin", "منطبق"],
      ["A03:2025", "اسکن رسمی وابستگی‌ها با پوشش ترانزیتیو، پین کردن نسخه‌های امن سه بستهٔ آسیب‌پذیر با فایل مرکزی، اسکن مجدد تا رسیدن به صفر", "منطبق"],
      ["A04:2025", "PBKDF2-SHA256 با ۱۰۰٬۰۰۰ تکرار و مقایسهٔ زمان‌ثابت، تولید و ماندگاری خودکار کلید ۳۸۴ بیتی JWT به‌جای مقدار پیش‌فرض، کلیدهای DataProtection پایدار، امضای RSA مجوز نصب", "منطبق"],
      ["A05:2025", "پرس‌وجوهای پارامتری EF Core در کل سامانه، بدون SQL خام مبتنی بر ورودی کاربر، انکودینگ خودکار خروجی Razor با انکودر سازگار با فارسی، CSP برای دفاع لایهٔ مرورگر", "منطبق"],
      ["A06:2025", "محدودسازی نرخ سراسری دو لایه (پنجرهٔ ثابت ورود + توکن‌باکت عمومی)، گیت ویزارد و حالت تعمیر برای جلوگیری از استقرار ناتمام، خاموشی پیش‌فرض ماژول‌های نصب‌نشده", "منطبق"],
      ["A07:2025", "قفل حساب ۵/۱۵، سقف ۱۰ تلاش در ۵ دقیقه، پنجرهٔ ۶۰ درخواست ورود در دقیقه، JWT با الگوریتم قفل‌شده و انحراف زمانی صفر، کوکی‌های HttpOnly/Secure/Partitioned، انقضای رمز عبور دوره‌ای، ورود یکپارچه بدون حدس‌پذیری", "منطبق"],
      ["A08:2025", "توکن ضدجعل روی عملیات‌های POST حساس، هدر سفارشی X-Requested-With برای درخواست‌های AJAX، امضای RSA فایل مجوز و اعتبارسنجی دوطرفه در استودیو صدور، کوکی نشست خودخدمتی رمزشده و امضاشده با DataProtection", "منطبق"],
      ["A09:2025", "لاگ Serilog چرخشی ۳۱ روزه، دفتر حسابرسی پایگاه‌داده‌ای با بازیگر/عملیات/موجودیت/آی‌پی، ثبت رویدادهای ورود موفق و ناموفق و قفل حساب و عملیات‌های حساس ماژول‌ها", "منطبق"],
      ["A10:2025", "صفحهٔ خطای سراسری در Production بدون افشای ردپای پشته، حذف جزئیات خطای بانک از پاسخ سلامت، حالت تعمیر امن هنگام خرابی اتصال، میان‌افزار تازه‌سازی دسترسی با حفظ ادعاهای کهنه در خطای گذرای بانک", "منطبق"],
    ], [12, 74, 14]),
];

// ---------- فصل ۶: شواهد راستی‌آزمایی ----------
const ch6 = [
  faH1("۶. شواهد راستی‌آزمایی"),
  faP("همهٔ کنترل‌های اعلام‌شده در تاریخ گزارش، روی نسخهٔ در حال اجرا و با بیلد نهایی راه‌حل آزمون شدند. جدول زیر هر آزمون و نتیجهٔ آن را ثبت می‌کند؛ خروجی خام آزمون‌ها در لاگ سرویس قابل ارائه به ممیز است."),
  tableTitle("جدول ۴: آزمون‌های راستی‌آزمایی زنده"),
  rtlTable(["آزمون", "روش", "نتیجه"],
    [
      ["ساخت کامل راه‌حل", "dotnet build روی کل Platform.sln", "صفر خطا؛ ۱۳ هشدار نقدشوندگی پیش‌موجود بدون تغییر"],
      ["سلامت سرویس", "GET /health پس از ری‌استارت", "کد 200 با وضعیت healthy"],
      ["هدرهای امنیتی", "بررسی سربرگ پاسخ صفحهٔ ورود و فایل استاتیک CSS", "هر ۷ هدر امنیتی حاضر؛ هدر Server غایب"],
      ["کلید JWT", "بررسی فایل App_Data/jwt-secret.key", "ساخته شد؛ حجم ۴۸ بایت؛ دسترسی ۶۰۰ (فقط مالک)"],
      ["چرخهٔ ورود وب", "دریافت توکن ضدجعل و ارسال POST /Auth/Login", "کد 302 به داشبورد؛ کوکی‌های نشست صادر شد"],
      ["دفاع در برابر سیلاب ورود", "۷۰ درخواست پشت‌سرهم POST به api/auth/login", "۱۰ پاسخ اول 401 (سقف هر حساب) و سپس 429 با Retry-After"],
      ["بازیابی پس از پنجره", "تکرار ورود پس از سپری‌شدن پنجرهٔ یک‌دقیقه‌ای", "کد 302 موفق — قابلیت ورود برای کاربر عادی سالم"],
      ["اسکن وابستگی‌ها", "dotnet list package با پوشش ترانزیتیو برای همهٔ پروژه‌ها", "پیش از وصله: ۳ بستهٔ آسیب‌پذیر؛ پس از وصله: صفر"],
      ["جاسازی در پنل پیش‌نمایش", "بررسی نبود X-Frame-Options و frame-ancestors در CSP", "رفتار جاسازی بدون تغییر حفظ شد"],
    ], [22, 40, 38]),
  faP("نکتهٔ شفاف‌سازی برای ممیز: تغییر کلید امضای JWT نسبت به نصب‌های قبلی (که از مقدار پیش‌فرض قالب استفاده می‌کردند) به این معناست که پس از اعمال این نسخه، همهٔ نشست‌های فعال یک‌بار برای ورود مجدد هدایت می‌شوند؛ این رفتار یک‌باره، خودِ اصلاح ریسک A04 است و مشکل عملیاتی ایجاد نمی‌کند."),
];

// ---------- فصل ۷: توصیه‌ها ----------
const ch7 = [
  faH1("۷. توصیه‌های تکمیلی پیش از اخذ گواهی"),
  faP("کنترل‌های سطح نرم‌افزار کامل‌اند؛ چند اقدام استقراری و سازمانی نیز برای موفقیت ممیزی گواهی توصیه می‌شود. این موارد در محیط کد اعمال نشدند چون به زیرساخت و تصمیم اپراتور مربوط‌اند."),
  tableTitle("جدول ۵: اقدامات استقراری پیشنهادی"),
  rtlTable(["اقدام", "مسئول", "زمان"],
    [
      ["استقرار پشت HTTPS معتبر و فعال‌کردن Security:EnableHsts=true در Production (در نسخهٔ جاری برای سازگاری با پراکسی HTTP سندباکس، HSTS پیش‌فرض خاموش است)", "اپراتور نصب", "پیش از ممیزی"],
      ["تعویض رمز حساب مدیر پیش‌فرض و حذف یا غیرفعال‌کردن حساب‌های دادهٔ نمایشی در نصب واقعی مشتری", "اپراتور نصب", "روز نخست استقرار"],
      ["پیکربندی پشتیبان‌گیری دوره‌ای بانک داده و انجام یک آزمون بازیابی واقعی و ثبت آن", "اپراتور نصب", "هفتگی"],
      ["پایش پاسخ‌های 429 و در صورت لزوم تنظیم مقادیر Security:RateLimit متناسب با ظرفیت واقعی هر نصب", "اپراتور نصب", "پس از استقرار"],
      ["اجرای اسکن وابستگی‌ها به‌عنوان گام الزامی هر انتشار جدید و بروزرسانی نسخه‌های پین‌شده در صورت انتشار وصلهٔ جدید", "تیم توسعه", "هر انتشار"],
      ["در استقرار مستقیم روی Kestrel بدون پراکسی معکوس، تنظیم Security:RateLimit:TrustForwardedIp=false برای مقاوم‌سازی پارتیشن آی‌پی", "اپراتور نصب", "پیش از ممیزی"],
      ["درخواست آزمون نفوذ مستقل (مثلاً بر پایهٔ OWASP WSTG) به‌عنوان گام تکمیلی گواهی", "ممیز مستقل", "برنامه‌ریزی ممیزی"],
    ], [64, 18, 18]),
];

// ---------- فصل ۸: محدودیت‌ها ----------
const ch8 = [
  faH1("۸. محدودیت‌ها و ملاحظات سند"),
  faP("این سند انطباق، کیفیت کنترل‌های امنیتی نرم‌افزار وب را در سطح کد و رفتار زندهٔ نسخهٔ گزارش‌شده اثبات می‌کند و جایگزین آزمون نفوذ مستقل نیست؛ ممیزان گواهی معمولاً هر دو را مکمل هم می‌دانند. محیط آزمون روی پراکسی HTTP سندباکس اجرا شد که به‌دلیل طراحی، HSTS در آن غیرفعال است؛ فعال‌سازی HSTS در استقرار HTTPS واقعی یک کلید پیکربندی است و در جدول توصیه‌ها آمده است. همچنین ارزیابی وابستگی‌ها به‌روزبودن‌سنجی در برابر پایگاه مشاوره‌های امنیتی رسمی NuGet است و تعهد بروزرسانی دوره‌ای آن بر عهدهٔ فرایند انتشار گذاشته شده است. ابزارهای دسکتاپ استودیوها و به‌ویژه LicenseStudio و TranslationStudio طبق دستور کارفرما از دامنهٔ تغییرات این فاز خارج بودند و سند حاضر دربارهٔ آن‌ها ادعایی ندارد."),
  faP("هیچ قابلیت یا مسیری از سامانه در جریان سخت‌سازی حذف نشد؛ همهٔ کنترل‌های جدید یا کاملاً افزودنی‌اند یا با کلید پیکربندی قابل بازگشت به رفتار قبلی. این اصل، ریسک ارتقای نصب‌های موجود مشتریان را نزدیک به صفر نگه می‌دارد."),
];

// ---------- پیوست الف ----------
const appA = [
  faH1("پیوست الف — تنظیمات امنیتی فاز ۸۵"),
  faP("بلوک زیر مقادیر پیش‌فرض بخش Security فایل appsettings.json پس از فاز ۸۵ است؛ ممیز می‌تواند انطباق مقادیر واقعی هر نصب را با همین کلیدها بررسی کند."),
  new Table({
    visuallyRightToLeft: true,
    width: { size: 100, type: WidthType.PERCENTAGE },
    borders: {
      top: { style: BorderStyle.SINGLE, size: 4, color: P.table.accentLine },
      bottom: { style: BorderStyle.SINGLE, size: 4, color: P.table.accentLine },
      left: { style: BorderStyle.NONE }, right: { style: BorderStyle.NONE },
      insideHorizontal: { style: BorderStyle.SINGLE, size: 2, color: P.table.innerLine },
      insideVertical: { style: BorderStyle.NONE },
    },
    rows: (() => {
      const appRows = [
        ["Security:Headers:Enabled", "true", "فعال‌بودن میان‌افزار هدرهای امنیتی"],
        ["Security:Headers:ContentSecurityPolicy", "default-src 'self'; script-src 'self' 'unsafe-inline'; ...", "سیاست امنیت محتوا؛ frame-ancestors برای جاسازی پنل تعیین نمی‌شود"],
        ["Security:Headers:PermissionsPolicy", "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()", "سلب دسترسی APIهای حساس مرورگر"],
        ["Security:Headers:ReferrerPolicy", "strict-origin-when-cross-origin", "سیاست ارجاع"],
        ["Security:RateLimit:LoginPermitPerMinute", "60", "سقف درخواست ورود در دقیقه به‌ازای هر آی‌پی"],
        ["Security:RateLimit:BurstCapacity", "600", "انفجار توکن‌باکت عمومی"],
        ["Security:RateLimit:TokensPerSecond", "200", "نرخ پرشدن توکن‌باکت عمومی"],
        ["Security:RateLimit:TrustForwardedIp", "true", "در استقرار بدون پراکسی: false"],
        ["Security:EnableHsts", "false", "در استقرار HTTPS واقعی: true"],
        ["Security:AllowEmbedding", "true", "جاسازی در iframe پنل پیش‌نمایش"],
      ];
      const header = new TableRow({
        tableHeader: true, cantSplit: true,
        children: ["کلید پیکربندی", "مقدار پیش‌فرض", "توضیح"].map((t, i) => new TableCell({
          children: [cellPara(t, { bold: true, color: P.table.headerText, align: AlignmentType.CENTER })],
          shading: { type: ShadingType.CLEAR, fill: P.table.headerBg },
          margins: { top: 60, bottom: 60, left: 120, right: 120 },
          width: { size: i === 0 ? 34 : (i === 1 ? 36 : 30), type: WidthType.PERCENTAGE },
        })),
      });
      const dataRows = appRows.map(r => new TableRow({
        cantSplit: true,
        children: r.map((t, i) => new TableCell({
          children: [cellPara(i === 1 ? [en(t, { size: 18 })] : [fr(t, { size: 19 })], { align: i === 1 ? AlignmentType.LEFT : undefined })],
          shading: { type: ShadingType.CLEAR, fill: "FFFFFF" },
          margins: { top: 60, bottom: 60, left: 120, right: 120 },
          width: { size: i === 0 ? 34 : (i === 1 ? 36 : 30), type: WidthType.PERCENTAGE },
        })),
      }));
      return [header, ...dataRows];
    })(),
  }),
  faP("هدرهای اعمال‌شده روی هر پاسخ: X-Content-Type-Options: nosniff؛ Referrer-Policy: strict-origin-when-cross-origin؛ Permissions-Policy (مقادیر جدول بالا)؛ X-Permitted-Cross-Domain-Policies: none؛ Cross-Origin-Opener-Policy: same-origin؛ Content-Security-Policy (مقدار پیکربندی). هدر Server نیز از پاسخ‌ها حذف شده است.", { }),
];

// ============================================================
// سند نهایی — سه سکشن (جلد / فهرست / بدنه)
// ============================================================
const doc = new Document({
  styles: {
    default: {
      document: {
        run: { font: FA, size: 24, color: BODY_COLOR },
        paragraph: { spacing: { line: 312 } },
      },
      heading1: {
        run: { font: FA, size: 32, bold: true, color: HEAD_COLOR },
        paragraph: { spacing: { before: 360, after: 160, line: 380 } },
      },
      heading2: {
        run: { font: FA, size: 28, bold: true, color: HEAD_COLOR },
        paragraph: { spacing: { before: 240, after: 120, line: 340 } },
      },
    },
  },
  sections: [
    { // سکشن ۱: جلد — بدون شماره/سرصفحه، حاشیهٔ صفر
      properties: {
        page: { size: { width: 11906, height: 16838 }, margin: { top: 0, bottom: 0, left: 0, right: 0 } },
      },
      children: buildCoverR1RTL(coverConfig),
    },
    { // سکشن ۲: فهرست — شماره رومی
      properties: {
        type: SectionType.NEXT_PAGE,
        page: {
          size: { width: 11906, height: 16838 },
          margin: { top: 1440, bottom: 1440, left: 1701, right: 1417 },
          pageNumbers: { start: 1, formatType: NumberFormat.UPPER_ROMAN },
        },
      },
      footers: { default: pageNumFooter() },
      children: [
        new Paragraph({
          bidirectional: true, alignment: AlignmentType.CENTER, spacing: { before: 480, after: 360 },
          children: [fr("فهرست مطالب", { size: 32, bold: true })],
        }),
        new TableOfContents("Table of Contents", { hyperlink: true, headingStyleRange: "1-2" }),
        new Paragraph({
          bidirectional: true, alignment: AlignmentType.RIGHT, spacing: { before: 200 },
          children: [fr("یادآوری: این فهرست با کد فیلد ساخته می‌شود؛ پس از باز کردن سند در Word برای درستی شماره صفحه‌ها روی فهرست راست‌کلیک و گزینهٔ Update Field را انتخاب کنید.", { italics: true, size: 18, color: "888888" })],
        }),
        // نکته: شکست صفحه با SectionType.NEXT_PAGE سکشن بدنه تأمین می‌شود — PageBreak اضافی اینجا صفحهٔ خالی می‌سازد (قاعدهٔ ① skill)
      ],
    },
    { // سکشن ۳: بدنه — شماره عددی از ۱
      properties: {
        type: SectionType.NEXT_PAGE,
        page: {
          size: { width: 11906, height: 16838 },
          margin: { top: 1440, bottom: 1440, left: 1701, right: 1417 },
          pageNumbers: { start: 1, formatType: NumberFormat.DECIMAL },
        },
      },
      headers: { default: pageHeader() },
      footers: { default: pageNumFooter() },
      children: [...ch1, ...ch2, ...ch3, ...ch4, ...ch5, ...ch6, ...ch7, ...ch8, ...appA],
    },
  ],
});

Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync("/home/z/my-project/download/گزارش-انطباق-OWASP-Top10-2025.docx", buf);
  console.log("DOCX written OK,", buf.length, "bytes");
});
