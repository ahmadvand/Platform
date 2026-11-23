# -*- coding: utf-8 -*-
"""فاز ۳۲ — بازنویسی site.css: پالت چندنقشی تخت + حذف کامل گرادیان/طیف"""
import io, sys

P = '/home/z/my-project/aspnet/src/HrAttendance.Web/wwwroot/css/site.css'
src = io.open(P, encoding='utf-8').read()
n0 = src

def rep(old, new, must=True):
    global src
    if old not in src:
        if must:
            print('!! NOT FOUND:', old[:90]); sys.exit(1)
        return
    src = src.replace(old, new, 1)

def rep_all(old, new, must=True):
    global src
    if old not in src:
        if must:
            print('!! NOT FOUND(all):', old[:90]); sys.exit(1)
        return
    src = src.replace(old, new)

# ---------- ۱) توکن‌های جدید در :root ----------
rep("""    --ring: 0 0 0 3px rgba(var(--accent-rgb), .20);
}""",
"""    --ring: 0 0 0 3px rgba(var(--accent-rgb), .20);

    /* نقش‌های استاندارد پالت (فاز ۳۲) — همه «تخت» و بدون گرادیان؛
       پالت فعال ادمین این‌ها را بازنویسی می‌کند (رنگ لینک‌ها، دکمه‌ها،
       سربرگ/سطر جدول، منو، متن، پس‌زمینه، وضعیت‌ها، اسکرول‌بار و اوورلی) */
    --link: #0f766e;                 /* رنگ لینک‌ها */
    --th-bg: #f4f9f8;                /* سربرگ جدول (ستون‌ها) */
    --row-alt: transparent;          /* سطر یک‌درمیان */
    --row-hover: #f2faf8;            /* سطر هاور */
    --sidebar-text: #b9d2d6;         /* متن منو */
    --sidebar-muted: #7d99a1;        /* متن کم‌رنگ منو */
    --sidebar-title: #eef7f7;        /* عنوان برند در منو */
    --sidebar-rgb: 13, 36, 44;       /* مثلث RGB منو (اوورلی/سایه) */
    --card-rgb: 255, 255, 255;       /* مثلث RGB کارت */
    --ink-rgb: 18, 38, 44;           /* مثلث RGB متن (سایه‌ها) */
    --ink-soft: #1d4a52;             /* حالت hover دکمه تیره/تب فعال */
    --btn-hover-bg: #f4faf9;         /* پس‌زمینه hover دکمه خنثی */
    --btn-hover-border: #c3d9d6;     /* حاشیه hover دکمه/ورودی */
    --scroll-thumb: #bed4d5;         /* اسکرول‌بار */
    --scroll-thumb-hover: #9dbfc1;
    --topbar-bg: rgba(255, 255, 255, .88);  /* نوار بالا */
    --overlay: rgba(10, 30, 36, .45);       /* پرده دیالوگ/مودال */
}""")

# ---------- ۲) پس‌زمینه صفحه تخت ----------
rep("""body {
    font-family: Vazirmatn, Tahoma, sans-serif;
    background:
        radial-gradient(1100px 420px at 88% -8%, rgba(var(--accent-rgb), .08), transparent 62%),
        radial-gradient(900px 380px at 4% 110%, rgba(var(--accent-rgb), .05), transparent 60%),
        var(--bg);
    background-attachment: fixed;
    color: var(--ink);""",
"""body {
    font-family: Vazirmatn, Tahoma, sans-serif;
    background: var(--bg);   /* تخت — بدون طیف (فاز ۳۲) */
    color: var(--ink);""")

rep("a { color: var(--accent-ink); text-decoration: none; }",
    "a { color: var(--link); text-decoration: none; }")

# ---------- ۳) اسکرول‌بار هم‌پالت ----------
rep("scrollbar-color: #bed4d5 transparent;", "scrollbar-color: var(--scroll-thumb) transparent;")
rep("*::-webkit-scrollbar-thumb { background: #bed4d5;", "*::-webkit-scrollbar-thumb { background: var(--scroll-thumb);")
rep("*::-webkit-scrollbar-thumb:hover { background: #9dbfc1;", "*::-webkit-scrollbar-thumb:hover { background: var(--scroll-thumb-hover);")

# ---------- ۴) سایه‌ها از توکن متن ----------
rep("--shadow: 0 1px 2px rgba(18, 38, 44, .05), 0 6px 20px -6px rgba(18, 38, 44, .07);",
    "--shadow: 0 1px 2px rgba(var(--ink-rgb), .05), 0 6px 20px -6px rgba(var(--ink-rgb), .07);")
