import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { evaluateAccess } from "@/lib/att";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "access.view");
  if (denied) return denied;
  await ensureSeed();
  const { searchParams } = new URL(req.url);
  const result = searchParams.get("result") ?? "";

  const [permissions, rawEvents] = await Promise.all([
    db.accessPermission.findMany({
      include: {
        employee: { select: { firstName: true, lastName: true, personnelCode: true } },
        zone: { select: { name: true, riskLevel: true } },
      },
      orderBy: { createdAt: "desc" },
    }),
    db.accessEvent.findMany({
      where: result ? { result } : {},
      include: {
        employee: { select: { firstName: true, lastName: true, personnelCode: true } },
        gate: { include: { zone: { select: { name: true } } } },
      },
      orderBy: { timestamp: "desc" },
      take: 60,
    }),
  ]);

  // visitor names resolved separately (works regardless of client version)
  const visitorIds = rawEvents.map((e) => e.visitorId).filter((v): v is string => !!v);
  const visitors = visitorIds.length
    ? await db.visitor.findMany({ where: { id: { in: visitorIds } }, select: { id: true, firstName: true, lastName: true } })
    : [];
  const vMap = new Map(visitors.map((v) => [v.id, v]));
  const events = rawEvents.map((e) => ({
    ...e,
    visitor: e.visitorId ? vMap.get(e.visitorId) ?? null : null,
  }));

  return NextResponse.json({ permissions, events });
}

/** صدور مجوز دسترسی جدید (AC-02) */
export async function POST(req: Request) {
  const denied = await guard(req, "access.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (!body.zoneId) return NextResponse.json({ error: "انتخاب منطقه الزامی است" }, { status: 400 });
    if (!body.employeeId) return NextResponse.json({ error: "انتخاب پرسنل الزامی است" }, { status: 400 });
    const perm = await db.accessPermission.create({
      data: {
        employeeId: body.employeeId,
        zoneId: body.zoneId,
        type: body.type || "دائم",
        validFrom: body.validFrom ? new Date(body.validFrom) : new Date(),
        validUntil: body.validUntil ? new Date(body.validUntil) : null,
        days: body.days || "0,1,2,3,4",
        timeFrom: body.timeFrom || null,
        timeUntil: body.timeUntil || null,
        status: "فعال",
      },
      include: { employee: { select: { firstName: true, lastName: true } }, zone: { select: { name: true } } },
    });
    return NextResponse.json({ permission: perm }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در صدور مجوز" }, { status: 500 });
  }
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "access.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (body.id && body.status) {
      const perm = await db.accessPermission.update({ where: { id: body.id }, data: { status: body.status } });
      return NextResponse.json({ permission: perm });
    }
    return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در به‌روزرسانی مجوز" }, { status: 500 });
  }
}

/**
 * شبیه‌سازی عبور از گیت (AC-05) — ارزیابی مجوز و ثبت رویداد.
 * اگر غیرمجاز باشد هشدار حراستی (VIS-05) ایجاد می‌شود.
 */
export async function PUT(req: Request) {
  const denied = await guard(req, "access.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    const { employeeId, gateId, direction, method } = body;
    if (!employeeId || !gateId) return NextResponse.json({ error: "پرسنل و گیت الزامی است" }, { status: 400 });

    const [employee, gate] = await Promise.all([
      db.employee.findUnique({ where: { id: employeeId } }),
      db.gate.findUnique({ where: { id: gateId }, include: { device: true } }),
    ]);
    if (!employee || !gate) return NextResponse.json({ error: "اطلاعات یافت نشد" }, { status: 404 });
    if (gate.status === "خراب") {
      return NextResponse.json({ error: `گیت «${gate.name}» خراب است — تردد امکان‌پذیر نیست` }, { status: 409 });
    }

    const perms = await db.accessPermission.findMany({ where: { employeeId, zoneId: gate.zoneId } });
    const evaluation = evaluateAccess(
      perms.map((p) => ({
        type: p.type,
        validFrom: p.validFrom,
        validUntil: p.validUntil,
        timeFrom: p.timeFrom,
        timeUntil: p.timeUntil,
        days: p.days,
        status: p.status,
      })),
      new Date(),
    );

    const event = await db.accessEvent.create({
      data: {
        employeeId,
        gateId,
        direction: direction || "ورود",
        method: method || "کارت",
        result: evaluation.allowed ? "مجاز" : "غیرمجاز",
        reason: evaluation.reason,
        timestamp: new Date(),
      },
      include: { gate: { include: { zone: true } }, employee: { select: { firstName: true, lastName: true } } },
    });

    let alert = null;
    if (!evaluation.allowed) {
      alert = await db.securityAlert.create({
        data: {
          type: "ورود غیرمجاز",
          severity: gate.zone.riskLevel === "فوق‌حساس" ? "بحرانی" : "بالا",
          message: `تلاش ${direction || "ورود"} غیرمجاز ${employee.firstName} ${employee.lastName} در ${gate.name} — ${evaluation.reason ?? ""}`,
          source: "کنترل تردد",
          refId: event.id,
        },
      });
    }

    return NextResponse.json({ event, alert, allowed: evaluation.allowed, reason: evaluation.reason });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت رویداد تردد" }, { status: 500 });
  }
}
