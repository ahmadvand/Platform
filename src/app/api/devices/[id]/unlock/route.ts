import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, getSession, guard, ipOf } from "@/lib/auth";
import { queueCommand } from "@/lib/device-ops";

export const dynamic = "force-dynamic";

type Params = { params: Promise<{ id: string }> };

/** بازکردن درب از راه دور (PLAN P2.4) */
export async function POST(req: Request, { params }: Params) {
  const denied = await guard(req, "devices.control");
  if (denied) return denied;
  const session = await getSession(req);
  const { id } = await params;
  const device = await db.device.findUnique({ where: { id } });
  if (!device) return NextResponse.json({ error: "دستگاه یافت نشد" }, { status: 404 });
  const seconds = Math.min(Number(((await req.json().catch(() => ({}))) as { seconds?: number }).seconds ?? 5), 60);
  const cmd = await queueCommand(id, "unlock", { seconds }, session);
  await audit(session, "device_command", "device", device.serial, { type: "unlock", seconds }, ipOf(req));
  return NextResponse.json({ command: { id: cmd.id, type: "unlock" } }, { status: 201 });
}
