using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations;

// پروب: رفتار EF Core 8 با اسکیما روی SQLite و تولید EnsureSchema روی SqlServer
var path = "/tmp/probe-schemadb.db";
if (File.Exists(path)) File.Delete(path);

using (var db = new ProbeDb())
{
    Console.WriteLine("Provider = " + db.Database.ProviderName);
    var table = db.Model.GetEntityTypes().Single();
    Console.WriteLine("Model table = " + table.GetTableName() + " ; schema = " + table.GetSchema());
    Console.WriteLine("EnsureCreated: " + await db.Database.EnsureCreatedAsync());
    using (var cmd = db.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        await db.Database.OpenConnectionAsync();
        var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) Console.WriteLine("sqlite_master table: " + r.GetString(0));
    }
    db.Add(new ProbeRow { Name = "x" });
    await db.SaveChangesAsync();
    Console.WriteLine("SaveChanges OK — count = " + await db.Set<ProbeRow>().CountAsync());

    // رفتار تفاضل‌گر (همان مسیر CreateMissingTablesAsync در DbInit)
    var differ = db.GetService<IMigrationsModelDiffer>();
    var design = db.GetService<IDesignTimeModel>().Model;
    var ops = differ.GetDifferences(null, design.GetRelationalModel());
    foreach (var op in ops)
        Console.WriteLine("DiffOp(null→model): " + op.GetType().Name +
            (op is CreateTableOperation ct ? " " + ct.Schema + "." + ct.Name : "") +
            (op is EnsureSchemaOperation es ? " " + es.Name : ""));
}

// پروب دوم: مدل با اسکیما روی پایگاه «موجود بدون اسکیما» — سلوک EnsureCreated و جدول‌های موجود
var path2 = "/tmp/probe-schemadb2.db";
if (File.Exists(path2)) File.Delete(path2);
using (var db = new BareDb())
{
    await db.Database.EnsureCreatedAsync();
    db.Add(new ProbeRow { Name = "old" });
    await db.SaveChangesAsync();
}
using (var db = new ProbeDb()) // همین مدل ولی با اسکیما — روی پایگاه قدیمی
{
    var has = await db.Database.EnsureCreatedAsync();
    Console.WriteLine("OldDB EnsureCreated again: " + has);
    try
    {
        Console.WriteLine("Query old DB with schema model: " + await db.Set<ProbeRow>().CountAsync());
    }
    catch (Exception ex) { Console.WriteLine("Query FAILED: " + ex.GetBaseException().Message); }
}

public class ProbeRow
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class ProbeDb : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder ob) => ob.UseSqlite("Data Source=/tmp/probe-schemadb.db");
    protected override void OnModelCreating(ModelBuilder mb) => mb.Entity<ProbeRow>().ToTable("Probes", "org");
}

public class BareDb : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder ob) => ob.UseSqlite("Data Source=/tmp/probe-schemadb2.db");
    protected override void OnModelCreating(ModelBuilder mb) => mb.Entity<ProbeRow>().ToTable("Probes");
}
