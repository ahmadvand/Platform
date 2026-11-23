import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, getSession, guard, ipOf } from "@/lib/auth";

export const dynamic = "force-dynamic";

type Params = { params: Promise<{ id: string }> };

/** چرخش توکن webhook دستگاه — ادمین (PLAN P0.5) */
export async function POST(req: Request, { params }: Params) {
  const denied = await guard(req, "devices.manage");
  if (denied) return denied;
  const session = await getSession(req);
  const { id } = await params;
  const device = await db.device.findUnique({ where: { id } });
  if (!device) return NextResponse.json({ error: "دستگاه یافت نشد" }, { status: 404 });
  const { randomUUID } = await import("node:crypto");
  const device2 = await db.device.update({
    where: { id },
    data: { webhookToken: randomUUID() },
  });
  await audit(session, "rotate_secret", "device", device.serial, null, ipOf(req));
  return NextResponse.json({ webhookToken: device2.webhookToken });
}
