#!/bin/bash
# فاز ۹۷ — تولید PDFهای مستندات با رندر واقعی فونت وزیر:
# fix-footers → soffice (پاس A) → تزریق شماره صفحهٔ واقعی فهرست → soffice (پاس نهایی)
set -uo pipefail
cd /home/z/my-project/scripts/docs
D=/home/z/my-project/download/docs
OUT=/tmp/docs-pdf; rm -rf $OUT; mkdir -p $OUT
export PATH="$PATH"
for f in HR-AI-Handoff-FA HR-Architecture-FA HR-Core-Rebuild-Guide-FA HR-Setup-Guide-FA HR-User-Guide-FA; do
  echo "== $f =="
  python3 fix-footers.py "$D/$f.docx" || { echo "fix-footers FAIL"; continue; }
  timeout 120 soffice -env:UserInstallation=file:///tmp/lo-profile --headless --convert-to pdf "$D/$f.docx" --outdir "$OUT" >/dev/null 2>&1 || { echo "soffice A FAIL"; continue; }
  python3 toc-pages.py "$D/$f.docx" "$OUT/$f.pdf" || { echo "toc-pages FAIL"; continue; }
  timeout 120 soffice -env:UserInstallation=file:///tmp/lo-profile --headless --convert-to pdf "$D/$f.docx" --outdir "$D" >/dev/null 2>&1 || { echo "soffice B FAIL"; continue; }
  ls -la "$D/$f.pdf" | awk '{print $5, $9}'
done
echo DONE
