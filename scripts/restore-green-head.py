# -*- coding: utf-8 -*-
# ======================================================================
# فریز کردن WIP ناتمام (۱۳۲ فایل) + بازسازی درخت سبز از HEAD + یکپارچه‌سازی مجدد
# ======================================================================
import os, shutil, subprocess

ROOT = "/home/z/my-project"
ASP = os.path.join(ROOT, "aspnet")
SRC = os.path.join(ASP, "src")
FROZEN = os.path.join(ASP, "wip-frozen-20261104")

def git(*a):
    return subprocess.run(["git", "-C", ROOT, *a], capture_output=True, text=True)

UNTRACKED = [l.strip() for l in open("/tmp/untracked-real.txt") if l.strip()]
KEEP = {"modules/Directory.Build.props"}   # خودرسانی ماژول‌ها به خروجی هاست (فاز ۷۰) — مفید و بی‌خطر

# ---------- ۱) فریز ----------
n = 0
for rel in UNTRACKED:
    if rel in KEEP:
        continue
    src = os.path.join(SRC, rel)
    if not os.path.exists(src):
        continue
    dst = os.path.join(FROZEN, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(src, dst)
    n += 1
print(f"[1] {n} فایل WIP فریز شد → aspnet/wip-frozen-20261104/")

# ---------- ۲) بازسازی درخت tracked از HEAD ----------
git("reset", "-q")                       # خروج از staging renameها
shutil.rmtree(SRC)
r = git("checkout", "HEAD", "--", "aspnet/src")
if r.returncode != 0:
    raise SystemExit("checkout failed: " + r.stderr)
print("[2] aspnet/src از HEAD بازسازی شد (Hr* names)")

# ---------- ۳) بازگرداندن modules/Directory.Build.props (KEEP) ----------
shutil.copy2(os.path.join(FROZEN, "modules/Directory.Build.props"),
             os.path.join(SRC, "modules", "Directory.Build.props"))
print("[3] modules/Directory.Build.props بازگشت (محتوای Platform.Web — صحیح پس از rename)")
print("DONE")
