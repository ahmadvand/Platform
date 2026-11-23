# -*- coding: utf-8 -*-
# ======================================================================
# یکپارچه‌سازی نهایی aspnet به قالب کانونی کاربر (2026-11-04)
#
#   src/HrAttendance.Web       -> src/Platform.Web
#   src/HrPlatform.Core        -> src/Platform.Core
#   src/HrPlatform.Contracts   -> src/Platform.Contracts
#   modules/HrModules.Parking  -> modules/Platform.Modules.Parking  (+ .Data)
#   modules/HrModules.Visitors -> modules/Platform.Modules.Visitors (+ .Data)
#   نگه‌داشت: HrModules.Access/Attendance/DeviceGateway/Devices/Org/Payroll (+ .Data)
#
# قواعد طلایی:
#   • DataProtection دست‌نخورده: SetApplicationName("HrAttendance.Web") و
#     purposeها ("HrAttendance.SetupSettings" و ...) عوض نمی‌شوند تا
#     hrsettings.bin و کوکی‌ها/لایسنس سالم بمانند.
#   • اسامی کلاس‌ها (HrModuleRuntime، HrLang، ...) تغییر نمی‌کند — فقط
#     نام پروژه/پوشه/فضانام.
#   • DB زنده (Platform.Web/hrdb قدیمی — منبع واقعی داده) قبل از حذف
#     پوشه‌های کهنه نجات داده می‌شود.
# ======================================================================
import os, re, shutil, subprocess, sys

ROOT = "/home/z/my-project"
ASP = os.path.join(ROOT, "aspnet")
SRC = os.path.join(ASP, "src")
MOD = os.path.join(SRC, "modules")
BK = os.path.join(ROOT, "backups")

def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()

def write(p, s):
    with open(p, "w", encoding="utf-8") as f:
        f.write(s)

def git(*args):
    return subprocess.run(["git", "-C", ROOT, *args], capture_output=True, text=True)

# ---------------------------------------------------------------
# ۰) نجات DB زنده (دادهٔ واقعی کاربر در Platform.Web/hr.db است)
# ---------------------------------------------------------------
os.makedirs(BK, exist_ok=True)
live_db = os.path.join(SRC, "Platform.Web", "hr.db")
stale_db = os.path.join(SRC, "HrAttendance.Web", "hr.db")
shutil.copy2(live_db, os.path.join(BK, "hr-LIVE-20261104.db"))
if os.path.exists(stale_db):
    shutil.copy2(stale_db, os.path.join(BK, "hr-STALE-HrAttendanceWeb-20261104.db"))
shutil.copy2(os.path.join(SRC, "Platform.Web", "app.db"),
             os.path.join(BK, "app-OLD-PlatformWeb-20261104.db"))
print("[0] DB زنده بکاپ شد → backups/")

# ---------------------------------------------------------------
# ۰-ب) بکاپ کامل پوشه‌های کهنهٔ Platform.* (بدون bin/obj/wwwroot) سپس حذف
# ---------------------------------------------------------------
STALE_DIRS = [
    "aspnet/src/Platform.Web",
    "aspnet/src/Platform.Core",
    "aspnet/src/Platform.Contracts",
    "aspnet/src/modules/Platform.Modules.Parking",
    "aspnet/src/modules/Platform.Modules.Visitors",
]
for rel in STALE_DIRS:
    p = os.path.join(ROOT, rel)
    if not os.path.isdir(p):
        continue
    bdir = os.path.join(BK, "old-Platform-src-20261104", rel.replace("aspnet/", ""))
    for dirpath, dirnames, filenames in os.walk(p):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "logs", "keys", "dl", "App_Data")]
        for fn in filenames:
            srcf = os.path.join(dirpath, fn)
            dstf = os.path.join(bdir, os.path.relpath(srcf, p))
            os.makedirs(os.path.dirname(dstf), exist_ok=True)
            shutil.copy2(srcf, dstf)
    shutil.rmtree(p)
    print(f"[0-b] بکاپ+حذف کهنه: {rel}")

