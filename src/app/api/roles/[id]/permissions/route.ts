import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, guard, invalidateUserCache, ipOf } from "@/lib/auth";
import { PERM_KEYS } from "@/lib/permissions";

export const dynamic = "force-dynamic";

type Params = { params: Promise<{ id: string }> };

/** تنظیم دسترسی‌های نقش (PLAN P0.3 — نقش‌محور واقعی) */
export async function PUT(req: Request, { params }: Params) {
  const denied = await guard(req, "users.manage");
  if (denied) return denied;
  const { id } = await params;
  const roleId = Number(id);
  if (!Number.isInteger(roleId))
    return NextResponse.json({ error: "شناسه نامعتبر" }, { status: 400 });
  const role = await db.role.findUnique({ where: { id: roleId } });
  if (!role) return NextResponse.json({ error: "نقش یافت نشد" }, { status: 404 });
  if (role.name === "superadmin")
    return NextResponse.json({ error: "دسترسی‌های نقش مدیر ارشد قابل تغییر نیست" }, { status: 400 });

  const body = (await req.json()) as { permissionKeys?: string[] };
  const keys = (body.permissionKeys ?? []).filter((k) => PERM_KEYS.includes(k));

  const permRows = await db.permission.findMany({ where: { key: { in: keys } } });
  await db.rolePermission.deleteMany({ where: { roleId } });
  if (permRows.length)
    await db.rolePermission.createMany({
      data: permRows.map((p) => ({ roleId, permissionId: p.id })),
    });

  // ابطال کش مجوز کاربرانِ این نقش
  const users = await db.userRole.findMany({ where: { roleId }, select: { userId: true } });
  users.forEach((u) => invalidateUserCache(u.userId));

  await audit(null, "set_permissions", "role", role.name, { keys }, ipOf(req));
  return NextResponse.json({ ok: true, count: permRows.length });
}
