#!/usr/bin/env node
// ============================================================
// سوییچ به SQL Server (خواسته اصلی کاربر)
//  1) بکاپ schema.prisma فعلی
//  2) کپی schema.sqlserver.prisma → schema.prisma
//  3) تنظیم DATABASE_URL در .env
//  4) prisma generate
// ============================================================
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, "..");
const PRISMA = path.join(ROOT, "prisma");

const args = process.argv.slice(2);
const urlArg = args.find((a) => a.startsWith("--url="));
const defaultUrl =
  "sqlserver://localhost:1433;database=attendance;user=sa;password=YourStrong!Passw0rd;trustServerCertificate=true;encrypt=true";

// 1) بکاپ
const mainSchema = path.join(PRISMA, "schema.prisma");
const backup = path.join(PRISMA, "schema.sqlite.backup.prisma");
if (!fs.existsSync(backup)) {
  fs.copyFileSync(mainSchema, backup);
  console.log("✓ بکاپ اسکیمای SQLite: prisma/schema.sqlite.backup.prisma");
}

// 2) جایگزینی اسکیما
fs.copyFileSync(path.join(PRISMA, "schema.sqlserver.prisma"), mainSchema);
console.log("✓ اسکیمای SQL Server فعال شد (provider = sqlserver)");

// 3) .env
const envPath = path.join(ROOT, ".env");
let env = fs.existsSync(envPath) ? fs.readFileSync(envPath, "utf8") : "";
const newUrl = urlArg ? urlArg.slice(6) : defaultUrl;
if (/^DATABASE_URL=/m.test(env)) {
  env = env.replace(/^DATABASE_URL=.*$/m, `DATABASE_URL="${newUrl}"`);
} else {
  env += `\nDATABASE_URL="${newUrl}"\n`;
}
fs.writeFileSync(envPath, env);
console.log("✓ DATABASE_URL در .env تنظیم شد");

// 4) راهنما
console.log(`
گام‌های بعدی:
  1) SQL Server باید در دسترس باشد؛ برای نصب تازه اول اجرا کنید:
       node scripts/migrate-sqlserver.mjs --schema
     یا با Prisma:
       bunx prisma db push
  2) bunx prisma generate  (اگر خودکار اجرا نشد)
  3) JWT_SECRET را در .env تنظیم کنید (الزامی برای عملیات)
  4) اجرای سرور: bun run dev
  بازگشت به SQLite:  node scripts/use-sqlite.mjs
`);
