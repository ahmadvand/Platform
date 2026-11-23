import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, getSession, guard, ipOf } from "@/lib/auth";

export const dynamic = "force-dynamic";

type Params = { params: Promise<{ id: string }> };

export async function GET(req: Request, { params }: Params) {
  const denied = await guard(req, "payroll.view");
  if (denied) return denied;
  const { id } = await params;
  const run = await db.payrollRun.findUnique({
    where: { id },
    include: {
      payslips: {
        include: {
          employee: {
            select: {
              firstName: true, lastName: true, personnelCode: true,
              department: { select: { name: true } },
              employmentType: true,
            },
          },
        },
        orderBy: { netPay: "desc" },
      },
    },
  });
  if (!run) return NextResponse.json({ error: "لیست حقوق یافت نشد" }, { status: 404 });
  return NextResponse.json({ run });
}

/** نهایی‌سازی (قفل) یا حذف پیش‌نویس */
export async function PATCH(req: Request, { params }: Params) {
  const denied = await guard(req, "payroll.manage");
  if (denied) return denied;
  const session = await getSession(req);
  const { id } = await params;
  const run = await db.payrollRun.findUnique({ where: { id } });
  if (!run) return NextResponse.json({ error: "لیست حقوق یافت نشد" }, { status: 404 });
  const body = (await req.json().catch(() => ({}))) as { action?: "finalize" };
  if (body.action !== "finalize")
    return NextResponse.json({ error: "عملیات نامعتبر" }, { status: 400 });
  if (run.status === "نهایی")
    return NextResponse.json({ error: "این لیست قبلاً نهایی شده است" }, { status: 409 });
  const updated = await db.payrollRun.update({
    where: { id },
    data: { status: "نهایی", finalizedAt: new Date() },
  });
  await audit(session, "finalize", "payroll_run", run.id, null, ipOf(req));
  return NextResponse.json({ run: updated });
}

export async function DELETE(req: Request, { params }: Params) {
  const denied = await guard(req, "payroll.manage");
  if (denied) return denied;
  const session = await getSession(req);
  const { id } = await params;
  const run = await db.payrollRun.findUnique({ where: { id } });
  if (!run) return NextResponse.json({ error: "لیست حقوق یافت نشد" }, { status: 404 });
  if (run.status === "نهایی")
    return NextResponse.json({ error: "لیست نهایی قابل حذف نیست" }, { status: 409 });
  await db.payrollRun.delete({ where: { id } });
  await audit(session, "delete", "payroll_run", run.id, null, ipOf(req));
  return NextResponse.json({ ok: true });
}
