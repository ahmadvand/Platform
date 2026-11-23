using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Razor.Hosting;

namespace Platform.Web.Infrastructure;

// ======================================================================
// فاز ۶۷ — Part ایمن MVC (حلقهٔ مفقود «حذف دستی DLL از بیلد کامل»)
// ----------------------------------------------------------------------
// تا این فاز، DI و ساخت مدل EF با گارد «#if بیلد» + «حضور فایل DLL» کاملاً
// مشروط شده بودند (ModuleRegistration / ComposeX در AppDbContext)؛ ولی اسکن
// کنترلر/ویوِ MVC روی «کل اسمبلی» GetTypes می‌زند و در NET. اگر حتی یک
// نوعِ اسمبلی به DLL غایب وابسته باشد (مثلاً closure/state-machine تولیدی
// کامپایلر برای یک متد async که entityهای ماژول را لمس می‌کند)، همان یک
// نوع، کل فراخوانی GetTypes را با FileNotFoundException می‌کشد — یعنی
// دقیقاً همان کرش Program.<Main> در MapControllerRoute که مشتری دید.
//
// راه‌حل این فایل: AssemblyPart سفارشی که به‌جای مرگِ تمام‌یا-هیچ،
// شمارش نوع‌به‌نوع می‌کند:
//   • انواع سالمِ اسمبلی حاضرند و به MVC معرفی می‌شوند؛
//   • فقط نوع‌های ناقص (وابسته به DLL غایب) از فهرست حذف می‌شوند —
//     همان‌هایی که در بستهٔ بدون ماژول به‌هرحال هیچ مسیری به آن‌ها نمی‌رسد
//     (ModuleGate مسیرهای ماژول غایب را می‌بندد و دادهٔ داشبورد با
//     HrModuleFallbacks صفر می‌شود).
// هزینهٔ عادی: صفر — ابتدا همان GetTypes معمولی زده می‌شود و فقط در
// صورت استثنا مسیر نوع‌به‌نوع اجرا می‌شود.
// ======================================================================

/// <summary>ApplicationPart مستقل با شمارش مقاوم نوع‌ها؛ وابستگی ناقصِ یک نوع، بقیهٔ اپ را نمی‌کشد.
/// دو قرارداد NET8. همین‌جا پیاده می‌شود:
///   • IApplicationPartTypeProvider.Types → کنترلرها/تگ‌هلپرها/ویوکامپوننت‌ها
///   • IRazorCompiledItemProvider.CompiledItems → ویوهای Razor کامپایل‌شده
///     (ویوها از این قرارداد می‌آیند — CompiledRazorAssemblyPart استاندارد فریم‌ورک همین کار را
///     می‌کند؛ نسخهٔ ما نوع‌به‌نوع و مقاوم است تا نوع ناقص، همهٔ ویوها را نکشد).
/// عمداً از AssemblyPart ارث نمی‌برد تا هیچ مسیر «پیاده‌سازی پایه» باقی نماند.</summary>
internal sealed class SafeAssemblyPart : ApplicationPart, IApplicationPartTypeProvider, IRazorCompiledItemProvider
{
    public SafeAssemblyPart(Assembly assembly) { Assembly = assembly; }

    public Assembly Assembly { get; }

    public override string Name => Assembly.GetName().Name ?? Assembly.FullName ?? "?";

    // قرارداد IApplicationPartTypeProvider — نسخهٔ امن
    public IEnumerable<TypeInfo> Types => ComputeTypes();

    // قرارداد IRazorCompiledItemProvider — ویوهای کامپایل‌شده (نوع‌به‌نوع)
    IEnumerable<RazorCompiledItem> IRazorCompiledItemProvider.CompiledItems => ComputeCompiledItems();

