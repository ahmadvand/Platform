#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""فاز ۷۵ — سیم‌کشی [HasPerm] ریز روی کنترلرها.
- هر [HasPerm("k")] با نزدیک‌ترین اکشن public بعدی جفت می‌شود.
- فقط وقتی کلید فعلی «درشتِ» مورد انتظار است جایگزین می‌شود (skip اگر جریان موازی سیم‌کشی کرده).
- حالت --dry فقط گزارش می‌دهد.
"""
import re, sys, io
from pathlib import Path

SRC = Path("/home/z/my-project/aspnet/src")

ATTR = re.compile(r'\[HasPerm\("([^"]+)"\)\]')
ACT = re.compile(r'public\s+(?:async\s+)?Task<[^>]+>\s+(\w+)\s*\(|public\s+(?:virtual\s+)?(?:async\s+)?IActionResult\s+(\w+)\s*\(')

# نقشهٔ نهایی ادغام‌شده: فایل → نام اکشن → کلید ریز
M = {
 "modules/HrModules.Org/Controllers/EmployeesController.cs": {
    "Create": "employees.create", "Edit": "employees.edit", "Delete": "employees.delete",
    "Toggle": "employees.edit",
 },
 "modules/HrModules.Attendance/Controllers/AttendanceController.cs": {
    "AddRecord": "attendance.records.manage", "AddPunch": "attendance.records.manage",
    "Manual": "attendance.records.manage", "EditRecord": "attendance.records.manage",
    "DeleteRecord": "attendance.records.manage", "Punch": "attendance.records.manage",
 },
 "modules/HrModules.Attendance/Controllers/RequestsController.cs": {
    "CreateLeave": "attendance.requests.create", "CreateMission": "attendance.requests.create",
    "CreatePermit": "attendance.requests.create",
    "DecideLeave": "attendance.leaves.approve", "DecideMission": "attendance.missions.approve",
    "DecidePermit": "attendance.permits.approve",  # DecideCorrection دست‌نخورده
 },
 "modules/HrModules.Attendance/Controllers/ShiftsController.cs": {
    "Create": "shifts.create", "Edit": "shifts.edit", "Delete": "shifts.delete",
 },
 "modules/HrModules.Org/Controllers/HolidaysController.cs": {
    "Add": "holidays.create", "Delete": "holidays.delete",
 },
 "modules/HrModules.Org/Controllers/OrgController.cs": {
    "CreateDept": "org.depts.manage", "EditDeptForm": "org.depts.manage",
    "MoveDeptForm": "org.depts.manage", "EditDept": "org.depts.manage",
    "MoveDept": "org.depts.manage", "DeleteDept": "org.depts.manage",
    "CreatePos": "org.positions.manage", "EditPosForm": "org.positions.manage",
    "EditPos": "org.positions.manage", "DeletePos": "org.positions.manage",
 },
 "modules/HrModules.Access/Controllers/AccessController.cs": {
    "CreateZone": "access.zones.manage",
    "EmergencyOpen": "access.zones.manage", "RestoreNormal": "access.zones.manage",
    "ToggleAntiPassback": "access.zones.manage",
    "CreateGate": "access.gates.manage", "GateScheduleForm": "access.gates.manage",
    "DoorStateForm": "access.gates.manage", "SetDoorState": "access.gates.manage",
    "SetGateSchedule": "access.gates.manage", "ToggleTwoFactor": "access.gates.manage",
    "SetTwoFactorWindow": "access.gates.manage",
    "CreatePerm": "access.perms.manage", "TogglePerm": "access.perms.manage",
    "Scan": "access.scan",
    "CreateGroup": "access.groups.manage", "DeleteGroup": "access.groups.manage",
    "AddGroupMember": "access.groups.manage", "RemoveGroupMember": "access.groups.manage",
    "SetGroupGate": "access.groups.manage", "RemoveGroupGate": "access.groups.manage",
    "ResolveAlert": "access.alerts.manage", "ExpireAlert": "access.alerts.manage",
    "SaveAlertTtl": "access.alerts.manage", "SaveMap": "access.map.manage",
    # Muster/TrafficPdf → access.view دست‌نخورده
 },
 "modules/HrModules.Parking/Controllers/ParkingController.cs": {
    "CreateLot": "parking.lots.manage", "ToggleLot": "parking.lots.manage",
    "CreateGate": "parking.lots.manage", "ToggleGate": "parking.lots.manage",
    "CreateVehicle": "parking.vehicles.manage", "ToggleVehicle": "parking.vehicles.manage",
    "CreatePermit": "parking.vehicles.manage", "TogglePermit": "parking.vehicles.manage",
    "CreateCamera": "parking.cameras.manage", "ToggleCamera": "parking.cameras.manage",
    "PingCamera": "parking.cameras.manage", "RotateCameraToken": "parking.cameras.manage",
    "SetGateOpenState": "parking.barrier", "OpenBarrier": "parking.barrier",
    "ScanForm": "parking.scan", "Scan": "parking.scan",
    # SimCameraHeartbeat/SimPunch → devices.simulate دست‌نخورده
 },
 "modules/HrModules.Devices/Controllers/DevicesController.cs": {
    "Create": "devices.register", "Edit": "devices.register", "ToggleActive": "devices.register",
    "TransferUser": "devices.transfer",
    "Command": "devices.commands", "FlushOffline": "devices.commands",
    # SimPunch/SimHeartbeat → devices.simulate، RotateSecret/بقیه دست‌نخورده
 },
 "modules/HrModules.Devices/Controllers/BiometricsController.cs": {
    "FetchUsers": "biometrics.fetch", "FetchBios": "biometrics.fetch",
    "DeleteDeviceUser": "biometrics.fetch",
    "PushToDevices": "biometrics.transfer", "TransferDeviceUsers": "biometrics.transfer",
    # SimulateFetch → devices.simulate دست‌نخورده
 },
 "HrAttendance.Web/Modules/Core/Controllers/CompaniesController.cs": {
    "Create": "companies.create", "EditForm": "companies.edit", "Edit": "companies.edit",
    "ToggleActive": "companies.edit", "Delete": "companies.delete",
 },
 "HrAttendance.Web/Modules/Core/Controllers/UsersController.cs": {
    "LinkEmployee": "users.edit", "SetPhoto": "users.edit", "ToggleActive": "users.edit",
    "SetRoles": "users.edit",
    "ResetPassword": "users.reset", "Unlock": "users.reset", "RevokeSessions": "users.reset",
    "RoleEdit": "roles.edit", "SetRolePermissions": "roles.edit", "RoleDelete": "roles.delete",
    # Create/RoleForm/RoleCreate دست‌نخورده
 },
 "modules/HrModules.Payroll/Controllers/PayrollController.cs": {
    "Create": "payroll.runs.create", "Finalize": "payroll.runs.finalize",
    "Delete": "payroll.runs.delete",
    "BonusCompute": "payroll.bonus.compute", "BonusFinalize": "payroll.bonus.compute",
    "BonusDeleteDraft": "payroll.bonus.compute",
    "BonusExport": "payroll.bonus.export",
    "SettlementCompute": "payroll.settlement", "SettlementFinalize": "payroll.settlement",
    "SettlementDelete": "payroll.settlement",
 },
}

# کلیدهای درشتِ مجاز برای جایگزینی (هرچیز دیگری = احتمالاً جریان موازی قبلاً ریز کرده → skip)
COARSE_OK = {"manage", "view", "control", "approve", "edit", "create", "import", "poll", "pdf", "bonus", "export"}

def coarse(key: str) -> bool:
    leaf = key.split(".")[-1]
    return leaf in COARSE_OK or leaf in {"records.manage", "gates.manage"} or key in {
        "attendance.manage", "access.manage", "cards.manage", "visitors.manage",
        "parking.manage", "parking.control", "devices.manage", "devices.control",
        "biometrics.manage", "org.manage", "shifts.manage", "holidays.manage",
        "users.manage", "roles.manage", "companies.manage", "payroll.manage",
        "payroll.bonus", "attendance.approve", "employees.manage",
    }

dry = "--dry" in sys.argv
changed, skipped_fine, unmatched = 0, [], []
for rel, amap in M.items():
    p = SRC / rel
    if not p.exists():
        unmatched.append(f"MISSING FILE {rel}"); continue
    lines = p.read_text(encoding="utf-8").splitlines(keepends=True)
    attr_at = {}  # line_index → (key, action_name|None)
    for i, ln in enumerate(lines):
        m = ATTR.search(ln)
        if not m: continue
        act = None
        for j in range(i + 1, min(i + 8, len(lines))):
            a = ACT.search(lines[j])
            if a:
                act = a.group(1) or a.group(2); break
        attr_at[i] = (m.group(1), act)
    file_changed = 0
    for i, (key, act) in attr_at.items():
        if act is None:
            unmatched.append(f"{rel}:{i+1} attr بدون اکشن ({key})"); continue
        if act not in amap: continue
        target = amap[act]
        if key == target: continue
        if not coarse(key):
            skipped_fine.append(f"{rel}:{i+1} {act}: «{key}» ریز است → skip (هدف {target})"); continue
        lines[i] = ATTR.sub(f'[HasPerm("{target}")]', lines[i], count=1)
        changed += 1; file_changed += 1
        print(f"  {rel}:{i+1} {act}: {key} → {target}")
    if file_changed and not dry:
        p.write_text("".join(lines), encoding="utf-8")

print(f"\nCHANGED={changed}")
for s in skipped_fine: print("SKIP-FINE:", s)
for u in unmatched: print("WARN:", u)
print("DRY" if dry else "APPLIED")
