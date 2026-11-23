import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { guard } from "@/lib/auth";

export const dynamic = "force-dynamic";

/** گزارش دستورات اخیر دستگاه‌ها (مانیتور — PLAN P2.7) */
export async function GET(req: Request) {
  const denied = await guard(req, "devices.view");
  if (denied) return denied;
  const { searchParams } = new URL(req.url);
  const take = Math.min(Number(searchParams.get("take") ?? 50), 200);
  const status = searchParams.get("status") || "";

  const commands = await db.deviceCommand.findMany({
    where: status ? { status } : {},
    include: { device: { select: { name: true, serial: true } } },
    orderBy: { createdAt: "desc" },
    take,
  });
  return NextResponse.json({ commands });
}
