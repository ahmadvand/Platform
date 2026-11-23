import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, guard, getSession, hashPassword, invalidateUserCache, ipOf, validatePasswordPolicy } from "@/lib/auth";

export const dynamic = "force-dynamic";

type Params = { params: Promise<{ id: string }> };

export async function PATCH(req: Request, { params }: Params) {
  const denied = await guard(req, "users.manage");
  if (denied) return denied;
  const session = await getSession(req);
  const { id } = await params;
  const userId = Number(id);
  if (!Number.isInteger(userId))
    return NextResponse.json({ error: "شناسه نامعتبر" }, { status: 400 });
  const target = await db.user.findUnique({ where: { id: userId } });
  if (!target) return NextResponse.json({ error: "کاربر یافت نشد" }, { status: 404 });

  try {
    const body = (await req.json()) as {
      action?: "toggle-active" | "unlock" | "reset-password" | "set-roles" | "set-fullname";
      password?: string;
      roleIds?: number[];
      fullName?: string;
    };

    switch (body.action) {
      case "toggle-active": {
        if (target.id === session?.userId)
          return NextResponse.json({ error: "نمی‌توانید حساب خودتان را غیرفعال کنید" }, { status: 400 });
        const user = await db.user.update({
          where: { id: userId },
          data: { isActive: !target.isActive },
        });
        if (!user.isActive)
          await db.refreshToken.updateMany({
            where: { userId, revokedAt: null },
            data: { revokedAt: new Date() },
          });
        invalidateUserCache(userId);
        await audit(session, "update", "user", target.username, { isActive: user.isActive }, ipOf(req));
        return NextResponse.json({ user: { id: user.id, isActive: user.isActive } });
      }
      case "unlock": {
        const user = await db.user.update({
          where: { id: userId },
          data: { lockedUntil: null, failedLoginAttempts: 0 },
        });
        invalidateUserCache(userId);
        await audit(session, "unlock", "user", target.username, null, ipOf(req));
        return NextResponse.json({ user: { id: user.id, lockedUntil: user.lockedUntil } });
      }
      case "reset-password": {
        const policyErr = validatePasswordPolicy(body.password || "", target.username);
        if (policyErr || !body.password)
          return NextResponse.json({ error: policyErr || "رمز عبور الزامی است" }, { status: 400 });
        // جلوگیری از تکرار ۳ رمز اخیر (P1.3)
        const history = await db.passwordHistory.findMany({
          where: { userId },
          orderBy: { createdAt: "desc" },
          take: 3,
        });
        const { verifyPassword } = await import("@/lib/auth");
        if (history.some((h) => verifyPassword(body.password!, h.passwordHash)))
          return NextResponse.json(
            { error: "رمز جدید نباید با ۳ رمز اخیر یکسان باشد" },
            { status: 400 }
          );
        const hash = hashPassword(body.password);
        await db.user.update({
          where: { id: userId },
          data: {
            passwordHash: hash,
            mustChangePassword: true,
            passwordChangedAt: new Date(),
            failedLoginAttempts: 0,
            lockedUntil: null,
          },
        });
        await db.passwordHistory.create({ data: { userId, passwordHash: hash } });
        // خروج اجباری همه نشست‌ها پس از تغییر رمز
        await db.refreshToken.updateMany({
          where: { userId, revokedAt: null },
          data: { revokedAt: new Date() },
        });
        invalidateUserCache(userId);
        await audit(session, "reset_password", "user", target.username, null, ipOf(req));
        return NextResponse.json({ ok: true });
      }
      case "set-roles": {
        const roleIds = body.roleIds ?? [];
        await db.userRole.deleteMany({ where: { userId } });
        if (roleIds.length)
          await db.userRole.createMany({ data: roleIds.map((roleId) => ({ userId, roleId })) });
        invalidateUserCache(userId);
        await audit(session, "set_roles", "user", target.username, { roleIds }, ipOf(req));
        return NextResponse.json({ ok: true });
      }
      case "set-fullname": {
        const user = await db.user.update({
          where: { id: userId },
          data: { fullName: (body.fullName || "").trim() || target.fullName },
        });
        invalidateUserCache(userId);
        await audit(session, "update", "user", target.username, { fullName: user.fullName }, ipOf(req));
        return NextResponse.json({ ok: true });
      }
      default:
        return NextResponse.json({ error: "عملیات نامعتبر" }, { status: 400 });
    }
  } catch (e) {
    console.error("[users PATCH]", e);
    return NextResponse.json({ error: "خطا در عملیات کاربر" }, { status: 500 });
  }
}

export async function DELETE(req: Request, { params }: Params) {
  const denied = await guard(req, "users.manage");
  if (denied) return denied;
  const session = await getSession(req);
  const { id } = await params;
  const userId = Number(id);
  if (!Number.isInteger(userId))
    return NextResponse.json({ error: "شناسه نامعتبر" }, { status: 400 });
  if (userId === session?.userId)
    return NextResponse.json({ error: "نمی‌توانید حساب خودتان را حذف کنید" }, { status: 400 });
  const target = await db.user.findUnique({ where: { id: userId } });
  if (!target) return NextResponse.json({ error: "کاربر یافت نشد" }, { status: 404 });
  await db.user.delete({ where: { id: userId } });
  invalidateUserCache(userId);
  await audit(session, "delete", "user", target.username, null, ipOf(req));
  return NextResponse.json({ ok: true });
}
