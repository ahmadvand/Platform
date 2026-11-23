import { db } from "./db";
import { computeWork, dateKey } from "./att";
import { addDays } from "./persian";

// Deterministic RNG so demo data stays stable between restarts
function mulberry32(a: number) {
  return function () {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const rand = mulberry32(1404);
const pick = <T,>(arr: T[]): T => arr[Math.floor(rand() * arr.length)];
const rint = (a: number, b: number) => a + Math.floor(rand() * (b - a + 1));

const FIRST_M = ["علی", "محمد", "رضا", "حسین", "امیر", "مهدی", "سعید", "احمد", "جواد", "حسن", "بهنام", "آرش", "فرهاد", "سامان", "هومن", "بهزاد", "مسعود", "کیان", "یاسر", "ناصر"];
const FIRST_F = ["مریم", "زهرا", "فاطمه", "سارا", "الناز", "شیما", "پریسا", "لیلا", "نگار", "مینا"];
const LAST = ["محمدی", "حسینی", "احمدی", "رضایی", "کریمی", "موسوی", "نوری", "صادقی", "جعفری", "علوی", "قاسمی", "بهرامی", "سلطانی", "اکبری", "کاظمی", "یوسفی", "زارعی", "تهرانی", "شریفی", "مرادی", "رستمی", "امیری", "فتحی", "دهقان", "کاویانی"];

// NOTE: Turbopack dev may instantiate this module once per route — store the
// lock on globalThis so concurrent first requests can never double-seed.
const g = globalThis as unknown as { __hrSeedPromise?: Promise<void> };

export async function ensureSeed(): Promise<void> {
  if (!g.__hrSeedPromise) {
    g.__hrSeedPromise = (async () => {
      await ensureRbac(); // RBAC مستقل از داده کسب‌وکار — همیشه آماده باشد
      const count = await db.employee.count();
      if (count > 0) return;
      await seedAll();
    })().catch((e) => {
      g.__hrSeedPromise = undefined;
      throw e;
    });
  }
  return g.__hrSeedPromise;
}

async function cleanAll(): Promise<void> {
  await db.securityAlert.deleteMany();
  await db.visitLog.deleteMany();
  await db.visitorCard.deleteMany();
  await db.visitor.deleteMany();
  await db.contractor.deleteMany();
  await db.accessEvent.deleteMany();
  await db.accessPermission.deleteMany();
  await db.gate.deleteMany();
  await db.zone.deleteMany();
  await db.attendanceRecord.deleteMany();
  await db.leaveRequest.deleteMany();
  await db.mission.deleteMany();
  await db.overtimePermit.deleteMany();
  await db.deviceCommand.deleteMany();
  await db.payslip.deleteMany();
  await db.payrollRun.deleteMany();
  await db.correctionRequest.deleteMany();
  await db.holiday.deleteMany();
  await db.device.deleteMany();
  await db.shift.deleteMany();
  await db.decree.deleteMany();
  await db.contract.deleteMany();
  await db.employee.deleteMany();
  await db.position.deleteMany();
  await db.department.deleteMany();
  await db.company.deleteMany();
}

export async function reseed(): Promise<void> {
  await cleanAll();
  g.__hrSeedPromise = undefined;
  await seedAll();
}

async function seedAll(): Promise<void> {
  // wipe any partial seed first (idempotent recovery)
  await cleanAll();

  // ---------- Company ----------
  const company = await db.company.create({
    data: { name: "شرکت پتروشیمی پارس آسیا", code: "PAC-01" },
  });

  // ---------- Departments (tree) ----------
  const mkDept = async (name: string, code: string, parentId?: string) =>
    db.department.create({
      data: { name, code, companyId: company.id, parentId: parentId ?? null },
    });

  const dExec = await mkDept("دفتر مدیریت عامل", "EXE");
  const dHR = await mkDept("منابع انسانی", "HR");
  const dSec = await mkDept("حراست و حفاظت", "SEC");
  const dProd = await mkDept("تولید و بهره‌برداری", "PRD");
  const dFin = await mkDept("مالی و اداری", "FIN");
  const dTech = await mkDept("فنی و مهندسی", "ENG");
  const dComm = await mkDept("بازرگانی و خرید", "COM");
  const dHSE = await mkDept("HSE و ایمنی", "HSE");
  const dOlef = await mkDept("واحد الفین", "PRD-OLF", dProd.id);
  const dPoly = await mkDept("واحد پلی‌اتیلن", "PRD-PLE", dProd.id);
  const dUtil = await mkDept("واحد یوتیلیتی", "PRD-UTL", dProd.id);

  // ---------- Positions ----------
  const mkPos = async (title: string, code: string, dept: { id: string }, grade?: string) =>
    db.position.create({ data: { title, code, departmentId: dept.id, grade: grade ?? null } });

  const pCEO = await mkPos("مدیرعامل", "P-CEO", dExec);
  const pHRM = await mkPos("مدیر منابع انسانی", "P-HRM", dHR);
  const pHRS = await mkPos("کارشناس منابع انسانی", "P-HRS", dHR);
  const pSecC = await mkPos("رئیس حراست", "P-SEC", dSec);
  const pGuard = await mkPos("نگهبان", "P-GRD", dSec);
  const pProdM = await mkPos("مدیر تولید", "P-PRM", dProd);
  const pShiftL = await mkPos("سرپرست شیفت", "P-SHL", dProd);
  const pOper = await mkPos("اپراتور تولید", "P-OPR", dProd);
  const pFinM = await mkPos("رئیس مالی", "P-FIN", dFin);
  const pAcc = await mkPos("حسابدار", "P-ACC", dFin);
  const pEngM = await mkPos("مدیر فنی و مهندسی", "P-ENG", dTech);
  const pTech = await mkPos("تکنسین نگهداری و تعمیرات", "P-TEC", dTech);
  const pBuy = await mkPos("کارشناس بازرگانی و خرید", "P-BUY", dComm);
  const pHse = await mkPos("کارشناس HSE", "P-HSE", dHSE);

  // ---------- Shifts (ATT-02) ----------
  const sAdmin = await db.shift.create({ data: { name: "شیفت اداری", type: "عادی", startTime: "08:00", endTime: "16:00", breakMinutes: 60, toleranceLate: 10 } });
  const sMorning = await db.shift.create({ data: { name: "نوبت اول (صبح)", type: "نوبت‌کاری", startTime: "07:00", endTime: "15:00", breakMinutes: 45, toleranceLate: 5 } });
  const sEvening = await db.shift.create({ data: { name: "نوبت دوم (عصر)", type: "نوبت‌کاری", startTime: "15:00", endTime: "23:00", breakMinutes: 45, toleranceLate: 5 } });
  const sNight = await db.shift.create({ data: { name: "نوبت سوم (شب)", type: "نوبت‌کاری", startTime: "23:00", endTime: "07:00", breakMinutes: 45, toleranceLate: 5 } });
  const s12 = await db.shift.create({ data: { name: "شیفت ۱۲ ساعته روز", type: "۱۲ساعته", startTime: "07:00", endTime: "19:00", breakMinutes: 60, toleranceLate: 10 } });
  const sCycle = await db.shift.create({ data: { name: "شیفت اقماری ۱۴/۱۴", type: "اقماری", startTime: "07:00", endTime: "19:00", breakMinutes: 60, cyclePattern: "14/14", toleranceLate: 15 } });

  // ---------- Devices (ATT-01) ----------
  const mkDev = (name: string, brand: string, serial: string, ip: string, location: string, purpose: string, mode: string, minsAgo: number | null) =>
    db.device.create({
      data: {
        name, brand, serial, ip, location, purpose, mode,
        lastSync: minsAgo === null ? null : new Date(Date.now() - minsAgo * 60000),
        lastSeenAt: minsAgo === null ? null : new Date(Date.now() - minsAgo * 60000),
      },
    });

  const dev1 = await mkDev("ترمینال ورودی اصلی", "ZKTeco", "ZK-U2000-0142", "192.168.10.11", "ورودی اصلی سایت", "حضور و غیاب", "آنلاین", 4);
  const dev2 = await mkDev("تشخیص چهره گیت شرقی", "ZKTeco", "ZK-FD7-0311", "192.168.10.12", "گیت شرقی", "کنترل تردد", "آنلاین", 7);
  const dev3 = await mkDev("دستگاه دفتر مرکزی", "Suprema", "SP-BS3-0907", "192.168.10.13", "دفتر مرکزی", "حضور و غیاب", "آنلاین", 2);
  const dev4 = await mkDev("ترمینال انبار محصول", "Anviz", "AV-W1-0455", "192.168.10.14", "انبار محصول", "کنترل تردد", "آفلاین", 2880);
  const dev5 = await mkDev("ترمینال کارگاه فنی", "Timmy", "TM-A5-0773", "192.168.10.15", "کارگاه فنی", "حضور و غیاب", "آنلاین", 15);
  const dev6 = await mkDev("کنترلر منطقه حساس", "Virdi", "VR-AC7-0221", "192.168.10.16", "انبار مواد شیمیایی", "کنترل تردد", "آنلاین", 9);

  // ---------- Zones & Gates (AC-01) ----------
  const zMain = await db.zone.create({ data: { name: "ورودی اصلی سایت", code: "Z-MAIN", riskLevel: "عادی", description: "ورود و خروج کلیه پرسنل و مراجعین" } });
  const zProd = await db.zone.create({ data: { name: "منطقه تولید", code: "Z-PRD", riskLevel: "حساس", description: "واحدهای فرآیندی الفین و پلی‌اتیلن" } });
  const zStore = await db.zone.create({ data: { name: "انبار محصول", code: "Z-STR", riskLevel: "حساس", description: "انبار محصول نهایی و سکوی بارگیری" } });
  const zOffice = await db.zone.create({ data: { name: "دفتر مرکزی", code: "Z-OFC", riskLevel: "عادی", description: "ساختمان اداری" } });
  const zChem = await db.zone.create({ data: { name: "انبار مواد شیمیایی", code: "Z-CHM", riskLevel: "فوق‌حساس", description: "انبار کاتالیست و مواد شیمیایی — دسترسی محدود" } });

  const gMain = await db.gate.create({ data: { name: "گیت اصلی ورودی", code: "G-01", zoneId: zMain.id, deviceId: dev1.id, type: "ورودی‌گردان", direction: "ورودی" } });
  const gMainOut = await db.gate.create({ data: { name: "گیت اصلی خروجی", code: "G-02", zoneId: zMain.id, deviceId: dev1.id, type: "ورودی‌گردان", direction: "خروجی" } });
  const gEast = await db.gate.create({ data: { name: "گیت شرقی", code: "G-03", zoneId: zMain.id, deviceId: dev2.id, type: "راهبند", direction: "دوطرفه" } });
  const gOlefin = await db.gate.create({ data: { name: "درب واحد الفین", code: "G-04", zoneId: zProd.id, deviceId: dev6.id, type: "درب برقی", direction: "دوطرفه" } });
  const gPoly = await db.gate.create({ data: { name: "درب واحد پلی‌اتیلن", code: "G-05", zoneId: zProd.id, deviceId: dev6.id, type: "درب برقی", direction: "دوطرفه" } });
  const gStore = await db.gate.create({ data: { name: "گیت انبار محصول", code: "G-06", zoneId: zStore.id, deviceId: dev4.id, type: "گیت میله‌ای", direction: "دوطرفه" } });
  const gOffice = await db.gate.create({ data: { name: "درب برقی دفتر مرکزی", code: "G-07", zoneId: zOffice.id, deviceId: dev3.id, type: "درب برقی", direction: "دوطرفه" } });
  const gChem = await db.gate.create({ data: { name: "درب انبار مواد شیمیایی", code: "G-08", zoneId: zChem.id, deviceId: dev6.id, type: "درب برقی", direction: "دوطرفه" } });

  await db.gate.update({ where: { id: gStore.id }, data: { status: "خراب" } });

  // ---------- Employees ----------
  interface EmpSpec {
    first: string; last: string; dept: string; pos: string;
    type: string; shift: string | null; salary: number;
    hireYearsAgo: number; leaveUsed?: number;
  }
  const specs: EmpSpec[] = [
    { first: "کامران", last: "رستمی", dept: dExec.id, pos: pCEO.id, type: "رسمی", shift: sAdmin.id, salary: 980_000_000, hireYearsAgo: 14 },
    { first: "مریم", last: "تهرانی", dept: dHR.id, pos: pHRM.id, type: "رسمی", shift: sAdmin.id, salary: 620_000_000, hireYearsAgo: 11 },
    { first: "سعید", last: "قاسمی", dept: dHR.id, pos: pHRS.id, type: "قراردادی", shift: sAdmin.id, salary: 340_000_000, hireYearsAgo: 4 },
    { first: "پریسا", last: "علوی", dept: dHR.id, pos: pHRS.id, type: "قراردادی", shift: sAdmin.id, salary: 315_000_000, hireYearsAgo: 2 },
    { first: "حسین", last: "بهرامی", dept: dSec.id, pos: pSecC.id, type: "رسمی", shift: sAdmin.id, salary: 540_000_000, hireYearsAgo: 12 },
    { first: "جواد", last: "صادقی", dept: dSec.id, pos: pGuard.id, type: "پیمانکاری", shift: s12.id, salary: 190_000_000, hireYearsAgo: 1 },
    { first: "یاسر", last: "فتحی", dept: dSec.id, pos: pGuard.id, type: "پیمانکاری", shift: sNight.id, salary: 190_000_000, hireYearsAgo: 1 },
    { first: "لیلا", last: "کاویانی", dept: dSec.id, pos: pGuard.id, type: "پیمانکاری", shift: sMorning.id, salary: 185_000_000, hireYearsAgo: 2 },
    { first: "فرهاد", last: "سلطانی", dept: dProd.id, pos: pProdM.id, type: "رسمی", shift: sAdmin.id, salary: 760_000_000, hireYearsAgo: 13 },
    { first: "رضا", last: "کریمی", dept: dOlef.id, pos: pShiftL.id, type: "رسمی", shift: sMorning.id, salary: 480_000_000, hireYearsAgo: 9 },
    { first: "مهدی", last: "نوری", dept: dOlef.id, pos: pShiftL.id, type: "رسمی", shift: sEvening.id, salary: 465_000_000, hireYearsAgo: 8 },
    { first: "امیر", last: "محمدی", dept: dOlef.id, pos: pOper.id, type: "قراردادی", shift: sMorning.id, salary: 260_000_000, hireYearsAgo: 3 },
    { first: "علی", last: "احمدی", dept: dOlef.id, pos: pOper.id, type: "قراردادی", shift: sEvening.id, salary: 255_000_000, hireYearsAgo: 3 },
    { first: "حسن", last: "جعفری", dept: dOlef.id, pos: pOper.id, type: "قراردادی", shift: sNight.id, salary: 260_000_000, hireYearsAgo: 2 },
    { first: "سامان", last: "امیری", dept: dPoly.id, pos: pOper.id, type: "اقماری", shift: sCycle.id, salary: 385_000_000, hireYearsAgo: 5 },
    { first: "هومن", last: "مرادی", dept: dPoly.id, pos: pOper.id, type: "اقماری", shift: sCycle.id, salary: 380_000_000, hireYearsAgo: 5 },
    { first: "بهزاد", last: "شریفی", dept: dPoly.id, pos: pOper.id, type: "قراردادی", shift: sMorning.id, salary: 250_000_000, hireYearsAgo: 2 },
    { first: "آرش", last: "رستمی", dept: dUtil.id, pos: pOper.id, type: "قراردادی", shift: sEvening.id, salary: 240_000_000, hireYearsAgo: 2 },
    { first: "مسعود", last: "اکبری", dept: dFin.id, pos: pFinM.id, type: "رسمی", shift: sAdmin.id, salary: 640_000_000, hireYearsAgo: 10 },
    { first: "زهرا", last: "حسینی", dept: dFin.id, pos: pAcc.id, type: "قراردادی", shift: sAdmin.id, salary: 330_000_000, hireYearsAgo: 4 },
    { first: "فاطمه", last: "موسوی", dept: dFin.id, pos: pAcc.id, type: "قراردادی", shift: sAdmin.id, salary: 310_000_000, hireYearsAgo: 3 },
    { first: "ناصر", last: "زارعی", dept: dTech.id, pos: pEngM.id, type: "رسمی", shift: sAdmin.id, salary: 700_000_000, hireYearsAgo: 12 },
    { first: "بهنام", last: "نیک‌نام", dept: dTech.id, pos: pTech.id, type: "قراردادی", shift: sAdmin.id, salary: 295_000_000, hireYearsAgo: 3 },
    { first: "کیان", last: "عبدی", dept: dTech.id, pos: pTech.id, type: "قراردادی", shift: sMorning.id, salary: 285_000_000, hireYearsAgo: 1 },
    { first: "سارا", last: "موسوی", dept: dComm.id, pos: pBuy.id, type: "قراردادی", shift: sAdmin.id, salary: 350_000_000, hireYearsAgo: 5 },
    { first: "الناز", last: "شاهسوند", dept: dComm.id, pos: pBuy.id, type: "قراردادی", shift: sAdmin.id, salary: 320_000_000, hireYearsAgo: 2 },
    { first: "شیما", last: "موحدی", dept: dHSE.id, pos: pHse.id, type: "رسمی", shift: sAdmin.id, salary: 420_000_000, hireYearsAgo: 7 },
    { first: "نگار", last: "پرویزی", dept: dHSE.id, pos: pHse.id, type: "قراردادی", shift: sAdmin.id, salary: 360_000_000, hireYearsAgo: 3 },
  ];

  const empIds: { id: string; dept: string; shiftId: string | null; type: string; posCode: string }[] = [];
  let persNo = 1001;

  for (const s of specs) {
    const natId = String(rint(1000000000, 9999999999));
    const hire = addDays(new Date(), -365 * s.hireYearsAgo - rint(0, 200));
    const gender = FIRST_F.includes(s.first) ? "زن" : "مرد";
    const emp = await db.employee.create({
      data: {
        personnelCode: String(persNo++),
        firstName: s.first,
        lastName: s.last,
        nationalId: natId,
        birthDate: `13${rint(40, 75)}/0${rint(1, 9)}/${String(rint(10, 28)).padStart(2, "0")}`,
        phone: `0912${String(rint(1000000, 9999999))}`,
        email: `emp${persNo - 1}@parsasia.ir`,
        employmentType: s.type,
        status: "فعال",
        hireDate: hire,
        companyId: company.id,
        departmentId: s.dept,
        positionId: s.pos,
        baseSalary: s.salary,
        shiftId: s.shift,
        annualLeaveEnt: s.type === "رسمی" ? 26 : 22,
        annualLeaveUsed: s.leaveUsed ?? rint(2, 14),
      },
    });
    empIds.push({ id: emp.id, dept: s.dept, shiftId: s.shift, type: s.type, posCode: s.pos });
    void gender;
  }

  // Guards hired later
  // ---------- Contracts & Decrees ----------
  const today = new Date();
  for (let i = 0; i < empIds.length; i++) {
    const e = empIds[i];
    const spec = specs[i];
    const start = addDays(today, -365 * spec.hireYearsAgo);
    let cType = "دائم";
    let end: Date | null = null;
    if (spec.type === "قراردادی") {
      cType = "موقت";
      end = addDays(start, 365);
      if (end < today) end = addDays(today, rint(10, 300));
    } else if (spec.type === "پیمانکاری") {
      cType = "پروژه‌ای";
      end = addDays(today, rint(-5, 120));
    } else if (spec.hireYearsAgo <= 1) {
      cType = "آزمایشی";
      end = addDays(start, 90);
    }
    await db.contract.create({
      data: {
        employeeId: e.id,
        type: cType,
        contractNo: `CT-${1404}-${String(100 + i)}`,
        startDate: start,
        endDate: end,
        salary: spec.salary,
        status: end && end < today ? "منقضی" : "فعال",
      },
    });
    await db.decree.create({
      data: {
        employeeId: e.id,
        type: "استخدام",
        decreeNo: `DEC-${start.getFullYear()}-${String(500 + i)}`,
        date: start,
        content: "احکام استخدام اولیه صادر و ابلاغ گردید.",
        acknowledged: rand() > 0.2,
      },
    });
    if (spec.hireYearsAgo >= 5 && rand() > 0.4) {
      await db.decree.create({
        data: {
          employeeId: e.id,
          type: "ترفیع",
          decreeNo: `DEC-1403-${String(700 + i)}`,
          date: addDays(today, -rint(60, 700)),
          content: "در اجرای ماده ۲۹ آیین‌نامه کارگزینی به رتبه بالاتر ارتقا یافت.",
          acknowledged: rand() > 0.3,
        },
      });
    }
  }

  // ---------- Attendance records (last 14 days) ----------
  const shifts = { sAdmin, sMorning, sEvening, sNight, s12, sCycle };
  const shiftParams: Record<string, { startTime: string; endTime: string; breakMinutes: number; toleranceLate: number }> = {
    [sAdmin.id]: { startTime: sAdmin.startTime, endTime: sAdmin.endTime, breakMinutes: sAdmin.breakMinutes, toleranceLate: sAdmin.toleranceLate },
    [sMorning.id]: { startTime: sMorning.startTime, endTime: sMorning.endTime, breakMinutes: sMorning.breakMinutes, toleranceLate: sMorning.toleranceLate },
    [sEvening.id]: { startTime: sEvening.startTime, endTime: sEvening.endTime, breakMinutes: sEvening.breakMinutes, toleranceLate: sEvening.toleranceLate },
    [sNight.id]: { startTime: sNight.startTime, endTime: sNight.endTime, breakMinutes: sNight.breakMinutes, toleranceLate: sNight.toleranceLate },
    [s12.id]: { startTime: s12.startTime, endTime: s12.endTime, breakMinutes: s12.breakMinutes, toleranceLate: s12.toleranceLate },
    [sCycle.id]: { startTime: sCycle.startTime, endTime: sCycle.endTime, breakMinutes: sCycle.breakMinutes, toleranceLate: sCycle.toleranceLate },
  };
  const devIds = [dev1.id, dev3.id, dev5.id];

  // leave days for some employees (approved)
  const leavePlan: { empIdx: number; fromOffset: number; days: number; type: string }[] = [
    { empIdx: 2, fromOffset: -2, days: 3, type: "استحقاقی" },
    { empIdx: 12, fromOffset: -1, days: 2, type: "استعلاجی" },
    { empIdx: 19, fromOffset: 1, days: 4, type: "استحقاقی" },
    { empIdx: 25, fromOffset: -4, days: 5, type: "بدون‌حقوق" },
  ];

  for (let off = -13; off <= 0; off++) {
    const day = addDays(today, off);
    const dk = dateKey(day);
    const friday = day.getDay() === 5;
    for (let i = 0; i < empIds.length; i++) {
      const e = empIds[i];
      if (!e.shiftId) continue;
      const sp = shiftParams[e.shiftId];

      const onLeave = leavePlan.find((l) => l.empIdx === i && off >= l.fromOffset && off < l.fromOffset + l.days);
      if (friday) {
        if (rand() > 0.75) {
          // تعطیل‌کاری برخی واحدهای عملیاتی
        } else {
          continue;
        }
      }
      if (onLeave) {
        await db.attendanceRecord.create({
          data: { employeeId: e.id, date: dk, status: onLeave.type === "استعلاجی" ? "مرخصی" : "مرخصی", note: onLeave.type, workMinutes: 0 },
        });
        continue;
      }
      const roll = rand();
      if (roll < 0.03) {
        await db.attendanceRecord.create({ data: { employeeId: e.id, date: dk, status: "غایب", workMinutes: 0 } });
        continue;
      }
      const [sh, sm] = sp.startTime.split(":").map(Number);
      const [eh, em] = sp.endTime.split(":").map(Number);
      const jitterIn = rint(-6, 22);
      const jitterOut = rint(-14, 85);
      const inD = new Date(day);
      inD.setHours(sh, sm + jitterIn, rint(0, 59), 0);
      if (jitterIn < 0) inD.setMinutes(inD.getMinutes());
      const outD = new Date(day);
      outD.setHours(eh, em + jitterOut, rint(0, 59), 0);
      if (eh < sh) outD.setDate(outD.getDate() + 1);
      const w = computeWork(sp, inD, outD, dk);
      await db.attendanceRecord.create({
        data: {
          employeeId: e.id,
          deviceId: pick(devIds),
          date: dk,
          checkIn: inD,
          checkOut: outD,
          workMinutes: w.workMinutes,
          lateMinutes: w.lateMinutes,
          earlyMinutes: w.earlyMinutes,
          overtimeMinutes: w.overtimeMinutes,
          status: w.lateMinutes > 0 ? "تأخیر" : "حاضر",
        },
      });
    }
  }

  // ---------- Leave / Mission / Permit requests ----------
  const mkLeave = (i: number, type: string, fOff: number, tOff: number, days: number, status: string, reason: string) =>
    db.leaveRequest.create({
      data: {
        employeeId: empIds[i].id, type,
        fromDate: dateKey(addDays(today, fOff)), toDate: dateKey(addDays(today, tOff)),
        days, reason, status, approver: status !== "در انتظار تأیید" ? "مریم تهرانی" : null,
      },
    });
  await mkLeave(6, "استحقاقی", 2, 4, 3, "در انتظار تأیید", "سفر خانوادگی");
  await mkLeave(13, "ساعتی", 0, 0, 0.5, "در انتظار تأیید", "مراجعه به بانک");
  await mkLeave(20, "استعلاجی", -1, 0, 2, "تأیید شده", "گواهی پزشکی پیوست است");
  await mkLeave(24, "استحقاقی", 5, 9, 5, "در انتظار تأیید", "مراسم عروسی");
  await mkLeave(9, "بدون‌حقوق", -6, -5, 2, "تأیید شده", "امور شخصی");
  await mkLeave(16, "زایمان", 8, 200, 90, "تأیید شده", "مرخصی زایمان طبق قانون کار");

  const mkMission = (i: number, type: string, dest: string, fOff: number, tOff: number, status: string, purpose: string) =>
    db.mission.create({
      data: {
        employeeId: empIds[i].id, type, destination: dest,
        fromDate: dateKey(addDays(today, fOff)), toDate: dateKey(addDays(today, tOff)),
        purpose, status, approver: status !== "در انتظار تأیید" ? "ناصر زارعی" : null,
      },
    });
  await mkMission(22, "داخلی", "تهران — شرکت پتروتجارت", 1, 2, "تأیید شده", "مذاکره قرارداد خرید کاتالیست");
  await mkMission(25, "خارجی", "دبی — نمایشگاه ADIPEC", 10, 14, "در انتظار تأیید", "شرکت در نمایشگاه نفت و گاز");
  await mkMission(17, "داخلی", "بندرعباس — اسکله", -2, -1, "تأیید شده", "هماهنگی حمل محموله");

  const mkPermit = (i: number, type: string, dOff: number, from: string, to: string, status: string, reason: string) =>
    db.overtimePermit.create({
      data: {
        employeeId: empIds[i].id, type, date: dateKey(addDays(today, dOff)),
        fromTime: from, toTime: to, reason, status,
        approver: status !== "در انتظار تأیید" ? "فرهاد سلطانی" : null,
      },
    });
  await mkPermit(11, "اضافه‌کاری", 0, "16:00", "20:00", "تأیید شده", "توقف خط تولید — تعمیر اضطراری");
  await mkPermit(3, "اضافه‌کاری", 1, "16:00", "19:00", "در انتظار تأیید", "بستن پرونده حقوق ماهانه");
  await mkPermit(23, "کار در تعطیل", 2, "08:00", "14:00", "در انتظار تأیید", "کالیبراسیون تجهیزات در تعطیل");
  await mkPermit(26, "اضافه‌کاری", -3, "16:00", "21:30", "تأیید شده", "آماده‌سازی ممیزی ISO");

  // ---------- Contractors (VIS-03) ----------
  const c1 = await db.contractor.create({ data: { name: "شرکت صنعت‌گستر بوشهر", code: "CTR-101", contactName: "عبدالرضا جدی", phone: "09171112233", contractNo: "CN-1404-88", validUntil: addDays(today, 45) } });
  const c2 = await db.contractor.create({ data: { name: "شرکت ایمن‌سازان پارس", code: "CTR-102", contactName: "مجید خوش‌نیت", phone: "09122223344", contractNo: "CN-1404-91", validUntil: addDays(today, 12) } });
  const c3 = await db.contractor.create({ data: { name: "شرکت نگین تعمیرات صنعتی", code: "CTR-103", contactName: "قادر ملکی", phone: "09133334455", contractNo: "CN-1403-77", validUntil: addDays(today, -8) } });

  // ---------- Access permissions (AC-02) ----------
  const daysWeek = "0,1,2,3,4"; // یکشنبه تا پنجشنبه
  const allWeek = "0,1,2,3,4,5,6";
  const mkPerm = (empIdx: number, zoneId: string, type: string, days: string, tf: string | null, tu: string | null) =>
    db.accessPermission.create({
      data: {
        employeeId: empIds[empIdx].id, zoneId, type, days,
        timeFrom: tf, timeUntil: tu,
        validFrom: addDays(today, -30), validUntil: type === "موقت" ? addDays(today, rint(5, 60)) : null,
        status: "فعال",
      },
    });
  // همه: ورودی اصلی و دفتر
  for (let i = 0; i < empIds.length; i++) {
    await mkPerm(i, zMain.id, "دائم", allWeek, null, null);
  }
  for (let i = 0; i < empIds.length; i++) {
    if ([4, 5, 6, 7, 9, 11, 12, 13, 14, 15, 16, 17, 22, 23].includes(i)) continue; // عملیاتی/حراست به دفتر نیاز کمتر
    await mkPerm(i, zOffice.id, "دائم", daysWeek, "07:00", "19:00");
  }
  // تولید
  await mkPerm(9, zProd.id, "دائم", allWeek, "06:00", "16:00");
  await mkPerm(10, zProd.id, "دائم", allWeek, "14:00", "23:59");
  for (const i of [11, 12, 13, 16, 17]) await mkPerm(i, zProd.id, "دائم", allWeek, null, null);
  for (const i of [14, 15]) await mkPerm(i, zProd.id, "دائم", allWeek, "06:30", "19:30");
  // انبار
  for (const i of [17, 22, 23]) await mkPerm(i, zStore.id, "دائم", daysWeek, "07:00", "17:00");
  // انبار مواد شیمیایی (فوق‌حساس)
  await mkPerm(22, zChem.id, "دائم", daysWeek, "08:00", "16:00");
  await mkPerm(26, zChem.id, "دائم", daysWeek, "08:00", "16:00");
  await mkPerm(27, zChem.id, "موقت", daysWeek, "09:00", "14:00");
  // مدیرعامل دسترسی کامل
  await mkPerm(0, zProd.id, "دائم", allWeek, null, null);
  await mkPerm(0, zChem.id, "دائم", allWeek, null, null);
  await mkPerm(0, zStore.id, "دائم", allWeek, null, null);

  // ---------- Access events (last 2 days) + alerts ----------
  const gatesList = [gMain, gMainOut, gEast, gOlefin, gPoly, gOffice, gChem];
  const methods = ["کارت", "اثر انگشت", "چهره", "QR"];
  for (let off = -1; off <= 0; off++) {
    const day = addDays(today, off);
    const baseHour = off === 0 ? new Date().getHours() : 18;
    for (let k = 0; k < 55; k++) {
      const empIdx = rint(0, empIds.length - 1);
      const gate = pick(gatesList);
      const dir = rand() > 0.5 ? "ورود" : "خروج";
      const method = pick(methods);
      const denied = rand() < 0.07;
      const t = new Date(day);
      t.setHours(Math.max(1, rint(1, Math.max(2, baseHour))), rint(0, 59), rint(0, 59), 0);

      let result = "مجاز";
      let reason: string | null = null;
      let employeeId: string | null = empIds[empIdx].id;

      if (denied) {
        if (rand() < 0.5) {
          result = "غیرمجاز";
          reason = "فاقد مجوز دسترسی به این منطقه";
        } else {
          result = "غیرمجاز";
          reason = "خارج از ساعات مجاز";
        }
      }
      const ev = await db.accessEvent.create({
        data: { employeeId, gateId: gate.id, direction: dir, method, result, reason, timestamp: t },
      });
      if (result === "غیرمجاز" && off === 0) {
        const emp = await db.employee.findUnique({ where: { id: employeeId! } });
        await db.securityAlert.create({
          data: {
            type: "ورود غیرمجاز",
            severity: reason === "فاقد مجوز دسترسی به این منطقه" ? "بحرانی" : "بالا",
            message: `تلاش ${dir} غیرمجاز توسط ${emp?.firstName ?? ""} ${emp?.lastName ?? ""} در ${gate.name} (${reason})`,
            source: "کنترل تردد",
            refId: ev.id,
          },
        });
      }
    }
  }

  // ---------- Visitors (VIS-01/02/04/07) ----------
  const mkVisitor = async (o: {
    first: string; last: string; type: string; host: string; hostDept: string;
    purpose: string; status: string; contractorId?: string;
    approvedBy?: string; card?: boolean; entered?: boolean; exited?: boolean;
    hoursIn: number; doc?: string;
  }) => {
    const v = await db.visitor.create({
      data: {
        firstName: o.first, lastName: o.last,
        nationalId: String(rint(1000000000, 9999999999)),
        phone: `091${rint(0, 9)}${String(rint(1000000, 9999999))}`,
        idDocType: o.doc ?? "کارت ملی",
        type: o.type, hostName: o.host, hostDept: o.hostDept,
        purpose: o.purpose, status: o.status,
        contractorId: o.contractorId ?? null,
        approvedBy: o.approvedBy ?? null,
        requestedAt: new Date(Date.now() - rint(1, 300) * 60000),
      },
    });
    if (o.card && (o.status === "داخل سازمان" || o.status === "خارج شده")) {
      await db.visitorCard.create({
        data: {
          cardNo: `VC-${rint(10000, 99999)}`,
          type: pick(["QR", "بارکد", "RFID"]),
          visitorId: v.id,
          validUntil: new Date(Date.now() + 8 * 3600000),
          returned: o.status === "خارج شده",
        },
      });
    }
    if (o.entered) {
      await db.visitLog.create({
        data: { visitorId: v.id, direction: "ورود", gateName: "گیت اصلی ورودی", timestamp: new Date(Date.now() - o.hoursIn * 3600000) },
      });
    }
    if (o.exited) {
      await db.visitLog.create({
        data: { visitorId: v.id, direction: "خروج", gateName: "گیت اصلی خروجی", timestamp: new Date(Date.now() - o.hoursIn * 1800000) },
      });
    }
    return v;
  };

  await mkVisitor({ first: "اسماعیل", last: "توکلی", type: "مراجع", host: "سعید قاسمی", hostDept: "منابع انسانی", purpose: "پیگیری پرونده بیمه", status: "در انتظار تأیید", hoursIn: 0 });
  await mkVisitor({ first: "هدیه", last: "اسدی", type: "مهمان", host: "مریم تهرانی", hostDept: "منابع انسانی", purpose: "جلسه همکاری آموزشی", status: "تأیید شده", approvedBy: "حسین بهرامی", hoursIn: 0 });
  await mkVisitor({ first: "کوروش", last: "جهانگیری", type: "پیمانکار", host: "فرهاد سلطانی", hostDept: "تولید و بهره‌برداری", purpose: "سرویس کمپرسور C-201", status: "داخل سازمان", contractorId: c1.id, approvedBy: "حسین بهرامی", card: true, entered: true, hoursIn: 3 });
  await mkVisitor({ first: "ژیار", last: "مرادی", type: "پیمانکار", host: "فرهاد سلطانی", hostDept: "تولید و بهره‌برداری", purpose: "آچاربند خط لوله", status: "داخل سازمان", contractorId: c2.id, approvedBy: "حسین بهرامی", card: true, entered: true, hoursIn: 2 });
  await mkVisitor({ first: "نرگس", last: "خسروی", type: "مهمان", host: "مسعود اکبری", hostDept: "مالی و اداری", purpose: "ممیزی حسابداری", status: "خارج شده", approvedBy: "حسین بهرامی", card: true, entered: true, exited: true, hoursIn: 6 });
  await mkVisitor({ first: "بهمن", last: "صالحی", type: "مراجع", host: "ناصر زارعی", hostDept: "فنی و مهندسی", purpose: "تحویل نمونه آزمایش", status: "خارج شده", approvedBy: "حسین بهرامی", card: true, entered: true, exited: true, hoursIn: 5 });
  await mkVisitor({ first: "روح‌الله", last: "بدری", type: "پیمانکار", host: "بهزاد شریفی", hostDept: "فنی و مهندسی", purpose: "تعمیر فن کلین‌تاور", status: "تأیید شده", contractorId: c3.id, approvedBy: "حسین بهرامی", hoursIn: 0 });
  await mkVisitor({ first: "شهاب", last: "لطفی", type: "مراجع", host: "سعید قاسمی", hostDept: "منابع انسانی", purpose: "بدون هماهنگی قبلی", status: "رد شده", hoursIn: 0 });

  // ---------- Extra security alerts ----------
  await db.securityAlert.create({
    data: {
      type: "تأخیر خروج",
      severity: "متوسط",
      message: "مهمان «کوروش جهانگیری» بیش از ۸ ساعت داخل سایت است — بررسی خروج لازم است",
      source: "ارباب رجوع",
    },
  });
  await db.securityAlert.create({
    data: {
      type: "مجوز منقضی",
      severity: "بالا",
      message: "اعتبار قرارداد شرکت نگین تعمیرات صنعتی منقضی شده است (CTR-103)",
      source: "ارباب رجوع",
    },
  });
  await db.securityAlert.create({
    data: {
      type: "کارت گمشده",
      severity: "متوسط",
      message: "کارت شماره VC-48213 به‌عنوان مفقود اعلام شد — ابطال گردید",
      source: "ارباب رجوع",
    },
  });

  // ---------- Correction requests (PLAN P4.3) ----------
  const corrSpecs = [
    { idx: 11, reason: "فراموشی زدن کارت هنگام ورود صبحگاهی", status: "در انتظار تأیید" },
    { idx: 12, reason: "قطعی برق واحد — ثبت نشدن خروج", status: "در انتظار تأیید" },
    { idx: 21, reason: "کارکرد دستگاه انبار خراب بود", status: "تأیید شده" },
    { idx: 22, reason: "مأموریت اداری خارج از سایت", status: "رد شده" },
  ];
  for (const c of corrSpecs) {
    const day = addDays(today, -1);
    const dk = dateKey(day);
    await db.correctionRequest.create({
      data: {
        employeeId: empIds[c.idx].id,
        date: dk,
        requestedIn: new Date(day.setHours(7, 55, 0, 0)),
        requestedOut: new Date(day.setHours(16, 5, 0, 0)),
        reason: c.reason,
        status: c.status,
        reviewedBy: c.status !== "در انتظار تأیید" ? "مریم تهرانی" : null,
      },
    });
  }
}

// ============================================================
// RBAC + حساب‌های کاربری (PLAN Phase 0/1) — idempotent و مستقل از seed کسب‌وکار
// ============================================================
const gRbac = globalThis as unknown as { __rbacPromise?: Promise<void> };

export async function ensureRbac(): Promise<void> {
  if (!gRbac.__rbacPromise) {
    gRbac.__rbacPromise = (async () => {
      const { PERMISSIONS, ROLES } = await import("./permissions");
      const { hashPassword } = await import("./auth");

      // کاتالوگ دسترسی‌ها
      for (const p of PERMISSIONS) {
        await db.permission.upsert({
          where: { key: p.key },
          update: { title: p.title, grp: p.grp },
          create: { key: p.key, title: p.title, grp: p.grp },
        });
      }
      const allPerms = await db.permission.findMany();
      const permIdByKey = new Map(allPerms.map((p) => [p.key, p.id]));

      // نقش‌ها + اتصال دسترسی‌ها
      const roleIdByName = new Map<string, number>();
      for (const r of ROLES) {
        const role = await db.role.upsert({
          where: { name: r.name },
          update: { title: r.title, description: r.description },
          create: { name: r.name, title: r.title, description: r.description },
        });
        roleIdByName.set(r.name, role.id);
        await db.rolePermission.deleteMany({ where: { roleId: role.id } });
        const keys = r.perms === "*" ? [...permIdByKey.keys()] : r.perms;
        const rows = keys
          .map((k) => permIdByKey.get(k))
          .filter((id): id is number => !!id)
          .map((permissionId) => ({ roleId: role.id, permissionId }));
        if (rows.length) await db.rolePermission.createMany({ data: rows });
      }

      // کاربران پیش‌فرض (در صورت نبود)
      const users: { username: string; password: string; fullName: string; roles: string[]; sa?: boolean; employeeName?: string }[] = [
        { username: "admin", password: "Admin@1234", fullName: "مدیر ارشد سیستم", roles: ["superadmin"], sa: true, employeeName: "کامران رستمی" },
        { username: "hr", password: "Hr@123456", fullName: "مریم تهرانی", roles: ["hr_manager"], employeeName: "مریم تهرانی" },
        { username: "operator", password: "Oper@1234", fullName: "رضا کریمی", roles: ["att_operator"], employeeName: "رضا کریمی" },
        { username: "guard", password: "Guard@1234", fullName: "حسین بهرامی", roles: ["security"], employeeName: "حسین بهرامی" },
        { username: "viewer", password: "View@1234", fullName: "کارشناس مشاهده‌گر", roles: ["viewer"] },
        { username: "svc_bridge", password: "Bridge@1234", fullName: "سرویس پل دستگاه‌ها", roles: ["device_agent"] },
      ];
      for (const u of users) {
        const exists = await db.user.findUnique({ where: { username: u.username } });
        if (exists) continue;
        let employeeId: string | null = null;
        if (u.employeeName) {
          const [fn, ...rest] = u.employeeName.split(" ");
          const emp = await db.employee.findFirst({
            where: { firstName: fn, lastName: rest.join(" ") },
            select: { id: true },
          });
          employeeId = emp?.id ?? null;
        }
        const hash = hashPassword(u.password);
        await db.user.create({
          data: {
            username: u.username,
            passwordHash: hash,
            fullName: u.fullName,
            isSuperadmin: !!u.sa,
            employeeId,
            passwordHistory: { create: { passwordHash: hash } },
            roles: {
              create: u.roles
                .map((n) => roleIdByName.get(n))
                .filter((id): id is number => !!id)
                .map((roleId) => ({ roleId })),
            },
          },
        });
      }

      // تعطیلات رسمی ۱۴۰۴ و ۱۴۰۵ (تقویم رسمی ایران — نمونه اصلی)
      const { jalaliToGregorian } = await import("./persian");
      const HOLIDAYS: [number, number, number, string][] = [
        [1404, 1, 1, "نوروز"], [1404, 1, 2, "نوروز"], [1404, 1, 3, "نوروز"], [1404, 1, 4, "نوروز"],
        [1404, 1, 12, "روز جمهوری اسلامی"], [1404, 1, 13, "روز طبیعت"],
        [1404, 3, 14, "رحلت امام خمینی"], [1404, 3, 15, "قیام ۱۵ خرداد"],
        [1404, 11, 22, "پیروزی انقلاب اسلامی"], [1404, 12, 29, "ملی شدن صنعت نفت"],
        [1405, 1, 1, "نوروز"], [1405, 1, 2, "نوروز"], [1405, 1, 3, "نوروز"], [1405, 1, 4, "نوروز"],
        [1405, 1, 12, "روز جمهوری اسلامی"], [1405, 1, 13, "روز طبیعت"],
        [1405, 11, 22, "پیروزی انقلاب اسلامی"], [1405, 12, 29, "ملی شدن صنعت نفت"],
      ];
      for (const [jy, jm, jd, title] of HOLIDAYS) {
        const g = jalaliToGregorian(jy, jm, jd);
        const dateKeyStr = `${g.getFullYear()}-${String(g.getMonth() + 1).padStart(2, "0")}-${String(g.getDate()).padStart(2, "0")}`;
        await db.holiday.upsert({
          where: { date: dateKeyStr },
          update: { title },
          create: { date: dateKeyStr, title, jYear: jy, jMonth: jm, jDay: jd },
        });
      }
    })().catch((e) => {
      gRbac.__rbacPromise = undefined;
      throw e;
    });
  }
  return gRbac.__rbacPromise;
}
