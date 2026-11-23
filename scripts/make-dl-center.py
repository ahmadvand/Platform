#!/usr/bin/env python3
# ======================================================================
# فاز ۵۸ — مرکز دانلود + مرورگر سورس داخل خود اپ (wwwroot/dl/)
# فاز ۱۰۶ (درخواست کاربر) — مهاجرت مرکز دانلود به مسیر جدید wwwroot/dlc/
#   (URL: /dlc) و حذف کامل مسیر قبلی /dl؛ بخش جدید «بسته‌های محافظ صفحه»
#   از assets/screensaver-packages (فایل‌های کامیت‌شده کنار سورس، شامل «باران شعر»).
# هدف: فایل‌های تحویلی و درخت سورس از طریق لینک پیش‌نمایش قابل دسترس باشند.
#   • zipهای بستهٔ مشتری → wwwroot/dlc/*.zip
#   • بسته‌های محافظ صفحه (رفع باگ اتصال حروف + فونت وزیر) → wwwroot/dlc/ss-packages/
#   • سورس کامل (فایل‌های tracked در git) → wwwroot/dlc/source/<path>
#   • index.html فارسی RTL با کارت‌های دانلود + درخت پوشه‌ها (details/summary)
# پوشه dlc/ در .gitignore است (آلوده کردن ریپو/پکیج مشتری ندارد).
# ======================================================================
import os, json, shutil, subprocess, html, datetime

ROOT   = "/home/z/my-project"
ASPNET = f"{ROOT}/aspnet"
WEB    = f"{ASPNET}/src/Platform.Web"
DL     = f"{WEB}/wwwroot/dlc"
SRC    = f"{DL}/source"
SSPKGS = f"{ROOT}/assets/screensaver-packages"   # بسته‌های محافظ — کامیت‌شده کنار سورس

# نسخهٔ فعلی سامانه — تنها نقطهٔ تغییر برای bump نسخه در این صفحه
APP_VER    = "2.7.28"
APP_VER_FA = "۲.۷.۲۸"

def size_h(n):
    for u in ("B", "KB", "MB", "GB"):
        if n < 1024: return f"{n:.0f} {u}" if u == "B" else f"{n:.1f} {u}"
        n /= 1024
    return f"{n:.1f} TB"

# ---------- ۰) حذف مرکز دانلود قدیمی (فاز ۱۰۶ — مسیر قبلی دیگر سرو نمی‌شود) ----------
shutil.rmtree(f"{WEB}/wwwroot/dl", ignore_errors=True)
shutil.rmtree(f"{ASPNET}/sc-run/wwwroot/dl", ignore_errors=True)

# ---------- ۱) پاکسازی و کپی فایل‌های تحویلی (دینامیک از download/ — فهرست هاردکد نمی‌میرد) ----------
shutil.rmtree(DL, ignore_errors=True)
os.makedirs(SRC, exist_ok=True)

