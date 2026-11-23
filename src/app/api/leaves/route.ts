import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { dateKey, addDays } from "@/lib/persian";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "attendance.view");
  if (denied) return denied;
  await ensureSeed();
  const leaves = await db.leaveRequest.findMany({
    include: {
      employee: {
        select: {
          firstName: true, lastName: true, personnelCode: true,
          department: { select: { name: true } },
        },
      },
    },
    orderBy: [{ status: "asc" }, { createdAt: "desc" }],
  });
  return NextResponse.json({ leaves });
}

export async function POST(req: Request) {
  const denied = await guard(req, "attendance.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.employeeId || !body.type || !body.fromDate)
      return NextResponse.json({ error: "پرسنل، نوع و تاریخ شروع الزامی است" }, { status: 400 });
    const leave = await db.leaveRequest.create({
      data: {
        employeeId: body.employeeId,
        type: body.type,
        fromDate: body.fromDate,
        toDate: body.toDate || body.fromDate,
        days: Number(body.days) || 1,
        hours: body.hours ? Number(body.hours) : null,
        reason: body.reason || null,
        status: "در انتظار تأیید",
      },
      include: { employee: { select: { firstName: true, lastName: true } } },
    });
    return NextResponse.json({ leave }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت درخواست مرخصی" }, { status: 500 });
  }
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "attendance.approve");
  if (denied) return denied;
  try {
    const body = await req.json();
    const { id, action } = body;
    if (!id || !action) return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });

    const leave = await db.leaveRequest.findUnique({ where: { id }, include: { employee: true } });
    if (!leave) return NextResponse.json({ error: "درخواست یافت نشد" }, { status: 404 });

    if (action === "approve") {
      await db.leaveRequest.update({
        where: { id },
        data: { status: "تأیید شده", approver: "مدیر منابع انسانی" },
      });
      // کسر از مانده مرخصی (ATT-04)
      if (leave.type === "استحقاقی") {
        await db.employee.update({
          where: { id: leave.employeeId },
          data: { annualLeaveUsed: { increment: leave.days } },
        });
      }
      // ثبت خودکار در کارکرد برای بازه مرخصی
      const from = new Date(leave.fromDate + "T12:00:00");
      const to = new Date(leave.toDate + "T12:00:00");
      for (let d = new Date(from); d <= to; d = addDays(d, 1)) {
        const dk = dateKey(d);
        const existing = await db.attendanceRecord.findUnique({
          where: { employeeId_date: { employeeId: leave.employeeId, date: dk } },
        });
        if (existing) {
          await db.attendanceRecord.update({ where: { id: existing.id }, data: { status: "مرخصی", note: leave.type } });
        } else {
          await db.attendanceRecord.create({
            data: { employeeId: leave.employeeId, date: dk, status: "مرخصی", note: leave.type },
          });
        }
      }
      return NextResponse.json({ ok: true });
    }

    if (action === "reject") {
      await db.leaveRequest.update({ where: { id }, data: { status: "رد شده", approver: "مدیر منابع انسانی" } });
      return NextResponse.json({ ok: true });
    }
    return NextResponse.json({ error: "عملیات نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در پردازش درخواست" }, { status: 500 });
  }
}
