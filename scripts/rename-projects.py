# -*- coding: utf-8 -*-
# ======================================================================
# یکپارچه‌سازی ساختار aspnet — تغییر نام فیزیکی پروژه‌ها به قالب نهایی
#   Platform.Web      -> Platform.Web
#   Platform.Core       -> Platform.Core
#   Platform.Contracts  -> Platform.Contracts
#   Platform.Modules.Parking     -> Platform.Modules.Parking  (+ .Data)
#   Platform.Modules.Visitors    -> Platform.Modules.Visitors (+ .Data)
#   tools/Platform.Migrate       -> tools/Platform.Migrate
# قواعد طلایی (بدون شکستن هیچ امکاناتی):
#   • فضانام‌ها (namespace) عمداً تغییر نمی‌کنند — قاعدهٔ موجود سورس
#   • SetApplicationName("Platform.Web") و Purpose="HrAttendance.SetupSettings"
#     دست‌نخورده می‌مانند تا keyring DataProtection و hrsettings.bin سالم بمانند
#   • کشف پلاگین (GatewayContracts) + wildcard کپی DLL هاست + اسکریپت بستهٔ مشتری
#     پیشوند جدید Platform.Modules.* را هم می‌پذیرند
# ======================================================================
import os, re, shutil, glob, subprocess, sys

ROOT = "/home/z/my-project"
ASP = os.path.join(ROOT, "aspnet")

def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()

def write(p, s):
    with open(p, "w", encoding="utf-8") as f:
        f.write(s)

def replace_exact(path, pairs):
    s = read(path)
    changed = 0
    for old, new in pairs:
        n = s.count(old)
        if n == 0:
            print(f"  (از قبل اعمال شده — رد شد): {os.path.relpath(path, ROOT)}")
            continue
        s = s.replace(old, new)
        changed += n
    if changed:
        write(path, s)
    print(f"  ویرایش {os.path.relpath(path, ROOT)} — {changed} جایگزینی")
    return changed

# ---------- ۱) تغییر نام پوشه‌ها ----------
DIR_RENAMES = [
    ("src/Platform.Web",     "src/Platform.Web"),
    ("src/Platform.Core",      "src/Platform.Core"),
    ("src/Platform.Contracts", "src/Platform.Contracts"),
    ("src/modules/Platform.Modules.Parking",  "src/modules/Platform.Modules.Parking"),
    ("src/modules/Platform.Modules.Visitors", "src/modules/Platform.Modules.Visitors"),
    ("tools/Platform.Migrate",          "tools/Platform.Migrate"),
]
for old_rel, new_rel in DIR_RENAMES:
    old, new = os.path.join(ASP, old_rel), os.path.join(ASP, new_rel)
    if not os.path.isdir(old):
        if os.path.isdir(new):
            print(f"rename dir  {old_rel}  ->  {new_rel}   (از قبل انجام شده)")
            continue
        raise SystemExit(f"!! پوشهٔ مبدأ نیست: {old}")
    if os.path.exists(new):
        raise SystemExit(f"!! مقصد از قبل هست: {new}")
    os.rename(old, new)
    print(f"rename dir  {old_rel}  ->  {new_rel}")

# ---------- ۲) تغییر نام فایل‌های csproj ----------
CSPROJ_RENAMES = [
    ("src/Platform.Web/Platform.Web.csproj",                       "Platform.Web.csproj"),
    ("src/Platform.Core/Platform.Core.csproj",                       "Platform.Core.csproj"),
    ("src/Platform.Contracts/Platform.Contracts.csproj",            "Platform.Contracts.csproj"),
    ("src/modules/Platform.Modules.Parking/Platform.Modules.Parking.csproj",  "Platform.Modules.Parking.csproj"),
    ("src/modules/Platform.Modules.Parking/Data/Platform.Modules.Parking.Data.csproj", "Platform.Modules.Parking.Data.csproj"),
    ("src/modules/Platform.Modules.Visitors/Platform.Modules.Visitors.csproj","Platform.Modules.Visitors.csproj"),
    ("src/modules/Platform.Modules.Visitors/Data/Platform.Modules.Visitors.Data.csproj", "Platform.Modules.Visitors.Data.csproj"),
    ("tools/Platform.Migrate/Platform.Migrate.csproj",                        "Platform.Migrate.csproj"),
]
for old_rel, new_name in CSPROJ_RENAMES:
    old = os.path.join(ASP, old_rel)
    new = os.path.join(os.path.dirname(old), new_name)
    if not os.path.isfile(old):
        if os.path.isfile(new):
            print(f"rename csproj  {old_rel}  ->  {new_name}   (از قبل انجام شده)")
            continue
        raise SystemExit(f"!! csproj مبدأ نیست: {old}")
    os.rename(old, new)
    print(f"rename csproj  {old_rel}  ->  {new_name}")