rep("--shadow-lift: 0 2px 4px rgba(18, 38, 44, .06), 0 14px 34px -10px rgba(18, 38, 44, .14);",
    "--shadow-lift: 0 2px 4px rgba(var(--ink-rgb), .06), 0 14px 34px -10px rgba(var(--ink-rgb), .14);")

# ---------- ۵) سایدبار تخت ----------
rep(""".sidebar {
    background: linear-gradient(188deg, var(--sidebar) 0%, var(--sidebar-2) 100%);
    color: #b9d2d6;""",
""".sidebar {
    background: var(--sidebar);   /* تخت — بدون گرادیان */
    color: var(--sidebar-text);""")

rep(""".brand-logo {
    width: 42px; height: 42px; border-radius: 12px; flex: none;
    background: linear-gradient(135deg, var(--accent-3), var(--accent) 55%, var(--accent-4));""",
""".brand-logo {
    width: 42px; height: 42px; border-radius: 12px; flex: none;
    background: var(--accent);   /* تخت */""")

rep(".brand-name { font-weight: 700; color: #eef7f7; font-size: 13.5px; line-height: 1.6; }",
    ".brand-name { font-weight: 700; color: var(--sidebar-title); font-size: 13.5px; line-height: 1.6; }")
rep(".brand-sub { font-size: 10.5px; color: #7d99a1; }",
    ".brand-sub { font-size: 10.5px; color: var(--sidebar-muted); }")
rep(".nav-group { font-size: 10.5px; color: #7d99a1;",
    ".nav-group { font-size: 10.5px; color: var(--sidebar-muted);")

rep("""    padding: 9px 12px; margin-bottom: 2px; border-radius: 10px;
    color: #b9d2d6; font-size: 13px; transition: background .15s, color .15s;""",
"""    padding: 9px 12px; margin-bottom: 2px; border-radius: 10px;
    color: var(--sidebar-text); font-size: 13px; transition: background .15s, color .15s;""")

rep(".nav-item:hover { background: rgba(148, 197, 200, .1); color: #eef7f7; text-decoration: none; }",
    ".nav-item:hover { background: color-mix(in srgb, var(--sidebar-text) 12%, transparent); color: var(--sidebar-title); text-decoration: none; }")

rep(""".nav-item.active {
    background: linear-gradient(90deg, rgba(var(--accent-rgb), .30), rgba(var(--accent-rgb), .08));
    color: var(--accent-soft-text); font-weight: 600;
    box-shadow: inset 2px 0 0 var(--accent-3);
}""",
""".nav-item.active {
    background: rgba(var(--accent-rgb), .28);   /* تخت */
    color: var(--accent-soft-text); font-weight: 600;
    box-shadow: inset 2px 0 0 var(--accent);
}""")

rep("""    background: rgba(var(--accent-rgb), .22); color: var(--accent-soft-text);""",
"""    background: rgba(var(--accent-rgb), .22); color: var(--accent-soft-text);""")

rep(".user-name { color: #eef7f7; font-size: 12.5px; font-weight: 600; }",
    ".user-name { color: var(--sidebar-title); font-size: 12.5px; font-weight: 600; }")
rep(".user-role { color: #7d99a1; font-size: 10.5px; }",
    ".user-role { color: var(--sidebar-muted); font-size: 10.5px; }")

# ---------- ۶) نوار بالا ----------
rep("background: rgba(255, 255, 255, .88);", "background: var(--topbar-bg);")

# ---------- ۷) جدول — سربرگ/سطر ----------
rep("    background: #f4f9f8; color: var(--ink-2); font-weight: 600; font-size: 11.5px;",
    "    background: var(--th-bg); color: var(--ink-2); font-weight: 600; font-size: 11.5px;")
rep(".tbl tbody tr { transition: background .12s; }\n.tbl tbody tr:hover { background: #f2faf8; }",
    ".tbl tbody tr { transition: background .12s; }\n.tbl tbody tr:nth-child(even) { background: var(--row-alt); }   /* سطر یک‌درمیان */\n.tbl tbody tr:hover { background: var(--row-hover); }")

# ---------- ۸) نشان‌ها ----------
rep(".badge-mute { background: #eff4f3; color: var(--ink-2); border-color: var(--line); }",
    ".badge-mute { background: var(--line-soft); color: var(--ink-2); border-color: var(--line); }")

