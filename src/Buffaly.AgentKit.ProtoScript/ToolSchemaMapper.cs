using System.Text.Json;

namespace Buffaly.AgentKit.ProtoScript;

internal sealed record ProtoScriptParameterSignature(string Name, Type RuntimeType, string TypeName, string Description);

internal sealed record ProtoScriptFunctionSignature(
    IReadOnlyList<ProtoScriptParameterSignature> Parameters,
    Type ReturnRuntimeType,
    string ReturnTypeName);

public static class ToolSchemaMapper
{
    internal static JsonElement CreateSchema(ProtoScriptFunctionSignature signature)
    {
        var properties = signature.Parameters.ToDictionary(
            parameter => parameter.Name,
            parameter => new
            {
                type = ToJsonSchemaType(parameter.RuntimeType),
                description = parameter.Description
            });

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            type = "object",
            properties,
            required = signature.Parameters.Select(parameter => parameter.Name).ToArray(),
            additionalProperties = false
        }));
        return document.RootElement.Clone();
    }

    internal static bool IsSupported(Type type) =>
        type == typeof(string) ||
        type == typeof(int) ||
        type == typeof(long) ||
        type == typeof(decimal) ||
        type == typeof(double) ||
        type == typeof(float) ||
        type == typeof(bool) ||
        type == typeof(System.Text.Json.Nodes.JsonObject) ||
        type == typeof(System.Text.Json.Nodes.JsonArray);

    internal static string GetTypeName(Type type) => type == typeof(System.Text.Json.Nodes.JsonObject)
        ? "JsonObject"
        : type == typeof(System.Text.Json.Nodes.JsonArray)
            ? "JsonArray"
            : type.Name switch
            {
                nameof(String) => "string",
                nameof(Int32) => "int",
                nameof(Int64) => "long",
                nameof(Decimal) => "decimal",
                nameof(Double) => "double",
                nameof(Single) => "float",
                nameof(Boolean) => "bool",
                _ => type.FullName ?? type.Name
            };

    private static string ToJsonSchemaType(Type type) => type == typeof(bool)
        ? "boolean"
        : type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double) || type == typeof(float)
            ? "number"
            : type == typeof(System.Text.Json.Nodes.JsonObject)
                ? "object"
                : type == typeof(System.Text.Json.Nodes.JsonArray)
                    ? "array"
                    : "string";
}