# ---------- ۳) مسیرهای ProjectReference در همهٔ csprojها (فقط داخل Include ارجاع) ----------
REF = re.compile(r'(ProjectReference\s+Include=")([^"]+)(")')
def fix_ref(m):
    v = m.group(2)
    v = v.replace("Platform.Web", "Platform.Web")
    v = v.replace("Platform.", "Platform.")
    v = v.replace("Platform.Modules.Parking", "Platform.Modules.Parking")
    v = v.replace("Platform.Modules.Visitors", "Platform.Modules.Visitors")
    return m.group(1) + v + m.group(3)

for path in sorted(glob.glob(os.path.join(ASP, "src", "**", "*.csproj"), recursive=True)) + \
            sorted(glob.glob(os.path.join(ASP, "tools", "**", "*.csproj"), recursive=True)):
    s = read(path)
    s2 = REF.sub(fix_ref, s)
    if s2 != s:
        write(path, s2)
        print(f"  refs fixed: {os.path.relpath(path, ROOT)}")

# ---------- ۴) wildcard کپی پلاگین‌ها در csproj هاست ----------
HOST = os.path.join(ASP, "src", "Platform.Web", "Platform.Web.csproj")
replace_exact(HOST, [
    (
        '<_HrPluginDlls Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />',
        '<_HrPluginDlls Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />\n'
        '      <_HrPluginDlls Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\Platform.Modules.*.dll" />'
    ),
    (
        '<_HrPluginDllsPub Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />',
        '<_HrPluginDllsPub Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\HrModules.*.dll" />\n'
        '      <_HrPluginDllsPub Include="..\\modules\\**\\bin\\$(Configuration)\\net8.0\\Platform.Modules.*.dll" />'
    ),
])

# ---------- ۵) Platform.sln — نام/مسیر پروژه‌ها ----------
replace_exact(os.path.join(ASP, "Platform.sln"), [
    ("Platform.Web",     "Platform.Web"),
    ("Platform.Core",      "Platform.Core"),
    ("Platform.Contracts", "Platform.Contracts"),
    ("Platform.Modules.Parking",    "Platform.Modules.Parking"),
    ("Platform.Modules.Visitors",   "Platform.Modules.Visitors"),
])

# ---------- ۶) کشف پلاگین در رانتایم — GatewayContracts.cs ----------
GC = os.path.join(ASP, "src", "Platform.Contracts", "GatewayContracts.cs")
replace_exact(GC, [
    (
        '/// فقط اسمبلی‌های «HrModules.*» از پوشه حل می‌شوند (وابستگی‌های بین‌ماژولی X → X.Data)؛',
        '/// فقط اسمبلی‌های «HrModules.*» و «Platform.Modules.*» از پوشه حل می‌شوند (وابستگی‌های بین‌ماژولی X → X.Data)؛'
    ),
    (
        '        if (an.Name is null || !an.Name.StartsWith("HrModules.", StringComparison.Ordinal)) return null;',
        '        // «HrModules.*» نسل اول و «Platform.Modules.*» نام‌گذاری کنونی — هر دو پلاگین‌اند\n'
        '        if (an.Name is null ||\n'
        '            !(an.Name.StartsWith("HrModules.", StringComparison.Ordinal) ||\n'
        '              an.Name.StartsWith("Platform.Modules.", StringComparison.Ordinal))) return null;'
    ),
    (
        '            try { files = Directory.GetFiles(dir, "HrModules.*.dll"); }',
        '            try\n'
        '            {\n'
        '                files = Directory.GetFiles(dir, "HrModules.*.dll")\n'
        '                    .Concat(Directory.GetFiles(dir, "Platform.Modules.*.dll"))\n'
        '                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();\n'
        '            }'
    ),
])

