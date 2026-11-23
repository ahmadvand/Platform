import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "attendance.view");
  if (denied) return denied;
  await ensureSeed();
  const permits = await db.overtimePermit.findMany({
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
  return NextResponse.json({ permits });
}

export async function POST(req: Request) {
  const denied = await guard(req, "attendance.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.employeeId || !body.date || !body.fromTime || !body.toTime)
      return NextResponse.json({ error: "پرسنل، تاریخ و بازه زمانی الزامی است" }, { status: 400 });
    const permit = await db.overtimePermit.create({
      data: {
        employeeId: body.employeeId,
        type: body.type || "اضافه‌کاری",
        date: body.date,
        fromTime: body.fromTime,
        toTime: body.toTime,
        reason: body.reason || null,
        status: "در انتظار تأیید",
      },
    });
    return NextResponse.json({ permit }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت مجوز" }, { status: 500 });
  }
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "attendance.approve");
  if (denied) return denied;
  try {
    const body = await req.json();
    const { id, action } = body;
    if (!id || !action) return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });
    if (action === "approve") {
      await db.overtimePermit.update({ where: { id }, data: { status: "تأیید شده", approver: "سرپرست واحد" } });
      return NextResponse.json({ ok: true });
    }
    if (action === "reject") {
      await db.overtimePermit.update({ where: { id }, data: { status: "رد شده", approver: "سرپرست واحد" } });
      return NextResponse.json({ ok: true });
    }
    return NextResponse.json({ error: "عملیات نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در پردازش مجوز" }, { status: 500 });
  }
}
