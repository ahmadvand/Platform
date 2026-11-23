#!/usr/bin/env python3
# patch-toc1.py — مدخل اول فهرست (hyperlink با PAGEREF merged خراب) را به پاراگراف سادهٔ هم‌سان بقیه تبدیل می‌کند
# تا LibreOffice در بازتولید فهرست (dirty field) دوباره‌سازی تمیز و بدون تکرار داشته باشد.
import sys, re, zipfile, shutil, tempfile, os

def main(path):
    tmp = tempfile.mkdtemp()
    with zipfile.ZipFile(path) as z:
        z.extractall(tmp)
    docp = os.path.join(tmp, "word", "document.xml")
    xml = open(docp, encoding="utf-8").read()

    m = re.search(r'<w:hyperlink w:history="1" w:anchor="_Toc1">(.*?)</w:hyperlink>', xml, re.S)
    if not m:
        print("no broken first entry — skip"); shutil.rmtree(tmp); return
    inner = m.group(1)
    # ران PAGEREF خراب (begin+instrText+separate+end در یک w:r) → عدد سادهٔ ۱
    inner2 = re.sub(
        r'<w:r>\s*<w:fldChar w:fldCharType="begin"[^>]*/>\s*<w:instrText[^>]*>PAGEREF[^<]*</w:instrText>\s*<w:fldChar w:fldCharType="separate"/>\s*<w:fldChar w:fldCharType="end"/>\s*</w:r>',
        '<w:r><w:t xml:space="preserve">1</w:t></w:r>', inner, flags=re.S)
    xml = xml[:m.start()] + inner2 + xml[m.end():]
    open(docp, "w", encoding="utf-8").write(xml)

    tmpzip = path + ".tmp"
    with zipfile.ZipFile(tmpzip, "w", zipfile.ZIP_DEFLATED) as z:
        for root, _, files in os.walk(tmp):
            for f in files:
                full = os.path.join(root, f)
                z.write(full, os.path.relpath(full, tmp))
    shutil.move(tmpzip, path)
    shutil.rmtree(tmp)
    print("patched first TOC entry:", path)

if __name__ == "__main__":
    main(sys.argv[1])
