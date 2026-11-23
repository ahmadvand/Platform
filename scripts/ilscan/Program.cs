using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

var dll = args[0];
var targetAsm = args[1];
using var fs = File.OpenRead(dll);
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();
var asmRefHandles = new List<AssemblyReferenceHandle>();
foreach (var h in md.AssemblyReferences)
    if (md.GetString(md.GetAssemblyReference(h).Name).Equals(targetAsm, StringComparison.OrdinalIgnoreCase))
        asmRefHandles.Add(h);
if (asmRefHandles.Count == 0) { Console.WriteLine($"no ref to {targetAsm}"); return; }

var decoder = new SigDecoder();
foreach (var tdh in md.TypeDefinitions)
{
    var td = md.GetTypeDefinition(tdh);
    var tname = $"{md.GetString(td.Namespace)}.{md.GetString(td.Name)}";
    var hits = new List<string>();
    foreach (var fh in td.GetFields())
    {
        var fsig = md.GetFieldDefinition(fh).DecodeSignature(decoder, 0);
        if (fsig.Contains(targetAsm)) hits.Add($"field: {md.GetString(md.GetFieldDefinition(fh).Name)}");
    }
    foreach (var mh in td.GetMethods())
    {
        var m = md.GetMethodDefinition(mh);
        var sig = m.DecodeSignature(decoder, 0);
        var sigStr = sig.ReturnType + "(" + string.Join(",", sig.ParameterTypes) + ")";
        if (sigStr.Contains(targetAsm)) hits.Add($"method-sig: {md.GetString(m.Name)}");
    }
    foreach (var ph in td.GetProperties())
    {
        var psig = md.GetPropertyDefinition(ph).DecodeSignature(decoder, 0);
        var pstr = string.Join(",", psig);
        if (pstr.Contains(targetAsm)) hits.Add($"prop: {md.GetString(md.GetPropertyDefinition(ph).Name)}");
    }
    if (hits.Count > 0)
    {
        Console.WriteLine($"TYPE {tname}");
        foreach (var h in hits.Take(14)) Console.WriteLine("   " + h);
    }
}

class SigDecoder : ISignatureTypeProvider<string, object?>
{
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";
    public string GetByReferenceType(string elementType) => elementType + "&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
    public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;
    public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var td = reader.GetTypeDefinition(handle);
        var ns = td.Namespace.IsNil ? "" : reader.GetString(td.Namespace);
        return (ns.Length == 0 ? "" : ns + ".") + reader.GetString(td.Name);
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var tr = reader.GetTypeReference(handle);
        var ns = tr.Namespace.IsNil ? "" : reader.GetString(tr.Namespace);
        return (ns.Length == 0 ? "" : ns + ".") + reader.GetString(tr.Name);
    }
    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    public string GetUnmanagedArrayType(string elementType) => elementType;
}
