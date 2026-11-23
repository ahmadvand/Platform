using System;
using System.IO;
using System.Linq;
using System.Reflection;
class P
{
    static void Main()
    {
        var dir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var f in Directory.GetFiles(dir, "Microsoft.AspNetCore.Mvc*.dll"))
        {
            try
            {
                var asm = Assembly.Load(File.ReadAllBytes(f));
                var types = asm.GetTypes().Where(t => t.Name.Contains("FeatureProvider") && t.Name.Contains("Razor"));
                foreach (var t in types) Console.WriteLine(asm.GetName().Name + " -> " + t.FullName + " | public=" + t.IsPublic);
            }
            catch (Exception ex) { Console.WriteLine(f + " : " + ex.GetType().Name); }
        }
    }
}
