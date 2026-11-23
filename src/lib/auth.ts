// هسته احراز هویت و مجوزدهی — فقط runtime اNode (route handlers)
// PLAN Phase 0 (P0.2/P0.3/P0.7) + Phase 1 (P1.1/P1.2/P1.6)

import { randomBytes, scryptSync, timingSafeEqual } from "node:crypto";
import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { PERM_KEYS } from "@/lib/permissions";
import { extractToken, verifyJwt, type JwtPayload } from "@/lib/jwt";

// ---------- هش رمز (scrypt) ----------
export function hashPassword(password: string): string {
  const salt = randomBytes(16).toString("hex");
  const hash = scryptSync(password, salt, 64).toString("hex");
  return `scrypt$${salt}$${hash}`;
}

export function verifyPassword(password: string, stored: string): boolean {
  try {
    const [algo, salt, hash] = stored.split("$");
    if (algo !== "scrypt" || !salt || !hash) return false;
    const test = scryptSync(password, salt, 64);
    const orig = Buffer.from(hash, "hex");
    return test.length === orig.length && timingSafeEqual(test, orig);
  } catch {
    return false;
  }
}

// ---------- سیاست رمز عبور (P1.2) ----------
export function validatePasswordPolicy(password: string, username?: string): string | null {
  if (password.length < 8) return "رمز عبور باید حداقل ۸ کاراکتر باشد";
  if (!/[A-Za-z]/.test(password)) return "رمز عبور باید حداقل یک حرف داشته باشد";
  if (!/[0-9]/.test(password)) return "رمز عبور باید حداقل یک رقم داشته باشد";
  if (username && password.toLowerCase().includes(username.toLowerCase()))
    return "رمز عبور نباید شامل نام کاربری باشد";
  if (/^(12345678|password|qwerty|11111111|aaaaaaaa)/i.test(password))
    return "این رمز عبور بسیار ساده است";
  return null;
}

// ---------- نشست کاربر ----------
export interface Session {
  userId: number;
  username: string;
  fullName: string;
  isSuperadmin: boolean;
  permissions: string[];
}

interface CacheEntry {
  perms: string[] | null; // null = کاربر یافت نشد/غیرفعال
  fullName: string;
  isSuperadmin: boolean;
  expires: number;
}

const g = globalThis as unknown as { __permCache?: Map<number, CacheEntry> };
const cache = (g.__permCache ??= new Map<number, CacheEntry>());
const CACHE_TTL = 30_000; // ۳۰ ثانیه

export function invalidateUserCache(userId?: number) {
  if (userId) cache.delete(userId);
  else cache.clear();
}

async function loadSession(userId: number): Promise<CacheEntry | null> {
  const hit = cache.get(userId);
  if (hit && hit.expires > Date.now()) return hit;
  const user = await db.user.findUnique({
    where: { id: userId },
    include: {
      roles: { include: { role: { include: { permissions: { include: { permission: true } } } } } },
    },
  });
  if (!user || !user.isActive) {
    const e: CacheEntry = { perms: null, fullName: "", isSuperadmin: false, expires: Date.now() + CACHE_TTL };
    cache.set(userId, e);
    return e;
  }
  const perms = new Set<string>();
  let superadmin = user.isSuperadmin;
  for (const ur of user.roles) {
    if (ur.role.name === "superadmin") superadmin = true;
    for (const rp of ur.role.permissions) perms.add(rp.permission.key);
  }
  const e: CacheEntry = {
    perms: superadmin ? [...PERM_KEYS] : [...perms],
    fullName: user.fullName,
    isSuperadmin: superadmin,
    expires: Date.now() + CACHE_TTL,
  };
  cache.set(userId, e);
  return e;
}

export async function getSession(req: Request): Promise<Session | null> {
  const token = extractToken(req);
  if (!token) return null;
  let payload: JwtPayload | null = null;
  try {
    payload = await verifyJwt(token);
  } catch {
    return null;
  }
  if (!payload) return null;
  const entry = await loadSession(Number(payload.sub));
  if (!entry || !entry.perms) return null;
  return {
    userId: Number(payload.sub),
    username: payload.username,
    fullName: entry.fullName || payload.name,
    isSuperadmin: entry.isSuperadmin,
    permissions: entry.perms,
  };
}

/**
 * نگهبان مسیرها — اگر دسترسی رد شود NextResponse خطا برمی‌گرداند، در غیر این صورت null.
 * PLAN P0.1/P0.7: همه مسیرهای کسب‌وکار باید JWT معتبر + دسترسی لازم داشته باشند.
 */
export async function guard(req: Request, perm?: string): Promise<NextResponse | null> {
  const session = await getSession(req);
  if (!session) {
    return NextResponse.json({ error: "احراز هویت لازم است — وارد شوید" }, { status: 401 });
  }
  if (perm && !session.permissions.includes(perm)) {
    await audit(session, "denied", "permission", perm, null, ipOf(req));
    return NextResponse.json(
      { error: "شما دسترسی لازم برای این عملیات را ندارید" },
      { status: 403 }
    );
  }
  return null;
}

// ---------- حسابرسی (P1.6) ----------
export function ipOf(req: Request): string {
  return (
    req.headers.get("x-forwarded-for")?.split(",")[0]?.trim() ||
    req.headers.get("x-real-ip") ||
    "local"
  );
}

export async function audit(
  actor: Session | null,
  action: string,
  entity: string,
  entityId?: string | null,
  detail?: unknown,
  ip?: string
): Promise<void> {
  try {
    await db.auditLog.create({
      data: {
        actorUserId: actor?.userId ?? null,
        actorName: actor?.fullName ?? "سیستم",
        action,
        entity,
        entityId: entityId ?? null,
        detail: detail === undefined ? null : typeof detail === "string" ? detail : JSON.stringify(detail),
        ip: ip ?? null,
      },
    });
  } catch (e) {
    console.error("[audit] خطا در ثبت رویداد:", e);
  }
}

// ---------- محدودسازی نرخ ورود (in-memory) ----------
const g2 = globalThis as unknown as { __loginHits?: Map<string, number[]> };
const hits = (g2.__loginHits ??= new Map<string, number[]>());

export function rateLimitLogin(key: string, max = 10, windowMs = 5 * 60_000): boolean {
  const now = Date.now();
  const arr = (hits.get(key) ?? []).filter((t) => now - t < windowMs);
  if (arr.length >= max) {
    hits.set(key, arr);
    return false;
  }
  arr.push(now);
  hits.set(key, arr);
  return true;
}

// ---------- قفل‌شدگی حساب (P1.1) ----------
export const MAX_LOGIN_ATTEMPTS = Number(process.env.MAX_LOGIN_ATTEMPTS ?? 5);
export const LOCK_MINUTES = Number(process.env.LOCK_MINUTES ?? 15);

// ---------- اعتبار سنجی ایمن پارامتر id ----------
export function badId(id: string | undefined): boolean {
  return !id || !/^[A-Za-z0-9_-]{1,40}$/.test(id);
}
