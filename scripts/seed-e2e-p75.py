#!/usr/bin/env python3.13
# ============================================================
# seed-e2e-p75.py — دادهٔ e2e فاز ۷۵ (کلیدهای ریز + دامنهٔ پارکینگ)
# خودرو + رویداد پارکینگ برای پرسنل E2E (IT/FIN) + کاربر superadmin آزمون.
# Idempotent — اجرای تکرار خراب نمی‌کند.
# ============================================================
import sqlite3, uuid, datetime

DB = "/home/z/my-project/aspnet/src/HrAttendance.Web/hr.db"
c = sqlite3.connect(DB)
now = datetime.datetime.now()
gid = lambda: uuid.uuid4().hex

# ---------- پاک‌سازی اجرای قبلی ----------
for u in ("p75_scan", "p75_dept", "p75_sa"):
    r = c.execute("SELECT Id FROM Users WHERE Username=?", (u,)).fetchone()
    if r:
        uid = r[0]
        for t in ("UserRoles", "PasswordHistories", "RefreshTokens"):
            c.execute(f"DELETE FROM {t} WHERE UserId=?", (uid,))
        c.execute("DELETE FROM Users WHERE Id=?", (uid,))
for rn in ("p75_scan_r", "p75_dept_r"):
    r = c.execute("SELECT Id FROM Roles WHERE Name=?", (rn,)).fetchone()
    if r:
        c.execute("DELETE FROM RoleScopes WHERE RoleId=?", (r[0],))
        c.execute("DELETE FROM RolePermissions WHERE RoleId=?", (r[0],))
        c.execute("DELETE FROM Roles WHERE Id=?", (r[0],))
c.execute("DELETE FROM ParkingEvents WHERE PlateKey LIKE 'E2E%'")
c.execute("DELETE FROM Vehicles WHERE PlateKey LIKE 'E2E%'")
c.commit()

co = c.execute("SELECT Id FROM Companies LIMIT 1").fetchone()[0]
IT = c.execute("SELECT Id FROM Departments WHERE Code='E2E-IT'").fetchone()
FIN = c.execute("SELECT Id FROM Departments WHERE Code='E2E-FIN'").fetchone()
if not (IT and FIN):
    raise SystemExit("seed-e2e-p74.py must run first (departments missing)")
IT, FIN = IT[0], FIN[0]
e_it = c.execute("SELECT Id, FirstName || ' ' || LastName FROM Employees WHERE DepartmentId=? AND PersonnelCode='E2E-1001'", (IT,)).fetchone()
e_fin = c.execute("SELECT Id FROM Employees WHERE DepartmentId=? AND PersonnelCode='E2E-2001'", (FIN,)).fetchone()
assert e_it and e_fin, "E2E employees missing (run seed-e2e-p74.py)"

gate = c.execute("SELECT Id FROM ParkingGates WHERE CompanyId=? LIMIT 1", (co,)).fetchone()
assert gate, "parking gate missing"
GATE = gate[0]

# ---------- خودروها + رویدادها ----------
VEH_E2E = "E2EPLATE"
vid_it, vid_fin = gid(), gid()
for vid, emp, plate in ((vid_it, e_it[0], "12ع471ایران36"), (vid_fin, e_fin[0], "88ب123ایران22")):
    c.execute("""INSERT INTO Vehicles (Id,CompanyId,EmployeeId,PlateNo,PlateKey,Type,Brand,Model,Color,Active,CreatedAt,UpdatedAt)
                 VALUES (?,?,?,?,?,?,?,?,?,1,datetime('now'),datetime('now'))""",
              (vid, co, emp, plate, VEH_E2E + emp[:8], "سواری", "پژو", "پارس", "سفید"))
ts = now.replace(hour=7, minute=30, second=0, microsecond=0)
for i, (vid, direction) in enumerate([(vid_it, "ورود"), (vid_it, "خروج"), (vid_fin, "ورود"), (vid_fin, "خروج")]):
    c.execute("""INSERT INTO ParkingEvents (Id,GateId,VehicleId,PlateText,PlateKey,Direction,Method,Result,Timestamp)
                 VALUES (?,?,?,?,?,?,?,?,?)""",
              (gid(), GATE, vid, "پلاک E2E", VEH_E2E + ("it" if vid == vid_it else "fin"),
               direction, "دستی", "مجاز", (ts + datetime.timedelta(minutes=10 * i)).strftime("%Y-%m-%d %H:%M:%S")))
c.commit()
print(f"PASS parking seed: IT-vehicle={vid_it[:8]} FIN-vehicle={vid_fin[:8]} gate={GATE[:8]} owner={e_it[1]}")

# ---------- کاربر superadmin آزمون ----------
import hashlib, base64, os
salt = os.urandom(16)
h = hashlib.pbkdf2_hmac("sha256", b"Sa@12345", salt, 100_000, dklen=32)
ph = f"pbkdf2-sha256$100000${base64.b64encode(salt).decode()}${base64.b64encode(h).decode()}"
c.execute("""INSERT INTO Users (Username,FullName,PasswordHash,IsActive,IsSuperadmin,FailedLoginAttempts,MustChangePassword,PasswordChangedAt,CreatedAt,UpdatedAt)
             VALUES (?,?,?,1,1,0,0,datetime('now'),datetime('now'),datetime('now'))""",
          ("p75_sa", "ابر کاربر آزمون ۷۵", ph))
c.commit()
print("PASS p75_sa superadmin created")
