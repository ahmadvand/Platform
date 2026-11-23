import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { computeWork, statusFromPunch } from "@/lib/att";
import { dateKey, toJalali, toFa } from "@/lib/persian";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

function shiftParamsOf(s: { startTime: string; endTime: string; breakMinutes: number; toleranceLate: number } | null) {
  return s ? { startTime: s.startTime, endTime: s.endTime, breakMinutes: s.breakMinutes, toleranceLate: s.toleranceLate } : null;
}

export async function GET(req: Request) {
  const denied = await guard(req, "attendance.view");
  if (denied) return denied;
  await ensureSeed();
  const { searchParams } = new URL(req.url);
  const date = searchParams.get("date") || dateKey();
  const employeeId = searchParams.get("employeeId") || "";
  const status = searchParams.get("status") || "";

  const where: Record<string, unknown> = { date };
  if (employeeId) where.employeeId = employeeId;
  if (status) where.status = status;

  const records = await db.attendanceRecord.findMany({
    where,
    include: {
      employee: {
        select: {
          firstName: true, lastName: true, personnelCode: true,
          department: { select: { name: true } },
          shift: { select: { name: true, startTime: true, endTime: true } },
        },
      },
      device: { select: { name: true, brand: true } },
    },
    orderBy: [{ status: "asc" }, { checkIn: "asc" }],
  });

  const stats = {
    present: records.filter((r) => r.status === "حاضر").length,
    late: records.filter((r) => r.status === "تأخیر").length,
    absent: records.filter((r) => r.status === "غایب").length,
    leave: records.filter((r) => r.status === "مرخصی").length,
    mission: records.filter((r) => r.status === "مأموریت").length,
    totalOvertime: records.reduce((s, r) => s + r.overtimeMinutes, 0),
    totalWork: records.reduce((s, r) => s + r.workMinutes, 0),
  };

  const j = toJalali(new Date(date + "T12:00:00"));
  return NextResponse.json({ records, stats, jalali: toFa(`${j.jy}/${String(j.jm).padStart(2, "0")}/${String(j.jd).padStart(2, "0")}`) });
}

/** شبیه‌سازی ثبت تردد از دستگاه (ATT-01) */
export async function POST(req: Request) {
  const denied = await guard(req, "attendance.manage");
  if (denied) return denied;
  try {
    const body = await req.json();
    const employeeId = body.employeeId;
    const kind = body.kind === "out" ? "out" : "in";
    if (!employeeId) return NextResponse.json({ error: "انتخاب پرسنل الزامی است" }, { status: 400 });

    const employee = await db.employee.findUnique({ where: { id: employeeId }, include: { shift: true } });
    if (!employee) return NextResponse.json({ error: "پرسنل یافت نشد" }, { status: 404 });

    const now = new Date();
    const dk = dateKey(now);
    let record = await db.attendanceRecord.findUnique({
      where: { employeeId_date: { employeeId, date: dk } },
    });

    if (kind === "in") {
      if (record?.checkIn) return NextResponse.json({ error: "ورود قبلاً ثبت شده است" }, { status: 409 });
      if (!record) {
        record = await db.attendanceRecord.create({
          data: { employeeId, deviceId: body.deviceId || null, date: dk },
        });
      }
      const sp = shiftParamsOf(employee.shift);
      const w = computeWork(sp, now, record.checkOut, dk);
      record = await db.attendanceRecord.update({
        where: { id: record.id },
        data: {
          checkIn: now,
          deviceId: body.deviceId || record.deviceId,
          lateMinutes: w.lateMinutes,
          workMinutes: w.workMinutes,
          overtimeMinutes: w.overtimeMinutes,
          earlyMinutes: w.earlyMinutes,
          status: statusFromPunch(w.lateMinutes, true),
        },
      });
    } else {
      if (!record?.checkIn) return NextResponse.json({ error: "ابتدا ورود ثبت شود" }, { status: 409 });
      if (record.checkOut) return NextResponse.json({ error: "خروج قبلاً ثبت شده است" }, { status: 409 });
      const sp = shiftParamsOf(employee.shift);
      const w = computeWork(sp, record.checkIn, now, dk);
      record = await db.attendanceRecord.update({
        where: { id: record.id },
        data: {
          checkOut: now,
          workMinutes: w.workMinutes,
          earlyMinutes: w.earlyMinutes,
          overtimeMinutes: w.overtimeMinutes,
        },
      });
    }

    return NextResponse.json({ record });
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "خطا در ثبت تردد" }, { status: 500 });
  }
}
