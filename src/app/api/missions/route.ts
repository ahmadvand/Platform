import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "attendance.view");
  if (denied) return denied;
  await ensureSeed();
  const missions = await db.mission.findMany({
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
  return NextResponse.json({ missions });
}

export async function POST(req: Request) {
  const denied = await guard(req, "attendance.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.employeeId || !body.destination || !body.fromDate)
      return NextResponse.json({ error: "پرسنل، مقصد و تاریخ شروع الزامی است" }, { status: 400 });
    const mission = await db.mission.create({
      data: {
        employeeId: body.employeeId,
        type: body.type || "داخلی",
        destination: body.destination,
        purpose: body.purpose || null,
        fromDate: body.fromDate,
        toDate: body.toDate || body.fromDate,
        status: "در انتظار تأیید",
      },
    });
    return NextResponse.json({ mission }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت مأموریت" }, { status: 500 });
  }
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "attendance.approve");
  if (denied) return denied;
  try {
    const body = await req.json();
    const { id, action } = body;
    if (!id || !action) return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });
    const mission = await db.mission.findUnique({ where: { id } });
    if (!mission) return NextResponse.json({ error: "مأموریت یافت نشد" }, { status: 404 });

    if (action === "approve") {
      await db.mission.update({ where: { id }, data: { status: "تأیید شده", approver: "مدیر واحد" } });
      const from = new Date(mission.fromDate + "T12:00:00");
      const to = new Date(mission.toDate + "T12:00:00");
      for (let d = new Date(from); d <= to; d = new Date(d.getTime() + 86400000)) {
        const dk = d.toISOString().slice(0, 10);
        const existing = await db.attendanceRecord.findUnique({
          where: { employeeId_date: { employeeId: mission.employeeId, date: dk } },
        });
        if (existing) {
          await db.attendanceRecord.update({ where: { id: existing.id }, data: { status: "مأموریت", note: mission.destination } });
        } else {
          await db.attendanceRecord.create({
            data: { employeeId: mission.employeeId, date: dk, status: "مأموریت", note: mission.destination },
          });
        }
      }
      return NextResponse.json({ ok: true });
    }
    if (action === "reject") {
      await db.mission.update({ where: { id }, data: { status: "رد شده", approver: "مدیر واحد" } });
      return NextResponse.json({ ok: true });
    }
    return NextResponse.json({ error: "عملیات نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در پردازش مأموریت" }, { status: 500 });
  }
}
