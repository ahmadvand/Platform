import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "access.view");
  if (denied) return denied;
  await ensureSeed();
  const { searchParams } = new URL(req.url);
  const onlyOpen = searchParams.get("open") === "1";
  const alerts = await db.securityAlert.findMany({
    where: onlyOpen ? { resolved: false } : {},
    orderBy: [{ resolved: "asc" }, { createdAt: "desc" }],
    take: 80,
  });
  return NextResponse.json({ alerts });
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "access.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (body.id) {
      const alert = await db.securityAlert.update({
        where: { id: body.id },
        data: { resolved: Boolean(body.resolved) },
      });
      return NextResponse.json({ alert });
    }
    return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در به‌روزرسانی هشدار" }, { status: 500 });
  }
}
