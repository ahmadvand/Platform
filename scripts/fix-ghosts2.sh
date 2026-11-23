#!/bin/bash
# ======================================================================
# ترمیم آلودگی ری‌بیلد سندباکس — نسخهٔ ۲ (baseline تاریخی)
# قانون:
#  A (فایل در HEAD هست ولی در 48dd5ad نیست):
#     - اگر مسیرش در تاريخچهٔ قبل از 48dd5اد سابقه دارد → فاز ۶۱ آن را
#       حذف/جابه‌جا کرده و ری‌بیلد زنده‌اش کرده → شبح → حذف
#     - اگر هیچ سابقه‌ای ندارد → فایل جدید مشروع (فاز ۶۲/۶۳) → نگه
#  M (تغییریافته نسبت به 48dd5ad):
#     - اگر محتوای HEAD دقیقاً برابر نسخهٔ 6b59436 (پیش از فاز ۶۱) باشد
#       → reverted توسط ری‌بیلد → از 48dd5ad بازگردانی
#     - وگرنه → تغییر مشروع → نگه
#  D (در HEAD حذف شده ولی در 48dd5ad هست) → فقط گزارش (بررسی دستی)
# مصرف:  fix-ghosts2.sh           (خشک)
#        fix-ghosts2.sh --apply   (اعمال)
# ======================================================================
set -uo pipefail
cd /home/z/my-project
CLEAN=48dd5ad
PRE=6b59436
GHOSTS=(); REVERTS=(); KEEP=(); DELETED=()

while IFS=$'\t' read -r st path; do
  case "$st" in
    A)
      if [ -n "$(git log --oneline "$CLEAN" -- "$path" 2>/dev/null | head -1)" ]; then
        GHOSTS+=("$path")
      else
        KEEP+=("A-NEW $path")
      fi ;;
    M)
      h_pre=$(git show "$PRE:$path" 2>/dev/null | git hash-object --stdin || echo none)
      h_head=$(git show "HEAD:$path" | git hash-object --stdin)
      if [ "$h_pre" = "$h_head" ]; then
        REVERTS+=("$path")
      else
        KEEP+=("M-CHANGED $path")
      fi ;;
    D) DELETED+=("$path") ;;
  esac
done < <(git diff --name-status "$CLEAN" HEAD -- aspnet/src | awk -F'\t' '{print $1"\t"$2}')

echo "=== GHOSTS ($#): delete ===";      printf '%s\n' "${GHOSTS[@]:-}"
echo "=== REVERTS (${$#REVERTS[@]):-0}: restore from $CLEAN ==="; printf '%s\n' "${REVERTS[@]:-}"
echo "=== KEEP (legit) ===";             printf '%s\n' "${KEEP[@]:-}"
echo "=== DELETED-in-HEAD (review) ==="; printf '%s\n' "${DELETED[@]:-}"

if [ "${1:-}" = "--apply" ]; then
  for f in "${GHOSTS[@]:-}"; do git rm -q -- "$f"; done
  for f in "${REVERTS[@]:-}"; do git checkout "$CLEAN" -- "$f"; done
  echo "=== APPLIED: ${#GHOSTS[@]} deleted, ${#REVERTS[@]} restored ==="
fi