# ---------- ۷) start-web.sh — مسیر و نام DLL هاست ----------
replace_exact(os.path.join(ROOT, "scripts", "start-web.sh"), [
    ("Platform.Web", "Platform.Web"),
])

# ---------- ۸) build-customer.sh — مسیر publish + الگوی DLL هر دو پیشوند ----------
replace_exact(os.path.join(ROOT, "scripts", "build-customer.sh"), [
    (
        'ARGS=(publish "$ROOT/src/Platform.Web/Platform.Web.csproj" -c Release -o "$ROOT/publish/customer" -p:WarningLevel=0)',
        'ARGS=(publish "$ROOT/src/Platform.Web/Platform.Web.csproj" -c Release -o "$ROOT/publish/customer" -p:WarningLevel=0)'
    ),
    (
        '      *)       Cap="$(tr \'[:lower:]\' \'[:upper:]\' <<< "${m:0:1}")${m:1}"; Pat="HrModules.$Cap*.dll" ;;',
        '      *)       Cap="$(tr \'[:lower:]\' \'[:upper:]\' <<< "${m:0:1}")${m:1}"; Pat="HrModules.$Cap*.dll Platform.Modules.$Cap*.dll" ;;'
    ),
    (
        '  ls "$ROOT/publish/customer"/HrModules.*.dll 2>/dev/null | xargs -n1 basename || true\n'
        '  N=$(ls "$ROOT/publish/customer"/HrModules.*.dll 2>/dev/null | wc -l)',
        '  ls "$ROOT/publish/customer"/HrModules.*.dll "$ROOT/publish/customer"/Platform.Modules.*.dll 2>/dev/null | xargs -n1 basename || true\n'
        '  N=$( { ls "$ROOT/publish/customer"/HrModules.*.dll "$ROOT/publish/customer"/Platform.Modules.*.dll 2>/dev/null || true; } | wc -l)'
    ),
])

# ---------- ۹) Dockerfile + README ----------
replace_exact(os.path.join(ASP, "Dockerfile"), [("Platform.Web", "Platform.Web")])
replace_exact(os.path.join(ASP, "README-FA.md"), [
    ("Platform.Web",     "Platform.Web"),
    ("Platform.Core",      "Platform.Core"),
    ("Platform.Contracts", "Platform.Contracts"),
    ("Platform.Modules.Parking",    "Platform.Modules.Parking"),
    ("Platform.Modules.Visitors",   "Platform.Modules.Visitors"),
])

# ---------- ۱۰) اسکریپت‌های پشتیبانی (scripts/**) ----------
MAP = [
    ("Platform.Web",     "Platform.Web"),
    ("Platform.Contracts", "Platform.Contracts"),
    ("Platform.Core",      "Platform.Core"),
    ("Platform.Modules.Parking",    "Platform.Modules.Parking"),
    ("Platform.Modules.Visitors",   "Platform.Modules.Visitors"),
    ("Platform.Migrate",            "Platform.Migrate"),
    ("Platform",           "Platform"),  # بقایای بدون پسوند — آخر
]
n_files = 0
for ext in ("sh", "py", "cs"):
    for path in glob.glob(os.path.join(ROOT, "scripts", "**", f"*.{ext}"), recursive=True):
        s = read(path)
        s2 = s
        for old, new in MAP:
            s2 = s2.replace(old, new)
        if s2 != s:
            write(path, s2)
            n_files += 1
print(f"  scripts به‌روزشده: {n_files} فایل")

