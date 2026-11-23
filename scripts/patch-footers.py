#!/usr/bin/env python3
# پس‌پردازش فوترها طبق references/toc.md — سوییچ فرمت PAGE + حذف pgNumType خالی
import re, sys, zipfile, shutil, os

path = sys.argv[1]
tmp = path + ".tmp"
shutil.copy(path, tmp)

with zipfile.ZipFile(tmp, "r") as z:
    names = z.namelist()
    doc = z.read("word/document.xml").decode("utf-8")
    rels = z.read("word/_rels/document.xml.rels").decode("utf-8")
    others = {n: z.read(n) for n in names if n not in ("word/document.xml",)}

# نقشهٔ rId → فایل فوتر
rel_map = dict(re.findall(r'<Relationship[^>]*Id="([^"]+)"[^>]*Target="(footer\d+\.xml)"', rels))
rel_map.update(dict((m[0], m[1]) for m in re.findall(r'<Relationship[^>]*Target="(?:/word/)?(footer\d+\.xml)"[^>]*Id="([^"]+)"', rels) for m in [(m[1], m[0])]))

# ترتیب سکشن‌ها در document.xml: جلد (بدون فوتر) → فهرست (ROMAN) → بدنه (arabic)
sect_footers = []  # فهرست rId فوتر هر سکشن به ترتیب
for sect in re.finditer(r"<w:sectPr[^>]*>.*?</w:sectPr>", doc, re.S):
    ids = re.findall(r'<w:footerReference[^>]*r:id="([^"]+)"', sect.group(0))
    sect_footers.append(ids[0] if ids else None)
print("section footer rIds (in order):", sect_footers)

# سکشن ۱ = جلد (None)، سکشن ۲ = فهرست (ROMAN)، سکشن ۳ = بدنه (arabic)
roman_ids = set()
arabic_ids = set()
if len(sect_footers) >= 3:
    if sect_footers[1]: roman_ids.add(sect_footers[1])
    for fid in sect_footers[2:]:
        if fid: arabic_ids.add(fid)
elif len(sect_footers) == 2:
    if sect_footers[0]: roman_ids.add(sect_footers[0])
    if sect_footers[1]: arabic_ids.add(sect_footers[1])

def patch_footer(xml, fmt):
    return re.sub(
        r"(<w:instrText[^>]*>)([^<]*\bPAGE\b[^<]*)(</w:instrText>)",
        lambda m: m.group(1) + f" PAGE \\* {fmt} \\* MERGEFORMAT " + m.group(3),
        xml)

changed = {}
for rid in roman_ids:
    fn = "word/" + rel_map.get(rid, "")
    if fn in others:
        changed[fn] = patch_footer(others[fn].decode("utf-8"), "ROMAN").encode("utf-8")
        print("patched ROMAN:", fn)
for rid in arabic_ids:
    fn = "word/" + rel_map.get(rid, "")
    if fn in others and fn not in changed:
        changed[fn] = patch_footer(others[fn].decode("utf-8"), "arabic").encode("utf-8")
        print("patched arabic:", fn)

# حذف pgNumType خالی سکشن جلد
doc2 = doc.replace("<w:pgNumType/>", "")
if doc2 != doc:
    print("removed empty pgNumType")
changed["word/document.xml"] = doc2.encode("utf-8")

with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
    for n in names:
        if n == "word/document.xml":
            z.writestr(n, changed["word/document.xml"])
        elif n in changed:
            z.writestr(n, changed[n])
        else:
            z.writestr(n, others[n])
os.remove(tmp)
print("OK — footers patched, pgNumType cleaned")
