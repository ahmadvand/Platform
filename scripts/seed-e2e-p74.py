#!/usr/bin/env python3.13
# ============================================================
# seed-e2e-p74.py — دادهٔ حداقلی برای e2e فاز ۷۴ (دامنهٔ رکوردی)
# جدول‌های org خالی‌اند (پاک‌شدگی در جریان توسعه) — درخت دوسطحی +
# ۴ پرسنل + تردد امروز + ۱ لیست حقوق با فیش + ۲ درخواست مرخصی می‌سازد.
# Idها hex-32 سازگار با ستون‌های Guid «N» (maxlen 64).
# ============================================================
import sqlite3, uuid, datetime

DB = "/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db"
c = sqlite3.connect(DB)
now = datetime.datetime.now()
today = now.strftime("%Y-%m-%d")
gid = lambda: uuid.uuid4().hex

# پاک‌سازی اجرای قبلی (idempotent)
for t, k in [("Employees", "PersonnelCode"), ("Departments", "Code"),
             ("AttendanceRecords", "Date"), ("LeaveRequests", "FromDate"),
             ("Payslips", "JYear"), ("PayrollRuns", "JYear")]:
    try: c.execute(f"DELETE FROM {t} WHERE {k} LIKE 'E2E%' OR {k} = ? OR {k} LIKE '1404%'", (today,))
    except Exception as ex: print("clean skip", t, ex)
c.commit()

co = c.execute("SELECT Id FROM Companies LIMIT 1").fetchone()
assert co, "company missing"
CO = co[0]

# ---------- واحدهای سازمانی ----------
IT, FIN = gid(), gid()
c.execute("INSERT INTO Departments (Id,Name,Code,CompanyId,ParentId,CreatedAt,UpdatedAt) VALUES (?,?,?,?,?,datetime('now'),datetime('now'))",
          (IT, "فناوری اطلاعات", "E2E-IT", CO, None))
c.execute("INSERT INTO Departments (Id,Name,Code,CompanyId,ParentId,CreatedAt,UpdatedAt) VALUES (?,?,?,?,?,datetime('now'),datetime('now'))",
          (FIN, "مالی و اداری", "E2E-FIN", CO, None))

# ---------- پرسنل ----------
emps = [
    ("E2E-1001", "علی", "رضایی", IT), ("E2E-1002", "سارا", "کریمی", IT),
    ("E2E-2001", "حسین", "مرادی", FIN), ("E2E-2002", "نگار", "شریفی", FIN),
]
E = {}
for code, fn, ln, dept in emps:
    eid = gid()
    E[code] = (eid, fn, ln, dept)
    c.execute("""INSERT INTO Employees (Id,PersonnelCode,FirstName,LastName,NationalId,EmploymentType,Status,HireDate,CompanyId,DepartmentId,BaseSalary,MaritalStatus,ChildCount,AnnualLeaveEnt,AnnualLeaveUsed,SickLeaveEnt,SickLeaveUsed,CreatedAt,UpdatedAt)
                 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,datetime('now'),datetime('now'))""",
              (eid, code, fn, ln, code.replace("E2E-", ""), "قراردادی", "فعال", now.date().isoformat(), CO, dept, 12000000, "متأهل", 1, 26, 0, 2, 0))

# ---------- تردد امروز (ساعت ورود/خروج) ----------
in1 = now.replace(hour=8, minute=17, second=0)
out1 = now.replace(hour=15, minute=54, second=0)
for code, (eid, *_ ) in E.items():
    c.execute("""INSERT INTO AttendanceRecords (Id,EmployeeId,Date,Status,CheckIn,CheckOut,WorkMinutes,LateMinutes,EarlyMinutes,OvertimeMinutes,CreatedAt,UpdatedAt)
                 VALUES (?,?,?,?,?,?,?,?,?,?,datetime('now'),datetime('now'))""",
              (gid(), eid, today, "حاضر", in1.isoformat(" "), out1.isoformat(" "), 457, 0, 0, 0))

# ---------- لیست حقوق + فیش‌ها (سال جاری ماه ۷) ----------
jy, jm = 1404, 7
RUN = gid()
c.execute("""INSERT INTO PayrollRuns (Id,CompanyId,JYear,JMonth,Status,TotalNet,TotalGross,EmployeeCount,CreatedAt,CreatedBy,UpdatedAt)
             VALUES (?,?,?,?,?,?,?,?,datetime('now'),'E2E',datetime('now'))""",
          (RUN, CO, jy, jm, "پیش‌نویس", 48000000, 52000000, 4))
for code, (eid, fn, ln, dept) in E.items():
    c.execute("""INSERT INTO Payslips (Id,RunId,EmployeeId,BaseSalary,OvertimePay,Allowances,AbsentDeduct,Insurance,Tax,NetPay,WorkedMin,OvertimeMin,AbsentDays,CreatedAt)
                 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,datetime('now'))""",
              (gid(), RUN, eid, 12000000, 0, 1000000, 0, 0, 0, 13000000, 457, 0, 0))

# ---------- درخواست‌های مرخصی ----------
c.execute("""INSERT INTO LeaveRequests (Id,EmployeeId,Type,FromDate,ToDate,Days,Status,Reason,CreatedAt,UpdatedAt)
             VALUES (?,?,?,?,?,?,?,?,datetime('now'),datetime('now'))""",
          (gid(), E["E2E-1001"][0], "استحقاقی", today, today, 1, "در انتظار تأیید", "e2e"))
c.execute("""INSERT INTO LeaveRequests (Id,EmployeeId,Type,FromDate,ToDate,Days,Status,Reason,CreatedAt,UpdatedAt)
             VALUES (?,?,?,?,?,?,?,?,datetime('now'),datetime('now'))""",
          (gid(), E["E2E-2001"][0], "ساعتی", today, today, 2, "در انتظار تأیید", "e2e"))

c.commit()
print("CO=", CO)
print("IT=", IT)
print("FIN=", FIN)
print("emps=", {k: v[0] for k, v in E.items()})
print("RUN=", RUN)
print("seeded OK — today:", today)
