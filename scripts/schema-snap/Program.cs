using HrAttendance.Web.Data;
using Microsoft.EntityFrameworkCore;

// اسنپ‌شات DDL اسکیما از مدل AppDbContext (مالک اسکیما) — برای diff صفر-رگرسیون فاز ۶۱
var cs = "Data Source=/tmp/schema-snap.db";
if (File.Exists("/tmp/schema-snap.db")) File.Delete("/tmp/schema-snap.db");
var ob = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs);
await using var db = new AppDbContext(ob.Options);
await db.Database.EnsureCreatedAsync();
var script = db.Database.GenerateCreateScript();
var outPath = args.Length > 0 ? args[0] : "/home/z/my-project/scripts/schema-old.sql";
await File.WriteAllTextAsync(outPath, script);
var tables = System.Text.RegularExpressions.Regex.Matches(script, @"CREATE TABLE [`""]?(\w+)").Select(m => m.Groups[1].Value).OrderBy(x => x).ToList();
Console.WriteLine($"OK: {tables.Count} tables -> {outPath}");
