#!/usr/bin/env python3
# فاز ۱۲۴-الف — توکن‌های مشترک هدر صفحه و بنرهای اعلان (الگوی Figma)
# یکسان در site.css (RTL) و site-ltr.css (LTR) — بدون مقادیر جهتی (margin-inline-start)
import io, sys

BLOCK = """
/* ============ فاز ۱۲۴ — هدر صفحه و بنرهای اعلان (توکن‌های مشترک همهٔ صفحات) ============ */
.page-h { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin: 2px 0 14px; }
.page-h h1 { margin: 0; font-size: 19px; font-weight: 800; letter-spacing: -.2px; color: var(--ink); }
.page-sub { margin-top: 4px; font-size: 12px; color: var(--ink-3); line-height: 1.9; }
.page-actions { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
.notice { display: flex; align-items: flex-start; gap: 10px; padding: 11px 14px; border-radius: var(--radius); font-size: 12.5px; line-height: 2; margin: 0 0 14px; border: 1px solid; }
.notice .n-ic { font-size: 15px; line-height: 1.7; flex: none; }
.notice-ok { background: #ecfdf5; border-color: #bbf7d0; color: #065f46; }
.notice-warn { background: #fffbeb; border-color: #fde68a; color: #92400e; }
.notice-bad { background: #fef2f2; border-color: #fecaca; color: #991b1b; }
.notice .n-act { margin-inline-start: auto; align-self: center; white-space: nowrap; }
"""

for p in ["/home/z/my-project/aspnet/src/Platform.Web/wwwroot/css/site.css",
          "/home/z/my-project/aspnet/src/Platform.Web/wwwroot/css/site-ltr.css"]:
    with io.open(p, encoding="utf-8") as f:
        src = f.read()
    if "فاز ۱۲۴ — هدر صفحه" in src:
        print(f"SKIP (موجود): {p}")
        continue
    if not src.endswith("\n"):
        src += "\n"
    with io.open(p, "w", encoding="utf-8") as f:
        f.write(src + BLOCK)
    print(f"OK: {p}")
