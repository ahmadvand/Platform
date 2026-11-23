import { NextResponse } from "next/server";
import { db } from "@/lib/db";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const denied = await guard(req, "employees.view");
  if (denied) return denied;
  const { id } = await params;
  const employee = await db.employee.findUnique({
    where: { id },
    include: {
      department: true,
      position: true,
      shift: true,
      company: true,
      contracts: { orderBy: { startDate: "desc" } },
      decrees: { orderBy: { date: "desc" } },
      leaves: { orderBy: { createdAt: "desc" }, take: 10 },
      attendance: { orderBy: { date: "desc" }, take: 14 },
      accessPerms: { include: { zone: true } },
    },
  });
  if (!employee) return NextResponse.json({ error: "پرسنل یافت نشد" }, { status: 404 });
  return NextResponse.json({ employee });
}

export async function PATCH(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const denied = await guard(req, "employees.manage");
  if (denied) return denied;
  const { id } = await params;
  try {
    const body = await req.json();
    const data: Record<string, unknown> = {};
    const fields = ["firstName", "lastName", "phone", "email", "employmentType", "status", "birthDate"];
    for (const f of fields) if (body[f] !== undefined) data[f] = body[f];
    if (body.departmentId !== undefined) data.departmentId = body.departmentId || null;
    if (body.positionId !== undefined) data.positionId = body.positionId || null;
    if (body.shiftId !== undefined) data.shiftId = body.shiftId || null;
    if (body.baseSalary !== undefined) data.baseSalary = Number(body.baseSalary) || 0;

    const employee = await db.employee.update({ where: { id }, data, include: { department: true, position: true, shift: true } });
    return NextResponse.json({ employee });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ویرایش پرسنل" }, { status: 500 });
  }
}

export async function DELETE(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const denied = await guard(req, "employees.manage");
  if (denied) return denied;
  const { id } = await params;
  try {
    // تسویه به‌جای حذف فیزیکی (Offboarding — HR-06)
    await db.employee.update({ where: { id }, data: { status: "تسویه‌شده", endDate: new Date() } });
    return NextResponse.json({ ok: true });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در تسویه پرسنل" }, { status: 500 });
  }
}