# ---------- ۹) فرم‌ها ----------
rep("    border: 1px solid var(--line); border-radius: var(--radius-sm); background: #fff; color: var(--ink);",
    "    border: 1px solid var(--line); border-radius: var(--radius-sm); background: var(--card); color: var(--ink);")
rep(".fld input:hover, .fld textarea:hover { border-color: #c3d9d6; }",
    ".fld input:hover, .fld textarea:hover { border-color: var(--btn-hover-border); }")
rep("    background-color: #fff; border: 1px solid var(--line); border-radius: var(--radius-sm);",
    "    background-color: var(--card); border: 1px solid var(--line); border-radius: var(--radius-sm);")
rep("select:hover { border-color: #c3d9d6; }",
    "select:hover { border-color: var(--btn-hover-border); }")

# ---------- ۱۰) دکمه‌ها ----------
rep("""    padding: 8px 14px; border-radius: var(--radius-sm); border: 1px solid var(--line);
    background: #fff; color: var(--ink-2);""",
"""    padding: 8px 14px; border-radius: var(--radius-sm); border: 1px solid var(--line);
    background: var(--card); color: var(--ink-2);""")
rep(".btn:hover { background: #f4faf9; border-color: #c3d9d6; color: var(--ink); text-decoration: none; }",
    ".btn:hover { background: var(--btn-hover-bg); border-color: var(--btn-hover-border); color: var(--ink); text-decoration: none; }")
rep(""".btn-primary {
    background: linear-gradient(180deg, var(--accent-2), var(--accent));
    color: #fff; border-color: var(--accent-deep);
    box-shadow: 0 1px 2px rgba(var(--accent-rgb), .3), 0 6px 16px -6px rgba(var(--accent-rgb), .45);
}
.btn-primary:hover { background: linear-gradient(180deg, var(--accent-2), var(--accent-deep)); color: #fff; filter: none; }""",
""".btn-primary {
    background: var(--accent);   /* تخت — بدون گرادیان */
    color: #fff; border-color: var(--accent-deep);
    box-shadow: 0 1px 2px rgba(var(--accent-rgb), .3), 0 6px 16px -6px rgba(var(--accent-rgb), .45);
}
.btn-primary:hover { background: var(--accent-deep); color: #fff; filter: none; }""")
rep(".btn-dark:hover { background: #1d4a52; color: #fff; }",
    ".btn-dark:hover { background: var(--ink-soft); color: #fff; }")

# ---------- ۱۱) تب‌ها و چیپ‌ها ----------
rep(".tabs a.on:hover { background: #1d4a52; color: #fff; }",
    ".tabs a.on:hover { background: var(--ink-soft); color: #fff; }")
rep(".chip-active { background: rgba(37,99,235,.10); border-color: var(--primary); color: var(--primary); font-weight: 700; }",
    ".chip-active { background: rgba(var(--accent-rgb), .10); border-color: var(--accent); color: var(--accent-ink); font-weight: 700; }")

# ---------- ۱۲) درخت و تقویم ----------
rep(".tree-node:hover { background: #f2faf8; }",
    ".tree-node:hover { background: var(--row-hover); }")
rep(".cal th { padding: 8px; font-size: 11.5px; color: var(--ink-2); border: 1px solid var(--line); background: #f4f9f8; }",
    ".cal th { padding: 8px; font-size: 11.5px; color: var(--ink-2); border: 1px solid var(--line); background: var(--th-bg); }")
rep(".cal td.off { background: #f4f9f8; }",
    ".cal td.off { background: var(--th-bg); }")

# ---------- ۱۳) صفحه ورود — تخت ----------
rep(""".login-page {
    min-height: 100vh; display: grid; place-items: center;
    background:
        radial-gradient(1100px 560px at 78% -12%, rgba(var(--accent-rgb), .40), transparent 58%),
        radial-gradient(800px 460px at 8% 108%, rgba(var(--accent-rgb), .30), transparent 55%),
        linear-gradient(160deg, var(--sidebar) 0%, var(--sidebar-2) 58%, color-mix(in srgb, var(--sidebar-2) 82%, white) 100%);
    padding: 20px;
}""",
""".login-page {
    min-height: 100vh; display: grid; place-items: center;
    background: var(--sidebar);   /* تخت — بدون طیف */
    padding: 20px;
}""")
rep("""    background: linear-gradient(135deg, #2dd4bf, #0d9488 55%, #0e7490);
    box-shadow: 0 10px 24px -6px rgba(20, 184, 166, .6), inset 0 1px 0 rgba(255, 255, 255, .3);""",
"""    background: var(--accent);   /* تخت */
    box-shadow: 0 10px 24px -6px rgba(var(--accent-rgb), .6), inset 0 1px 0 rgba(255, 255, 255, .3);""")
