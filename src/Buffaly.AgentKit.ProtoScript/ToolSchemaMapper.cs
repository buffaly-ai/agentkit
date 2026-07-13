using System.Text.Json;

namespace Buffaly.AgentKit.ProtoScript;

public static class ToolSchemaMapper
{
    public static JsonElement CreateSchema(AgentKitManifestExport export)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            type = "object",
            properties = export.Parameters.ToDictionary(p => p.Name, p => new { type = ToJsonSchemaType(p.Type), description = p.Description }),
            required = export.Parameters.Where(p => p.Required).Select(p => p.Name).ToArray(),
            additionalProperties = false
        }));
        return document.RootElement.Clone();
    }

    private static string ToJsonSchemaType(string type) => type.Equals("bool", StringComparison.OrdinalIgnoreCase) ? "boolean" : type.Equals("int", StringComparison.OrdinalIgnoreCase) || type.Equals("long", StringComparison.OrdinalIgnoreCase) || type.Equals("decimal", StringComparison.OrdinalIgnoreCase) || type.Equals("double", StringComparison.OrdinalIgnoreCase) || type.Equals("float", StringComparison.OrdinalIgnoreCase) ? "number" : type.Equals("JsonObject", StringComparison.OrdinalIgnoreCase) ? "object" : type.Equals("JsonArray", StringComparison.OrdinalIgnoreCase) ? "array" : "string";
}
