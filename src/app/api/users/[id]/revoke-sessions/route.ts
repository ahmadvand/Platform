import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, guard, ipOf } from "@/lib/auth";

export const dynamic = "force-dynamic";

type Params = { params: Promise<{ id: string }> };

/** خروج اجباری همه نشست‌های فعال کاربر (PLAN P1.5) */
export async function POST(req: Request, { params }: Params) {
  const denied = await guard(req, "users.manage");
  if (denied) return denied;
  const { id } = await params;
  const userId = Number(id);
  if (!Number.isInteger(userId))
    return NextResponse.json({ error: "شناسه نامعتبر" }, { status: 400 });
  const target = await db.user.findUnique({ where: { id: userId } });
  if (!target) return NextResponse.json({ error: "کاربر یافت نشد" }, { status: 404 });
  const res = await db.refreshToken.updateMany({
    where: { userId, revokedAt: null },
    data: { revokedAt: new Date() },
  });
  await audit(null, "revoke_sessions", "user", target.username, { count: res.count }, ipOf(req));
  return NextResponse.json({ revoked: res.count });
}
