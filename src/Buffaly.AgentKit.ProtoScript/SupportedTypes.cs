namespace Buffaly.AgentKit.ProtoScript;

public static class SupportedTypes
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase) { "string", "int", "long", "decimal", "double", "float", "bool", "JsonObject", "JsonArray", "object", "void" };
    public static bool IsSupported(string? typeName) => string.IsNullOrWhiteSpace(typeName) || Names.Contains(typeName);
}
