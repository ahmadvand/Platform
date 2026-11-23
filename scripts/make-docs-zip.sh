#!/usr/bin/env bash
# ساخت بستهٔ یکپارچهٔ مستندات — HR-Platform-2.7.11-Docs-FA.zip
# محتوا: هر ۱۱ فایل مستندات آخرین نسخه (فونت وزیر) در پوشهٔ HR-Platform-2.7.11-Docs-FA/
set -euo pipefail
ROOT=/home/z/my-project
DOCS="$ROOT/download/docs"
STAGE="$ROOT/assets/zipstage"
OUT="$ROOT/download/HR-Platform-2.7.11-Docs-FA.zip"

FILES=(
  handoff.md
  HR-AI-Handoff-FA.docx       HR-AI-Handoff-FA.pdf
  HR-Architecture-FA.docx     HR-Architecture-FA.pdf
  HR-Core-Rebuild-Guide-FA.docx HR-Core-Rebuild-Guide-FA.pdf
  HR-Setup-Guide-FA.docx      HR-Setup-Guide-FA.pdf
  HR-User-Guide-FA.docx       HR-User-Guide-FA.pdf
)

rm -rf "$STAGE" "$OUT"
mkdir -p "$STAGE/HR-Platform-2.7.11-Docs-FA"
for f in "${FILES[@]}"; do
  test -s "$DOCS/$f" || { echo "MISSING: $f"; exit 1; }
  cp "$DOCS/$f" "$STAGE/HR-Platform-2.7.11-Docs-FA/"
done
( cd "$STAGE" && zip -X -9 -r "$OUT" "HR-Platform-2.7.11-Docs-FA" >/dev/null )
rm -rf "$STAGE"
echo "== محتوای بسته:"
unzip -l "$OUT"
ls -la "$OUT"
