import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { audit, getSession, guard, ipOf } from "@/lib/auth";
import { jalaliMonthRange } from "@/lib/persian";

export const dynamic = "force-dynamic";

const INSURANCE_RATE = 0.07; // سهم کارگر بیمه
const TAX_EXEMPT = 24_000_000; // معافیت ماهانه (دمو — پارامتریک از تنظیمات)
const TAX_RATE = 0.1;
const OVERTIME_FACTOR = 1.4; // ضریب قانون کار
const MONTH_DAYS = 30;

export async function GET(req: Request) {
  const denied = await guard(req, "payroll.view");
  if (denied) return denied;
  await ensureSeed();
  const runs = await db.payrollRun.findMany({
    include: { _count: { select: { payslips: true } } },
    orderBy: [{ jYear: "desc" }, { jMonth: "desc" }],
  });
  return NextResponse.json({ runs });
}

/** محاسبه و ایجاد لیست حقوق یک ماه جلالی (PLAN P4.1) */
export async function POST(req: Request) {
  const denied = await guard(req, "payroll.manage");
  if (denied) return denied;
  const session = await getSession(req);
  try {
    const body = (await req.json()) as {
      jYear?: number;
      jMonth?: number;
      note?: string;
      allowancesPerEmployee?: number;
    };
    const jYear = Number(body.jYear);
    const jMonth = Number(body.jMonth);
    if (!jYear || !jMonth || jMonth < 1 || jMonth > 12)
      return NextResponse.json({ error: "سال و ماه جلالی معتبر الزامی است" }, { status: 400 });

    const dup = await db.payrollRun.findUnique({
      where: { jYear_jMonth: { jYear, jMonth } },
    });
    if (dup)
      return NextResponse.json(
        { error: "برای این ماه قبلاً لیست حقوق ایجاد شده است" },
        { status: 409 }
      );

    const { start, end } = jalaliMonthRange(jYear, jMonth);
    const allowances = Number(body.allowancesPerEmployee) || 0;

    const employees = await db.employee.findMany({
      where: { status: "فعال" },
      include: { shift: true },
    });

    const attendance = await db.attendanceRecord.findMany({
      where: { checkIn: { gte: start, lt: end } },
      select: {
        employeeId: true,
        workMinutes: true,
        overtimeMinutes: true,
        status: true,
      },
    });

    const byEmployee = new Map<string, { work: number; ot: number; absent: number }>();
    for (const a of attendance) {
      const e = byEmployee.get(a.employeeId) ?? { work: 0, ot: 0, absent: 0 };
      e.work += a.workMinutes;
      e.ot += a.overtimeMinutes;
      if (a.status === "غایب") e.absent += 1;
      byEmployee.set(a.employeeId, e);
    }

    let totalGross = 0;
    let totalNet = 0;
    const payslipRows = employees.map((emp) => {
      const agg = byEmployee.get(emp.id) ?? { work: 0, ot: 0, absent: 0 };
      const base = emp.baseSalary;
      const hourly = base / 192; // ۲۴ روز × ۸ ساعت
      const overtimePay = Math.round((agg.ot / 60) * hourly * OVERTIME_FACTOR);
      const absentDeduct = Math.round((agg.absent * base) / MONTH_DAYS);
      const insurance = Math.round(base * INSURANCE_RATE);
      const gross = base + overtimePay + allowances;
      const taxable = Math.max(0, gross - absentDeduct);
      const tax = Math.round(Math.max(0, taxable - TAX_EXEMPT) * TAX_RATE);
      const net = Math.max(0, taxable - insurance - tax);
      totalGross += gross;
      totalNet += net;
      return {
        employeeId: emp.id,
        baseSalary: base,
        overtimePay,
        allowances,
        absentDeduct,
        insurance,
        tax,
        netPay: net,
        workedMin: agg.work,
        overtimeMin: agg.ot,
        absentDays: agg.absent,
        detail: JSON.stringify({
          hourly: Math.round(hourly),
          formula: "پایه + اضافه‌کاری(۱٫۴) + مزایا − غیبت − بیمه(۷٪) − مالیات(پلکانی ساده)",
          monthDays: MONTH_DAYS,
        }),
      };
    });

    const run = await db.payrollRun.create({
      data: {
        jYear,
        jMonth,
        note: body.note ?? null,
        status: "پیش‌نویس",
        totalGross,
        totalNet,
        employeeCount: payslipRows.length,
        createdBy: session?.fullName ?? null,
        payslips: { create: payslipRows },
      },
      include: { _count: { select: { payslips: true } } },
    });
    await audit(session, "create", "payroll_run", run.id, { jYear, jMonth, count: payslipRows.length }, ipOf(req));
    return NextResponse.json({ run }, { status: 201 });
  } catch (e) {
    console.error("[payroll POST]", e);
    return NextResponse.json({ error: "خطا در محاسبه لیست حقوق" }, { status: 500 });
  }
}
