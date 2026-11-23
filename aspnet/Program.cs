using HrAttendance.Web.Data;
using HrPlatform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

// پروب فاز ۶۹ — ساخت مدل مرکب و چاپ روابط/خاصیت‌های Device
HrModuleRuntime.RegisterIntrinsic(new HrAttendance.Web.Modules.CoreModule());

var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Database:Provider"] = "Sqlite",
    ["Database:DbPath"] = ":memory:",
}).Build();

var sc = new ServiceCollection();
sc.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=:memory:"));
HrModuleRuntime.ConfigureDataServices(sc, cfg);

using var sp = sc.BuildServiceProvider();
using var scope = sp.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var model = db.Model;

var dev = model.FindEntityType(typeof(HrAttendance.Web.Domain.Device));
foreach (var fk in dev.GetForeignKeys().Where(k => k.PrincipalEntityType.ClrType == typeof(HrAttendance.Web.Domain.Company)))
    Console.WriteLine($"Device FK→Company props=[{string.Join(",", fk.Properties.Select(x => x.Name))}] nav={fk.DependentToPrincipal?.Name ?? "-"}  prinNav={fk.PrincipalToDependent?.Name ?? "-"}");
// چه کسی nav را گرفته؟
var nav = dev.GetNavigations().FirstOrDefault(n => n.Name == "Company");
Console.WriteLine($"Device.Company nav → FK props=[{string.Join(",", nav?.ForeignKey.Properties.Select(x => x.Name) ?? Array.Empty<string>())}]");
Console.WriteLine($"Total entity types: {model.GetEntityTypes().Count()}");
