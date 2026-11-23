using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
class Probe {
  static void Main(string[] args) {
    // usage: probe <dir>  — loads entry in that dir (deps applied), reports failing typedefs with names
    var dir = args[0];
    var asm = Assembly.Load("HrAttendance.Web");
    var failed = new List<int>();
    try { asm.GetTypes(); Console.WriteLine("NO-FAILURES"); }
    catch (ReflectionTypeLoadException rtle) {
      // resolve token-by-token to enumerate failing typedefs
      foreach (var mod in asm.GetModules()) {
        for (int i = 1; i < 100000; i++) {
          int token = 0x02000000 + i;
          try { mod.ResolveType(token); }
          catch (FileNotFoundException) { failed.Add(i); }
          catch (System.Runtime.InteropServices.COMException) { break; }
          catch (ArgumentOutOfRangeException) { break; }
          catch (MissingMemberException) { break; }
          catch { }
        }
      }
    }
    if (failed.Count > 0) {
      using var fs = File.OpenRead(Path.Combine(dir, "HrAttendance.Web.dll"));
      using var pe = new PEReader(fs);
      var mr = pe.GetMetadataReader();
      int row = 0;
      foreach (var tdh in mr.TypeDefinitions) {
        row++;
        if (failed.Contains(row)) {
          var td = mr.GetTypeDefinition(tdh);
          Console.WriteLine($"POLLUTED row {row}: {mr.GetString(td.Namespace)}.{mr.GetString(td.Name)}");
        }
      }
    }
    Console.WriteLine("probe-done");
  }
}
