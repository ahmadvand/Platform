#!/usr/bin/env python3
# prepare-core-pdf.py — PDF سند «راهنمای ساخت هسته» با فهرست ایستا
# LibreOffice روی این سند در بازمحاسبهٔ فیلد TOC (dirty=true) حلقهٔ چیدمان می‌سازد
# (۹۹.۸% CPU، بی‌پایان). راه‌حل: مدخل‌های ایستا با شمارهٔ صفحهٔ واقعی تزریق و
# dirty حذف می‌شود تا LO فقط محتوای کش‌شده را رندر کند. سند Word اصلی دست‌نخورده
# می‌ماند (Word فهرست خودش را درست می‌سازد).
import re, shutil, subprocess, sys, zipfile, tempfile, os, json

SRC = "/home/z/my-project/download/docs/HR-Core-Rebuild-Guide-FA.docx"
OLD_PDF = "/home/z/my-project/aspnet/src/Platform.Web/wwwroot/dl/docs/HR-Core-Rebuild-Guide-FA.pdf"
OUT_PDF = "/home/z/my-project/download/docs/HR-Core-Rebuild-Guide-FA.pdf"
INJECT = "/home/z/my-project/skills/docx/scripts/add_toc_placeholders.py"

# ۱) متن دقیق عناوین از خود سند تازه (به‌ترتیب) — سطح همه h1 است
with zipfile.ZipFile(SRC) as z:
    doc = z.read("word/document.xml").decode("utf-8")
heads = re.findall(r'<w:p[^>]*>(?:(?!</w:p>).)*?<w:pStyle w:val="Heading1"/>(?:(?!</w:p>).)*?</w:p>', doc, re.S)
texts = []
for h in heads:
    ts = re.findall(r'<w:t[^>]*>([^<]*)</w:t>', h)
    t = "".join(ts).strip()
    if t and not t.startswith("فهرست"):
        texts.append(t)
print(f"{len(texts)} h1 headings found")
for t in texts:
    print("  -", t[:60])

# ۲) شمارهٔ صفحهٔ واقعی از OUTLINE واسط PDF (صفحه‌بندی با تغییر جزئی متن جابه‌جا نمی‌شود)
from pypdf import PdfReader
r = PdfReader(OLD_PDF)
def flat(items):
    out = []
    for it in items:
        if isinstance(it, list):
            out += flat(it)
        else:
            out.append(it)
    return out
ols = flat(r.outline)
pages = [r.get_destination_page_number(it) + 1 for it in ols]
assert len(pages) == len(texts), f"mismatch: {len(pages)} outline vs {len(texts)} headings"
entries = [{"level": 1, "text": t, "page": str(p)} for t, p in zip(texts, pages)]
print("entries:", json.dumps(entries, ensure_ascii=False)[:200], "...")

# ۳) کپی کاری + حذف dirty از فیلد TOC
tmpd = tempfile.mkdtemp()
work = os.path.join(tmpd, "HR-Core-Rebuild-Guide-FA.docx")
shutil.copyfile(SRC, work)
tmpx = tempfile.mkdtemp()
with zipfile.ZipFile(work) as z:
    z.extractall(tmpx)
docp = os.path.join(tmpx, "word", "document.xml")
x = open(docp, encoding="utf-8").read()
x2 = x.replace('<w:fldChar w:fldCharType="begin" w:dirty="true"/>', '<w:fldChar w:fldCharType="begin"/>')
open(docp, "w", encoding="utf-8").write(x2)
print("dirty removed:", x != x2)
tmpzip = work + ".tmp"
with zipfile.ZipFile(tmpzip, "w", zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk(tmpx):
        for f in files:
            full = os.path.join(root, f)
            z.write(full, os.path.relpath(full, tmpx))
shutil.move(tmpzip, work)

# ۴) تزریق مدخل‌های ایستا
res = subprocess.run([sys.executable, INJECT, work, "--entries", json.dumps(entries, ensure_ascii=False)],
                     capture_output=True, text=True)
print(res.stdout[-400:], res.stderr[-200:])
if res.returncode != 0:
    sys.exit("inject failed")

# ۵) خاموش‌کردن updateFields تا LO فیلد را بازمحاسبه نکند
sx = os.path.join(tmpx, "word", "settings.xml")
with zipfile.ZipFile(work) as z:
    z.extractall(tmpx)
s = open(sx, encoding="utf-8").read()
s2 = re.sub(r'<w:updateFields[^/]*/>', '<w:updateFields w:val="false"/>', s)
open(sx, "w", encoding="utf-8").write(s2)
tmpzip = work + ".tmp"
with zipfile.ZipFile(tmpzip, "w", zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk(tmpx):
        for f in files:
            full = os.path.join(root, f)
            z.write(full, os.path.relpath(full, tmpx))
shutil.move(tmpzip, work)
print("updateFields=false set")

print("WORK:", work)
