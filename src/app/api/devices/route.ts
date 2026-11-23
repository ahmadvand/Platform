import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { audit, getSession, guard, ipOf } from "@/lib/auth";
import { isDeviceOnline, sweepOffline } from "@/lib/device-ops";

export const dynamic = "force-dynamic";

export async function GET(req: Request) {
  const denied = await guard(req, "devices.view");
  if (denied) return denied;
  await ensureSeed();
  await sweepOffline(); // P2.6: تشخیص آفلاین بدون تایمر

  const devices = await db.device.findMany({
    include: {
      gates: { include: { zone: true } },
      _count: { select: { commands: { where: { status: "pending" } } } },
    },
    orderBy: { name: "asc" },
  });

  // ماسک توکن webhook در پاسخ (PLAN P0.5)
  const safe = devices.map((d) => ({
    ...d,
    webhookToken: undefined,
    online: isDeviceOnline(d),
    pendingCommands: d._count.commands,
  }));
  return NextResponse.json({ devices: safe });
}

export async function POST(req: Request) {
  const denied = await guard(req, "devices.manage");
  if (denied) return denied;
  const session = await getSession(req);
  try {
    const body = await req.json();
    if (!body.name || !body.brand || !body.serial)
      return NextResponse.json({ error: "نام، برند و شماره سریال الزامی است" }, { status: 400 });
    const dup = await db.device.findUnique({ where: { serial: body.serial } });
    if (dup) return NextResponse.json({ error: "شماره سریال تکراری است" }, { status: 409 });
    const device = await db.device.create({
      data: {
        name: body.name,
        brand: body.brand,
        serial: body.serial,
        ip: body.ip || null,
        location: body.location || null,
        purpose: body.purpose || "حضور و غیاب",
        mode: body.mode || "آنلاین",
        pollingInterval: Number(body.pollingInterval) || 60,
        // P2.1: تولید خودکار توکن webhook (default cuid)
        lastSync: new Date(),
      },
    });
    await audit(session, "create", "device", device.serial, { name: device.name }, ipOf(req));
    return NextResponse.json(
      { device: { ...device, webhookToken: device.webhookToken } }, // توکن فقط هنگام ثبت اولیه برگردانده می‌شود
      { status: 201 }
    );
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت دستگاه" }, { status: 500 });
  }
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "devices.manage");
  if (denied) return denied;
  const session = await getSession(req);
  try {
    const body = await req.json();
    if (body.action === "toggle" && body.id) {
      const cur = await db.device.findUnique({ where: { id: body.id } });
      if (!cur) return NextResponse.json({ error: "دستگاه یافت نشد" }, { status: 404 });
      const device = await db.device.update({
        where: { id: body.id },
        data: { active: !cur.active, mode: !cur.active ? "آنلاین" : "آفلاین" },
      });
      await audit(session, "update", "device", cur.serial, { active: device.active }, ipOf(req));
      return NextResponse.json({ device: { ...device, webhookToken: undefined } });
    }
    if (body.action === "set-interval" && body.id) {
      const device = await db.device.update({
        where: { id: body.id },
        data: { pollingInterval: Math.max(30, Number(body.pollingInterval) || 60) },
      });
      return NextResponse.json({ device: { ...device, webhookToken: undefined } });
    }
    return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در عملیات دستگاه" }, { status: 500 });
  }
}
