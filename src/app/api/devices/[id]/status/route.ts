import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { guard } from "@/lib/auth";
import { isDeviceOnline } from "@/lib/device-ops";

export const dynamic = "force-dynamic";

type Params = { params: Promise<{ id: string }> };

/** وضعیت دستگاه: عمق صف، آخرین اتصال، آنلاین/آفلاین (PLAN P2.4) */
export async function GET(req: Request, { params }: Params) {
  const denied = await guard(req, "devices.view");
  if (denied) return denied;
  const { id } = await params;
  const device = await db.device.findUnique({
    where: { id },
    select: {
      id: true, name: true, serial: true, mode: true, active: true,
      pollingInterval: true, lastSeenAt: true, lastPulledAt: true, lastSync: true,
      firmware: true, userCount: true, ip: true, brand: true, location: true, purpose: true,
      _count: {
        select: {
          commands: { where: { status: "pending" } },
          attendanceRecords: true,
        },
      },
    },
  });
  if (!device) return NextResponse.json({ error: "دستگاه یافت نشد" }, { status: 404 });

  const lastCommand = await db.deviceCommand.findFirst({
    where: { deviceId: id },
    orderBy: { createdAt: "desc" },
    select: { type: true, status: true, createdAt: true, result: true },
  });

  return NextResponse.json({
    device,
    online: isDeviceOnline(device),
    pendingCommands: device._count.commands,
    attendanceCount: device._count.attendanceRecords,
    lastCommand,
  });
}
