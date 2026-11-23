import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { toEnDigits } from "@/lib/persian";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "employees.view");
  if (denied) return denied;
  await ensureSeed();
  const { searchParams } = new URL(req.url);
  const q = searchParams.get("q")?.trim() ?? "";
  const dept = searchParams.get("dept") ?? "";
  const type = searchParams.get("type") ?? "";
  const status = searchParams.get("status") ?? "";

  const where: Record<string, unknown> = {};
  if (dept) where.departmentId = dept;
  if (type) where.employmentType = type;
  if (status) where.status = status;
  if (q) {
    where.OR = [
      { firstName: { contains: q } },
      { lastName: { contains: q } },
      { personnelCode: { contains: toEnDigits(q) } },
      { nationalId: { contains: toEnDigits(q) } },
    ];
  }

  const employees = await db.employee.findMany({
    where,
    include: {
      department: { select: { name: true } },
      position: { select: { title: true } },
      shift: { select: { name: true } },
      company: { select: { name: true } },
    },
    orderBy: { personnelCode: "asc" },
  });

  return NextResponse.json({ employees });
}

export async function POST(req: Request) {
  const denied = await guard(req, "employees.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    const required = ["firstName", "lastName", "nationalId", "employmentType"];
    for (const r of required) {
      if (!body[r]) return NextResponse.json({ error: `فیلد ${r} الزامی است` }, { status: 400 });
    }

    const dup = await db.employee.findFirst({ where: { nationalId: toEnDigits(body.nationalId) } });
    if (dup) return NextResponse.json({ error: "کد ملی تکراری است" }, { status: 409 });

    const last = await db.employee.findFirst({ orderBy: { personnelCode: "desc" } });
    const nextCode = String((Number(last?.personnelCode ?? 1000) || 1000) + 1);

    const company = await db.company.findFirst();
    if (!company) return NextResponse.json({ error: "شرکتی یافت نشد" }, { status: 400 });

    const emp = await db.employee.create({
      data: {
        personnelCode: nextCode,
        firstName: body.firstName,
        lastName: body.lastName,
        nationalId: toEnDigits(body.nationalId),
        birthDate: body.birthDate || null,
        phone: body.phone || null,
        email: body.email || null,
        employmentType: body.employmentType,
        status: body.status || "فعال",
        hireDate: body.hireDate ? new Date(body.hireDate) : new Date(),
        companyId: body.companyId || company.id,
        departmentId: body.departmentId || null,
        positionId: body.positionId || null,
        shiftId: body.shiftId || null,
        baseSalary: Number(body.baseSalary) || 0,
        annualLeaveEnt: body.employmentType === "رسمی" ? 26 : 22,
      },
      include: { department: true, position: true, shift: true },
    });

    // قرارداد خودکار بر اساس نوع استخدام (HR-04)
    const end =
      body.employmentType === "قراردادی" ? new Date(Date.now() + 365 * 86400000)
      : body.employmentType === "پیمانکاری" ? new Date(Date.now() + 90 * 86400000)
      : body.employmentType === "کارآموز" ? new Date(Date.now() + 30 * 86400000)
      : null;
    const cType = body.employmentType === "رسمی" ? "دائم" : body.employmentType === "اقماری" ? "موقت" : body.employmentType === "پیمانکاری" ? "پروژه‌ای" : "آزمایشی";
    await db.contract.create({
      data: {
        employeeId: emp.id,
        type: cType,
        contractNo: `CT-1404-${String(Math.floor(Math.random() * 9000) + 1000)}`,
        startDate: new Date(),
        endDate: end,
        salary: Number(body.baseSalary) || 0,
        status: "فعال",
      },
    });
    await db.decree.create({
      data: {
        employeeId: emp.id,
        type: "استخدام",
        decreeNo: `DEC-1404-${String(Math.floor(Math.random() * 9000) + 1000)}`,
        date: new Date(),
        content: "حکم استخدام صادر شد.",
        acknowledged: false,
      },
    });

    return NextResponse.json({ employee: emp }, { status: 201 });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت پرسنل" }, { status: 500 });
  }
}
