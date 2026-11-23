#!/usr/bin/env python3
# فاز ۱۲۴-ه (E) — توکن ردیف کلید-مقدار پنل‌های جزئیات (الگوی یکسان RTL/LTR)
# همهٔ مقادیر منطقی (logical properties): text-align:end در RTL=چپ، در LTR=راست — یک قانون برای هر دو جهت.
import io

BLOCK = """
/* ============ فاز ۱۲۴-ه — توکن ردیف کلید-مقدار پنل‌های جزئیات (الگوی lic-row تعمیم‌یافته) ============ */
.kvs { display: grid; gap: 8px; }
.kvr { display: flex; justify-content: space-between; align-items: center; gap: 14px; padding: 8px 12px; border: 1px solid var(--line-soft); border-radius: 10px; font-size: 12.5px; line-height: 1.9; }
.kvr .k { color: var(--ink-3); flex: none; }
.kvr .v { color: var(--ink); font-weight: 600; text-align: end; word-break: break-word; }
"""

OLD = BLOCK.replace(".kvr", ".kv")  # نام اولیه که با .tbl.kv موجود در Settings تعارض داشت

for p in ["/home/z/my-project/aspnet/src/Platform.Web/wwwroot/css/site.css",
          "/home/z/my-project/aspnet/src/Platform.Web/wwwroot/css/site-ltr.css"]:
    with io.open(p, encoding="utf-8") as f:
        src = f.read()
    if "فاز ۱۲۴-ه — توکن ردیف" not in src:
        if not src.endswith("\n"):
            src += "\n"
        src += BLOCK
        note = "OK (append)"
    elif OLD in src:
        src = src.replace(OLD, BLOCK, 1)
        note = "OK (rename .kv→.kvr)"
    else:
        print(f"SKIP (بلوک درست موجود): {p}")
        continue
    with io.open(p, "w", encoding="utf-8") as f:
        f.write(src)
    print(f"{note}: {p}")