# ---------------------------------------------------------------
# ۱) تغییر نام پوشه‌ها (git mv برای trackedها)
# ---------------------------------------------------------------
RENAMES = [
    ("src/HrAttendance.Web",       "src/Platform.Web"),
    ("src/HrPlatform.Core",        "src/Platform.Core"),
    ("src/HrPlatform.Contracts",   "src/Platform.Contracts"),
    ("src/modules/HrModules.Parking",  "src/modules/Platform.Modules.Parking"),
    ("src/modules/HrModules.Visitors", "src/modules/Platform.Modules.Visitors"),
]
for old_rel, new_rel in RENAMES:
    old, new = os.path.join(ASP, old_rel), os.path.join(ASP, new_rel)
    if os.path.isdir(old):
        # مقصد نباید وجود داشته باشد
        if os.path.isdir(new):
            raise SystemExit(f"!! مقصد موجود است: {new_rel} — ابتدا کهنه‌ها را پاک کنید")
        r = git("mv", old_rel.replace("/", os.sep), new_rel.replace("/", os.sep))
        if r.returncode != 0:
            os.rename(old, new)  # مسیر untracked → mv ساده
        print(f"[1] dir  {old_rel} -> {new_rel}")
    elif os.path.isdir(new):
        print(f"[1] dir  {old_rel} (از قبل renamed)")
    else:
        raise SystemExit(f"!! مبدأ نیست: {old_rel}")

# csprojها
CSPROJ_RENAMES = [
    ("src/Platform.Web/HrAttendance.Web.csproj",                    "src/Platform.Web/Platform.Web.csproj"),
    ("src/Platform.Core/HrPlatform.Core.csproj",                    "src/Platform.Core/Platform.Core.csproj"),
    ("src/Platform.Contracts/HrPlatform.Contracts.csproj",         "src/Platform.Contracts/Platform.Contracts.csproj"),
    ("src/modules/Platform.Modules.Parking/HrModules.Parking.csproj", "src/modules/Platform.Modules.Parking/Platform.Modules.Parking.csproj"),
    ("src/modules/Platform.Modules.Parking/Data/HrModules.Parking.Data.csproj", "src/modules/Platform.Modules.Parking/Data/Platform.Modules.Parking.Data.csproj"),
    ("src/modules/Platform.Modules.Visitors/HrModules.Visitors.csproj", "src/modules/Platform.Modules.Visitors/Platform.Modules.Visitors.csproj"),
    ("src/modules/Platform.Modules.Visitors/Data/HrModules.Visitors.Data.csproj", "src/modules/Platform.Modules.Visitors/Data/Platform.Modules.Visitors.Data.csproj"),
]
for old_rel, new_rel in CSPROJ_RENAMES:
    old, new = os.path.join(ASP, old_rel), os.path.join(ASP, new_rel)
    if os.path.exists(old):
        r = git("mv", old_rel.replace("/", os.sep), new_rel.replace("/", os.sep))
        if r.returncode != 0:
            os.rename(old, new)
        print(f"[1] csproj {os.path.basename(old_rel)} -> {os.path.basename(new_rel)}")

# ---------------------------------------------------------------
# ۲) بازنویسی متن در کل aspnet/src + ابزارها + sln
# ---------------------------------------------------------------
DP_TOKEN = "§§DP_APP_NAME§§"
TOKENS = [
    # DP محافظت‌شده — موقتاً ماسک می‌شود
    (f'SetApplicationName("HrAttendance.Web")', f'SetApplicationName({DP_TOKEN})'),
]
REPLACES = [
    ("HrModules.Parking.Data",   "Platform.Modules.Parking.Data"),
    ("HrModules.Parking",        "Platform.Modules.Parking"),
    ("HrModules.Visitors.Data",  "Platform.Modules.Visitors.Data"),
    ("HrModules.Visitors",       "Platform.Modules.Visitors"),
    ("HrPlatform.Core",          "Platform.Core"),
    ("HrPlatform.Contracts",     "Platform.Contracts"),
    ("HrAttendance.Web",         "Platform.Web"),
]
UNMASK = [(f'SetApplicationName({DP_TOKEN})', 'SetApplicationName("HrAttendance.Web")')]

def iter_files():
    for base in (SRC, os.path.join(ASP, "tools", "Platform.Migrate")):
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "logs", "App_Data", "keys", "dl")]
            for fn in filenames:
                if fn.rsplit(".", 1)[-1].lower() in ("cs", "cshtml", "csproj", "json", "props", "targets", "config"):
                    yield os.path.join(dirpath, fn)
    yield os.path.join(ASP, "Platform.sln")

changed_files = 0
for path in iter_files():
    try:
        s = read(path)
    except (UnicodeDecodeError, OSError):
        continue
    orig = s
    for old, new in TOKENS:
        s = s.replace(old, new)
    for old, new in REPLACES:
        s = s.replace(old, new)
    for old, new in UNMASK:
        s = s.replace(old, new)
    if s != orig:
        write(path, s)
        changed_files += 1
