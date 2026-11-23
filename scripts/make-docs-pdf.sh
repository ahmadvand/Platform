#!/bin/bash
# تبدیل هر ۵ سند Word به PDF با فونت وزیرمتن (fontconfig محلی پروژه + پروفایل LibreOffice)
set -euo pipefail
ROOT=/home/z/my-project
DOCS="$ROOT/download/docs"
export FONTCONFIG_FILE="$ROOT/scripts/fonts.conf"
PROFILE="file:///home/z/my-project/assets/lo-profile"
for f in HR-AI-Handoff-FA HR-Architecture-FA HR-Core-Rebuild-Guide-FA HR-Setup-Guide-FA HR-User-Guide-FA; do
  rm -f "$DOCS/$f.pdf"
  timeout 120 soffice -env:UserInstallation="$PROFILE" --headless --convert-to pdf "$DOCS/$f.docx" --outdir "$DOCS" >/dev/null
  test -s "$DOCS/$f.pdf" || { echo "PDF FAILED: $f"; exit 1; }
  fonts=$(pdffonts "$DOCS/$f.pdf" | rg -c 'Vazirmatn' || true)
  echo "$f.pdf — Vazirmatn-fonts: $fonts ($(du -h "$DOCS/$f.pdf" | cut -f1))"
done
echo ALL PDFS OK
