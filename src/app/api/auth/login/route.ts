import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureRbac } from "@/lib/seed";
import {
  ipOf,
  rateLimitLogin,
  verifyPassword,
  audit,
  LOCK_MINUTES,
  MAX_LOGIN_ATTEMPTS,
} from "@/lib/auth";
import { ACCESS_TTL_SEC, RT_COOKIE, AT_COOKIE, cookieHeader, randomToken, sha256, signJwt } from "@/lib/jwt";

export const dynamic = "force-dynamic";

export async function POST(req: Request) {
  const ip = ipOf(req);
  try {
    await ensureRbac(); // تضمین وجود کاربران/نقش‌ها حتی پس از ری‌استارت سرد
    const body = (await req.json()) as { username?: string; password?: string };
    const username = (body.username || "").trim();
    const password = body.password || "";
    if (!username || !password)
      return NextResponse.json({ error: "نام کاربری و رمز عبور الزامی است" }, { status: 400 });

    if (!rateLimitLogin(`${ip}:${username}`))
      return NextResponse.json(
        { error: "تلاش‌های ورود بیش از حد مجاز — چند دقیقه بعد دوباره امتحان کنید" },
        { status: 429 }
      );

    const user = await db.user.findUnique({
      where: { username },
      include: { roles: { include: { role: true } } },
    });

    if (!user || !(await verifyPassword(password, user.passwordHash))) {
      if (user) {
        const attempts = user.failedLoginAttempts + 1;
        const lock = attempts >= MAX_LOGIN_ATTEMPTS;
        await db.user.update({
          where: { id: user.id },
          data: {
            failedLoginAttempts: lock ? 0 : attempts,
            lockedUntil: lock ? new Date(Date.now() + LOCK_MINUTES * 60_000) : user.lockedUntil,
          },
        });
        await audit(null, lock ? "account_locked" : "login_failed", "user", username, { attempts }, ip);
        if (lock)
          return NextResponse.json(
            { error: `حساب به دلیل تلاش‌های ناموفق قفل شد — ${LOCK_MINUTES} دقیقه بعد تلاش کنید` },
            { status: 423 }
          );
      } else {
        await audit(null, "login_failed", "user", username, null, ip);
      }
      return NextResponse.json({ error: "نام کاربری یا رمز عبور اشتباه است" }, { status: 401 });
    }

    if (!user.isActive)
      return NextResponse.json({ error: "حساب کاربری غیرفعال است" }, { status: 403 });
    if (user.lockedUntil && user.lockedUntil > new Date())
      return NextResponse.json(
        { error: `حساب موقتاً قفل است — بعداً تلاش کنید` },
        { status: 423 }
      );

    const passwordExpired = !!user.passwordExpiresAt && user.passwordExpiresAt < new Date();

    const accessToken = await signJwt({
      sub: String(user.id),
      username: user.username,
      name: user.fullName,
      sa: user.isSuperadmin,
    });

    // refresh token چرخشی مبتنی بر DB (PLAN P1.5)
    const rt = randomToken();
    const expiresAt = new Date(Date.now() + 7 * 24 * 3600_000);
    await db.refreshToken.create({
      data: {
        userId: user.id,
        tokenHash: await sha256(rt),
        expiresAt,
        deviceInfo: (req.headers.get("user-agent") || "").slice(0, 200) || null,
        ip,
      },
    });

    await db.user.update({
      where: { id: user.id },
      data: { failedLoginAttempts: 0, lockedUntil: null, lastLoginAt: new Date() },
    });
    await audit(
      {
        userId: user.id,
        username: user.username,
        fullName: user.fullName,
        isSuperadmin: user.isSuperadmin,
        permissions: [],
      },
      "login",
      "user",
      user.username,
      { roles: user.roles.map((r) => r.role.name) },
      ip
    );

    const res = NextResponse.json({
      user: {
        id: user.id,
        username: user.username,
        fullName: user.fullName,
        isSuperadmin: user.isSuperadmin,
        mustChangePassword: user.mustChangePassword,
        passwordExpired,
      },
    });
    res.headers.append("Set-Cookie", cookieHeader(AT_COOKIE, accessToken, ACCESS_TTL_SEC));
    res.headers.append("Set-Cookie", cookieHeader(RT_COOKIE, rt, 7 * 24 * 3600));
    return res;
  } catch (e) {
    console.error("[login]", e);
    return NextResponse.json({ error: "خطای داخلی در ورود" }, { status: 500 });
  }
}
