using Platform.Web.Data;
using Microsoft.EntityFrameworkCore;

// Model probe — کل مدل EF را با تمام پلاگین‌های کشف‌شده می‌سازد و:
//   ۱) ستون‌های شادو (PropertyInfo == null — مثل PositionId1) را فهرست می‌کند
//   ۲) ستون‌های مدل را با ddl-sqlserver.sql مقایسه می‌کند
var opts = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=/tmp/probe-empty.db")
    .Options;

using var db = new AppDbContext(opts);

Console.WriteLine("=== SHADOW COLUMNS (no CLR property) ===");
int shadowCount = 0;
foreach (var et in db.Model.GetEntityTypes().OrderBy(e => e.Name))
{
    var shadows = et.GetProperties().Where(p => p.PropertyInfo is null).ToList();
    foreach (var s in shadows)
    {
        shadowCount++;
        Console.WriteLine($"  {et.ClrType.Name}.{s.Name}  (table: {et.GetTableName()}, col: {s.GetColumnName()})");
    }
}
if (shadowCount == 0) Console.WriteLine("  (none)");

Console.WriteLine();
Console.WriteLine("=== MODEL vs DDL DIFF ===");
// DDL: all (table, column) pairs
var ddl = new HashSet<(string, string)>();
var ddlText = File.ReadAllText("/home/z/my-project/scripts/ddl-sqlserver.sql");
foreach (System.Text.RegularExpressions.Match m in
         System.Text.RegularExpressions.Regex.Matches(ddlText,
         @"CREATE TABLE \[([^\]]+)\]\s*\((.*?)\n\)", System.Text.RegularExpressions.RegexOptions.Singleline))
{
    var tbl = m.Groups[1].Value;
    foreach (var line in m.Groups[2].Value.Split('\n'))
    {
        var t = line.Trim();
        var cm = System.Text.RegularExpressions.Regex.Match(t, @"^\[([^\]]+)\]");
        if (cm.Success && !t.StartsWith("CONSTRAINT") && !t.StartsWith("PRIMARY KEY") && !t.StartsWith("UNIQUE") && !t.StartsWith("CHECK") && !t.StartsWith("FOREIGN KEY"))
            ddl.Add((tbl, cm.Groups[1].Value));
    }
}
Console.WriteLine($"  DDL columns parsed: {ddl.Count}");

var model = new HashSet<(string, string)>();
foreach (var et in db.Model.GetEntityTypes())
{
    var tbl = et.GetTableName();
    if (tbl is null) continue;
    foreach (var p in et.GetProperties())
        model.Add((tbl, p.GetColumnName()));
}
Console.WriteLine($"  Model columns: {model.Count}");

var modelOnly = model.Except(ddl).OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();
var ddlOnly = ddl.Except(model).OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();

Console.WriteLine($"  In MODEL but NOT in DDL ({modelOnly.Count}):");
foreach (var (t, c) in modelOnly) Console.WriteLine($"    - [{t}].[{c}]");
Console.WriteLine($"  In DDL but NOT in MODEL ({ddlOnly.Count}):");
foreach (var (t, c) in ddlOnly.Take(40)) Console.WriteLine($"    + [{t}].[{c}]");
if (ddlOnly.Count > 40) Console.WriteLine($"    ... +{ddlOnly.Count - 40} more");
