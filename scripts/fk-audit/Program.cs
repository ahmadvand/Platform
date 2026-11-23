using System.Text.RegularExpressions;
using HrAttendance.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

// ============================================================
// ممیزی FK و ایندکس — کشف نقض‌های SQL Server روی مدل واقعی EF
// بدون نیاز به سرور واقعی:
//   (بدون آرگومان)  → ممیزی «چرخه یا مسیرهای آبشاری چندگانه»
//   ddl <مسیر>      → تولید اسکریپت واقعی SQL Server (همان DDL م ensureCreated)
//   idx             → ممیزی طول کلید ایندکس‌ها (سقف ۱۷۰۰ بایت nonclustered / ۹۰۰ خوشه‌ای)
// ============================================================
var sc = new ServiceCollection();
sc.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer("Server=127.0.0.1;Database=NoRealConnection;User Id=x;Password=y;Encrypt=False;TrustServerCertificate=True"));
var sp = sc.BuildServiceProvider();
using var scope = sp.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var rel = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();

var mode = args.Length > 0 ? args[0] : "audit";

// ---------- حالت ddl: خروجی اسکریپت واقعی ----------
if (mode == "ddl")
{
    var differ = db.GetService<IMigrationsModelDiffer>();
    var generator = db.GetService<IMigrationsSqlGenerator>();
    var designModel = db.GetService<IDesignTimeModel>().Model;
    var ops = differ.GetDifferences(null, designModel.GetRelationalModel());
    var cmds = generator.Generate(ops, db.Model);
    var path = args.Length > 1 ? args[1] : "ddl-sqlserver.sql";
    await File.WriteAllLinesAsync(path, cmds.Select(c => c.CommandText));
    Console.WriteLine($"اسکریپت SQL Server با {cmds.Count()} دستور در «{path}» ذخیره شد");
    return;
}

// ---------- ممیزی مسیر آبشاری ----------
var edges = new List<(string Dep, string Prin, string Action)>();
foreach (var t in rel.Tables)
    foreach (var fk in t.ForeignKeyConstraints)
        edges.Add((t.Name, fk.PrincipalTable.Name, fk.OnDeleteAction.ToString()));

if (mode == "audit")
{
    Console.WriteLine($"جمع FKها: {edges.Count}");
    foreach (var e in edges.OrderBy(x => x.Dep).ThenBy(x => x.Prin))
        Console.WriteLine($"  {e.Dep,-24} ← {e.Prin,-22} [{e.Action}]");
}

// گراف اکشن‌های آبشاری (Cascade / SetNull / SetDefault) — فقط این‌ها مسیر آبشاری می‌سازند
var casc = edges.Where(e => e.Action is "Cascade" or "SetNull" or "SetDefault")
                .Select(e => (From: e.Prin, To: e.Dep))
                .ToList();
var nodes = casc.SelectMany(x => new[] { x.From, x.To }).Distinct().ToList();
var adj = nodes.ToDictionary(n => n, n => casc.Where(x => x.From == n).Select(x => x.To).ToList());

// شمار مسیرهای ساده از هر رأس — اگر جفتی با بیش از یک مسیر باشد نقض است
bool violation = false;
foreach (var s in nodes)
{
    var pathCount = nodes.ToDictionary(n => n, n => 0L);
    void Dfs(string cur, HashSet<string> seen)
    {
        if (!adj.TryGetValue(cur, out var outs)) return;
        foreach (var nxt in outs)
        {
            if (seen.Contains(nxt)) continue; // چرخه جداگانه گزارش می‌شود
            pathCount[nxt]++;
            seen.Add(nxt);
            Dfs(nxt, seen);
            seen.Remove(nxt);
        }
    }
    Dfs(s, new HashSet<string> { s });
    foreach (var (d, c) in pathCount.Where(x => x.Value > 1))
    {
        violation = true;
        Console.WriteLine($"نقض: از [{s}] به [{d}] بیش از یک مسیر آبشاری وجود دارد ({c})");
    }
}
// چرخه‌ها
foreach (var s in nodes)
    foreach (var n in adj[s])
        if (Reachable(adj, n, s)) { violation = true; Console.WriteLine($"نقض: چرخه آبشاری [{s}] → [{n}] → ... → [{s}]"); }

