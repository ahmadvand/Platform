// probe-rtl.js — verify docx-js RTL/complex-script options exist in this build
const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
        AlignmentType, WidthType, ShadingType, BorderStyle, HeadingLevel,
        TableOfContents, SectionType, NumberFormat } = require("docx");
const fs = require("fs");

const doc = new Document({
  sections: [{
    properties: {},
    children: [
      new Paragraph({
        bidirectional: true,
        alignment: AlignmentType.RIGHT,
        heading: HeadingLevel.HEADING_1,
        children: [new TextRun({
          text: "سلام دنیا — عنوان فارسی",
          bold: true, boldComplexScript: true,
          size: 32, sizeComplexScript: 32,
          rightToLeft: true,
          font: { ascii: "Vazirmatn", hAnsi: "Vazirmatn", cs: "Vazirmatn" },
        })],
      }),
      new Paragraph({
        bidirectional: true,
        alignment: AlignmentType.JUSTIFIED,
        spacing: { line: 312 },
        children: [new TextRun({
          text: "این یک پاراگراف آزمایشی فارسی است تا جهت راست‌به‌چپ و فونت پیچیده بررسی شود.",
          size: 24, sizeComplexScript: 24, rightToLeft: true,
          font: { ascii: "Vazirmatn", hAnsi: "Vazirmatn", cs: "Vazirmatn" },
        })],
      }),
      new Table({
        visuallyRightToLeft: true,
        width: { size: 100, type: WidthType.PERCENTAGE },
        borders: {
          top: { style: BorderStyle.SINGLE, size: 4, color: "1284BA" },
          bottom: { style: BorderStyle.SINGLE, size: 4, color: "1284BA" },
          left: { style: BorderStyle.NONE }, right: { style: BorderStyle.NONE },
          insideHorizontal: { style: BorderStyle.SINGLE, size: 1, color: "D0D0D0" },
          insideVertical: { style: BorderStyle.NONE },
        },
        rows: [new TableRow({
          tableHeader: true, cantSplit: true,
          children: ["ستون یک", "ستون دو"].map(t => new TableCell({
            children: [new Paragraph({ bidirectional: true, children: [new TextRun({ text: t, rightToLeft: true, sizeComplexScript: 21 })] })],
            shading: { type: ShadingType.CLEAR, fill: "EDF4F9" },
            margins: { top: 60, bottom: 60, left: 120, right: 120 },
          })),
        })],
      }),
      new TableOfContents("TOC", { hyperlink: true, headingStyleRange: "1-3" }),
    ],
  }],
});

Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync("/home/z/my-project/scripts/probe-rtl.docx", buf);
  console.log("probe OK", buf.length, "bytes");
}).catch(e => { console.error("PROBE FAIL:", e.message); process.exit(1); });
