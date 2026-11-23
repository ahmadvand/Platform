import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { audit, getSession, ipOf } from "@/lib/auth";
import { RT_COOKIE, AT_COOKIE, clearCookie, extractRefresh, sha256 } from "@/lib/jwt";

export const dynamic = "force-dynamic";

export async function POST(req: Request) {
  const session = await getSession(req);
  const rt = extractRefresh(req);
  if (rt) {
    const stored = await db.refreshToken
      .findUnique({ where: { tokenHash: await sha256(rt) } })
      .catch(() => null);
    if (stored && !stored.revokedAt) {
      await db.refreshToken.update({ where: { id: stored.id }, data: { revokedAt: new Date() } });
    }
  }
  if (session) await audit(session, "logout", "user", session.username, null, ipOf(req));
  const res = NextResponse.json({ ok: true });
  res.headers.append("Set-Cookie", clearCookie(RT_COOKIE));
  res.headers.append("Set-Cookie", clearCookie(AT_COOKIE));
  return res;
}
