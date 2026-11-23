#!/bin/bash
# پاک‌سازی حجم قدیمی PolarFS سندباکس قبلی (/tmp/my-project — ۶.۱G)
# بعد از بازیابی کامل محتوا به درخت فعلی. طبق دستور صریح کاربر: حذف فایل‌های
# کهنه/بی‌مصرف. فقط سه عنصر پلتفرمی حفظ می‌شوند (مانیفست اسنپ‌شات + پیکربندی).
set -u
V=/tmp/my-project
LOG=/tmp/oldvol-cleanup.log
echo ">> before: $(du -sh $V 2>/dev/null | cut -f1)" > "$LOG"
for item in "$V"/* "$V"/.[!.]*; do
  base=$(basename "$item" 2>/dev/null)
  case "$base" in
    .initial_snapshot.json|.env|.zscripts) echo "keep: $base" >> "$LOG"; continue ;;
  esac
  [ -e "$item" ] || continue
  rm -rf "$item" && echo "removed: $base" >> "$LOG" || echo "FAILED: $base" >> "$LOG"
done
echo ">> after: $(du -sh $V 2>/dev/null | cut -f1)" >> "$LOG"
echo ">> DONE" >> "$LOG"