DESCS = {
    "HR-Platform-2.7.27-customer-FULL.zip":              "بستهٔ کامل مشتری — هر ۵ ماژول سیستم + ۳ پایه — فاز ۱۲۴ (بازطراحی بصری Figma): توکن هدر/بنر ۱۸ صفحه + بنرهای داده‌محور دستگاه‌ها و شیفت‌ها + توکن ردیف کلید-مقدار .kvr در مجوز و پرتال من + نسخهٔ 2.7.27",
    "HR-Platform-2.7.27-customer-MINIMAL-base-only.zip": "بستهٔ حداقلی — فقط ۳ ماژول پایه؛ بدون هیچ DLL ماژول (اثبات کارکرد بدون ماژول) — همان فاز ۱۲۴ و نسخهٔ 2.7.27",
    "HR-Platform-Source-2.7.27":                         "سورس کامل 2.7.27 (git archive از کامیت انتشار؛ نام فایل حاوی تاریخ + ساعت تهران) — فاز ۱۲۴ کامل: توکن‌های مشترک هدر/بنر/ردیف کلید-مقدار (.kvr)، بنرهای داده‌محور، ۱۸ هدر صفحه، اسکریپت دیپلوی پارامتری با چک دیسک — فقط آخرین نسخهٔ سورس در مرکز نگه داشته می‌شود (قانون همیشگی کاربر)",
    "HR-Platform-2.7.19-customer-FULL.zip":              "بستهٔ کامل مشتری — هر ۵ ماژول سیستم + ۳ پایه — فاز ۱۰۶ + ترمیم 2.7.19: رفع ریدایرکت «http://» در نشانی /dlc (Time-out پشت لبهٔ https) + مهاجرت مرکز دانلود به /dlc + رفع کامل «NetworkError» پنل رمز بیدارسازی (retry شبکه‌ای + KeepAliveTimeout) + رفع «مقطع بودن» متن محافظ‌ها (تزریق فونت وزیر در میزبان + بستهٔ جدید «باران شعر») + فاز ۱۰۵: انتقال خودِ اسکرین‌سیور + سندباکس پنج‌لایه",
    "HR-Platform-2.7.19-customer-MINIMAL-base-only.zip": "بستهٔ حداقلی — فقط ۳ ماژول پایه؛ بدون هیچ DLL ماژول (اثبات کارکرد بدون ماژول) — همان فاز ۱۰۶ + ترمیم 2.7.19",
    "HR-Platform-Source-2.7.19":                         "سورس کامل 2.7.19 (git archive از کامیت انتشار؛ نام فایل حاوی تاریخ + ساعت تهران) — فاز ۱۰۶ + ترمیم 2.7.19: rewrite داخلی «/dlc» بدون ریدایرکت مطلق (رفع Time-out پشت لبهٔ https) + مرکز دانلود در مسیر جدید /dlc + بسته‌های محافظ در assets/screensaver-packages کنار سورس (کامیت‌شده، شامل «باران شعر») با رفع باگ «حروف جدا/مقطع» — رسم کل عبارت فارسی با یک fillText + فونت وزیرمتن embed + تزریق فونت در میزبان Runtime + retry شبکه‌ای رمز بیدارسازی + فازهای ۱۰۵ تا ۱۰۱ محافظ صفحه — فقط آخرین نسخهٔ سورس در مرکز نگه داشته می‌شود (قانون همیشگی کاربر)",
    "HR-Platform-Windows-Tools-2.7.11.zip":              "ابزارهای ویندوزی: ساخت نصب‌کنندهٔ MSI مانند روی خود ویندوز (build-installer.bat با رفع خطای Execution Policy — «is not digitally signed») + ساخت بستهٔ به‌روزرسانی (make-update-package.ps1) + نصب‌کنندهٔ PowerShell + راهنمای فارسی گام‌به‌گام",
    "LicenseStudio.zip":                                "استودیو مجوز نصب ۱.۷.۱ ویندوز (تک‌فایل) — رفع نمایش تب راهنما (شکست خط‌ها؛ سورس LF ویندوز CRLF می‌خواهد) + ویزارد صدور گام‌به‌گام + انتخاب ماژول‌ها + حالت اجرای قفل + بررسی با کلید عمومی دلخواه؛ راست‌چین کامل",
    "TranslationStudio.zip":                            "استودیو ترجمه ۱.۲.۱ ویندوز (تک‌فایل) — رفع به‌هم‌ریختگی چیدمان و دیده‌شدن باکس جستجو و راهنما (مقیدسازی عرض ستون‌ها) + جدول از لحظهٔ باز شدن با ۵۲۵ کلید مرجع سامانه پر است + ستون مرجع English + «پرکردن از مرجع»؛ فرم راست‌چین",
    # ---------- اسناد تحویلی (فاز دستور به هوش مصنوعی + مستندات) ----------
    "handoff.md":                                       "دستور تحویل به هوش مصنوعی (Markdown) — معماری، قراردادها، ساختار سورس و دستورالعمل گام‌به‌گام افزودن فیچر جدید به همین کدبیس با همین قواعد",
    "HR-AI-Handoff-FA.docx":                            "سند تحویل به هوش مصنوعی (Word) — هر آنچه یک AI برای ساخت فیچر جدید روی همین معماری لازم دارد",
    "HR-AI-Handoff-FA.pdf":                             "سند تحویل به هوش مصنوعی (PDF) — هر آنچه یک AI برای ساخت فیچر جدید روی همین معماری لازم دارد",
    "HR-Architecture-FA.docx":                          "مستندات معماری نرم‌افزار (Word) — لایه‌ها، ماژول‌ها، جریان داده، قراردادهای گیت‌وی و نقاط افزونش",
    "HR-Architecture-FA.pdf":                           "مستندات معماری نرم‌افزار (PDF) — لایه‌ها، ماژول‌ها، جریان داده، قراردادهای گیت‌وی و نقاط افزونش",
    "HR-Core-Rebuild-Guide-FA.docx":                    "راهنمای ساخت پروژهٔ مشابه از صفر (Word) — فقط هستهٔ اصلی و موارد پایه، بدون ماژول‌های اضافی",
    "HR-Core-Rebuild-Guide-FA.pdf":                     "راهنمای ساخت پروژهٔ مشابه از صفر (PDF) — فقط هستهٔ اصلی و موارد پایه، بدون ماژول‌های اضافی",
    "HR-Setup-Guide-FA.docx":                           "راهنمای راه‌اندازی قدم‌به‌قدم (Word) — ایجاد پایگاه داده، اتصالات، اولیه‌سازی و ورود",
    "HR-Setup-Guide-FA.pdf":                            "راهنمای راه‌اندازی قدم‌به‌قدم (PDF) — ایجاد پایگاه داده، اتصالات، اولیه‌سازی و ورود",
    "HR-User-Guide-FA.docx":                            "راهنمای کاربر (Word) — کار روزمره با سامانه: ورود، داشبورد، پرسنل، حضور، حقوق و تنظیمات",
    "HR-User-Guide-FA.pdf":                             "راهنمای کاربر (PDF) — کار روزمره با سامانه: ورود، داشبورد، پرسنل، حضور، حقوق و تنظیمات",
    "HR-AI-Module-Creation-Guide.md":                   "راهنمای ایجاد ماژول جدید — نسخهٔ هوش مصنوعی (Markdown) — نقشهٔ راه ۱۴ گامی + قواعد طلایی + قالب‌های کد قراردادهای IHrDataModule/IHrUiModule + سید خودترمیم + چک‌لیست پذیرش و خطاهای رایج (نسخهٔ 2.7.27)",
    "HR-Module-Creation-Guide-Dev.pdf":                 "راهنمای ایجاد ماژول جدید — سند برنامه‌نویس (PDF، ۱۲ صفحه، فارسی RTL) — معماری پلاگین دواسمبلی، موجودیت‌ها و DbContext، سید، گیت‌وی بین‌ماژولی، منو و مجوزها، تنظیمات، مرکز برون‌بری/درون‌ریزی و بستهٔ مشتری ماژول‌محور (نسخهٔ 2.7.27)",
    "HR-Platform-2.7.11-Docs-FA.zip":                   "هر ۱۱ فایل مستندات در یک بسته (فاز ۹۸ — به‌روز با نسخهٔ 2.7.11؛ فونت وزیر) — handoff.md + تحویل به AI + معماری + بازسازی هسته از صفر + راه‌اندازی (ویزارد سه‌تبی + عیب‌یابی Execution Policy) + راهنمای کاربر (محافظ صفحه، تقویم تعطیلات و قالب برون‌بری) — Word و PDF",
}
SKIP_SOURCE_PREFIXES = ("aspnet/sc-run/", "aspnet/plugins/")   # باینری‌های دیپلوی — وارد مرورگر سورس و سورس‌زیپ نشوند

