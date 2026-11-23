# -*- coding: utf-8 -*-
# ======================================================================
# یکپارچه‌سازی نهایی aspnet — نسخهٔ ۲ (روی درخت سبز HEAD)
#   HrAttendance.Web     -> Platform.Web
#   HrPlatform.Core      -> Platform.Core
#   HrPlatform.Contracts -> Platform.Contracts
#   HrModules.Parking    -> Platform.Modules.Parking  (+ .Data)
#   HrModules.Visitors   -> Platform.Modules.Visitors (+ .Data)
# قواعد طلایی: DP دست‌نخورده (SetApplicationName/purposeها)،
# اسامی کلاس‌ها ثابت، DB زنده حفظ، پلاگین‌کشی هر دو پیشوند.
# ======================================================================
import os, shutil, subprocess

ROOT = "/home/z/my-project"
ASP = os.path.join(ROOT, "aspnet")
SRC = os.path.join(ASP, "src")
BK = os.path.join(ROOT, "backups")

def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()

def write(p, s):
    with open(p, "w", encoding="utf-8") as f:
        f.write(s)

def git(*a):
    return subprocess.run(["git", "-C", ROOT, *a], capture_output=True, text=True)

# ---------- ۱) rename پوشه‌ها ----------
RENAMES = [
    ("src/HrAttendance.Web",           "src/Platform.Web"),
    ("src/HrPlatform.Core",            "src/Platform.Core"),
    ("src/HrPlatform.Contracts",       "src/Platform.Contracts"),
    ("src/modules/HrModules.Parking",  "src/modules/Platform.Modules.Parking"),
    ("src/modules/HrModules.Visitors", "src/modules/Platform.Modules.Visitors"),
]
for old_rel, new_rel in RENAMES:
    old, new = os.path.join(ASP, old_rel), os.path.join(ASP, new_rel)
    if os.path.isdir(old):
        r = git("mv", old_rel, new_rel)
        if r.returncode != 0:
            os.rename(old, new)
        print(f"[1] dir {old_rel} -> {new_rel}")
    elif os.path.isdir(new):
        print(f"[1] dir {old_rel} قبلاً renamed")

CSPROJ = [
    ("src/Platform.Web/HrAttendance.Web.csproj", "src/Platform.Web/Platform.Web.csproj"),
    ("src/Platform.Core/HrPlatform.Core.csproj", "src/Platform.Core/Platform.Core.csproj"),
    ("src/Platform.Contracts/HrPlatform.Contracts.csproj", "src/Platform.Contracts/Platform.Contracts.csproj"),
    ("src/modules/Platform.Modules.Parking/HrModules.Parking.csproj", "src/modules/Platform.Modules.Parking/Platform.Modules.Parking.csproj"),
    ("src/modules/Platform.Modules.Parking/Data/HrModules.Parking.Data.csproj", "src/modules/Platform.Modules.Parking/Data/Platform.Modules.Parking.Data.csproj"),
    ("src/modules/Platform.Modules.Visitors/HrModules.Visitors.csproj", "src/modules/Platform.Modules.Visitors/Platform.Modules.Visitors.csproj"),
    ("src/modules/Platform.Modules.Visitors/Data/HrModules.Visitors.Data.csproj", "src/modules/Platform.Modules.Visitors/Data/Platform.Modules.Visitors.Data.csproj"),
]
for old_rel, new_rel in CSPROJ:
    old = os.path.join(ASP, old_rel)
    if os.path.exists(old):
        r = git("mv", old_rel, new_rel)
        if r.returncode != 0:
            os.rename(old, os.path.join(ASP, new_rel))
        print(f"[1] csproj {os.path.basename(old_rel)} -> {os.path.basename(new_rel)}")

# ---------- ۲) بازنویسی متن ----------
DP = "§§DP_APP_NAME§§"
REPLACES = [
    ("HrModules.Parking.Data",   "Platform.Modules.Parking.Data"),
    ("HrModules.Parking",        "Platform.Modules.Parking"),
    ("HrModules.Visitors.Data",  "Platform.Modules.Visitors.Data"),
    ("HrModules.Visitors",       "Platform.Modules.Visitors"),
    ("HrPlatform.Core",          "Platform.Core"),
    ("HrPlatform.Contracts",     "Platform.Contracts"),
    ("HrAttendance.Web",         "Platform.Web"),
]
PROTECT = [(f'SetApplicationName("HrAttendance.Web")', f'SetApplicationName({DP})')]
UNPROT  = [(f'SetApplicationName({DP})', 'SetApplicationName("HrAttendance.Web")')]

