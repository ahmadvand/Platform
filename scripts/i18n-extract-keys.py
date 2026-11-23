#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Phase 81 Task F — extract ALL runtime translation keys, diff against DictEn."""
import re, os, json

ROOT = "/home/z/my-project/aspnet/src"
FA = re.compile(r'[\u0600-\u06FF]')

keys = set()

def add(k):
    k = k.strip()
    if FA.search(k) and len(k) < 400:
        keys.add(k)

# ---------- 1) HrT.T / HrT.F literals in .cs ----------
for root, ds, fs in os.walk(ROOT):
    if '/wwwroot' in root or '/dl/' in root or '/bin/' in root or '/obj/' in root: continue
    for fn in fs:
        if not fn.endswith('.cs'): continue
        p = os.path.join(root, fn)
        rel = os.path.relpath(p, ROOT)
        if rel.replace('\\','/') == 'HrPlatform.Core/Services/HrLang.cs': continue
        src = open(p, encoding='utf-8').read()
        for m in re.finditer(r'\bHrT\.[TF]\(\s*"((?:[^"\\]|\\.)*)"', src):
            add(m.group(1).replace('\\"', '"'))

# ---------- 2) cshtml literals in HrT.T / HrT.F / HrT('...') ----------
for root, ds, fs in os.walk(ROOT):
    if '/wwwroot' in root or '/dl/' in root or '/bin/' in root or '/obj/' in root: continue
    for fn in fs:
        if not fn.endswith('.cshtml'): continue
        src = open(os.path.join(root, fn), encoding='utf-8').read()
        for m in re.finditer(r'\bHrT\.[TF]\(\s*"((?:[^"\\]|\\.)*)"', src):
            add(m.group(1).replace('\\"', '"'))
        for m in re.finditer(r"HrT\(\s*'([^']+)'", src):
            add(m.group(1))

# ---------- 3) gateway-passed literals (Toast/ToastDone/AjaxResult args) ----------
for root, ds, fs in os.walk(ROOT):
    if '/wwwroot' in root or '/dl/' in root or '/bin/' in root or '/obj/' in root: continue
    for fn in fs:
        if not fn.endswith('.cs') or 'Controller' not in fn: continue
        src = open(os.path.join(root, fn), encoding='utf-8').read()
        for m in re.finditer(r'(?:ToastOk|ToastErr|ToastDone|AjaxResult)\([^;]{0,400}?"((?:[^"\\]|\\.)*)"', src):
            add(m.group(1))

# ---------- 4) canonical DB values + manifest/seeder display data ----------
def file_strings(rel, only_arrays=False):
    p = os.path.join(ROOT, rel)
    if not os.path.exists(p): return
    src = open(p, encoding='utf-8').read()
    # array contents: new[] { "a", "b" } / [ "a", "b" ]
    for m in re.finditer(r'(?:new\[\]\s*\{|new\s*string\[\]\s*\{|\[)\s*("((?:[^"\\]|\\.)*)"(?:\s*,\s*"((?:[^"\\]|\\.)*)")*)\s*[}\]]', src):
        for lit in re.finditer(r'"((?:[^"\\]|\\.)*)"', m.group(0)):
            add(lit.group(1))
    if not only_arrays:
        for m in re.finditer(r'=\s*"((?:[^"\\]|\\.)*)"', src):
            add(m.group(1))
        for m in re.finditer(r'"((?:[^"\\]|\\.)*)"', src):
            s = m.group(1)
            if FA.search(s): add(s)

# manifests (menu labels, groups, module titles, permission titles)
for rel in ["HrAttendance.Web/Modules/CoreModule.cs",
            "modules/HrModules.Org/OrgDataModule.cs",
            "modules/HrModules.Access/Data/AccessDataModule.cs",
            "modules/HrModules.Attendance/Data/AttendanceDataModule.cs",
            "modules/HrModules.Devices/DevicesDataModule.cs",
            "modules/HrModules.Payroll/PayrollUiModule.cs",
            "modules/HrModules.Attendance/AttendanceUiModule.cs",
            "modules/HrModules.Parking/ParkingDataModule.cs",
            "modules/HrModules.Visitors/VisitorsDataModule.cs"]:
    p = os.path.join(ROOT, rel)
    if os.path.exists(p):
        src = open(p, encoding='utf-8').read()
        for m in re.finditer(r'new HrNavItem\([^)]*?"((?:[^"\\]|\\.)*)"', src): add(m.group(1))
        for m in re.finditer(r'new\("((?:[^"\\]|\\.)*)"', src): add(m.group(1))
        for m in re.finditer(r'Title\s*=>\s*"((?:[^"\\]|\\.)*)"', src): add(m.group(1))
        for m in re.finditer(r'Label\s*=\s*"((?:[^"\\]|\\.)*)"', src): add(m.group(1))

# seeders (permission titles, pay item titles, shift names, etc.)
for root, ds, fs in os.walk(ROOT):
    if '/wwwroot' in root or '/dl/' in root or '/bin/' in root or '/obj/' in root: continue
    for fn in fs:
        if 'Seeder' not in fn or not fn.endswith('.cs'): continue
        file_strings(os.path.relpath(os.path.join(root, fn), ROOT))

# canonical values from engine/domain files
for rel in ["modules/HrModules.Payroll/Controllers/PayrollController.cs",
            "modules/HrModules.Attendance/Controllers/RequestsController.cs",
            "modules/HrModules.Access/Controllers/CardsController.cs",
            "modules/HrModules.Attendance/Controllers/ShiftsController.cs",
            "HrPlatform.Core/Services/DeviceOps.cs",
            "modules/HrModules.Parking/Services/ParkingOps.cs",
            "HrPlatform.Core/Services/Notifications.cs",
            "HrPlatform.Core/Services/BioSyncCore.cs",
            "modules/HrModules.Payroll/Data/PayrollEngine.cs",
            "modules/HrModules.Org/Controllers/EmployeesController.cs",
            "modules/HrModules.Visitors/Controllers/VisitorsController.cs",
            "modules/HrModules.Parking/Controllers/ParkingController.cs",
            "modules/HrModules.Devices/Controllers/DevicesController.cs"]:
    file_strings(rel)

# ---------- diff ----------
d = json.load(open("/home/z/my-project/tools/TranslationStudio/Resources/ref-en.json", encoding="utf-8"))
existing = set(d.keys())
missing = sorted(keys - existing)
print("extracted:", len(keys), " existing:", len(existing), " MISSING:", len(missing))
json.dump(missing, open("/home/z/my-project/scripts/missing-keys.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
for k in missing[:80]: print("  ", k[:100])
