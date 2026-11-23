using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

var bin = "/home/z/my-project/aspnet/src/Platform.Web/appsettings.bin";
var sc = new ServiceCollection();
sc.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("/home/z/my-project/aspnet/keys"))
    .SetApplicationName("Platform.Web");
var sp = sc.BuildServiceProvider();
var prot = sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("Platform.SetupSettings");
var node = JsonNode.Parse(Encoding.UTF8.GetString(prot.Unprotect(File.ReadAllBytes(bin))))!;

if (args.Length > 0 && args[0] == "--set")
{
    foreach (var arg in args.Skip(1))
    {
        var i = arg.IndexOf('=');
        var k = arg[..i];
        var v = arg[(i + 1)..];
        var parts = k.Split(':');
        if (parts.Length == 2)
        {
            node[parts[0]] ??= new JsonObject();
            node[parts[0]]![parts[1]] = v;
        }
        else node[k] = v;
    }
    File.WriteAllBytes(bin, prot.Protect(Encoding.UTF8.GetBytes(node.ToJsonString())));
    Console.WriteLine("written: " + string.Join(", ", args.Skip(1)));
    return;
}
Console.WriteLine(node.ToJsonString());
