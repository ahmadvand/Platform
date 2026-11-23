import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { guard } from "@/lib/auth";
import { formatJalali } from "@/lib/persian";
import * as XLSX from "xlsx";

export const dynamic = "force-dynamic";

/**
 * خروجی اکسل (PLAN P4.6) — types: employees | attendance | payroll | audit
 * ?type=...&from=&to=&runId=
 */
export async function GET(req: Request) {
  const denied = await guard(req, "reports.export");
  if (denied) return denied;
  const { searchParams } = new URL(req.url);
  const type = searchParams.get("type") || "employees";

  let rows: Record<string, string | number>[] = [];
  let filename = "export";

  if (type === "employees") {
    filename = "employees";
    const emps = await db.employee.findMany({
      include: {
        department: true, position: true, shift: true,
      },
      orderBy: { personnelCode: "asc" },
    });
    rows = emps.map((e) => ({
      "کد پرسنلی": e.personnelCode,
      نام: e.firstName,
      "نام خانوادگی": e.lastName,
      "کد ملی": e.nationalId,
      "نوع استخدام": e.employmentType,
      وضعیت: e.status,
      دپارتمان: e.department?.name ?? "—",
      "پست سازمانی": e.position?.title ?? "—",
      شیفت: e.shift?.name ?? "—",
      "تاریخ استخدام": formatJalali(e.hireDate),
      "حقوق پایه": e.baseSalary,
      "مانده مرخصی استحقاقی": e.annualLeaveEnt - e.annualLeaveUsed,
      "مانده استعلاجی": e.sickLeaveEnt - e.sickLeaveUsed,
      موبایل: e.phone ?? "—",
    }));
  } else if (type === "attendance") {
    filename = "attendance";
    const from = searchParams.get("from") || "";
    const to = searchParams.get("to") || "";
    const recs = await db.attendanceRecord.findMany({
      where: {
        ...(from || to ? { date: { ...(from ? { gte: from } : {}), ...(to ? { lte: to } : {}) } } : {}),
      },
      include: {
        employee: { select: { firstName: true, lastName: true, personnelCode: true, department: { select: { name: true } } } },
        device: { select: { name: true } },
      },
      orderBy: [{ date: "desc" }, { checkIn: "asc" }],
      take: 5000,
    });
    rows = recs.map((r) => ({
      "کد پرسنلی": r.employee.personnelCode,
      نام: `${r.employee.firstName} ${r.employee.lastName}`,
      دپارتمان: r.employee.department?.name ?? "—",
      تاریخ: r.date,
      "ورود": r.checkIn ? r.checkIn.toLocaleTimeString("fa-IR") : "—",
      "خروج": r.checkOut ? r.checkOut.toLocaleTimeString("fa-IR") : "—",
      "کارکرد (دقیقه)": r.workMinutes,
      "تأخیر (دقیقه)": r.lateMinutes,
      "اضافه‌کاری (دقیقه)": r.overtimeMinutes,
      وضعیت: r.status,
      دستگاه: r.device?.name ?? "—",
    }));
  } else if (type === "payroll") {
    filename = "payroll";
    const runId = searchParams.get("runId");
    if (!runId) return NextResponse.json({ error: "runId الزامی است" }, { status: 400 });
    const run = await db.payrollRun.findUnique({
      where: { id: runId },
      include: {
        payslips: {
          include: {
            employee: {
              select: { firstName: true, lastName: true, personnelCode: true, department: { select: { name: true } } },
            },
          },
        },
      },
    });
    if (!run) return NextResponse.json({ error: "لیست یافت نشد" }, { status: 404 });
    filename = `payroll-${run.jYear}-${String(run.jMonth).padStart(2, "0")}`;
    rows = run.payslips.map((p) => ({
      "کد پرسنلی": p.employee.personnelCode,
      نام: `${p.employee.firstName} ${p.employee.lastName}`,
      دپارتمان: p.employee.department?.name ?? "—",
      "حقوق پایه": p.baseSalary,
      "اضافه‌کاری": p.overtimePay,
      مزایا: p.allowances,
      "کسور غیبت": p.absentDeduct,
      بیمه: p.insurance,
      مالیات: p.tax,
      "خالص پرداختی": p.netPay,
      "کارکرد (دقیقه)": p.workedMin,
    }));
  } else if (type === "audit") {
    filename = "audit";
    const logs = await db.auditLog.findMany({ orderBy: { at: "desc" }, take: 3000 });
    rows = logs.map((l) => ({
      زمان: l.at.toLocaleString("fa-IR"),
      کاربر: l.actorName ?? "—",
      عملیات: l.action,
      موجودیت: l.entity,
      شناسه: l.entityId ?? "—",
      جزئیات: l.detail ?? "—",
      IP: l.ip ?? "—",
    }));
  } else {
    return NextResponse.json({ error: "نوع خروجی نامعتبر" }, { status: 400 });
  }

  const ws = XLSX.utils.json_to_sheet(rows);
  // عرض ستون‌ها بر اساس طول محتوا
  const colWidths = Object.keys(rows[0] ?? {}).map((k) => ({
    wch: Math.min(
      40,
      Math.max(k.length + 2, ...rows.slice(0, 100).map((r) => String(r[k] ?? "").length + 2))
    ),
  }));
  ws["!cols"] = colWidths;
  // راست‌به‌چپ برای فارسی
  ws["!views"] = [{ RTL: true }];

  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, "گزارش");
  const buf = XLSX.write(wb, { type: "buffer", bookType: "xlsx" }) as Buffer;

  return new NextResponse(new Uint8Array(buf), {
    headers: {
      "Content-Type": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      "Content-Disposition": `attachment; filename="${filename}.xlsx"`,
      "Cache-Control": "no-store",
    },
  });
}