import glob as _glob
deliv = []   # (مسیر نسبی زیر download/، مسیر مطلق مبدأ)
for _p in sorted(_glob.glob(f"{ROOT}/download/*.zip")):
    deliv.append((os.path.basename(_p), _p))
for _p in sorted(_glob.glob(f"{ROOT}/download/*/*")):
    if os.path.isfile(_p):
        deliv.append((os.path.relpath(_p, f"{ROOT}/download"), _p))

DOC_EXTS = (".md", ".docx", ".pdf")

def card(rel, srcp, icon):
    base = os.path.basename(rel)
    # تطبیق پیشوندی — نام فایل‌های زمان‌دار (مثل HR-Platform-Source-2.7.12-<datetime>.zip) هم توضیح می‌گیرند
    desc = next((v for k, v in DESCS.items() if base.startswith(k)),
                "فایل تحویلی پروژه")
    dst = f"{DL}/{rel}"
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(srcp, dst)
    return f"""
    <a class="card" href="/dlc/{rel}" download>
      <div class="ic">{icon}</div>
      <div class="tx"><b>{rel}</b><span>{html.escape(desc)}</span></div>
      <div class="sz">{size_h(os.path.getsize(dst))}</div>
    </a>"""

docs_rows = ""
zip_rows = ""
for rel, srcp in deliv:
    if rel.lower().endswith(DOC_EXTS):
        docs_rows += card(rel, srcp, "📄")
    else:
        zip_rows += card(rel, srcp, "📦")

