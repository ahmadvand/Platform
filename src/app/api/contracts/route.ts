import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "employees.view");
  if (denied) return denied;
  await ensureSeed();
  const contracts = await db.contract.findMany({
    include: { employee: { select: { firstName: true, lastName: true, personnelCode: true } } },
    orderBy: { startDate: "desc" },
  });
  return NextResponse.json({ contracts });
}

export async function POST(req: Request) {
  const denied = await guard(req, "employees.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.employeeId || !body.contractNo) return NextResponse.json({ error: "پرسنل و شماره قرارداد الزامی است" }, { status: 400 });
    const contract = await db.contract.create({
      data: {
        employeeId: body.employeeId,
        type: body.type || "موقت",
        contractNo: body.contractNo,
        startDate: body.startDate ? new Date(body.startDate) : new Date(),
        endDate: body.endDate ? new Date(body.endDate) : null,
        salary: Number(body.salary) || 0,
        status: "فعال",
      },
      include: { employee: { select: { firstName: true, lastName: true } } },
    });
    return NextResponse.json({ contract }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در صدور قرارداد" }, { status: 500 });
  }
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "employees.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.id) return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });
    const data: Record<string, unknown> = {};
    if (body.status) data.status = body.status;
    if (body.endDate) data.endDate = new Date(body.endDate);
    const contract = await db.contract.update({ where: { id: body.id }, data });
    return NextResponse.json({ contract });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در به‌روزرسانی قرارداد" }, { status: 500 });
  }
}
