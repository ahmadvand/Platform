import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "visitors.view");
  if (denied) return denied;
  await ensureSeed();
  const { searchParams } = new URL(req.url);
  const status = searchParams.get("status") ?? "";

  const visitors = await db.visitor.findMany({
    where: status ? { status } : {},
    include: { card: true, contractor: { select: { name: true, code: true } }, logs: { orderBy: { timestamp: "desc" } } },
    orderBy: { requestedAt: "desc" },
  });

  const contractors = await db.contractor.findMany({
    include: { visitors: { select: { id: true } } },
    orderBy: { name: "asc" },
  });

  const inside = visitors.filter((v) => v.status === "داخل سازمان");
  return NextResponse.json({ visitors, contractors, insideCount: inside.length });
}

/** ثبت مراجع جدید (VIS-01) + گردش تأیید (VIS-04) */
export async function POST(req: Request) {
  const denied = await guard(req, "visitors.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.firstName || !body.lastName || !body.nationalId)
      return NextResponse.json({ error: "نام، نام خانوادگی و کد ملی الزامی است" }, { status: 400 });
    const visitor = await db.visitor.create({
      data: {
        firstName: body.firstName,
        lastName: body.lastName,
        nationalId: body.nationalId,
        phone: body.phone || null,
        idDocType: body.idDocType || "کارت ملی",
        type: body.type || "مهمان",
        hostName: body.hostName || "—",
        hostDept: body.hostDept || null,
        purpose: body.purpose || "",
        contractorId: body.contractorId || null,
        status: "در انتظار تأیید",
      },
    });
    return NextResponse.json({ visitor }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت مراجع" }, { status: 500 });
  }
}

/**
 * گردش تأیید و صدور کارت موقت (VIS-02):
 * approve | reject | checkin (صدور کارت + ثبت ورود) | checkout (بازگشت کارت + ثبت خروج)
 */
export async function PATCH(req: Request) {
  const denied = await guard(req, "visitors.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    const { id, action } = body;
    if (!id || !action) return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });

    const visitor = await db.visitor.findUnique({ where: { id }, include: { card: true, contractor: true } });
    if (!visitor) return NextResponse.json({ error: "مراجع یافت نشد" }, { status: 404 });

    if (action === "approve") {
      await db.visitor.update({ where: { id }, data: { status: "تأیید شده", approvedBy: "حسین بهرامی (حراست)" } });
      return NextResponse.json({ ok: true });
    }
    if (action === "reject") {
      await db.visitor.update({ where: { id }, data: { status: "رد شده", approvedBy: "حسین بهرامی (حراست)" } });
      return NextResponse.json({ ok: true });
    }
    if (action === "checkin") {
      if (visitor.card && !visitor.card.returned) {
        // کارت معتبر قبلی — فقط ورود
        await db.visitor.update({ where: { id }, data: { status: "داخل سازمان" } });
        await db.visitLog.create({ data: { visitorId: id, direction: "ورود", gateName: "گیت اصلی ورودی" } });
        return NextResponse.json({ ok: true });
      }
      const cardNo = `VC-${Math.floor(Math.random() * 90000) + 10000}`;
      const cardType = ["QR", "بارکد", "RFID"][Math.floor(Math.random() * 3)];
      await db.visitorCard.deleteMany({ where: { visitorId: id } });
      await db.visitorCard.create({
        data: {
          cardNo,
          type: cardType,
          visitorId: id,
          validUntil: new Date(Date.now() + 8 * 3600000),
          returned: false,
        },
      });
      await db.visitor.update({ where: { id }, data: { status: "داخل سازمان" } });
      await db.visitLog.create({ data: { visitorId: id, direction: "ورود", gateName: "گیت اصلی ورودی" } });
      return NextResponse.json({ ok: true, card: { cardNo, type: cardType, validUntil: new Date(Date.now() + 8 * 3600000) } });
    }
    if (action === "checkout") {
      await db.visitor.update({ where: { id }, data: { status: "خارج شده" } });
      if (visitor.card) await db.visitorCard.update({ where: { id: visitor.card.id }, data: { returned: true } });
      await db.visitLog.create({ data: { visitorId: id, direction: "خروج", gateName: "گیت اصلی خروجی" } });
      return NextResponse.json({ ok: true });
    }
    return NextResponse.json({ error: "عملیات نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در پردازش درخواست" }, { status: 500 });
  }
}
