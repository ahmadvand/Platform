#!/usr/bin/env python3
# bisect-core-lo.py — یافتن عنصر حلقه‌ساز LibreOffice با جستجوی دودویی روی بلوک‌های بدنه
import zipfile, tempfile, shutil, os, re, subprocess, sys

WORK = sys.argv[1] if len(sys.argv) > 1 else "/tmp/tmpdedj2elz/HR-Core-Rebuild-Guide-FA.docx"
TAG = sys.argv[2] if len(sys.argv) > 2 else "b"

with zipfile.ZipFile(WORK) as z:
    z.extractall("/tmp/bisect-x")
DOC = "/tmp/bisect-x/word/document.xml"
x = open(DOC, encoding="utf-8").read()

import json
from lxml import etree

NS = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
tree = etree.parse("/tmp/bisect-x/word/document.xml")
root = tree.getroot()
body = root.find(NS + "body")
children = list(body)
print(f"{len(children)} top-level body children", flush=True)
xmls = [etree.tostring(c, encoding="unicode") for c in children]
head_xml = ""
tail_xml = etree.tostring(children[-1], encoding="unicode") if children else ""

def build(k, out):
    # فاز تشخیص ۲ — آخرین فرزند (sectPr پایانی) همیشه می‌ماند تا حلقهٔ «بخش آخر» هم بایسکت شود
    tmp = tempfile.mkdtemp()
    shutil.rmtree(tmp)
    shutil.copytree("/tmp/bisect-x", tmp)
    p = os.path.join(tmp, "word", "document.xml")
    new_root = etree.fromstring(etree.tostring(root))
    nb = new_root.find(NS + "body")
    allc = list(nb)
    for c in allc[k:]:
        if c is not allc[-1]:
            nb.remove(c)
    etree.ElementTree(new_root).write(p, encoding="UTF-8", xml_declaration=True)
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for r2, _, files in os.walk(tmp):
            for f in files:
                full = os.path.join(r2, f)
                z.write(full, os.path.relpath(full, tmp))

def ok(k):
    out = f"/tmp/bisect-{TAG}.docx"
    build(k, out)
    try:
        r = subprocess.run(["soffice", "--headless", f"-env:UserInstallation=file:///tmp/lo-bis{TAG}",
                            "--convert-to", "pdf", "--outdir", "/tmp/bisect-out", out],
                           capture_output=True, timeout=60)
        return os.path.exists(f"/tmp/bisect-out/bisect-{TAG}.pdf")
    except subprocess.TimeoutExpired:
        return False
    finally:
        subprocess.run(["pkill", "-f", f"lo-bis{TAG}"], capture_output=True)
        try:
            os.remove(f"/tmp/bisect-out/bisect-{TAG}.pdf")
        except OSError:
            pass

os.makedirs("/tmp/bisect-out", exist_ok=True)
STATE = f"/tmp/bisect-state-{TAG}.json"
lo, hi = 0, len(children)
try:
    st = json.load(open(STATE))
    lo, hi = st["lo"], st["hi"]
    print(f"resumed: lo={lo} hi={hi}", flush=True)
except Exception:
    assert ok(0), "even empty body fails?!"
    if ok(hi):
        print("FULL converts now — environment healed", flush=True)
        sys.exit(0)

while hi - lo > 1:
    mid = (lo + hi) // 2
    g = ok(mid)
    print(f"  children[0:{mid}] -> {'OK' if g else 'LOOP'}", flush=True)
    if g:
        lo = mid
    else:
        hi = mid
    json.dump({"lo": lo, "hi": hi}, open(STATE, "w"))
print(f"first bad prefix ends at child {hi}", flush=True)
from lxml import etree as ET2
print("BAD:", xmls[hi][:800], flush=True)
