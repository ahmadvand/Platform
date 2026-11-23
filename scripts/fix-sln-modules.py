#!/usr/bin/env python3
# ======================================================================
# فاز ۶۶ — ترمیم sln برای بستهٔ مشتری:
# ۱) افزودن ۷ پروژهٔ جاافتاده به Platform.sln:
#    HrPlatform.Contracts + پنج HrModules.*.Data + HrModules.DeviceGateway
#    (مشتری در Visual Studio باید کل معماری ماژولار را ببیند)
# ۲) NestedProjects: Contracts زیر پوشهٔ "src"، بقیه زیر "modules"
# ======================================================================
import uuid, re, io, os

SLN = "/home/z/my-project/aspnet/Platform.sln"

CS_PROJ = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"   # C# project type guid
FOLDER  = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}"   # solution folder guid

SRC_FOLDER  = "{6C698A5D-064A-4D1B-8CB4-5C6D2F0F438A}"  # "src"
MOD_FOLDER  = "{A73A20B8-A8CA-4C23-9275-CF075AADB5DF}"  # "modules"

# (name, relpath-from-aspnet, parent-solution-folder)
NEW_PROJECTS = [
    ("HrPlatform.Contracts",        "src\\HrPlatform.Contracts\\HrPlatform.Contracts.csproj",          SRC_FOLDER),
    ("HrModules.DeviceGateway",     "src\\modules\\HrModules.DeviceGateway\\HrModules.DeviceGateway.csproj", MOD_FOLDER),
    ("HrModules.Attendance.Data",   "src\\modules\\HrModules.Attendance\\Data\\HrModules.Attendance.Data.csproj", MOD_FOLDER),
    ("HrModules.Access.Data",       "src\\modules\\HrModules.Access\\Data\\HrModules.Access.Data.csproj",       MOD_FOLDER),
    ("HrModules.Parking.Data",      "src\\modules\\HrModules.Parking\\Data\\HrModules.Parking.Data.csproj",     MOD_FOLDER),
    ("HrModules.Payroll.Data",      "src\\modules\\HrModules.Payroll\\Data\\HrModules.Payroll.Data.csproj",     MOD_FOLDER),
    ("HrModules.Visitors.Data",     "src\\modules\\HrModules.Visitors\\Data\\HrModules.Visitors.Data.csproj",   MOD_FOLDER),
]

def fmt_guid(g):
    return g.upper()

with io.open(SLN, "r", encoding="utf-8-sig") as f:
    text = f.read()

# --- sanity: don't double-add ---
for name, rel, _ in NEW_PROJECTS:
    if rel.replace("\\\\", "\\") in text:
        print("skip (already present):", name)
        NEW_PROJECTS = [p for p in NEW_PROJECTS if p[0] != name]

if not NEW_PROJECTS:
    print("nothing to add")
    raise SystemExit(0)

proj_blocks, cfg_lines, nest_lines = [], [], []
for name, rel, parent in NEW_PROJECTS:
    g = fmt_guid(str(uuid.uuid4()))
    proj_blocks.append(
        f'Project("{CS_PROJ}") = "{name}", "{rel}", "{{{g}}}"\nEndProject\n'
    )
    for cfg in ("Debug", "Release"):
        cfg_lines.append(f"\t\t{{{g}}}.{cfg}|Any CPU.ActiveCfg = {cfg}|Any CPU")
        cfg_lines.append(f"\t\t{{{g}}}.{cfg}|Any CPU.Build.0 = {cfg}|Any CPU")
    nest_lines.append(f"\t\t{{{g}}} = {parent}")

# --- insert Project blocks right before 'Global' ---
text = text.replace("Global\r\n", "".join(proj_blocks) + "Global\r\n", 1) \
    if "Global\r\n" in text else text.replace("Global\n", "".join(proj_blocks) + "Global\n", 1)

# --- insert config lines before EndGlobalSection of ProjectConfigurationPlatforms ---
m = re.search(r"(GlobalSection\(ProjectConfigurationPlatforms\)[^\n]*\n(?:.*?\n)*?)\s*EndGlobalSection", text)
assert m, "ProjectConfigurationPlatforms section not found"
cfg_block = m.group(1)
eol = "\r\n" if "\r\n" in cfg_block else "\n"
new_cfg = cfg_block.rstrip("\r\n") + eol + eol.join(cfg_lines) + eol
text = text.replace(cfg_block, new_cfg, 1)

# --- insert nesting lines before EndGlobalSection of NestedProjects ---
m = re.search(r"(GlobalSection\(NestedProjects\)[^\n]*\n(?:.*?\n)*?)\s*EndGlobalSection", text)
assert m, "NestedProjects section not found"
nest_block = m.group(1)
new_nest = nest_block.rstrip("\r\n") + eol + eol.join(nest_lines) + eol
text = text.replace(nest_block, new_nest, 1)

with io.open(SLN, "w", encoding="utf-8-sig", newline="") as f:
    f.write(text)

print("added:", len(NEW_PROJECTS), "projects")
for name, _, _ in NEW_PROJECTS:
    print("  +", name)
