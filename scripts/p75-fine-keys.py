#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
فاز ۷۵ — تعویض [HasPerm] اکشن‌های نوشتاری به کلیدهای ریز.
الگو: هر اکشن → کلید ریز؛ والدِ درشت در RbacService.PermParents سازگاری نقش‌های
موجود را حفظ می‌کند (صفر مهاجرت). فقط بلاک صفتِ اکشن‌های مپ‌شده دستکاری می‌شود.
"""
import re, sys, pathlib

ROOT = pathlib.Path("/home/z/my-project/aspnet/src")

# file → {action_name: new_key}  (GET+POST هم‌نام = هم‌کلید)
MAPPING = {
    "modules/HrModules.Parking/Controllers/ParkingController.cs": {
        "CreateLot": "parking.lots.manage", "CreateGate": "parking.lots.manage",
        "ToggleLot": "parking.lots.manage", "ToggleGate": "parking.lots.manage",
        "CreateVehicle": "parking.vehicles.manage", "CreatePermit": "parking.vehicles.manage",
        "ToggleVehicle": "parking.vehicles.manage", "TogglePermit": "parking.vehicles.manage",
        "CreateCamera": "parking.cameras.manage", "ToggleCamera": "parking.cameras.manage",
        "RotateCameraToken": "parking.cameras.manage",
        "SetGateOpenState": "parking.barrier", "OpenBarrier": "parking.barrier",
        "ScanForm": "parking.scan", "Scan": "parking.scan",
    },
    "modules/HrModules.Access/Controllers/AccessController.cs": {
        "CreateZone": "access.zones.manage", "ToggleAntiPassback": "access.zones.manage",
        "EmergencyOpen": "access.zones.manage", "RestoreNormal": "access.zones.manage",
        "CreateGate": "access.gates.manage", "GateScheduleForm": "access.gates.manage",
        "DoorStateForm": "access.gates.manage", "SetDoorState": "access.gates.manage",
        "CreatePerm": "access.perms.manage", "TogglePerm": "access.perms.manage",
        "ResolveAlert": "access.alerts.manage",
        "Scan": "access.scan",
    },
    "modules/HrModules.Access/Controllers/CardsController.cs": {
        "Create": "cards.issue", "Issue": "cards.issue", "Activate": "cards.issue", "Edit": "cards.issue",
        "Return": "cards.state", "MarkLost": "cards.state", "Void": "cards.state", "Delete": "cards.state",
    },
    "modules/HrModules.Visitors/Controllers/VisitorsController.cs": {
        "Create": "visitors.register", "CreateContractor": "visitors.register", "Decide": "visitors.register",
        "IssueCard": "visitors.flow", "Enter": "visitors.flow", "Exit": "visitors.flow",
    },
    "modules/HrModules.Org/Controllers/OrgController.cs": {
        "CreateDept": "org.depts.manage", "EditDeptForm": "org.depts.manage", "MoveDeptForm": "org.depts.manage",
        "EditDept": "org.depts.manage", "MoveDept": "org.depts.manage", "DeleteDept": "org.depts.manage",
        "CreatePos": "org.positions.manage", "EditPosForm": "org.positions.manage", "EditPos": "org.positions.manage",
    },
    "modules/HrModules.Devices/Controllers/DevicesController.cs": {
        "Create": "devices.register", "Edit": "devices.register",
        "ToggleActive": "devices.register", "RotateSecret": "devices.register",
        "TransferUser": "devices.transfer",
    },
    "modules/HrModules.Devices/Controllers/BiometricsController.cs": {
        "FetchUsers": "biometrics.fetch", "FetchBios": "biometrics.fetch",
        "DeleteDeviceUser": "biometrics.fetch",
        "PushToDevices": "biometrics.transfer", "TransferDeviceUsers": "biometrics.transfer",
    },
    "modules/HrModules.Payroll/Controllers/PayrollController.cs": {
        "Create": "payroll.runs.create", "Finalize": "payroll.runs.finalize", "Delete": "payroll.runs.delete",
    },
    "HrAttendance.Web/Modules/Core/Controllers/UsersController.cs": {
        "LinkEmployee": "users.edit", "SetPhoto": "users.edit", "Photo": "users.edit",
        "ToggleActive": "users.edit", "SetRoles": "users.edit",
        "ResetPassword": "users.reset", "Unlock": "users.reset", "RevokeSessions": "users.reset",
        "RoleForm": "roles.view", "RoleCreate": "roles.create",
        "RoleEdit": "roles.edit", "RoleDelete": "roles.delete", "SetRolePermissions": "roles.edit",
    },
    "HrAttendance.Web/Modules/Core/Controllers/CompaniesController.cs": {
        "Create": "companies.create",
        "EditForm": "companies.edit", "Edit": "companies.edit", "ToggleActive": "companies.edit",
        "Delete": "companies.delete",
    },
}

ACT_RE = re.compile(r'public\s+(?:async\s+Task<IActionResult>|IActionResult)\s+([A-Za-z_]\w*)\s*\(')
HASPERM_RE = re.compile(r'\[HasPerm\("([^"]+)"\)\]')

total = 0
for rel, actions in MAPPING.items():
    path = ROOT / rel
    lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
    changed = 0
    for i, line in enumerate(lines):
        m = ACT_RE.search(line)
        if not m or m.group(1) not in actions:
            continue
        key = actions[m.group(1)]
        # بازگشت روی بلاک صفت (حداکثر ۶ خط): [HttpPost]/[HttpGet]/[ValidateAntiForgeryToken]/[HasPerm]
        j = i - 1
        swapped = False
        while j >= 0 and j >= i - 6:
            t = lines[j].strip()
            if t.startswith("[HttpPost]") or t.startswith("[HttpGet]") or t.startswith("[ValidateAntiForgeryToken]"):
                j -= 1
                continue
            hm = HASPERM_RE.search(lines[j])
            if hm:
                if hm.group(1) != key:
                    lines[j] = lines[j].replace(f'[HasPerm("{hm.group(1)}")]', f'[HasPerm("{key}")]')
                    changed += 1
                swapped = True
            break
        if not swapped:
            print(f"  !! بدون HasPerm: {rel} :: {m.group(1)} (خط {i+1})", file=sys.stderr)
    path.write_text("".join(lines), encoding="utf-8")
    total += changed
    print(f"{rel}: {changed} تعویض")
print(f"مجموع: {total}")