if (mode == "audit")
    Console.WriteLine(violation ? "نتیجه: نقض‌های SQL Server یافت شد" : "نتیجه: گراف آبشاری برای SQL Server سالم است");

// ---------- ممیزی طول کلید ایندکس‌ها ----------
if (mode == "idx")
{
    bool bad = false;
    long ColBytes(string? colType)
    {
        if (string.IsNullOrEmpty(colType)) return 4;
        var t = colType.ToLowerInvariant();
        if (t.Contains("max") || t.StartsWith("ntext") || t.StartsWith("text") || t.StartsWith("image"))
            return long.MaxValue; // غیرقابل استفاده در کلید ایندکس
        var m = Regex.Match(t, @"\((\d+)\)");
        var n = m.Success ? long.Parse(m.Groups[1].Value) : 0;
        if (t.StartsWith("nvarchar") || t.StartsWith("nchar")) return 2 * n;
        if (t.StartsWith("varchar") || t.StartsWith("char") || t.StartsWith("varbinary") || t.StartsWith("binary")) return n;
        return t switch
        {
            "int" or "real" or "smallint" or "float" or "datetime" or "datetime2" or "smalldatetime" => t == "smallint" ? 2 : t == "int" ? 4 : 8,
            "bigint" => 8,
            "tinyint" => 1,
            "bit" => 1,
            "uniqueidentifier" => 16,
            "date" => 3,
            "time" => 5,
            "money" => 8,
            _ => t.StartsWith("decimal") || t.StartsWith("numeric") ? 17 : 4,
        };
    }

    void Check(IEnumerable<(string Name, IEnumerable<string> ColTypes, bool Clustered)> items, string kind)
    {
        foreach (var it in items)
        {
            long total = 0; bool hasMax = false;
            var parts = new List<string>();
            foreach (var ct in it.ColTypes)
            {
                var b = ColBytes(ct);
                if (b == long.MaxValue) { hasMax = true; parts.Add($"{ct}(MAX!)"); }
                else { total += b; parts.Add($"{ct}={b}B"); }
            }
            var limit = it.Clustered ? 900 : 1700;
            var flag = hasMax || total > limit ? "❌ خطا" : total > 900 ? "⚠ هشدار" : null;
            if (flag == null) continue;
            bad = true;
            Console.WriteLine($"{flag}: [{kind}] {it.Name} — جمع {total} بایت (سقف {limit}): {string.Join(" + ", parts)}");
        }
    }

    foreach (var t in rel.Tables)
    {
        if (t.PrimaryKey is { } pk)
            Check(new[] { (t.Name + " (PK)", pk.Columns.Select(c => c.StoreType), true) }, "کلید خوشه‌ای");
        Check(t.Indexes.Select(i => ($"{t.Name}.{i.Name ?? string.Join('_', i.Columns.Select(c => c.Name))}",
                i.Columns.Select(c => c.StoreType), false)), "ایندکس");
        // unique constraintها (جدا از PK) — nonclustered
        foreach (var uc in t.UniqueConstraints.Where(u => u != t.PrimaryKey))
            Check(new[] { (t.Name + " (UQ:" + uc.Name + ")", uc.Columns.Select(c => c.StoreType), false) }, "قید یکتا");
    }
    Console.WriteLine(bad ? "نتیجه: ایندکس/کلید ناقض سقف طول SQL Server یافت شد" : "نتیجه: همه ایندکس‌ها/کلیدها زیر سقف طول SQL Server هستند");
}

static bool Reachable(Dictionary<string, List<string>> adj, string from, string target)
{
    var seen = new HashSet<string>();
    var st = new Stack<string>(); st.Push(from);
    while (st.Count > 0)
    {
        var c = st.Pop();
        if (c == target) return true;
        if (!seen.Add(c) || !adj.ContainsKey(c)) continue;
        foreach (var n in adj[c]) st.Push(n);
    }
    return false;
}
