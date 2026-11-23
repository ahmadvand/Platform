#!/usr/bin/env python3
# ======================================================================
# فاز ۶۱ — تفکیک Entities.cs به لایهٔ دیتای مستقل هر ماژول
# کلاس‌های پایه می‌مانند (Core)؛ کلاس‌های هر ماژول به پروژهٔ Data خودش می‌رود
# با namespace جدید. محتوای کلاس‌ها دست‌نخورده (بدون رگرسیون).
# ======================================================================
import os, re, sys

SRC = "/home/z/my-project/aspnet/src"
ENTS = f"{SRC}/HrPlatform.Core/Domain/Entities.cs"

BASE    = ["Company","Department","Position","Employee","Contract","Decree","User","Role","Permission",
           "UserRole","RolePermission","RefreshToken","PasswordHistory","PasswordReset","AuditLog",
           "DeviceCommand","DeviceUser","DeviceBiometric","DeviceOfflinePunch","DevicePunchLog",
           "EmployeeBiometric","Holiday","NotificationRule","NotificationLog","NotificationSetting",
           "GatewaySetting","AppSettings","ColorPalette"]
ATT     = ["Shift","AttendanceRecord","LeaveRequest","Mission","OvertimePermit","CorrectionRequest"]
ACCESS  = ["Zone","Gate","AccessPermission","AccessEvent","Card","SecurityAlert","AccessGroup","AccessGroupMember","AccessGroupGate"]
VISIT   = ["Contractor","Visitor","VisitorCard","VisitLog"]
PAYROLL = ["PayrollRun","Payslip","PayItem","PayrollParameter","PayrollSetting","PayslipLine","PayrollBonusRow","PayrollSettlement"]
PARKING = ["ParkingLot","ParkingGate","Vehicle","ParkingPermit","ParkingEvent","AnprCamera","AnprCommand"]

MODS = {
    "Attendance": (ATT,     f"{SRC}/modules/HrModules.Attendance/Data/Domain/AttendanceEntities.cs", "HrModules.Attendance.Domain"),
    "Access":     (ACCESS,  f"{SRC}/modules/HrModules.Access/Data/Domain/AccessEntities.cs",         "HrModules.Access.Domain"),
    "Visitors":   (VISIT,   f"{SRC}/modules/HrModules.Visitors/Data/Domain/VisitorsEntities.cs",     "HrModules.Visitors.Domain"),
    "Payroll":    (PAYROLL, f"{SRC}/modules/HrModules.Payroll/Data/Domain/PayrollEntities.cs",       "HrModules.Payroll.Domain"),
    "Parking":    (PARKING, f"{SRC}/modules/HrModules.Parking/Data/Domain/ParkingEntities.cs",       "HrModules.Parking.Domain"),
}

lines = open(ENTS, encoding="utf-8").read().splitlines()
# مرز کلاس‌ها: هر خط «public class X» یا «public enum X» (سطح صفر)
decl = re.compile(r"^(public (?:class|enum|record|interface) (\w+))")
starts = []
for i, ln in enumerate(lines):
    m = decl.match(ln)
    if m: starts.append((i, m.group(2)))
assert starts, "no classes found"
ranges = {}
for idx, (i, name) in enumerate(starts):
    end = starts[idx+1][0] if idx+1 < len(starts) else len(lines)
    # حذف خطوط خالی انتهایی
    while end > i and lines[end-1].strip() == "": end -= 1
    ranges[name] = (i, end)

def block(name):
    i, end = ranges[name]
    return "\n".join(lines[i:end]) + "\n"

HEADER = "using System.ComponentModel.DataAnnotations;\n\n"

# ۱) پایه — همان فایل Entities.cs با فقط کلاس‌های پایه
base_body = "\n".join(block(n) for n in BASE if n in ranges)
base_out = "namespace HrAttendance.Web.Domain;\n\n// ============================================================\n// مدل‌های دامنهٔ پایه (پلتفرم + ماژول‌های پایهٔ core/org/devices) — فاز ۶۱\n// فقط دادهٔ مشترک؛ هیچ ارجاعی به entity ماژول‌های سیستم ندارد\n// ============================================================\n\n" + base_body
open(ENTS, "w", encoding="utf-8").write(base_out)

# ۲) ماژول‌ها
for mod, (names, path, ns) in MODS.items():
    os.makedirs(os.path.dirname(path), exist_ok=True)
    body = "\n".join(block(n) for n in names if n in ranges)
    out = HEADER + f"namespace {ns};\n\n// ============================================================\n// لایهٔ دیتای مستقل ماژول {mod} (فاز ۶۱) — entityهای اختصاصی این ماژول\n// ارتباط با ماژول‌های دیگر فقط از طریق HrPlatform.Contracts\n// ============================================================\n\n" + body
    open(path, "w", encoding="utf-8").write(out)
    print(f"{mod}: {sum(1 for n in names if n in ranges)} classes -> {path}")

missing = [n for grp in (BASE, ATT, ACCESS, VISIT, PAYROLL, PARKING) for n in grp if n not in ranges]
print("MISSING:", missing if missing else "none")
print(f"base: {sum(1 for n in BASE if n in ranges)} classes -> {ENTS}")
