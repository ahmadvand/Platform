import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ipOf } from "@/lib/auth";
import {
  ACCESS_TTL_SEC,
  RT_COOKIE,
  AT_COOKIE,
  clearCookie,
  cookieHeader,
  extractRefresh,
  randomToken,
  sha256,
  signJwt,
} from "@/lib/jwt";

export const dynamic = "force-dynamic";

/** چرخش refresh token (PLAN P1.5) — توکن قدیمی باطل و جایگزین می‌شود */
export async function POST(req: Request) {
  const oldRt = extractRefresh(req);
  if (!oldRt) return NextResponse.json({ error: "refresh token یافت نشد" }, { status: 401 });

  const tokenHash = await sha256(oldRt);
  const stored = await db.refreshToken.findUnique({
    where: { tokenHash },
    include: { user: true },
  });

  if (!stored || stored.revokedAt || stored.expiresAt < new Date() || !stored.user.isActive) {
    // توکن تکراری/مسروقه: همه توکن‌های این کاربر را باطل کن (تشخیص حملات replay)
    if (stored && stored.revokedAt) {
      await db.refreshToken.updateMany({
        where: { userId: stored.userId, revokedAt: null },
        data: { revokedAt: new Date() },
      });
    }
    const res = NextResponse.json({ error: "نشست منقضی است — دوباره وارد شوید" }, { status: 401 });
    res.headers.append("Set-Cookie", clearCookie(RT_COOKIE));
    res.headers.append("Set-Cookie", clearCookie(AT_COOKIE));
    return res;
  }

  const user = stored.user;
  const accessToken = await signJwt({
    sub: String(user.id),
    username: user.username,
    name: user.fullName,
    sa: user.isSuperadmin,
  });
  const rt = randomToken();
  await db.refreshToken.update({
    where: { id: stored.id },
    data: { revokedAt: new Date() },
  });
  await db.refreshToken.create({
    data: {
      userId: user.id,
      tokenHash: await sha256(rt),
      expiresAt: new Date(Date.now() + 7 * 24 * 3600_000),
      deviceInfo: stored.deviceInfo,
      ip: ipOf(req),
    },
  });

  const res = NextResponse.json({ ok: true });
  res.headers.append("Set-Cookie", cookieHeader(AT_COOKIE, accessToken, ACCESS_TTL_SEC));
  res.headers.append("Set-Cookie", cookieHeader(RT_COOKIE, rt, 7 * 24 * 3600));
  return res;
}