rep("box-shadow: 0 24px 70px rgba(6, 26, 32, .55), 0 0 0 1px rgba(255, 255, 255, .06) inset;",
    "box-shadow: 0 24px 70px rgba(var(--sidebar-rgb), .55), 0 0 0 1px rgba(255, 255, 255, .06) inset;")
rep("""    border: 1px solid var(--line); background: #f4f9f8; cursor: pointer; color: var(--ink-2);""",
"""    border: 1px solid var(--line); background: var(--th-bg); cursor: pointer; color: var(--ink-2);""")

# ---------- ۱۴) تب‌های هویتی ----------
rep(".btab:hover { color: var(--ink); background: rgba(20, 184, 166, .08); }",
    ".btab:hover { color: var(--ink); background: rgba(var(--accent-rgb), .08); }")

# ---------- ۱۵) پرتال خودخدمتی — تخت ----------
rep("""    background: linear-gradient(188deg, var(--sidebar) 0%, var(--sidebar-2) 100%);
    color: #b9d2d6; padding: 12px 22px; position: sticky; top: 0; z-index: 30;""",
"""    background: var(--sidebar);   /* تخت */
    color: var(--sidebar-text); padding: 12px 22px; position: sticky; top: 0; z-index: 30;""")
rep("""    background: linear-gradient(135deg, #2dd4bf, #0d9488 55%, #0e7490);
    color: #fff; font-size: 17px; flex: none;
    box-shadow: 0 4px 12px -2px rgba(20, 184, 166, .55);""",
"""    background: var(--accent);   /* تخت */
    color: #fff; font-size: 17px; flex: none;
    box-shadow: 0 4px 12px -2px rgba(var(--accent-rgb), .55);""")
rep(".ess-name { color: #eef7f7; font-weight: 700; font-size: 13.5px; }",
    ".ess-name { color: var(--sidebar-title); font-weight: 700; font-size: 13.5px; }")
rep(".ess-sub { font-size: 10px; color: #7d99a1; }",
    ".ess-sub { font-size: 10px; color: var(--sidebar-muted); }")
rep("""    color: #b9d2d6; font-size: 12.5px; padding: 7px 14px; border-radius: 9px;""",
"""    color: var(--sidebar-text); font-size: 12.5px; padding: 7px 14px; border-radius: 9px;""")
rep(".ess-nav a:hover { background: rgba(148, 197, 200, .12); color: #eef7f7; text-decoration: none; }",
    ".ess-nav a:hover { background: color-mix(in srgb, var(--sidebar-text) 12%, transparent); color: var(--sidebar-title); text-decoration: none; }")
rep(".ess-nav a.on { background: rgba(20, 184, 166, .28); color: #99e8da; font-weight: 600; }",
    ".ess-nav a.on { background: rgba(var(--accent-rgb), .28); color: var(--accent-soft-text); font-weight: 600; }")
rep(".ess-code { color: #7d99a1; font-size: 10px; }",
    ".ess-code { color: var(--sidebar-muted); font-size: 10px; }")

# ---------- ۱۶) اوورلی و سایه دیالوگ/مودال ----------
rep("background: rgba(10, 30, 36, .45);", "background: var(--overlay);")
rep("background: rgba(13, 36, 44, .45);", "background: var(--overlay);")
rep("box-shadow: 0 24px 60px -12px rgba(13, 36, 44, .35);",
    "box-shadow: 0 24px 60px -12px rgba(var(--sidebar-rgb), .35);")
rep("box-shadow: 0 24px 64px rgba(13, 36, 44, .35);",
    "box-shadow: 0 24px 64px rgba(var(--sidebar-rgb), .35);")

# ---------- ۱۷) نوار پیشرفت pjax — تخت ----------
rep("background: linear-gradient(90deg, var(--accent-2), var(--accent-deep));",
    "background: var(--accent);   /* تخت */")