# ---------- ۱۱) حذف برنامهٔ JS (فقط مسیرهای داده‌شده) ----------
JS_TARGETS = ["src/app", "src/components", "src/hooks", "src/lib", "src/middleware.ts"]
for rel in JS_TARGETS:
    p = os.path.join(ROOT, rel)
    if os.path.isdir(p):
        shutil.rmtree(p)
        print(f"deleted dir   {rel}/")
    elif os.path.isfile(p):
        os.remove(p)
        print(f"deleted file  {rel}")
    else:
        print(f"!! غایب (رد شد): {rel}")

# ---------- ۱۲) پاک‌سازی bin/obj (DLLهای با نام قدیمی نباید کنار اپ بمانند) ----------
removed = 0
for dirpath, dirnames, _ in os.walk(ASP):
    for d in list(dirnames):
        if d in ("bin", "obj"):
            p = os.path.join(dirpath, d)
            shutil.rmtree(p, ignore_errors=True)
            dirnames.remove(d)
            removed += 1
print(f"  پوشه‌های bin/obj حذف‌شده: {removed}")

# ---------- ۱۲) راستی‌آزمایی تغییرات حساس ----------
print("\n== راستی‌آزمایی ==")
errors = []
def must_contain(path, needle, label=""):
    if needle not in read(path):
        errors.append(f"{os.path.relpath(path, ROOT)} فاقد: {needle} {label}")

def must_not_contain(path, needle):
    if needle in read(path):
        errors.append(f"{os.path.relpath(path, ROOT)} هنوز دارد: {needle}")

gc = os.path.join(ASP, "src", "Platform.Contracts", "GatewayContracts.cs")
must_contain(gc, 'an.Name.StartsWith("Platform.Modules.", StringComparison.Ordinal)')
must_contain(gc, 'Directory.GetFiles(dir, "Platform.Modules.*.dll")')
host = os.path.join(ASP, "src", "Platform.Web", "Platform.Web.csproj")
must_contain(host, 'Platform.Modules.*.dll" />')
sln = os.path.join(ASP, "Platform.sln")
must_contain(sln, 'src\\Platform.Web\\Platform.Web.csproj')
must_contain(sln, 'src\\modules\\Platform.Modules.Parking\\Platform.Modules.Parking.csproj')
must_not_contain(sln, "Platform.Web")
must_not_contain(sln, "Platform.Modules.Parking")
sw = os.path.join(ROOT, "scripts", "start-web.sh")
must_contain(sw, "Platform.Web.dll")
bc = os.path.join(ROOT, "scripts", "build-customer.sh")
must_contain(bc, "src/Platform.Web/Platform.Web.csproj")
df = os.path.join(ASP, "Dockerfile")
must_contain(df, '"dotnet", "Platform.Web.dll"')
# هیچ ProjectReference به نام‌های قدیمی نمانده باشد
for path in glob.glob(os.path.join(ASP, "**", "*.csproj"), recursive=True):
    for m in REF.finditer(read(path)):
        if re.search(r"HrAttendance\.Web|Platform\.|HrModules\.(Parking|Visitors)", m.group(2)):
            errors.append(f"{os.path.relpath(path, ROOT)} ارجاع قدیمی: {m.group(2)}")
# حافظهٔ رمزنگاری دست‌نخورده باشد
m1 = os.path.join(ASP, "src", "Platform.Web", "Program.cs")
must_contain(m1, '.SetApplicationName("Platform.Web")')
m2 = os.path.join(ASP, "src", "Platform.Contracts", "..", "Platform.Web", "Infrastructure", "SetupSecrets.cs")
must_contain(os.path.normpath(m2), 'o.SetApplicationName("Platform.Web")')
must_contain(os.path.normpath(m2), 'Purpose = "HrAttendance.SetupSettings"')
if errors:
    for e in errors:
        print("  ✗ " + e)
    raise SystemExit("!! راستی‌آزمایی ناموفق")
print("  ✓ همهٔ تغییرات حساس درست اعمال شد")

# ---------- ۱۳) گزارش نهایی ----------
print("\n== ساختار نهایی aspnet/src ==")
for base in ("aspnet/src", "aspnet/tools", "tools", "src"):
    p = os.path.join(ROOT, base)
    if os.path.isdir(p):
        entries = sorted(os.listdir(p))
        print(f"{base}/: " + ", ".join(entries))
print("\nOK")
