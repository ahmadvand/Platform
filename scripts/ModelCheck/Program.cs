using Platform.Web.Data;
using Platform.Web.Modules;
using Microsoft.EntityFrameworkCore;

// ======================================================================
// ModelCheck — مقایسهٔ مدل ترکیب EF با DDL اسکیول‌سرور مشتری (فاز ۷۰)
// ۱) هسته ثبت می‌شود، پلاگین‌ها از کنار exe کشف می‌شوند (همان قرارداد اپ).
// ۲) GenerateCreateScript فقط از مدل می‌سازد — هیچ اتصالی باز نمی‌شود.
// ۳) ستون‌های هر جدول با scripts/ddl-sqlserver.sql مقایسه می‌شود.
// خروجی: /tmp/modelcheck-create.sql + گزارش مغایرت‌ها
// ======================================================================

Platform.Contracts.ModuleRuntime.RegisterIntrinsic(new CoreModule());

var opts = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer("Server=localhost;Database=ModelCheck;User Id=x;Password=x;")
    .Options;

string script;
using (var db = new AppDbContext(opts))
{
    // لمس مدل → OnModelCreating + ComposeModules (کشف پلاگین‌ها) اجرا می‌شود
    _ = db.Model.GetEntityTypes().Count();
    script = db.Database.GenerateCreateScript();
}

File.WriteAllText("/tmp/modelcheck-create.sql", script);
Console.WriteLine($"generated script: {script.Length} chars, {script.Split("CREATE TABLE").Length - 1} tables");

// ---------- مقایسه با DDL ----------
var ddlPath = "/home/z/my-project/scripts/ddl-sqlserver.sql";
var genTables = ParseTables(script);
var ddlTables = ParseTables(File.ReadAllText(ddlPath));

int problems = 0;
foreach (var t in genTables.Keys.OrderBy(k => k))
    if (!ddlTables.ContainsKey(t)) { Console.WriteLine($"TABLE ONLY-IN-MODEL: {t}"); problems++; }
foreach (var t in ddlTables.Keys.OrderBy(k => k))
    if (!genTables.ContainsKey(t)) { Console.WriteLine($"TABLE ONLY-IN-DDL:   {t}"); problems++; }

foreach (var t in genTables.Keys.OrderBy(k => k))
{
    if (!ddlTables.TryGetValue(t, out var ddlCols)) continue;
    foreach (var c in genTables[t].Where(c => !ddlCols.Contains(c)).OrderBy(c => c))
    { Console.WriteLine($"COL ONLY-IN-MODEL {t}.{c}"); problems++; }
    foreach (var c in ddlCols.Where(c => !genTables[t].Contains(c)).OrderBy(c => c))
    { Console.WriteLine($"COL ONLY-IN-DDL   {t}.{c}"); problems++; }
}

Console.WriteLine(problems == 0
    ? $"MODEL==DDL ✓ — {genTables.Count} tables, all column sets identical"
    : $"MISMATCHES: {problems}");
Environment.Exit(problems == 0 ? 0 : 1);

// ---------- parser: جدول‌ها و ستون‌ها از CREATE TABLE ----------
static Dictionary<string, HashSet<string>> ParseTables(string sql)
{
    var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    // خطوط جدول: CREATE TABLE [Name] ( ... ) — تا پرانتز بستهٔ سرِخط
    foreach (var block in System.Text.RegularExpressions.Regex.Matches(
                 sql, @"CREATE TABLE \[([^\]]+)\] \((.*?)\n\)", System.Text.RegularExpressions.RegexOptions.Singleline))
    {
        var m = (System.Text.RegularExpressions.Match)block;
        var table = m.Groups[1].Value;
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in m.Groups[2].Value.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("[") && !t.Contains("CONSTRAINT", StringComparison.OrdinalIgnoreCase))
                cols.Add(t.Substring(1, t.IndexOf(']') - 1));
        }
        result[table] = cols;
    }
    return result;
}
