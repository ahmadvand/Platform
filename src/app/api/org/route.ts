import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "org.view");
  if (denied) return denied;
  await ensureSeed();
  const [companies, departments, positions] = await Promise.all([
    db.company.findMany(),
    db.department.findMany({
      include: {
        parent: { select: { name: true } },
        positions: { select: { id: true } },
        employees: { where: { status: "فعال" }, select: { id: true } },
      },
      orderBy: { code: "asc" },
    }),
    db.position.findMany({
      include: {
        department: { select: { name: true } },
        employees: { where: { status: "فعال" }, select: { id: true, firstName: true, lastName: true } },
      },
      orderBy: { code: "asc" },
    }),
  ]);
  return NextResponse.json({ companies, departments, positions });
}

export async function POST(req: Request) {
  const denied = await guard(req, "org.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    if (body.kind === "department") {
      if (!body.name || !body.code) return NextResponse.json({ error: "نام و کد واحد الزامی است" }, { status: 400 });
      const dup = await db.department.findUnique({ where: { code: body.code } });
      if (dup) return NextResponse.json({ error: "کد واحد تکراری است" }, { status: 409 });
      const company = body.companyId ? body.companyId : (await db.company.findFirst())?.id;
      const dept = await db.department.create({
        data: { name: body.name, code: body.code, companyId: company!, parentId: body.parentId || null },
      });
      return NextResponse.json({ department: dept }, { status: 201 });
    }
    if (body.kind === "position") {
      if (!body.title || !body.code || !body.departmentId)
        return NextResponse.json({ error: "عنوان، کد و واحد سازمانی الزامی است" }, { status: 400 });
      const dup = await db.position.findUnique({ where: { code: body.code } });
      if (dup) return NextResponse.json({ error: "کد شغل تکراری است" }, { status: 409 });
      const pos = await db.position.create({
        data: { title: body.title, code: body.code, departmentId: body.departmentId, grade: body.grade || null },
      });
      return NextResponse.json({ position: pos }, { status: 201 });
    }
    return NextResponse.json({ error: "نوع نامعتبر" }, { status: 400 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت اطلاعات سازمانی" }, { status: 500 });
  }
}