# ---------- ۱٫۵) بسته‌های محافظ صفحه — از assets/screensaver-packages (کامیت‌شده کنار سورس) ----------
# فاز ۱۰۶ (درخواست کاربر): بسته‌های محافظ با رفع باگ «حروف جدا/مقطع» + فونت وزیر embed +
# بستهٔ جدید «باران شعر» — ماندگار در درخت git و اینجا هم برای دانلود مستقیم سرو می‌شوند.
ss_rows = ""
ss_count = 0
for _p in sorted(_glob.glob(f"{SSPKGS}/*.json")):
    try:
        _b = json.loads(open(_p, encoding="utf-8").read())
        _m = _b.get("meta", {})
        _nm = html.escape(str(_m.get("name", os.path.basename(_p))))
        _vs = html.escape(str(_m.get("version", "")))
        _ds = html.escape(str(_m.get("description", "بستهٔ محافظ صفحه")))
    except Exception:
        _nm, _vs, _ds = html.escape(os.path.basename(_p)), "", "بستهٔ محافظ صفحه"
    _dst = f"{DL}/ss-packages/{os.path.basename(_p)}"
    os.makedirs(os.path.dirname(_dst), exist_ok=True)
    shutil.copy2(_p, _dst)
    _sz = os.path.getsize(_dst)
    ss_count += 1
    ss_rows += f"""
    <a class="card" href="/dlc/ss-packages/{html.escape(os.path.basename(_p))}" download>
      <div class="ic">🌌</div>
      <div class="tx"><b>{_nm}</b><span>{_ds}</span></div>
      <div class="sz">{_vs} · {size_h(_sz)}</div>
    </a>"""
if ss_count and os.path.isfile(f"{SSPKGS}/README.md"):
    shutil.copy2(f"{SSPKGS}/README.md", f"{DL}/ss-packages/README.md")

# ---------- ۲) کپی سورس tracked ----------
files = [f for f in subprocess.run(["git", "ls-files"], cwd=ROOT, capture_output=True, text=True).stdout.splitlines()
         if not f.startswith(SKIP_SOURCE_PREFIXES)]
