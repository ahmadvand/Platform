import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, getSession, guard, ipOf } from "@/lib/auth";
import { COMMAND_TYPES, queueCommand, type CommandType } from "@/lib/device-ops";

export const dynamic = "force-dynamic";

type Params = { params: Promise<{ id: string }> };

/** دستور عمومی به دستگاه (PLAN P2.4) — مجوز devices.control */
export async function POST(req: Request, { params }: Params) {
  const denied = await guard(req, "devices.control");
  if (denied) return denied;
  const session = await getSession(req);
  const { id } = await params;
  const device = await db.device.findUnique({ where: { id } });
  if (!device) return NextResponse.json({ error: "دستگاه یافت نشد" }, { status: 404 });
  if (!device.active)
    return NextResponse.json({ error: "دستگاه غیرفعال است" }, { status: 409 });

  try {
    const body = (await req.json()) as { type?: CommandType; payload?: unknown };
    if (!body.type || !COMMAND_TYPES.includes(body.type))
      return NextResponse.json({ error: "نوع دستور نامعتبر است" }, { status: 400 });

    const cmd = await queueCommand(id, body.type, body.payload ?? null, session);
    await audit(session, "device_command", "device", device.serial, { type: body.type, commandId: cmd.id }, ipOf(req));
    return NextResponse.json({ command: { id: cmd.id, type: cmd.type, status: cmd.status } }, { status: 201 });
  } catch (e) {
    console.error("[device command]", e);
    return NextResponse.json({ error: "خطا در صف کردن دستور" }, { status: 500 });
  }
}
