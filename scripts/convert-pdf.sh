#!/bin/bash
# تبدیل پس‌زمینه‌ای docx→pdf (سوفیس سرد و کند است؛ در پیش‌زمینه timeout می‌خورد)
LOG=/home/z/my-project/scripts/convert-pdf.log
cd /home/z/my-project/download/docs || exit 1
: > "$LOG"
for f in HR-Core-Rebuild-Guide-FA HR-Setup-Guide-FA HR-User-Guide-FA; do
  timeout 240 soffice --headless --convert-to pdf "$f.docx" >/dev/null 2>&1
  echo "$f exit=$? $(date '+%T')" >> "$LOG"
done
echo "ALL-PDF-DONE $(date '+%T')" >> "$LOG"