tree = {}   # path parts -> dict/None(file)
copied = 0
for rel in files:
    src_path = os.path.join(ROOT, rel)
    if not os.path.isfile(src_path):
        # فایل حذف‌شده از دیسک ولی هنوز در ایندکس گیت (شبح/اسکرچ حذف‌نشده) —
        # مرکز را نمی‌کشد؛ هشدار می‌دهد و رد می‌کند (درس فاز ۱۲۹: tmp-p81b).
        print(f"WARN: indexed file missing on disk, skipped: {rel}")
        continue
    dst = os.path.join(SRC, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(src_path, dst)
    parts = rel.split("/")
    node = tree
    for p in parts[:-1]: node = node.setdefault(p, {})
    node[parts[-1]] = None
    copied += 1

# ---------- ۳) رندر درخت HTML ----------
def render(node, path=""):
    dirs  = sorted([k for k, v in node.items() if v is not None], key=str.lower)
    files_= sorted([k for k, v in node.items() if v is None], key=str.lower)
    out = ['<ul class="tree">']
    for d in dirs:
        cnt = count_files(node[d])
        out.append(f'<li><details open><summary>📁 {html.escape(d)} <i>({cnt} فایل)</i></summary>')
        out.append(render(node[d], f"{path}/{d}"))
        out.append("</details></li>")
    for f in files_:
        rel = f"{path}/{f}".lstrip("/")
        try: sz = size_h(os.path.getsize(os.path.join(SRC, rel)))
        except: sz = ""
        icon = "⚙️" if f.endswith((".cs", ".csproj")) else "🧩" if f.endswith(".cshtml") else "📜" if f.endswith((".sh", ".py", ".mjs")) else "📄"
        out.append(f'<li class="f">{icon} <a href="/dlc/source/{html.escape(rel)}">{html.escape(f)}</a> <i>{sz}</i></li>')
    out.append("</ul>")
    return "".join(out)

def count_files(node):
    n = 0
    for k, v in node.items():
        n += 1 if v is None else count_files(v)
    return n

now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
page = f"""<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>مرکز دانلود و سورس — نسخهٔ {APP_VER_FA}</title>
<style>
  * {{ box-sizing: border-box; }}
  body {{ font-family: Vazirmatn, "Segoe UI", Tahoma, sans-serif; margin:0; background:#f4f6fb; color:#1e2433; }}
  .hd {{ background:linear-gradient(135deg,#0f766e,#115e59); color:#fff; padding:28px 24px; }}
  .hd h1 {{ margin:0 0 6px; font-size:22px; }}
  .hd p  {{ margin:0; opacity:.85; font-size:13px; }}
  main {{ max-width: 960px; margin: 0 auto; padding: 20px 16px 60px; }}
  h2 {{ font-size:16px; margin:28px 0 12px; color:#0f766e; border-right:4px solid #0f766e; padding-right:10px; }}
  .card {{ display:flex; align-items:center; gap:14px; background:#fff; border:1px solid #e3e8f0; border-radius:12px;
          padding:14px 16px; margin-bottom:10px; text-decoration:none; color:inherit; transition:.15s; }}
  .card:hover {{ border-color:#0f766e; box-shadow:0 2px 10px rgba(15,118,110,.12); }}
  .card .ic {{ font-size:26px; }}
  .card .tx {{ flex:1; display:flex; flex-direction:column; gap:3px; font-size:13px; }}
  .card .tx b {{ font-size:14px; direction:ltr; text-align:right; }}
  .card .sz {{ background:#e6f5f3; color:#0f766e; border-radius:8px; padding:4px 10px; font-size:12px; white-space:nowrap; }}
  details {{ background:#fff; border:1px solid #e3e8f0; border-radius:10px; margin-bottom:8px; }}
  summary {{ cursor:pointer; padding:10px 14px; font-weight:700; font-size:13px; }}
  summary i, .f i {{ color:#8a93a6; font-weight:400; font-size:11px; font-style:normal; }}
  ul.tree {{ list-style:none; margin:0; padding:4px 18px 12px 0; }}
  ul.tree ul {{ border-right:1px dashed #d5dbe7; margin-right:8px; }}
  .f {{ padding:3px 0; font-size:13px; }}
  .f a {{ color:#115e59; text-decoration:none; }}
  .f a:hover {{ text-decoration:underline; }}
  .note {{ background:#fffbeb; border:1px solid #fde68a; border-radius:10px; padding:12px 14px; font-size:13px; margin-top:22px; }}
  code {{ background:#eef2f7; border-radius:6px; padding:1px 6px; font-size:12px; direction:ltr; display:inline-block; }}
</style>
</head>
<body>
<div class="hd">
  <h1>مرکز دانلود و سورس — سامانه یکپارچه منابع انسانی</h1>
  <p>نسخهٔ {APP_VER_FA} — مسیر جدید <code style="background:rgba(255,255,255,.15);color:#fff">/dlc</code> (مسیر قبلی /dl حذف شد — فاز ۱۰۶) · مستندات + بسته‌های محافظ + سورس کامل (Platform.sln) · تولید {now}</p>
</div>
<main>
  <h2>بسته‌های محافظ صفحه — نسخهٔ ۲.۱ با رفع کامل «حروف جدا/مقطع» + فونت وزیر embed + «باران شعر»</h2>
  {ss_rows}
  <h2>اسناد و مستندات (Word / PDF / Markdown)</h2>
  {docs_rows}
  <h2>بسته‌های آمادهٔ مشتری</h2>
  {zip_rows}
  <h2>دایرکتوری سورس ({copied} فایل — کل مخزن)</h2>
  {render(tree)}
  <div class="note">
    💡 ساخت بستهٔ مشتری دلخواه روی ماشین خودتان: <code>scripts/build-customer.sh parking,payroll 2.7.12 "نام شرکت"</code>
    — ماژول‌های حذف‌شده هیچ DLLای در بسته ندارند و سامانه بدون آن‌ها سالم بالا می‌آید (صفحهٔ «نصب نشده در این نسخه»).
    ماژول‌های پایه (هسته/سازمان/تجهیزات) ستون فقرات هاست‌اند و همیشه همراه سامانه می‌مانند.
  </div>
</main>
</body>
</html>"""

with open(f"{DL}/index.html", "w", encoding="utf-8") as f:
    f.write(page)

print(f"OK: {len(deliv)} deliverable file(s) + {copied} source files → {DL}")
