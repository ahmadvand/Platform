import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "employees.view");
  if (denied) return denied;
  await ensureSeed();
  const decrees = await db.decree.findMany({
    include: { employee: { select: { firstName: true, lastName: true, personnelCode: true } } },
    orderBy: { date: "desc" },
  });
  return NextResponse.json({ decrees });
}

export async function POST(req: Request) {
  const denied = await guard(req, "employees.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.employeeId || !body.type) return NextResponse.json({ error: "پرسنل و نوع حکم الزامی است" }, { status: 400 });
    const last = await db.decree.findFirst({ orderBy: { createdAt: "desc" } });
    const no = `DEC-1404-${String(Math.floor(Math.random() * 9000) + 1000)}`;
    void last;
    const decree = await db.decree.create({
      data: {
        employeeId: body.employeeId,
        type: body.type,
        decreeNo: body.decreeNo || no,
        date: body.date ? new Date(body.date) : new Date(),
        content: body.content || null,
        acknowledged: false,
      },
      include: { employee: { select: { firstName: true, lastName: true } } },
    });
    return NextResponse.json({ decree }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در صدور حکم" }, { status: 500 });
  }
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "employees.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.id) return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });
    const decree = await db.decree.update({ where: { id: body.id }, data: { acknowledged: true } });
    return NextResponse.json({ decree });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ابلاغ حکم" }, { status: 500 });
  }
}
