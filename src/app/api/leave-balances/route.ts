import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { guard } from "@/lib/auth";
import { toJalali } from "@/lib/persian";

export const dynamic = "force-dynamic";

/** مانده مرخصی قانونی پرسنل (PLAN P4.4) — استحقاقی ۲۶ روز + استعلاجی ۲ روز */
export async function GET(req: Request) {
  const denied = await guard(req, "employees.view");
  if (denied) return denied;
  await ensureSeed();
  const { searchParams } = new URL(req.url);
  const employeeId = searchParams.get("employeeId");
  const year = toJalali(new Date()).jy;

  const employees = await db.employee.findMany({
    where: employeeId ? { id: employeeId } : {},
    select: {
      id: true,
      firstName: true,
      lastName: true,
      personnelCode: true,
      employmentType: true,
      annualLeaveEnt: true,
      annualLeaveUsed: true,
      sickLeaveEnt: true,
      sickLeaveUsed: true,
    },
    orderBy: { personnelCode: "asc" },
  });

  const balances = employees.map((e) => ({
    employeeId: e.id,
    personnelCode: e.personnelCode,
    name: `${e.firstName} ${e.lastName}`,
    employmentType: e.employmentType,
    year,
    annualEnt: e.annualLeaveEnt,
    annualUsed: e.annualLeaveUsed,
    annualRemaining: Math.max(0, e.annualLeaveEnt - e.annualLeaveUsed),
    sickEnt: e.sickLeaveEnt,
    sickUsed: e.sickLeaveUsed,
    sickRemaining: Math.max(0, e.sickLeaveEnt - e.sickLeaveUsed),
  }));

  return NextResponse.json({ balances, jYear: year });
}
