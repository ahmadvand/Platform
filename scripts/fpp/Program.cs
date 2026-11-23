using System.Reflection;
var dir = "/home/z/my-project/scripts/e2e67/app2";
// register resolver so module deps resolve from app2 dir
AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
{
    var n = new AssemblyName(e.Name).Name + ".dll";
    var p = Path.Combine(dir, n);
    return File.Exists(p) ? Assembly.LoadFrom(p) : null;
};
foreach (var f in Directory.GetFiles(dir, "HrModules.*.dll").OrderBy(x => x))
{
    try
    {
        var a = Assembly.LoadFrom(f);
        var ts = a.GetTypes();
        Console.WriteLine($"{Path.GetFileName(f)}: OK {ts.Length}");
    }
    catch (ReflectionTypeLoadException ex)
    {
        var msgs = ex.LoaderExceptions.Select(l => l?.Message).Distinct().Take(3);
        Console.WriteLine($"{Path.GetFileName(f)}: FAILED ({ex.Types.Count(t => t != null)}/{ex.Types.Length}) -> " + string.Join(" | ", msgs));
    }
    catch (Exception ex) { Console.WriteLine($"{Path.GetFileName(f)}: {ex.GetType().Name}: {ex.Message}"); }
}
// entry assembly too
var entry = Assembly.LoadFrom(Path.Combine(dir, "HrAttendance.Web.dll"));
try { var ts = entry.GetTypes(); Console.WriteLine($"HrAttendance.Web.dll: OK {ts.Length}"); }
catch (ReflectionTypeLoadException ex)
{
    var access = ex.LoaderExceptions.Any(l => l?.Message?.Contains("Access.Data") == true);
    Console.WriteLine($"HrAttendance.Web.dll: FAILED ({ex.Types.Count(t => t != null)}/{ex.Types.Length}) access-related={access}");
    foreach (var lx in ex.LoaderExceptions.Where(l => l?.Message?.Contains("Access.Data") == true).Take(3))
        Console.WriteLine("  " + lx?.Message);
}
