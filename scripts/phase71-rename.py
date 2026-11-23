#!/usr/bin/env python3
# ======================================================================
# فاز ۷۱ — تغییر نام هستهٔ پلتفرم به نام‌های خنثی (بدون HR/Attendance)
#   Platform.sln        → Platform.sln
#   Platform.Web        → Platform.Web   (هاست)
#   Platform.Core         → Platform.Core
#   Platform.Contracts    → Platform.Contracts
#   tools/Platform.Migrate         → tools/Platform.Migrate
# ماژول‌ها (HrModules.*) عمداً دست‌نخورده می‌مانند — پیشوند HR فقط برای ماژول‌ها.
# ======================================================================
import os, re, sys

ROOT = "/home/z/my-project"

# ---------- ۱) جابه‌جایی پوشه‌ها/فایل‌ها ----------
MOVES = [
    ("aspnet/Platform.sln",                      "aspnet/Platform.sln"),
    ("aspnet/src/Platform.Web",                  "aspnet/src/Platform.Web"),
    ("aspnet/src/Platform.Core",                   "aspnet/src/Platform.Core"),
    ("aspnet/src/Platform.Contracts",              "aspnet/src/Platform.Contracts"),
    ("aspnet/tools/Platform.Migrate",                       "aspnet/tools/Platform.Migrate"),
    # csprojها
    ("aspnet/src/Platform.Web/Platform.Web.csproj",   "aspnet/src/Platform.Web/Platform.Web.csproj"),
    ("aspnet/src/Platform.Core/Platform.Core.csproj",   "aspnet/src/Platform.Core/Platform.Core.csproj"),
    ("aspnet/src/Platform.Contracts/Platform.Contracts.csproj", "aspnet/src/Platform.Contracts/Platform.Contracts.csproj"),
    ("aspnet/tools/Platform.Migrate/Platform.Migrate.csproj",    "aspnet/tools/Platform.Migrate/Platform.Migrate.csproj"),
    # فایل‌های سورس با نام Hr
    ("aspnet/src/Platform.Core/Data/BaseDbContext.cs",  "aspnet/src/Platform.Core/Data/BaseDbContext.cs"),
    ("aspnet/src/Platform.Core/Data/ModelDefaults.cs",  "aspnet/src/Platform.Core/Data/ModelDefaults.cs"),
    ("aspnet/src/Platform.Core/Controllers/ModuleControllerBase.cs", "aspnet/src/Platform.Core/Controllers/ModuleControllerBase.cs"),
    ("aspnet/src/Platform.Web/Modules/BuildModules.cs", "aspnet/src/Platform.Web/Modules/BuildModules.cs"),
    ("aspnet/src/Platform.Contracts/HrContracts.cs",      "aspnet/src/Platform.Contracts/CoreContracts.cs"),
]
for src, dst in MOVES:
    s, d = os.path.join(ROOT, src), os.path.join(ROOT, dst)
    if os.path.exists(s):
        os.makedirs(os.path.dirname(d), exist_ok=True)
        os.rename(s, d)
        print(f"moved  {src}  →  {dst}")
    else:
        print(f"SKIP (missing) {src}")

