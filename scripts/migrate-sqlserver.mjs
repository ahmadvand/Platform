#!/usr/bin/env node
// ============================================================
// Runner مهاجرت‌های SQL Server (PLAN Phase 3 — P3.1)
// استفاده:
//   node scripts/migrate-sqlserver.mjs            # اعمال migrationهای اعمال‌نشده
//   node scripts/migrate-sqlserver.mjs --schema   # ابتدا schema.sql کامل (نصب تازه)
// متغیر محیطی DATABASE_URL نمونه:
//   sqlserver://localhost:1433;database=attendance;user=sa;password=***;trustServerCertificate=true;encrypt=true
// ============================================================
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import sql from "mssql";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const MIGRATIONS_DIR = path.join(__dirname, "..", "database", "migrations");
const SCHEMA_FILE = path.join(__dirname, "..", "database", "schema.sql");

function parseDbUrl() {
  const raw = process.env.DATABASE_URL;
  if (!raw || !raw.startsWith("sqlserver://")) {
    console.error("✗ DATABASE_URL با پیشوند sqlserver:// تنظیم نشده است. نمونه:\n  sqlserver://localhost:1433;database=attendance;user=sa;password=***;trustServerCertificate=true");
    process.exit(1);
  }
  const url = new URL(raw.replace("sqlserver://", "http://"));
  const params = {};
  raw.split(";").forEach((p) => {
    const [k, v] = p.split("=");
    if (k && v && !k.includes("//")) params[k.trim().toLowerCase()] = v.trim();
  });
  return {
    server: url.hostname,
    port: Number(url.port || 1433),
    user: decodeURIComponent(url.username),
    password: decodeURIComponent(url.password),
    database: url.pathname.replace("/", "") || params.database || "attendance",
    trustServerCertificate: params.trustservercertificate === "true",
    encrypt: params.encrypt !== "false",
  };
}

async function main() {
  const cfg = parseDbUrl();
  const pool = await new sql.ConnectionPool(cfg).connect();
  console.log(`✓ اتصال به SQL Server: ${cfg.server}:${cfg.port}/${cfg.database}`);

  // تضمین وجود جدول ثبت مهاجرت‌ها
  await pool.request().query(`
    IF OBJECT_ID(N'[dbo].[_migrations]') IS NULL
    CREATE TABLE [dbo].[_migrations] (
      [Name] NVARCHAR(100) PRIMARY KEY,
      [AppliedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
    );`);
  // تضمین وجود اسکیمای attendance
  await pool.request().query(`
    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'attendance')
    EXEC('CREATE SCHEMA attendance');`);

  const applied = new Set(
    (await pool.request().query("SELECT [Name] FROM [dbo].[_migrations]")).recordset.map((r) => r.Name)
  );

  const installSchema = process.argv.includes("--schema");
  if (installSchema && !applied.has("_schema.sql")) {
    console.log("▶ اعمال schema.sql (نصب کامل)...");
    const tsql = fs.readFileSync(SCHEMA_FILE, "utf8");
    const batches = tsql.split(/\nGO\n/i);
    for (const b of batches) {
      if (b.trim()) await pool.request().query(b);
    }
    await pool.request().query("INSERT INTO [dbo].[_migrations] ([Name]) VALUES (N'_schema.sql')");
    console.log("✓ schema.sql اعمال شد");
  }

  const files = fs
    .readdirSync(MIGRATIONS_DIR)
    .filter((f) => f.endsWith(".sql"))
    .sort();

  let count = 0;
  for (const f of files) {
    if (applied.has(f)) continue;
    console.log(`▶ ${f} ...`);
    const tsql = fs.readFileSync(path.join(MIGRATIONS_DIR, f), "utf8");
    const batches = tsql.split(/\nGO\n/i);
    for (const b of batches) {
      if (b.trim()) await pool.request().query(b);
    }
    await pool.request().query(`INSERT INTO [dbo].[_migrations] ([Name]) VALUES (N'${f}')`);
    console.log(`  ✓ اعمال شد`);
    count++;
  }

  if (count === 0) console.log("✓ همه مهاجرت‌ها قبلاً اعمال شده‌اند");
  else console.log(`✓ ${count} مهاجرت اعمال شد`);

  await pool.close();
}

main().catch((e) => {
  console.error("✗ خطای مهاجرت:", e.message);
  process.exit(1);
});