print(f"[2] بازنویسی متن: {changed_files} فایل تغییر کرد")

# ---------------------------------------------------------------
# ۳) ویلدکارد پلاگین‌ها: Platform.Modules.*.dll هم کشف/کپی شود
# ---------------------------------------------------------------
def patch(path, pairs, required=False):
    p = os.path.join(ASP, path)
    if not os.path.exists(p):
        if required: raise SystemExit(f"!! فایل نیست: {path}")
        return
    s = read(p); orig = s
    for old, new in pairs:
        if old in s and new not in s:
            s = s.replace(old, new)
    if s != orig:
        write(p, s); print(f"[3] patch {path}")

# کشف پلاگین در رانتایم (GatewayContracts) — الگوی دوم
patch("src/Platform.Contracts/GatewayContracts.cs", [
    ('Directory.GetFiles(dir, "HrModules.*.dll")',
     'Directory.GetFiles(dir, "HrModules.*.dll").Concat(Directory.Exists(dir) ? Directory.GetFiles(dir, "Platform.Modules.*.dll") : Array.Empty<string>())'),
])
# کپی پلاگین‌ها در csproj هاست — الگوی دوم (دو تارگت)
patch("src/Platform.Web/Platform.Web.csproj", [
    ('<_HrPluginDlls Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />',
     '<_HrPluginDlls Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />\n    <_HrPluginDlls Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\Platform.Modules.*.dll" />'),
    ('<_HrPluginDllsPub Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />',
     '<_HrPluginDllsPub Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />\n    <_HrPluginDllsPub Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\Platform.Modules.*.dll" />'),
])

# ---------------------------------------------------------------
# ۴) .gitignore — مسیرهای HrAttendance.Web → Platform.Web
# ---------------------------------------------------------------
gi = os.path.join(ROOT, ".gitignore")
s = read(gi)
s2 = s.replace("aspnet/src/HrAttendance.Web", "aspnet/src/Platform.Web")
if s2 != s:
    write(gi, s2); print("[4] .gitignore به‌روز شد")

# ---------------------------------------------------------------
# ۵) start-web.sh — مسیر و نام DLL جدید
# ---------------------------------------------------------------
sw = os.path.join(ROOT, "scripts", "start-web.sh")
s = read(sw)
s2 = s.replace("aspnet/src/HrAttendance.Web", "aspnet/src/Platform.Web") \
      .replace("HrAttendance.Web.dll", "Platform.Web.dll") \
      .replace("launching HrAttendance.Web", "launching Platform.Web")
if s2 != s:
    write(sw, s2); print("[5] start-web.sh به‌روز شد")

# ---------------------------------------------------------------
# ۶) حذف کهنه‌ها: پوشه‌های Platform.Web/Core/Contracts/Modules.* قدیمی
#    (قبلش DB زنده نجات پیدا کرد) + HrMigrate + tools/Hr* + پروژه JS
# ---------------------------------------------------------------
def rmgit(rel):
    p = os.path.join(ROOT, rel)
    if not os.path.exists(p) and not os.path.islink(p):
        return False
    r = git("rm", "-r", "-q", "--cached", rel) if os.path.isdir(p) else git("rm", "-q", "--cached", rel)
    if os.path.isdir(p):
        shutil.rmtree(p, ignore_errors=True)
    else:
        os.remove(p)
    print(f"[6] حذف شد: {rel}")
    return True

# ۶-الف) DB زنده را داخل Platform.Web جدید بریز (جایگزین hr.db کهنه)
new_dir = os.path.join(SRC, "Platform.Web")
shutil.copy2(live_db, os.path.join(new_dir, "hr.db"))
for ext in ("-wal", "-shm"):
    stale = os.path.join(new_dir, f"hr.db{ext}")
    if os.path.exists(stale):
        os.remove(stale)
print("[6] DB زنده → src/Platform.Web/hr.db")

for rel in [
    "aspnet/src/Platform.Core.old",     # اگر جایی مانده
    "aspnet/tools/HrMigrate",
    "tools/HrLicenseStudio",
    "tools/HrTranslationStudio",
    "src/app", "src/components", "src/hooks", "src/lib", "src/middleware.ts",
]:
    rmgit(rel)

# HrAttendance.sln (untracked) پاک شود؛ Platform.sln کانونی می‌ماند
hs = os.path.join(ASP, "HrAttendance.sln")
if os.path.exists(hs):
    os.remove(hs); print("[6] حذف HrAttendance.sln (Platform.sln کانونی است)")

print("DONE")
