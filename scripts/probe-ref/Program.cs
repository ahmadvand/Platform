// probe_ref.cs — بررسی منبع جاسازی‌شدهٔ ref-en.json داخل DLL منتشرشدهٔ TranslationStudio
using System.Reflection;

var dllPath = args[0];
var asm = Assembly.LoadFrom(dllPath);
Console.WriteLine("assembly: " + asm.GetName().Name + " v" + asm.GetName().Version);
var names = asm.GetManifestResourceNames();
Console.WriteLine("manifest resources: " + names.Length);
foreach (var n in names) Console.WriteLine("  - " + n);

const string res = "TranslationStudio.Resources.ref-en.json";
var s = asm.GetManifestResourceStream(res);
if (s is null)
{
    Console.WriteLine("RESULT: MISSING " + res);
    return 1;
}
using var ms = new MemoryStream();
s.CopyTo(ms);
var bytes = ms.ToArray();
Console.WriteLine($"resource size: {bytes.Length} bytes");
try
{
    var json = System.Text.Json.JsonDocument.Parse(bytes);
    int count = 0;
    foreach (var p in json.RootElement.EnumerateObject()) count++;
    Console.WriteLine("RESULT: OK — keys=" + count);
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine("RESULT: BROKEN — " + ex.Message);
    return 2;
}
