import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { audit, getSession, guard, ipOf } from "@/lib/auth";
import { toJalali } from "@/lib/persian";

export const dynamic = "force-dynamic";

export async function GET(req: Request) {
  const denied = await guard(req, "holidays.view");
  if (denied) return denied;
  await ensureSeed();
  const { searchParams } = new URL(req.url);
  const year = Number(searchParams.get("jYear")) || toJalali(new Date()).jy;
  const holidays = await db.holiday.findMany({
    where: { jYear: year },
    orderBy: [{ jMonth: "asc" }, { jDay: "asc" }],
  });
  return NextResponse.json({ holidays, jYear: year });
}

export async function POST(req: Request) {
  const denied = await guard(req, "holidays.manage");
  if (denied) return denied;
  const session = await getSession(req);
  try {
    const body = (await req.json()) as { date?: string; title?: string };
    if (!body.date || !body.title)
      return NextResponse.json({ error: "تاریخ و عنوان تعطیلی الزامی است" }, { status: 400 });
    const d = new Date(body.date + "T12:00:00");
    if (isNaN(d.getTime())) return NextResponse.json({ error: "تاریخ نامعتبر است" }, { status: 400 });
    const j = toJalali(d);
    const holiday = await db.holiday.upsert({
      where: { date: body.date },
      update: { title: body.title },
      create: { date: body.date, title: body.title, jYear: j.jy, jMonth: j.jm, jDay: j.jd },
    });
    await audit(session, "create", "holiday", holiday.date, { title: holiday.title }, ipOf(req));
    return NextResponse.json({ holiday }, { status: 201 });
  } catch (e) {
    console.error("[holidays POST]", e);
    return NextResponse.json({ error: "خطا در ثبت تعطیلی" }, { status: 500 });
  }
}

export async function DELETE(req: Request) {
  const denied = await guard(req, "holidays.manage");
  if (denied) return denied;
  const session = await getSession(req);
  const { searchParams } = new URL(req.url);
  const id = searchParams.get("id");
  if (!id) return NextResponse.json({ error: "شناسه الزامی است" }, { status: 400 });
  const holiday = await db.holiday.delete({ where: { id } }).catch(() => null);
  if (!holiday) return NextResponse.json({ error: "یافت نشد" }, { status: 404 });
  await audit(session, "delete", "holiday", holiday.date, null, ipOf(req));
  return NextResponse.json({ ok: true });
}
