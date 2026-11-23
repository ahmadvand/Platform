#!/bin/bash
# ======================================================================
# ترمیم آلودگی ری‌بیلد سندباکس (۲۰۲۶-۱۰-۲۷):
# ری‌بیلد، فایل‌های قدیمیِ حذف‌شده در فاز ۶۱ را زنده کرد و کامیت‌های
# خودکار cron آنها را کامیت کردند → بیلد سورس می‌شکند (۹۴ خطا).
# قانون جراحی: هر فایلِ «اضافه‌شده در HEAD نسبت به 48dd5ad» که در
# اسنپ‌شات پیش از فاز ۶۱ (1401789) وجود داشته و محتوایش با آن یکی است
# → شبح زنده‌شده → حذف. اگر نبود → فایل جدید مشروع فاز ۶۲/۶۳ → نگه.
# فایل‌های M (تغییریافته): اگر محتوای HEAD با پیش از فاز ۶۱ یکی باشد
# یعنی reverted شده → از 48dd5ad برمی‌گردد؛ وگرنه دستی بررسی می‌شود.
# ======================================================================
set -uo pipefail
cd /home/z/my-project
CLEAN=48dd5ad   # فاز ۶۱ + worklog (درخت سبز تأییدشده)
PRE=1401789     # اسنپ‌شات پیش از فاز ۶۱
GHOSTS=()
REVERTS=()
REVIEW=()

while IFS=$'\t' read -r st path; do
  case "$st" in
    A)
      if git cat-file -e "$PRE:$path" 2>/dev/null; then
        h_pre=$(git show "$PRE:$path" | git hash-object --stdin)
        h_head=$(git show "HEAD:$path" | git hash-object --stdin)
        if [ "$h_pre" = "$h_head" ]; then
          GHOSTS+=("$path")
        else
          REVIEW+=("A-MODIFIED $path")
        fi
      else
        REVIEW+=("A-NEW $path")
      fi
      ;;
    M)
      h_pre=$(git show "$PRE:$path" 2>/dev/null | git hash-object --stdin || echo none)
      h_head=$(git show "HEAD:$path" | git hash-object --stdin)
      if [ "$h_pre" = "$h_head" ]; then
        REVERTS+=("$path")
      else
        REVIEW+=("M-CHANGED $path")
      fi
      ;;
    *) : ;;  # D و R و غیره — دست نمی‌زنیم
  esac
done < <(git diff --name-status "$CLEAN" HEAD -- aspnet/src | awk -F'\t' '{print $1"\t"$2}')

echo "=== GHOSTS (delete) ==="
printf '%s\n' "${GHOSTS[@]:-}"
echo "=== REVERTS (restore from $CLEAN) ==="
printf '%s\n' "${REVERTS[@]:-}"
echo "=== REVIEW (keep / manual) ==="
printf '%s\n' "${REVIEW[@]:-}"

if [ "${1:-}" = "--apply" ]; then
  for f in "${GHOSTS[@]:-}"; do git rm -q -- "$f"; done
  for f in "${REVERTS[@]:-}"; do git checkout "$CLEAN" -- "$f"; done
  echo "=== APPLIED: ${#GHOSTS[@]} ghosts deleted, ${#REVERTS[@]} reverts restored ==="
fi