    private IReadOnlyList<RazorCompiledItem> ComputeCompiledItems()
    {
        // صفت RazorCompiledItemAttribute «سطح اسمبلی» است (نه سطح نوع) و آرگومان Type آن
        // به کلاسِ ویو اشاره می‌کند؛ اگر آن ویو به DLL غایب وابسته باشد، resolve همان
        // آرگومان استثنا می‌دهد — پس با CustomAttributeData (بدون instantiate صفت)
        // و try/catch «موردبه‌مورد»، فقط ویوهای ناقص حذف می‌شوند.
        var list = new List<RazorCompiledItem>();
        try
        {
            foreach (var data in Assembly.GetCustomAttributesData())
            {
                try
                {
                    if (data.AttributeType?.Name != nameof(RazorCompiledItemAttribute)) continue;
                    var args = data.ConstructorArguments;
                    if (args.Count != 3) continue;
                    if (args[0].Value is not Type t) continue;
                    if (args[1].Value is not string kind || args[2].Value is not string identifier) continue;
                    list.Add(new AttrCompiledItem(t, kind, identifier));
                }
                catch { /* ویوی وابستگی‌ناقص — فقط همان ویو حذف می‌شود */ }
            }
        }
        catch { /* خود اسمبلی قابل خواندن نبود — هیچ ویویی از آن بخش نمی‌شود */ }
        return list;
    }

    /// <summary>همان آیتم پیش‌فرض لودر فریم‌ورک — از (Type, Kind, Identifier) صفت ساخته می‌شود.</summary>
    private sealed class AttrCompiledItem : RazorCompiledItem
    {
        public AttrCompiledItem(Type type, string kind, string identifier)
        {
            Type = type;
            Kind = kind;
            Identifier = identifier;
        }
        public override string Kind { get; }
        public override string Identifier { get; }
        public override Type Type { get; }
        public override IReadOnlyList<object> Metadata => Type.GetCustomAttributes(inherit: true);
    }

    private IReadOnlyList<TypeInfo> ComputeTypes()
    {
        try
        {
            return Assembly.GetTypes().Select(t => t.GetTypeInfo()).ToList();
        }
        catch (Exception ex) when (ex is ReflectionTypeLoadException
                                      or FileNotFoundException
                                      or FileLoadException
                                      or TypeLoadException
                                      or BadImageFormatException)
        {
            return SafeTypeScan.Enumerate(Assembly, ex);
        }
    }
}

/// <summary>شمارش نوع‌به‌نوع انواع یک اسمبلی با پشتیبانی MetadataReader (بدون بارگذاری انواع ناقص).</summary>
internal static class SafeTypeScan
{
    public static IReadOnlyList<TypeInfo> Enumerate(Assembly asm, Exception? ex)
    {
        var list = new List<TypeInfo>();
        // ۱) انواعی که خود RTLE توانست بارگذاری کند (مسیر سریع)
        if (ex is ReflectionTypeLoadException rtle)
            foreach (var t in rtle.Types)
                if (t is not null) list.Add(t.GetTypeInfo());

        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in list)
            if (t.FullName is not null) known.Add(t.FullName);

        // ۲) کامل‌کردن از روی متادیتا — فقط انواعی که واقعاً بار می‌شوند
        try
        {
            foreach (var name in EnumerateTypeNames(asm.Location))
            {
                if (known.Contains(name)) continue;
                try
                {
                    var t = asm.GetType(name, throwOnError: false);
                    if (t is not null) list.Add(t.GetTypeInfo());
                }
                catch { /* نوع ناقص — از فهرست MVC کنار می‌رود */ }
            }
        }
        catch { /* متادیتا خواندنی نبود — همان فهرست مرحلهٔ ۱ ملاک است */ }

        return list;
    }

    /// <summary>نام کامل همهٔ TypeDefها (شامل تو در تو با جداکنندهٔ +) — بدون اجرای هیچ کدی.</summary>
    private static IEnumerable<string> EnumerateTypeNames(string path)
    {
        using var fs = File.OpenRead(path);
        using var pe = new PEReader(fs);
        var md = pe.GetMetadataReader();
        foreach (var th in md.TypeDefinitions)
            yield return BuildFullName(md.GetTypeDefinition(th), md);
    }

    private static string BuildFullName(TypeDefinition td, MetadataReader md)
    {
        var name = md.GetString(td.Name);
        var decl = td.GetDeclaringType();
        if (decl.IsNil)
        {
            var ns = md.GetString(td.Namespace);
            return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
        }
        return BuildFullName(md.GetTypeDefinition(decl), md) + "+" + name;
    }
}
