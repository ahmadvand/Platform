import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, getSession, guard, ipOf } from "@/lib/auth";
import { queueCommand } from "@/lib/device-ops";

export const dynamic = "force-dynamic";

type Params = { params: Promise<{ id: string }> };

/** ری‌استارت دستگاه (PLAN P2.4) */
export async function POST(req: Request, { params }: Params) {
  const denied = await guard(req, "devices.control");
  if (denied) return denied;
  const session = await getSession(req);
  const { id } = await params;
  const device = await db.device.findUnique({ where: { id } });
  if (!device) return NextResponse.json({ error: "دستگاه یافت نشد" }, { status: 404 });
  const cmd = await queueCommand(id, "restart", null, session);
  await audit(session, "device_command", "device", device.serial, { type: "restart" }, ipOf(req));
  return NextResponse.json({ command: { id: cmd.id, type: "restart" } }, { status: 201 });
}
