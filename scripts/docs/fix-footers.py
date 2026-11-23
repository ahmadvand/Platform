#!/usr/bin/env python3
# fix-footers.py — طبق toc.md: حذف <w:pgNumType/> خالی بخش جلد + پچ instrText فوترها
#   بخش ۲ (فهرست) = PAGE \* ROMAN ، بخش ۳ (بدنه) = PAGE \* arabic
import sys, re, zipfile, shutil, tempfile, os

def main(path):
    tmp = tempfile.mkdtemp()
    with zipfile.ZipFile(path) as z:
        z.extractall(tmp)
    docp = os.path.join(tmp, "word", "document.xml")
    xml = open(docp, encoding="utf-8").read()

    # ۱) حذف pgNumType خالی (متعلق به جلد)
    xml = re.sub(r"<w:pgNumType/>", "", xml)

    # ۲) استخراج footerReference هر sectPr به ترتیب
    sects = re.findall(r"<w:sectPr[ >].*?</w:sectPr>", xml, re.S)
    rels_xml = open(os.path.join(tmp, "word", "_rels", "document.xml.rels"), encoding="utf-8").read()
    rid2file = dict(re.findall(r'Id="([^"]+)"[^>]*Target="([^"]+)"', rels_xml))

    foot_files = []  # (sect_index, filename)
    for i, s in enumerate(sects):
        m = re.search(r'<w:footerReference w:type="default" r:id="([^"]+)"', s)
        if m:
            foot_files.append((i, os.path.basename(rid2file[m.group(1)])))
    assert len(foot_files) >= 2, f"expected 2 footer refs, got {foot_files}"

    # سند ما: جلد(بدون فوتر) ، فهرست(فوتر اول) ، بدنه(فوتر دوم)
    fmt_for_sect = {1: "ROMAN", 2: "arabic"}
    for idx, fname in foot_files[:2]:
        fpath = os.path.join(tmp, "word", fname)
        fxml = open(fpath, encoding="utf-8").read()
        fmt = fmt_for_sect.get(idx)
        if fmt:
            fxml = re.sub(
                r"(<w:instrText[^>]*>)\s*PAGE\s*(</w:instrText>)",
                r"\1 PAGE \\* " + fmt + r" \\* MERGEFORMAT \2", fxml)
        open(fpath, "w", encoding="utf-8").write(fxml)

    open(docp, "w", encoding="utf-8").write(xml)
    # بازبسته‌بندی
    out = path
    tmpzip = path + ".tmp"
    with zipfile.ZipFile(tmpzip, "w", zipfile.ZIP_DEFLATED) as z:
        for root, _, files in os.walk(tmp):
            for f in files:
                full = os.path.join(root, f)
                z.write(full, os.path.relpath(full, tmp))
    shutil.move(tmpzip, out)
    shutil.rmtree(tmp)
    print(f"footers fixed: {out} — sections={len(sects)} footers={foot_files}")

if __name__ == "__main__":
    main(sys.argv[1])