# ---------- ۲) بازگزینی متن ----------
# ترتیب مهم: نام‌های بلندتر اول؛ «ModuleDef» تنها (کلاس هاست) با مرز کلمه و بعد از بقیه.
PLAIN = [
    ("IDataModule", "IDataModule"),
    ("IUiModule", "IUiModule"),
    ("IDashContributor", "IDashContributor"),
    ("IModuleBootstrapper", "IModuleBootstrapper"),
    ("ModuleRuntime", "ModuleRuntime"),
    ("ModulePresence", "ModulePresence"),
    ("ModuleFallbacks", "ModuleFallbacks"),
    ("ModuleControllerBase", "ModuleControllerBase"),
    ("PluginLoadContext", "PluginLoadContext"),
    ("BuildModules", "BuildModules"),
    ("ModNavItem", "ModNavItem"),
    ("ModNavGroup", "ModNavGroup"),
    ("SeedContext", "SeedContext"),
    ("DemoShiftKeys", "DemoShiftKeys"),
    ("DemoEmployee", "DemoEmployee"),
    ("DbResolver", "DbResolver"),
    ("BaseDbContext", "BaseDbContext"),
    ("ModelDefaults", "ModelDefaults"),
    ("DashKpi", "DashKpi"),
    ("DashChart", "DashChart"),
    ("DashTable", "DashTable"),
    ("DashPanel", "DashPanel"),
    ("DashContribution", "DashContribution"),
    ("DashPoint", "DashPoint"),
    ("AttDashDto", "AttDashDto"),
    ("AccessDashDto", "AccessDashDto"),
    ("AttExportRow", "AttExportRow"),
    ("AttImportRow", "AttImportRow"),
    ("SelfHomeDto", "SelfHomeDto"),
    ("SelfProfile", "SelfProfile"),
    ("PersonOption", "PersonOption"),
    ("DeptOption", "DeptOption"),
    ("PersonBrief", "PersonBrief"),
    ("PersonAuth", "PersonAuth"),
    ("PersonExportRow", "PersonExportRow"),
    ("ContractExpiryRow", "ContractExpiryRow"),
    ("DeviceStripItem", "DeviceStripItem"),
    ("DevicesDashDto", "DevicesDashDto"),
    ("DeviceStatusRow", "DeviceStatusRow"),
    ("VehicleHitDto", "VehicleHitDto"),
    ("ZoneCount", "ZoneCount"),
    ("SearchHit", "SearchHit"),
    # نام‌های پروژه/فضای نام/مسیر
    ("Platform.Web", "Platform.Web"),
    ("Platform.Contracts", "Platform.Contracts"),
    ("Platform.Core", "Platform.Core"),
    ("Platform.sln", "Platform.sln"),
    ("Platform.Migrate", "Platform.Migrate"),
]
REGEX = [
    # کلاس ModuleDef هاست (بعد از همهٔ ModuleDef*) — هرگز HrModules را نمی‌گیرد
    (re.compile(r"\bHrModule\b"), "ModuleDef"),
]

SKIP_DIRS = {"bin", "obj", "keys", "logs", "dl", "node_modules", ".git", "finalverify", ".git"}
SKIP_EXT = {".dll", ".exe", ".db", ".zip", ".png", ".jpg", ".woff", ".woff2", ".ttf",
            ".log", ".bin", ".pfx", ".so", ".pdf", ".lock", ".shm", ".wal"}

def targets():
    for top in ["aspnet", "scripts", "DEPLOY-FA.md"]:
        p = os.path.join(ROOT, top)
        if os.path.isfile(p):
            yield p
            continue
        for base, dirs, files in os.walk(p):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for f in files:
                ext = os.path.splitext(f)[1].lower()
                if ext in SKIP_EXT:
                    continue
                yield os.path.join(base, f)

changed = 0
scanned = 0
for path in targets():
    try:
        with open(path, "r", encoding="utf-8-sig") as fh:
            text = fh.read()
    except (UnicodeDecodeError, OSError):
        continue
    scanned += 1
    new = text
    for a, b in PLAIN:
        if a in new:
            new = new.replace(a, b)
    for rx, b in REGEX:
        new = rx.sub(b, new)
    if new != text:
        # BOM فایل sln حفظ شود
        bom = "\ufeff" if text.startswith("\ufeff") else ""
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(bom + new)
        changed += 1

print(f"scanned={scanned} changed={changed}")

# ---------- ۳) گزارش باقیمانده‌ها (باید فقط HrModules.* باشد) ----------
left = {}
rx = re.compile(r"\bHr\w*")
for path in targets():
    try:
        with open(path, "r", encoding="utf-8-sig") as fh:
            for i, line in enumerate(fh, 1):
                for m in rx.findall(line):
                    if m == "HrModules" or m.startswith("HrModules."):
                        continue
                    left.setdefault(m, []).append(f"{os.path.relpath(path, ROOT)}:{i}")
    except (UnicodeDecodeError, OSError):
        continue
print("\n---- leftover Hr* tokens (excluding HrModules) ----")
for k, v in sorted(left.items(), key=lambda x: -len(x[1])):
    print(f"{k}: {len(v)}  e.g. {v[:3]}")
print("---------------------------------------------------")
