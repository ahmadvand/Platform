import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "attendance.view");
  if (denied) return denied;
  await ensureSeed();
  const shifts = await db.shift.findMany({
    include: { employees: { where: { status: "فعال" }, select: { id: true, firstName: true, lastName: true } } },
    orderBy: { startTime: "asc" },
  });
  return NextResponse.json({ shifts });
}

export async function POST(req: Request) {
  const denied = await guard(req, "attendance.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.name || !body.startTime || !body.endTime)
      return NextResponse.json({ error: "نام و ساعت شروع/پایان الزامی است" }, { status: 400 });
    const shift = await db.shift.create({
      data: {
        name: body.name,
        type: body.type || "عادی",
        startTime: body.startTime,
        endTime: body.endTime,
        breakMinutes: Number(body.breakMinutes) || 60,
        cyclePattern: body.cyclePattern || null,
        toleranceLate: Number(body.toleranceLate) || 10,
      },
    });
    return NextResponse.json({ shift }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت شیفت" }, { status: 500 });
  }
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "attendance.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (body.action === "assign" && body.employeeId && body.shiftId !== undefined) {
      const emp = await db.employee.update({
        where: { id: body.employeeId },
        data: { shiftId: body.shiftId || null },
        include: { shift: true },
      });
      return NextResponse.json({ employee: emp });
    }
    if (body.id) {
      const data: Record<string, unknown> = {};
      for (const f of ["name", "type", "startTime", "endTime", "cyclePattern"]) if (body[f] !== undefined) data[f] = body[f];
      if (body.breakMinutes !== undefined) data.breakMinutes = Number(body.breakMinutes);
      if (body.toleranceLate !== undefined) data.toleranceLate = Number(body.toleranceLate);
      if (body.active !== undefined) data.active = Boolean(body.active);
      const shift = await db.shift.update({ where: { id: body.id }, data });
      return NextResponse.json({ shift });
    }
    return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در به‌روزرسانی شیفت" }, { status: 500 });
  }
}
