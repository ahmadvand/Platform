import { NextResponse } from "next/server";
import { ensureSeed, reseed } from "@/lib/seed";
import { db } from "@/lib/db";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "dashboard.view");
  if (denied) return denied;
  await ensureSeed();
  const counts = {
    companies: await db.company.count(),
    employees: await db.employee.count(),
    departments: await db.department.count(),
    shifts: await db.shift.count(),
    devices: await db.device.count(),
    zones: await db.zone.count(),
    gates: await db.gate.count(),
    visitors: await db.visitor.count(),
    alerts: await db.securityAlert.count(),
    attendance: await db.attendanceRecord.count(),
    accessEvents: await db.accessEvent.count(),
  };
  return NextResponse.json({ seeded: counts.employees > 0, counts });
}

export async function POST(req: Request) {
  const denied = await guard(req, "users.manage");
  if (denied) return denied;
  const { searchParams } = new URL(req.url);
  if (searchParams.get("force") === "1") {
    await reseed();
    return NextResponse.json({ ok: true, reseeded: true });
  }
  await ensureSeed();
  return NextResponse.json({ ok: true });
}
