// ============================================================
// HrMigrate — مهاجرت داده از SQLite به SQL Server
// استفاده:
//   dotnet run -- --from "Data Source=hr.db" \
//                --to "Server=localhost,1433;Database=HrAttendance;User Id=sa;Password=...;TrustServerCertificate=True"
//   --create   ساخت دیتابیس در صورت نبود (پیش‌فرض: فعال)
//   --force    حذف و بازسازی کامل دیتابیس مقصد (DROP + CREATE)
//   --verify   فقط مقایسه تعداد رکوردها بدون کپی
// ============================================================
using Platform.Web.Data;
using Platform.Web.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
bool Has(string name) => args.Contains(name);

var fromCs = Arg("--from") ?? "Data Source=hr.db";
var toCs = Arg("--to") ?? "Server=localhost,1433;Database=HrAttendance;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true";
var createDb = !Has("--no-create");
var force = Has("--force");
var verifyOnly = Has("--verify");

var masterBuilder = new SqlConnectionStringBuilder(toCs) { InitialCatalog = "master" };
var targetDbName = new SqlConnectionStringBuilder(toCs).InitialCatalog;
if (string.IsNullOrWhiteSpace(targetDbName)) { Console.Error.WriteLine("✗ نام دیتابیس (Initial Catalog) در رشته اتصال مشخص نشده است."); return 2; }

Console.WriteLine("== HrMigrate — SQLite → SQL Server ==");
Console.WriteLine($"مقصد: {targetDbName}");

// ---------- ۱) ساخت دیتابیس مقصد در صورت نیاز ----------
if (createDb && !verifyOnly)
{
    await using var master = new SqlConnection(masterBuilder.ConnectionString);
    await master.OpenAsync();
    bool exists;
    await using (var cmd = master.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@n";
        cmd.Parameters.AddWithValue("@n", targetDbName);
        exists = (int)(await cmd.ExecuteScalarAsync())! > 0;
    }
    if (exists && force)
    {
        Console.WriteLine($"  حذف دیتابیس موجود ({targetDbName}) — حالت --force");
        await using var drop = master.CreateCommand();
        drop.CommandText = $"ALTER DATABASE [{targetDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{targetDbName}]";
        await drop.ExecuteNonQueryAsync();
        exists = false;
    }
    if (!exists)
    {
        Console.WriteLine($"  ساخت دیتابیس {targetDbName} ...");
        await using var cr = master.CreateCommand();
        cr.CommandText = $"CREATE DATABASE [{targetDbName}]";
        await cr.ExecuteNonQueryAsync();
    }
}

// ---------- ۲) ساخت کانتکست‌ها (یک مدل، دو پروایدر) ----------
var srcOpts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(fromCs).Options;
var tgtOpts = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(new SqlConnectionStringBuilder(toCs)
    {
        InitialCatalog = targetDbName,
        CommandTimeout = 300,
    }.ConnectionString).Options;

await using var src = new AppDbContext(srcOpts);
await using var tgt = new AppDbContext(tgtOpts);

if (!verifyOnly)
{
    Console.WriteLine("  ساخت اسکیمای مقصد (EnsureCreated) ...");
    await tgt.Database.EnsureCreatedAsync();
}

// ---------- ۳) ترتیب وابستگی جدول‌ها ----------
var plan = new List<(string Table, bool Identity)>
{
    ("Companies", false),
    ("Shifts", false),
    ("Zones", false),
    ("Contractors", false),
    ("Devices", false),
    ("Roles", true),
    ("Permissions", true),
    ("Holidays", false),
    ("Departments", false),   // خود-ارجاع: مرتب‌سازی توپولوژیک
    ("Positions", false),
    ("Employees", false),
    ("Visitors", false),
    ("Gates", false),
    ("Users", true),
    ("UserRoles", false),
    ("RolePermissions", false),
    ("RefreshTokens", false),
    ("PasswordHistories", false),
    ("PasswordResets", false),
    ("AuditLogs", false),
    ("Contracts", false),
    ("Decrees", false),
    ("AttendanceRecords", false),
    ("LeaveRequests", false),
    ("Missions", false),
    ("OvertimePermits", false),
    ("CorrectionRequests", false),
    ("DeviceCommands", false),
    ("DeviceUsers", false),
    ("DeviceBiometrics", false),
    ("EmployeeBiometrics", false),
    ("AccessPermissions", false),
    ("AccessEvents", false),
    ("VisitorCards", false),
    ("VisitLogs", false),
    ("SecurityAlerts", false),
    ("PayrollRuns", false),
    ("Payslips", false),
    // فاز ۱۳ — اعلان‌ها
    ("NotificationRules", false),
    ("NotificationLogs", true),
    ("NotificationSettings", false),
};

static async Task<List<object>> Read<T>(IQueryable<T> q) where T : class =>
    await q.AsNoTracking().Cast<object>().ToListAsync();

