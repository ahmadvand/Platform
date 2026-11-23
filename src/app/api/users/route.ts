import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, guard, getSession, hashPassword, ipOf, validatePasswordPolicy } from "@/lib/auth";

export const dynamic = "force-dynamic";

export async function GET(req: Request) {
  const denied = await guard(req, "users.view");
  if (denied) return denied;
  const users = await db.user.findMany({
    select: {
      id: true,
      username: true,
      fullName: true,
      isActive: true,
      isSuperadmin: true,
      mustChangePassword: true,
      lockedUntil: true,
      lastLoginAt: true,
      failedLoginAttempts: true,
      createdAt: true,
      employee: { select: { id: true, firstName: true, lastName: true, personnelCode: true } },
      roles: { include: { role: { select: { id: true, name: true, title: true } } } },
    },
    orderBy: { id: "asc" },
  });
  return NextResponse.json({ users });
}

export async function POST(req: Request) {
  const denied = await guard(req, "users.manage");
  if (denied) return denied;
  const session = await getSession(req);
  try {
    const body = (await req.json()) as {
      username?: string;
      password?: string;
      fullName?: string;
      roleIds?: number[];
      employeeId?: string | null;
      isSuperadmin?: boolean;
    };
    const username = (body.username || "").trim().toLowerCase();
    if (!/^[a-z0-9_.-]{3,32}$/.test(username))
      return NextResponse.json(
        { error: "نام کاربری باید ۳ تا ۳۲ کاراکتر انگلیسی، رقم، نقطه یا خط تیره باشد" },
        { status: 400 }
      );
    const dup = await db.user.findUnique({ where: { username } });
    if (dup) return NextResponse.json({ error: "نام کاربری تکراری است" }, { status: 409 });

    const policyErr = validatePasswordPolicy(body.password || "", username);
    if (policyErr || !body.password)
      return NextResponse.json({ error: policyErr || "رمز عبور الزامی است" }, { status: 400 });

    const passwordHash = hashPassword(body.password);
    const user = await db.user.create({
      data: {
        username,
        passwordHash,
        fullName: (body.fullName || username).trim(),
        employeeId: body.employeeId || null,
        isSuperadmin: !!body.isSuperadmin,
        passwordHistory: { create: { passwordHash } },
        roles: { create: (body.roleIds ?? []).map((roleId) => ({ roleId })) },
      },
      include: { roles: true },
    });
    await audit(session, "create", "user", user.username, { roleIds: body.roleIds }, ipOf(req));
    return NextResponse.json({ user: { id: user.id, username: user.username } }, { status: 201 });
  } catch (e) {
    console.error("[users POST]", e);
    return NextResponse.json({ error: "خطا در ایجاد کاربر" }, { status: 500 });
  }
}
