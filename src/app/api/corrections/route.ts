import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { audit, getSession, guard, ipOf } from "@/lib/auth";
import { computeWork, statusFromPunch } from "@/lib/att";

export const dynamic = "force-dynamic";

export async function GET(req: Request) {
  const denied = await guard(req, "corrections.view");
  if (denied) return denied;
  await ensureSeed();
  const { searchParams } = new URL(req.url);
  const status = searchParams.get("status") || "";

  const corrections = await db.correctionRequest.findMany({
    where: status ? { status } : {},
    include: {
      employee: {
        select: {
          id: true, firstName: true, lastName: true, personnelCode: true,
          department: { select: { name: true } },
        },
      },
    },
    orderBy: { createdAt: "desc" },
    take: 300,
  });
  return NextResponse.json({ corrections });
}

export async function POST(req: Request) {
  const denied = await guard(req, "attendance.manage");
  if (denied) return denied;
  const session = await getSession(req);
  try {
    const body = (await req.json()) as {
      employeeId?: string;
      date?: string;
      requestedIn?: string;
      requestedOut?: string;
      reason?: string;
    };
    if (!body.employeeId || !body.date || !body.reason)
      return NextResponse.json(
        { error: "پرسنل، تاریخ و دلیل اصلاح الزامی است" },
        { status: 400 }
      );
    const employee = await db.employee.findUnique({ where: { id: body.employeeId } });
    if (!employee) return NextResponse.json({ error: "پرسنل یافت نشد" }, { status: 404 });

    const correction = await db.correctionRequest.create({
      data: {
        employeeId: body.employeeId,
        date: body.date,
        requestedIn: body.requestedIn ? new Date(body.requestedIn) : null,
        requestedOut: body.requestedOut ? new Date(body.requestedOut) : null,
        reason: body.reason,
      },
    });
    await audit(session, "create", "correction", correction.id, { employee: employee.personnelCode }, ipOf(req));
    return NextResponse.json({ correction }, { status: 201 });
  } catch (e) {
    console.error("[corrections POST]", e);
    return NextResponse.json({ error: "خطا در ثبت درخواست اصلاح تردد" }, { status: 500 });
  }
}

/** تأیید/رد — تأیید، رکورد حضور روز را واقعاً اصلاح می‌کند */
export async function PATCH(req: Request) {
  const denied = await guard(req, "corrections.approve");
  if (denied) return denied;
  const session = await getSession(req);
  try {
    const body = (await req.json()) as { id?: string; action?: "approve" | "reject"; reviewNote?: string };
    if (!body.id || !body.action)
      return NextResponse.json({ error: "پارامترهای ناقص" }, { status: 400 });
    const correction = await db.correctionRequest.findUnique({ where: { id: body.id } });
    if (!correction) return NextResponse.json({ error: "درخواست یافت نشد" }, { status: 404 });
    if (correction.status !== "در انتظار تأیید")
      return NextResponse.json({ error: "این درخواست قبلاً بررسی شده است" }, { status: 409 });

    if (body.action === "approve") {
      // اعمال اصلاح روی رکورد حضور
      const employee = await db.employee.findUnique({
        where: { id: correction.employeeId },
        include: { shift: true },
      });
      if (employee) {
        let record = await db.attendanceRecord.findUnique({
          where: { employeeId_date: { employeeId: employee.id, date: correction.date } },
        });
        const checkIn = correction.requestedIn ?? record?.checkIn ?? null;
        const checkOut = correction.requestedOut ?? record?.checkOut ?? null;
        const sp = employee.shift
          ? {
              startTime: employee.shift.startTime,
              endTime: employee.shift.endTime,
              breakMinutes: employee.shift.breakMinutes,
              toleranceLate: employee.shift.toleranceLate,
            }
          : null;
        const w = computeWork(sp, checkIn, checkOut, correction.date);
        if (!record) {
          record = await db.attendanceRecord.create({
            data: {
              employeeId: employee.id,
              date: correction.date,
              checkIn,
              checkOut,
              workMinutes: w.workMinutes,
              lateMinutes: w.lateMinutes,
              earlyMinutes: w.earlyMinutes,
              overtimeMinutes: w.overtimeMinutes,
              status: statusFromPunch(w.lateMinutes, !!checkIn),
            },
          });
        } else {
          await db.attendanceRecord.update({
            where: { id: record.id },
            data: {
              checkIn,
              checkOut,
              workMinutes: w.workMinutes,
              lateMinutes: w.lateMinutes,
              earlyMinutes: w.earlyMinutes,
              overtimeMinutes: w.overtimeMinutes,
              status: record.status === "غایب" ? statusFromPunch(w.lateMinutes, !!checkIn) : record.status,
            },
          });
        }
      }
    }

    const updated = await db.correctionRequest.update({
      where: { id: correction.id },
      data: {
        status: body.action === "approve" ? "تأیید شده" : "رد شده",
        reviewedBy: session?.fullName ?? null,
        reviewNote: body.reviewNote ?? null,
      },
    });
    await audit(session, body.action === "approve" ? "approve" : "reject", "correction", correction.id, null, ipOf(req));
    return NextResponse.json({ correction: updated });
  } catch (e) {
    console.error("[corrections PATCH]", e);
    return NextResponse.json({ error: "خطا در بررسی درخواست" }, { status: 500 });
  }
}
