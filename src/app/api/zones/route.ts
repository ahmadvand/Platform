import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "access.view");
  if (denied) return denied;
  await ensureSeed();
  const zones = await db.zone.findMany({
    include: {
      gates: { include: { device: { select: { name: true, brand: true, mode: true } } } },
      accessPerms: { select: { id: true } },
    },
    orderBy: { code: "asc" },
  });
  return NextResponse.json({ zones });
}

export async function POST(req: Request) {
  const denied = await guard(req, "access.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (body.kind === "zone") {
      if (!body.name || !body.code) return NextResponse.json({ error: "نام و کد منطقه الزامی است" }, { status: 400 });
      const dup = await db.zone.findUnique({ where: { code: body.code } });
      if (dup) return NextResponse.json({ error: "کد منطقه تکراری است" }, { status: 409 });
      const zone = await db.zone.create({
        data: { name: body.name, code: body.code, riskLevel: body.riskLevel || "عادی", description: body.description || null },
      });
      return NextResponse.json({ zone }, { status: 201 });
    }
    if (body.kind === "gate") {
      if (!body.name || !body.code || !body.zoneId) return NextResponse.json({ error: "نام، کد و منطقه الزامی است" }, { status: 400 });
      const dup = await db.gate.findUnique({ where: { code: body.code } });
      if (dup) return NextResponse.json({ error: "کد گیت تکراری است" }, { status: 409 });
      const gate = await db.gate.create({
        data: {
          name: body.name, code: body.code, zoneId: body.zoneId,
          deviceId: body.deviceId || null,
          type: body.type || "درب برقی",
          direction: body.direction || "دوطرفه",
        },
      });
      return NextResponse.json({ gate }, { status: 201 });
    }
    return NextResponse.json({ error: "نوع نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت" }, { status: 500 });
  }
}

export async function PATCH(req: Request) {
  const denied = await guard(req, "access.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (body.kind === "gate" && body.id) {
      const data: Record<string, unknown> = {};
      for (const f of ["status", "name", "direction", "type"]) if (body[f] !== undefined) data[f] = body[f];
      const gate = await db.gate.update({ where: { id: body.id }, data });
      return NextResponse.json({ gate });
    }
    if (body.kind === "zone" && body.id) {
      const data: Record<string, unknown> = {};
      for (const f of ["name", "riskLevel", "description"]) if (body[f] !== undefined) data[f] = body[f];
      const zone = await db.zone.update({ where: { id: body.id }, data });
      return NextResponse.json({ zone });
    }
    return NextResponse.json({ error: "درخواست نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در به‌روزرسانی" }, { status: 500 });
  }
}
