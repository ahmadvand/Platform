// PLAN Phase 0 — P0.1: اجرای احراز هویت در سطح مسیر برای همه APIها.
// مسیرهای عمومی: ورود، refresh، webhook دستگاه‌ها (با توکن اختصاصی دستگاه) و health-check.
// لایه دوم: guard() دیتابیس‌محور در هر route handler (فعال/نقش/مجوز).

import { NextResponse, type NextRequest } from "next/server";
import { extractToken, verifyJwt } from "@/lib/jwt";

const PUBLIC_PATHS = ["/api/auth/login", "/api/auth/refresh", "/api/devices/webhook", "/api"];

function unauthorized(msg: string) {
  return NextResponse.json({ error: msg }, { status: 401 });
}

export async function middleware(req: NextRequest) {
  if (req.method === "OPTIONS") return NextResponse.next();

  const { pathname } = req.nextUrl;
  const isPublic = PUBLIC_PATHS.some((p) => (p === "/api" ? pathname === p : pathname === p || pathname.startsWith(p + "/")));
  if (isPublic) return NextResponse.next();

  const token = extractToken(req);
  if (!token) return unauthorized("احراز هویت لازم است — وارد شوید");
  const payload = await verifyJwt(token);
  if (!payload) return unauthorized("نشست منقضی یا نامعتبر است");
  return NextResponse.next();
}

export const config = {
  matcher: ["/api/:path*"],
};
