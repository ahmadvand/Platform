import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, getSession, guard, ipOf } from "@/lib/auth";
import { queueCommand } from "@/lib/device-ops";

export const dynamic = "force-dynamic";

/**
 * انتقال کاربر بین دستگاه‌ها (خواسته اصلی کاربر):
 * POST { employeeId, targetDeviceIds?: string[], allTargets?: boolean,
 *        removeFromDeviceIds?: string[] }
 * برای هر مقصد push_user و برای هر مبدأ delete_user صف می‌شود.
 */
export async function POST(req: Request) {
  const denied = await guard(req, "devices.manage");
  if (denied) return denied;
  const session = await getSession(req);
  try {
    const body = (await req.json()) as {
      employeeId?: string;
      targetDeviceIds?: string[];
      allTargets?: boolean;
      removeFromDeviceIds?: string[];
    };
    if (!body.employeeId)
      return NextResponse.json({ error: "انتخاب پرسنل الزامی است" }, { status: 400 });

    const employee = await db.employee.findUnique({ where: { id: body.employeeId } });
    if (!employee) return NextResponse.json({ error: "پرسنل یافت نشد" }, { status: 404 });

    const userPayload = {
      pin: employee.personnelCode,
      name: `${employee.firstName} ${employee.lastName}`,
      employmentType: employee.employmentType,
      status: employee.status,
    };

    const targets = body.allTargets
      ? await db.device.findMany({ where: { active: true }, select: { id: true, name: true } })
      : await db.device.findMany({
          where: { id: { in: body.targetDeviceIds ?? [] }, active: true },
          select: { id: true, name: true },
        });

    const removals = (body.removeFromDeviceIds ?? []).filter(
      (rid) => !targets.some((t) => t.id === rid)
    );

    const commands: string[] = [];
    for (const t of targets) {
      const c = await queueCommand(t.id, "push_user", userPayload, session);
      commands.push(c.id);
    }
    for (const rid of removals) {
      const c = await queueCommand(rid, "delete_user", { pin: employee.personnelCode }, session);
      commands.push(c.id);
    }

    if (!commands.length)
      return NextResponse.json({ error: "هیچ دستگاه مقصدی انتخاب نشده است" }, { status: 400 });

    await audit(
      session,
      "transfer_user",
      "device",
      employee.personnelCode,
      { targets: targets.map((t) => t.name), removals },
      ipOf(req)
    );
    return NextResponse.json(
      {
        queued: commands.length,
        commands,
        detail: {
          pushTo: targets.map((t) => t.name),
          removeFrom: removals.length,
        },
      },
      { status: 201 }
    );
  } catch (e) {
    console.error("[transfer-user]", e);
    return NextResponse.json({ error: "خطا در انتقال کاربر" }, { status: 500 });
  }
}
