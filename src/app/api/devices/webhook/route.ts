import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import {
  authenticateDevice,
  ingestPunches,
  markSeen,
  queueCommand,
  type RawPunch,
} from "@/lib/device-ops";
import { audit, getSession, ipOf } from "@/lib/auth";

export const dynamic = "force-dynamic";

/**
 * پل ارتباط دستگاه‌ها (الگوی ADMS/PUSH — PLAN P2.3)
 * GET  ?SN=...  با هدر X-Device-Token (یا نشست سرویس پل) → تحویل دستورات صف‌شده + heartbeat
 * POST { SN, event: "ping|records|users|ack|status", data }
 */
export async function GET(req: Request) {
  const sn = new URL(req.url).searchParams.get("SN");
  const auth = await authenticateDevice(req, sn);
  if (!auth.ok) return auth.res;
  const device = auth.device;

  await markSeen(device.id);

  // تحویل دستورات در انتظار (الگوی ACK پروتکل ZK)
  const pending = await db.deviceCommand.findMany({
    where: { deviceId: device.id, status: "pending" },
    orderBy: { createdAt: "asc" },
    take: 20,
  });
  if (pending.length) {
    await db.deviceCommand.updateMany({
      where: { id: { in: pending.map((c) => c.id) } },
      data: { status: "delivered", deliveredAt: new Date() },
    });
  }

  return NextResponse.json({
    ok: true,
    time: new Date().toISOString(),
    commands: pending.map((c) => ({
      id: c.id,
      type: c.type,
      payload: c.payload ? JSON.parse(c.payload) : null,
    })),
  });
}

export async function POST(req: Request) {
  const ip = ipOf(req);
  try {
    const body = (await req.json()) as {
      SN?: string;
      event?: "ping" | "records" | "users" | "ack" | "status";
      data?: unknown;
    };
    const auth = await authenticateDevice(req, body.SN ?? null);
    if (!auth.ok) return auth.res;
    const device = auth.device;
    await markSeen(device.id);

    switch (body.event) {
      case "ping":
      case "status": {
        const d = (body.data ?? {}) as { firmware?: string; userCount?: number; online?: boolean };
        await markSeen(device.id, {
          firmware: d.firmware ?? undefined,
          userCount: typeof d.userCount === "number" ? d.userCount : undefined,
        });
        return NextResponse.json({ ok: true });
      }

      case "records": {
        const r = await ingestPunches(device.id, ((body.data ?? {}) as { punches?: RawPunch[] }).punches ?? []);
        return NextResponse.json({ ...r, ok: true });
      }

      case "users": {
        // فهرست کاربران موجود روی دستگاه (نتیجه refresh_data / poll)
        const list = ((body.data ?? {}) as { users?: { pin: string; name?: string }[] }).users ?? [];
        await db.device.update({ where: { id: device.id }, data: { userCount: list.length } });
        return NextResponse.json({ ok: true, count: list.length });
      }

      case "ack": {
        const a = (body.data ?? {}) as { commandId: string; ok?: boolean; result?: string };
        const cmd = await db.deviceCommand.findUnique({ where: { id: a.commandId } });
        if (!cmd || cmd.deviceId !== device.id)
          return NextResponse.json({ error: "دستور یافت نشد" }, { status: 404 });
        await db.deviceCommand.update({
          where: { id: cmd.id },
          data: {
            status: a.ok === false ? "failed" : "acked",
            ackedAt: new Date(),
            result: a.result ?? (a.ok === false ? "دستگاه اجرای دستور را رد کرد" : "با موفقیت اجرا شد"),
          },
        });
        return NextResponse.json({ ok: true });
      }

      default:
        return NextResponse.json({ error: "رویداد نامعتبر" }, { status: 400 });
    }
  } catch (e) {
    console.error("[devices/webhook]", e);
    return NextResponse.json({ error: "خطای پردازش داده دستگاه" }, { status: 500 });
  }
}
