// ابزار تست فاز ۸۳ب — ساخت hrsettings.bin رمزشده با پیکربندی دلخواه
// (همان قالب SetupSecrets: JSON رمزشده با DataProtection keyring مشترک)
using Microsoft.AspNetCore.DataProtection;

var root = args.Length > 0 ? args[0] : "/home/z/my-project/aspnet/src/HrAttendance.Web";
var json = args.Length > 1 ? args[1] : """
{
  "Setup": { "Configured": true },
  "Database": { "Provider": "SqlServer", "ConnectionString": "Server=localhost,1433;Database=HrAttendance;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true;ConnectTimeout=3" },
  "SeedDemo": true,
  "License": { "Mode": "Off", "TrialDays": 30 },
  "Ui": { "Language": "fa" }
}
""";
var provider = DataProtectionProvider.Create(new DirectoryInfo("/home/z/my-project/aspnet/keys"),
        o => o.SetApplicationName("HrAttendance.Web"))
    .CreateProtector("HrAttendance.SetupSettings");
var cipher = provider.Protect(System.Text.Encoding.UTF8.GetBytes(json));
var path = Path.Combine(root, "hrsettings.bin");
File.WriteAllBytes(path, cipher);
Console.WriteLine("written: " + path + " (" + cipher.Length + " bytes)");