// ---------- ۴) کپی ----------
var total = 0;
var sw = System.Diagnostics.Stopwatch.StartNew();
foreach (var (table, identity) in plan)
{
    var rows = table switch
    {
        "Companies" => await Read(src.Companies),
        "Shifts" => await Read(src.Shifts),
        "Zones" => await Read(src.Zones),
        "Contractors" => await Read(src.Contractors),
        "Devices" => await Read(src.Devices),
        "Roles" => await Read(src.Roles),
        "Permissions" => await Read(src.Permissions),
        "Holidays" => await Read(src.Holidays),
        "Departments" => await Read(src.Departments),
        "Positions" => await Read(src.Positions),
        "Employees" => await Read(src.Employees),
        "Visitors" => await Read(src.Visitors),
        "Gates" => await Read(src.Gates),
        "Users" => await Read(src.Users),
        "UserRoles" => await Read(src.UserRoles),
        "RolePermissions" => await Read(src.RolePermissions),
        "RefreshTokens" => await Read(src.RefreshTokens),
        "PasswordHistories" => await Read(src.PasswordHistories),
        "PasswordResets" => await Read(src.PasswordResets),
        "AuditLogs" => await Read(src.AuditLogs),
        "Contracts" => await Read(src.Contracts),
        "Decrees" => await Read(src.Decrees),
        "AttendanceRecords" => await Read(src.AttendanceRecords),
        "LeaveRequests" => await Read(src.LeaveRequests),
        "Missions" => await Read(src.Missions),
        "OvertimePermits" => await Read(src.OvertimePermits),
        "CorrectionRequests" => await Read(src.CorrectionRequests),
        "DeviceCommands" => await Read(src.DeviceCommands),
        "DeviceUsers" => await Read(src.DeviceUsers),
        "DeviceBiometrics" => await Read(src.DeviceBiometrics),
        "EmployeeBiometrics" => await Read(src.EmployeeBiometrics),
        "AccessPermissions" => await Read(src.AccessPermissions),
        "AccessEvents" => await Read(src.AccessEvents),
        "VisitorCards" => await Read(src.VisitorCards),
        "VisitLogs" => await Read(src.VisitLogs),
        "SecurityAlerts" => await Read(src.SecurityAlerts),
        "PayrollRuns" => await Read(src.PayrollRuns),
        "Payslips" => await Read(src.Payslips),
        "NotificationRules" => await Read(src.NotificationRules),
        "NotificationLogs" => await Read(src.NotificationLogs),
        "NotificationSettings" => await Read(src.NotificationSettings),
        _ => throw new InvalidOperationException($"جدول ناشناخته: {table}"),
    };
    if (table == "Departments") rows = TopoSortDepartments(rows.Cast<Department>()).Cast<object>().ToList();

    int dst = await CountAsync(tgt, table);

    if (verifyOnly)
    {
        Console.WriteLine($"  {table,-22} src={rows.Count,-6} dst={dst,-6}");
        continue;
    }
    if (rows.Count == 0) { Console.WriteLine($"  {table,-22} — ۰ ردیف، رد شد"); continue; }
    if (dst > 0) { Console.WriteLine($"  {table,-22} مقصد پر است ({dst}) — رد شد (idempotent)"); continue; }

    var conn = (SqlConnection)tgt.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

    async Task Exec(string sql)
    {
        await using var c = conn.CreateCommand();
        c.CommandText = sql;
        await c.ExecuteNonQueryAsync();
    }

    if (identity) await Exec($"SET IDENTITY_INSERT [{table}] ON");
    tgt.AddRange(rows);
    await tgt.SaveChangesAsync();
    tgt.ChangeTracker.Clear();
    if (identity)
    {
        await Exec($"SET IDENTITY_INSERT [{table}] OFF");
        // بازچینش IDENTITY برای ادامه درج آینده
        await using var maxc = conn.CreateCommand();
        maxc.CommandText = $"SELECT COALESCE(MAX(Id),0) FROM [{table}]";
        var maxId = Convert.ToInt32(await maxc.ExecuteScalarAsync());
        await Exec($"DBCC CHECKIDENT ('[{table}]', RESEED, {maxId})");
    }

    total += rows.Count;
    Console.WriteLine($"  {table,-22} {rows.Count,6} ردیف کپی شد");
}
Console.WriteLine($"کپی کامل شد: {total} ردیف در {sw.ElapsedMilliseconds}ms");

// ---------- ۵) راستی‌آزمایی تعداد‌ها ----------
Console.WriteLine("== راستی‌آزمایی ==");
var bad = 0;
foreach (var (table, _) in plan)
{
    var s = await CountAsync(src, table);
    var d = await CountAsync(tgt, table);
    var ok = s == d;
    if (!ok) bad++;
    Console.WriteLine($"  {(ok ? "OK" : "XX")} {table,-22} src={s,-6} dst={d,-6}");
}
Console.WriteLine(bad == 0 ? "همه جدول‌ها هم‌تعداد — مهاجرت موفق ✓" : $"⚠ {bad} جدول هم‌تعداد نیست!");
return bad == 0 ? 0 : 1;

static async Task<int> CountAsync(AppDbContext db, string table)
{
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
    await using var c = conn.CreateCommand();
    c.CommandText = $"SELECT COUNT(*) FROM [{table}]";
    return Convert.ToInt32(await c.ExecuteScalarAsync());
}

// مرتب‌سازی توپولوژیک برای خود-ارجاع Department (والد قبل از فرزند)
static List<Department> TopoSortDepartments(IEnumerable<Department> all)
{
    var done = new HashSet<string?>();
    var outp = new List<Department>();
    var remaining = all.ToList();
    while (remaining.Count > 0)
    {
        var ready = remaining.Where(d => d.ParentId is null || done.Contains(d.ParentId)).ToList();
        if (ready.Count == 0) { outp.AddRange(remaining); break; } // چرخه نامعتبر — همه را بریز
        foreach (var d in ready) { outp.Add(d); done.Add(d.Id); remaining.Remove(d); }
    }
    return outp;
}
