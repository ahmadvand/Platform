#!/usr/bin/env node
// بازگشت به SQLite (حالت دمو/توسعه)
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, "..");
const PRISMA = path.join(ROOT, "prisma");
const backup = path.join(PRISMA, "schema.sqlite.backup.prisma");

if (!fs.existsSync(backup)) {
  console.error("✗ بکاپ SQLite یافت نشد: prisma/schema.sqlite.backup.prisma");
  process.exit(1);
}
fs.copyFileSync(backup, path.join(PRISMA, "schema.prisma"));
let env = fs.readFileSync(path.join(ROOT, ".env"), "utf8");
env = env.replace(/^DATABASE_URL=.*$/m, 'DATABASE_URL="file:/home/z/my-project/db/custom.db"');
fs.writeFileSync(path.join(ROOT, ".env"), env);
console.log("✓ به SQLite بازگشت — bunx prisma generate && bun run db:push");
