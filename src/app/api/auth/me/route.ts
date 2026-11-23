import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { getSession } from "@/lib/auth";

export const dynamic = "force-dynamic";

export async function GET(req: Request) {
  const session = await getSession(req);
  if (!session) return NextResponse.json({ error: "احراز هویت لازم است" }, { status: 401 });
  const user = await db.user.findUnique({
    where: { id: session.userId },
    select: {
      id: true,
      username: true,
      fullName: true,
      isSuperadmin: true,
      mustChangePassword: true,
      passwordExpiresAt: true,
      lastLoginAt: true,
      roles: { include: { role: { select: { name: true, title: true } } } },
    },
  });
  if (!user) return NextResponse.json({ error: "کاربر یافت نشد" }, { status: 401 });
  return NextResponse.json({
    user: {
      ...user,
      passwordExpired: !!user.passwordExpiresAt && user.passwordExpiresAt < new Date(),
    },
    permissions: session.permissions,
  });
}