def iter_files():
    for dirpath, dirnames, filenames in os.walk(SRC):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "logs", "App_Data", "keys", "dl")]
        for fn in filenames:
            if fn.rsplit(".", 1)[-1].lower() in ("cs", "cshtml", "csproj", "json", "props", "targets", "config"):
                yield os.path.join(dirpath, fn)
    yield os.path.join(ASP, "Platform.sln")
    tm = os.path.join(ASP, "tools", "Platform.Migrate")
    for dirpath, dirnames, filenames in os.walk(tm):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for fn in filenames:
            if fn.rsplit(".", 1)[-1].lower() in ("cs", "csproj", "json"):
                yield os.path.join(dirpath, fn)

n = 0
for path in iter_files():
    try:
        s = read(path)
    except (UnicodeDecodeError, OSError):
        continue
    orig = s
    for a, b in PROTECT: s = s.replace(a, b)
    for a, b in REPLACES: s = s.replace(a, b)
    for a, b in UNPROT: s = s.replace(a, b)
    if s != orig:
        write(path, s); n += 1
print(f"[2] {n} فایل بازنویسی شد")

# ---------- ۳) کشف/کپی پلاگین‌ها: Platform.Modules.*.dll ----------
def patch(rel, pairs):
    p = os.path.join(ASP, rel)
    if not os.path.exists(p): return
    s = read(p); o = s
    for a, b in pairs:
        if a in s and b not in s:
            s = s.replace(a, b)
    if s != o:
        write(p, s); print(f"[3] patch {rel}")

patch("src/Platform.Contracts/GatewayContracts.cs", [
    ('try { files = Directory.GetFiles(dir, "HrModules.*.dll"); }',
     '''try
            {
                // فاز ۸۷ — ماژول‌های Platform.Modules.* (پارکینگ/ارباب‌رجوع) هم کشف شوند
                files = Directory.GetFiles(dir, "HrModules.*.dll")
                    .Concat(Directory.GetFiles(dir, "Platform.Modules.*.dll"))
                    .ToArray();
            }'''),
])
patch("src/Platform.Web/Platform.Web.csproj", [
    ('<_HrPluginDlls Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />',
     '<_HrPluginDlls Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />\n    <_HrPluginDlls Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\Platform.Modules.*.dll" />'),
    ('<_HrPluginDllsPub Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />',
     '<_HrPluginDllsPub Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />\n    <_HrPluginDllsPub Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\Platform.Modules.*.dll" />'),
])

# ---------- ۴) .gitignore + start-web.sh ----------
gi = os.path.join(ROOT, ".gitignore")
s = read(gi)
s2 = s.replace("aspnet/src/HrAttendance.Web", "aspnet/src/Platform.Web")
if s2 != s: write(gi, s2); print("[4] .gitignore")
sw = os.path.join(ROOT, "scripts", "start-web.sh")
s = read(sw)
s2 = s.replace("aspnet/src/HrAttendance.Web", "aspnet/src/Platform.Web") \
      .replace("HrAttendance.Web.dll", "Platform.Web.dll") \
      .replace("launching HrAttendance.Web", "launching Platform.Web")
if s2 != s: write(sw, s2); print("[5] start-web.sh")

# ---------- ۵) حذف HrMigrate + بازگردانی DB زنده ----------
git("rm", "-r", "-q", "aspnet/tools/HrMigrate")
if os.path.isdir(os.path.join(ASP, "tools", "HrMigrate")):
    shutil.rmtree(os.path.join(ASP, "tools", "HrMigrate"))
print("[6] HrMigrate حذف شد")

live = os.path.join(BK, "hr-LIVE-20261104.db")
dst = os.path.join(SRC, "Platform.Web", "hr.db")
shutil.copy2(live, dst)
for ext in ("-wal", "-shm"):
    p = dst + ext
    if os.path.exists(p): os.remove(p)
print("[7] DB زنده → src/Platform.Web/hr.db")

# HrAttendance.sln در HEAD نیست (قبلاً پاک شد) — چک نهایی
hs = os.path.join(ASP, "HrAttendance.sln")
if os.path.exists(hs): os.remove(hs)
print("DONE")