# ---------- ۱۸) نقشه سایت — بدون درخشش شعاعی ----------
rep("""    background:
        linear-gradient(rgba(13, 148, 136, .045) 1px, transparent 1px),
        linear-gradient(90deg, rgba(13, 148, 136, .045) 1px, transparent 1px),
        radial-gradient(600px 260px at 80% 0%, rgba(20, 184, 166, .07), transparent 60%),
        #f8fcfb;""",
"""    background:
        linear-gradient(rgba(var(--accent-rgb), .05) 1px, transparent 1px),
        linear-gradient(90deg, rgba(var(--accent-rgb), .05) 1px, transparent 1px),
        var(--bg);""")
rep(".smap-gate.dragging { cursor: grabbing; box-shadow: 0 10px 26px -8px rgba(13, 148, 136, .45); z-index: 5; }",
    ".smap-gate.dragging { cursor: grabbing; box-shadow: 0 10px 26px -8px rgba(var(--accent-rgb), .45); z-index: 5; }")

# ---------- ۱۹) لوگوی تصویری — زمینه هم‌کارت ----------
rep("""    object-fit: contain; background: #fff; padding: 3px;""",
    """    object-fit: contain; background: var(--card); padding: 3px;""")
rep("""    object-fit: contain; background: #fff; padding: 5px; margin: 0 auto 12px;""",
    """    object-fit: contain; background: var(--card); padding: 5px; margin: 0 auto 12px;""")

# ---------- ۲۰) سبک کارت‌های پالت (فاز ۳۲) ----------
rep("""/* جدول کلید-مقدار کارت درباره */
.tbl.kv td:first-child { color: var(--ink-2); width: 130px; background: var(--line-soft); }""",
"""/* جدول کلید-مقدار کارت درباره */
.tbl.kv td:first-child { color: var(--ink-2); width: 130px; background: var(--line-soft); }

/* ============ فاز ۳۲ — کتابخانه پالت‌های استاندارد ============ */
.pal-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(215px, 1fr)); gap: 12px; }
.pal-card {
    border: 1.5px solid var(--line); border-radius: var(--radius-sm);
    background: var(--card); padding: 12px 13px; display: flex; flex-direction: column; gap: 9px;
    transition: border-color .15s, box-shadow .15s;
}
.pal-card:hover { border-color: var(--accent-border); box-shadow: var(--shadow); }
.pal-card.on { border-color: var(--accent); box-shadow: 0 0 0 3px rgba(var(--accent-rgb), .14); }
.pal-head { display: flex; align-items: center; justify-content: space-between; gap: 6px; }
.pal-name { font-weight: 700; font-size: 12.5px; color: var(--ink); }
.pal-tag {
    font-size: 9.5px; font-weight: 700; border-radius: 999px; padding: 1.5px 8px;
    background: var(--accent-soft); color: var(--accent-ink); border: 1px solid var(--accent-border);
}
.pal-tag.builtin { background: var(--line-soft); color: var(--ink-2); border-color: var(--line); }
.pal-swatches { display: flex; gap: 4px; }
.pal-sw { width: 22px; height: 22px; border-radius: 6px; border: 1px solid rgba(0, 0, 0, .08); flex: none; }
.pal-acts { display: flex; gap: 5px; flex-wrap: wrap; }
.pal-mini {
    border: 1px solid var(--line); border-radius: 9px; overflow: hidden;
    display: flex; height: 44px; font-size: 10px;
}
.pal-mini .m-side { width: 30%; padding: 5px 6px; color: var(--sidebar-text); }
.pal-mini .m-main { flex: 1; padding: 5px 7px; }
.pal-mini .m-link { color: var(--link); font-weight: 700; }
.pal-mini .m-btn {
    display: inline-block; background: var(--accent); color: #fff;
    border-radius: 5px; padding: 1px 7px; font-weight: 700; font-size: 9px;
}
/* فیلد رنگ — انتخاب‌گر + کد */
.color-pair { display: flex; gap: 8px; align-items: center; }
.color-pair input[type="color"] {
    width: 46px; height: 34px; padding: 2px; border: 1px solid var(--line);
    border-radius: 8px; background: var(--card); cursor: pointer; flex: none;
}
.color-pair code { font-size: 11.5px; color: var(--ink-2); direction: ltr; }""")

io.open(P, 'w', encoding='utf-8').write(src)
print('OK — CSS بازنویسی شد؛ طول:', len(n0), '→', len(src))
# گرادیان‌های باقی‌مانده
import re
for m in re.finditer(r'.*gradient.*', src):
    line = m.group(0).strip()
    print('GRAD:', line[:110])
