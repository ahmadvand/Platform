import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "visitors.view");
  if (denied) return denied;
  await ensureSeed();
  const contractors = await db.contractor.findMany({
    include: { visitors: { select: { id: true } } },
    orderBy: { name: "asc" },
  });
  return NextResponse.json({ contractors });
}

export async function POST(req: Request) {
  const denied = await guard(req, "visitors.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.name || !body.code) return NextResponse.json({ error: "نام و کد شرکت الزامی است" }, { status: 400 });
    const dup = await db.contractor.findUnique({ where: { code: body.code } });
    if (dup) return NextResponse.json({ error: "کد شرکت تکراری است" }, { status: 409 });
    const contractor = await db.contractor.create({
      data: {
        name: body.name,
        code: body.code,
        contactName: body.contactName || null,
        phone: body.phone || null,
        contractNo: body.contractNo || null,
        validUntil: body.validUntil ? new Date(body.validUntil) : null,
      },
    });
    return NextResponse.json({ contractor }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت پیمانکار" }, { status: 500 });
  }
}
