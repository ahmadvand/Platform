// JWT HS256 — پیاده‌سازی با Web Crypto (سازگار با edge + node)
// PLAN Phase 0/1: access token کوتاه‌عمر + refresh token چرخشی مبتنی بر DB

const enc = new TextEncoder();
const dec = new TextDecoder();

function b64url(buf: ArrayBuffer | Uint8Array): string {
  const bytes = buf instanceof Uint8Array ? buf : new Uint8Array(buf);
  let s = "";
  for (const b of bytes) s += String.fromCharCode(b);
  return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function b64urlDecode(s: string): Uint8Array {
  s = s.replace(/-/g, "+").replace(/_/g, "/");
  while (s.length % 4) s += "=";
  const bin = atob(s);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

async function hmacKey(secret: string): Promise<CryptoKey> {
  return crypto.subtle.importKey("raw", enc.encode(secret), { name: "HMAC", hash: "SHA-256" }, false, [
    "sign",
    "verify",
  ]);
}

export interface JwtPayload {
  sub: string; // userId
  username: string;
  name: string;
  sa?: boolean; // superadmin
  exp: number;
  iat: number;
}

export const ACCESS_TTL_SEC = 15 * 60; // ۱۵ دقیقه

let cachedSecret: string | null = null;
export async function ensureJwtSecret(): Promise<string> {
  if (cachedSecret) return cachedSecret;
  const fromEnv = process.env.JWT_SECRET;
  if (fromEnv && fromEnv.length >= 16) {
    cachedSecret = fromEnv;
    return cachedSecret;
  }
  // fallback قطعی: از DATABASE_URL مشتق می‌شود تا در همه module instances (middleware/روت) یکسان باشد.
  // برای محیط عملیاتی حتماً JWT_SECRET را در .env تنظیم کنید.
  cachedSecret = await sha256(`hr-jwt-fallback|${process.env.DATABASE_URL ?? "local"}`);
  console.warn("[auth] JWT_SECRET تنظیم نشده — کلید fallback قطعی استفاده شد. در عملیات JWT_SECRET را در .env قرار دهید.");
  return cachedSecret;
}

export async function signJwt(payload: Omit<JwtPayload, "exp" | "iat">, ttlSec = ACCESS_TTL_SEC): Promise<string> {
  const secret = await ensureJwtSecret();
  const now = Math.floor(Date.now() / 1000);
  const body: JwtPayload = { ...payload, iat: now, exp: now + ttlSec };
  const head = b64url(enc.encode(JSON.stringify({ alg: "HS256", typ: "JWT" })));
  const data = `${head}.${b64url(enc.encode(JSON.stringify(body)))}`;
  const sig = await crypto.subtle.sign("HMAC", await hmacKey(secret), enc.encode(data));
  return `${data}.${b64url(sig)}`;
}

export async function verifyJwt(token: string): Promise<JwtPayload | null> {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    const secret = await ensureJwtSecret();
    const data = `${parts[0]}.${parts[1]}`;
    const ok = await crypto.subtle.verify(
      "HMAC",
      await hmacKey(secret),
      b64urlDecode(parts[2]).buffer as ArrayBuffer,
      enc.encode(data)
    );
    if (!ok) return null;
    const payload = JSON.parse(dec.decode(b64urlDecode(parts[1]))) as JwtPayload;
    if (!payload.exp || payload.exp * 1000 < Date.now()) return null;
    return payload;
  } catch {
    return null;
  }
}

/** هش SHA-256 (برای ذخیره امن refresh token و رمزهای موقت) */
export async function sha256(input: string): Promise<string> {
  const d = await crypto.subtle.digest("SHA-256", enc.encode(input));
  return Array.from(new Uint8Array(d))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

export function randomToken(bytes = 48): string {
  return b64url(crypto.getRandomValues(new Uint8Array(bytes)));
}

// ---------- کمکی‌های کوکی ----------
export const AT_COOKIE = "hr_at";
export const RT_COOKIE = "hr_rt";

export function cookieHeader(name: string, value: string, maxAgeSec: number): string {
  const attrs = [
    `${name}=${value}`,
    "Path=/",
    "HttpOnly",
    "SameSite=Lax",
    `Max-Age=${maxAgeSec}`,
  ];
  if (process.env.NODE_ENV === "production") attrs.push("Secure");
  return attrs.join("; ");
}

export function clearCookie(name: string): string {
  return `${name}=; Path=/; HttpOnly; SameSite=Lax; Max-Age=0`;
}

/** استخراج access token از کوکی یا هدر Authorization (پشتیبانی از سرویس پل) */
export function extractToken(req: Request): string | null {
  const auth = req.headers.get("authorization");
  if (auth?.toLowerCase().startsWith("bearer ")) return auth.slice(7).trim();
  const cookie = req.headers.get("cookie") || "";
  const m = cookie.match(new RegExp(`(?:^|;\\s*)${AT_COOKIE}=([^;]+)`));
  return m ? m[1] : null;
}

export function extractRefresh(req: Request): string | null {
  const cookie = req.headers.get("cookie") || "";
  const m = cookie.match(new RegExp(`(?:^|;\\s*)${RT_COOKIE}=([^;]+)`));
  return m ? m[1] : null;
}
