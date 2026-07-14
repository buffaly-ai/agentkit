using Microsoft.Extensions.AI;
using Buffaly.AgentKit.ProtoScript;
using Xunit;

namespace Buffaly.AgentKit.Tests;

public class ProtoScriptToolSetTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static string ManifestPath => Path.Combine(Root, "samples", "Tools", "agentkit.json");

    [Fact]
    public async Task ManifestLoadsExplicitAllowlist()
    {
        AgentKitManifest manifest = await AgentKitManifest.LoadAsync(ManifestPath);
        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Single(manifest.Exports);
        Assert.Equal("add_numbers", manifest.Exports[0].Name);
    }

    [Fact]
    public async Task ToolSetLoadsFunctionFromManifest()
    {
        await using ProtoScriptToolSet toolSet = await ProtoScriptToolSet.LoadAsync(ManifestPath);
        Assert.Single(toolSet.Functions);
        Assert.Equal("add_numbers", toolSet.Functions[0].Name);
        Assert.Contains("properties", toolSet.Functions[0].JsonSchema.GetRawText());
    }

    [Fact]
    public async Task ToolSetInvokesAddNumbersThroughInterpreter()
    {
        await using ProtoScriptToolSet toolSet = await ProtoScriptToolSet.LoadAsync(ManifestPath);
        object? result = await toolSet.Functions[0].InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["a"] = 7, ["b"] = 8 }));
        Assert.Equal(15, Convert.ToInt32(result));
    }

    [Fact]
    public async Task ToolSetLoadsFunctionFromIncludedProjectFile()
    {
        string dir = Directory.CreateTempSubdirectory("agentkit-include-").FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "Project.pts"), "include \"MathTools.pts\";");
        await File.WriteAllTextAsync(Path.Combine(dir, "MathTools.pts"), "function EchoIncluded(string value): string { return value; }");
        string manifest = Path.Combine(dir, "agentkit.json");
        await File.WriteAllTextAsync(manifest, "{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{\"name\":\"echo_included\",\"method\":\"EchoIncluded\"}]}");

        await using ProtoScriptToolSet toolSet = await ProtoScriptToolSet.LoadAsync(manifest);
        object? result = await toolSet.Functions[0].InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["value"] = "included" }));

        Assert.Equal("included", result?.ToString());
    }

    [Fact]
    public async Task ManifestRejectsLegacySignatureFields()
    {
        string temp = Path.Combine(Path.GetTempPath(), "agentkit-bad-" + Guid.NewGuid().ToString("n") + ".json");
        await File.WriteAllTextAsync(temp, "{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{\"name\":\"bad\",\"method\":\"Bad\",\"parameters\":[{\"name\":\"x\",\"type\":\"DateTime\"}],\"returnType\":\"string\"}]}");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => AgentKitManifest.LoadAsync(temp));
    }

    [Fact]
    public async Task ManifestRejectsProjectFilePathEscape()
    {
        string dir = Directory.CreateTempSubdirectory("agentkit-manifest-").FullName;
        string outside = Path.Combine(Path.GetDirectoryName(dir)!, "outside.pts");
        await File.WriteAllTextAsync(outside, "function AddNumbers(int a, int b): int { return a + b; }");
        string manifest = Path.Combine(dir, "agentkit.json");
        await File.WriteAllTextAsync(manifest, "{\"schemaVersion\":1,\"projectFile\":\"../outside.pts\",\"exports\":[{\"name\":\"add_numbers\",\"method\":\"AddNumbers\"}]}");
        AgentKitManifest parsed = await AgentKitManifest.LoadAsync(manifest);
        Assert.Throws<InvalidDataException>(() => parsed.ResolveProjectFile(manifest));
    }

    [Fact]
    public async Task ManifestRejectsLegacyParametersArray()
    {
        string temp = Path.Combine(Path.GetTempPath(), "agentkit-dupe-param-" + Guid.NewGuid().ToString("n") + ".json");
        await File.WriteAllTextAsync(temp, "{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{\"name\":\"bad\",\"method\":\"Bad\",\"parameters\":[{\"name\":\"x\",\"type\":\"int\"},{\"name\":\"x\",\"type\":\"int\"}],\"returnType\":\"int\"}]}");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => AgentKitManifest.LoadAsync(temp));
    }

    [Fact]
    public async Task ToolSetRejectsMissingExportMethod()
    {
        string dir = Directory.CreateTempSubdirectory("agentkit-missing-method-").FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "Project.pts"), "function AddNumbers(int a, int b): int { return a + b; }");
        string manifest = Path.Combine(dir, "agentkit.json");
        await File.WriteAllTextAsync(manifest, "{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{\"name\":\"missing\",\"method\":\"Missing\"}]}");
        await Assert.ThrowsAsync<InvalidDataException>(() => ProtoScriptToolSet.LoadAsync(manifest));
    }

    [Fact]
    public async Task JsonSchemaIsDerivedFromCompiledProtoScriptSignature()
    {
        await using ProtoScriptToolSet toolSet = await ProtoScriptToolSet.LoadAsync(ManifestPath);
        var schema = toolSet.Functions[0].JsonSchema;

        Assert.Equal("number", schema.GetProperty("properties").GetProperty("a").GetProperty("type").GetString());
        Assert.Equal("number", schema.GetProperty("properties").GetProperty("b").GetProperty("type").GetString());
        Assert.Equal(["a", "b"], schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()!).ToArray());
    }

    [Fact]
    public async Task ChangingProtoScriptParameterTypeChangesSchema()
    {
        string directory = Directory.CreateTempSubdirectory("agentkit-schema-").FullName;
        string project = Path.Combine(directory, "Project.pts");
        string manifest = Path.Combine(directory, "agentkit.json");
        await File.WriteAllTextAsync(manifest, """{"schemaVersion":1,"projectFile":"Project.pts","exports":[{"name":"echo","method":"Echo"}]}""");
        await File.WriteAllTextAsync(project, "function Echo(int value): int { return value; }");
        await using ProtoScriptToolSet integerTools = await ProtoScriptToolSet.LoadAsync(manifest);
        string integerType = integerTools.Functions[0].JsonSchema.GetProperty("properties").GetProperty("value").GetProperty("type").GetString()!;

        await File.WriteAllTextAsync(project, "function Echo(string value): string { return value; }");
        await using ProtoScriptToolSet stringTools = await ProtoScriptToolSet.LoadAsync(manifest);
        string stringType = stringTools.Functions[0].JsonSchema.GetProperty("properties").GetProperty("value").GetProperty("type").GetString()!;

        Assert.Equal("number", integerType);
        Assert.Equal("string", stringType);
    }

    [Fact]
    public async Task UnknownParameterDescriptionFailsLoading()
    {
        string manifest = await WriteToolAsync(
            "function Echo(string value): string { return value; }",
            """{"name":"echo","method":"Echo","parameterDescriptions":{"missing":"Unknown."}}""");

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => ProtoScriptToolSet.LoadAsync(manifest));
        Assert.Contains("unknown compiled parameter 'missing'", error.Message);
    }

    [Fact]
    public async Task UnsupportedCompiledParameterTypeFailsLoading()
    {
        string manifest = await WriteToolAsync(
            "function Unsupported(Prototype value): string { return value.ToString(); }",
            """{"name":"unsupported","method":"Unsupported"}""");

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => ProtoScriptToolSet.LoadAsync(manifest));
        Assert.Contains("unsupported", error.Message);
        Assert.Contains("value", error.Message);
        Assert.Contains("Prototype", error.Message);
    }

    [Fact]
    public async Task DuplicateProjectedToolNameFailsLoading()
    {
        string directory = Directory.CreateTempSubdirectory("agentkit-duplicate-tool-").FullName;
        await File.WriteAllTextAsync(Path.Combine(directory, "Project.pts"), "function Echo(string value): string { return value; }");
        string manifest = Path.Combine(directory, "agentkit.json");
        await File.WriteAllTextAsync(manifest, """{"schemaVersion":1,"projectFile":"Project.pts","exports":[{"name":"echo","method":"Echo"},{"name":"echo","method":"Echo"}]}""");

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => ProtoScriptToolSet.LoadAsync(manifest));
        Assert.Contains("Duplicate projected tool name 'echo'", error.Message);
    }

    [Fact]
    public async Task ProtoScriptToolContainsSourceMetadata()
    {
        await using ProtoScriptToolSet toolSet = await ProtoScriptToolSet.LoadAsync(ManifestPath);
        ProtoScriptAIFunction tool = toolSet.Functions[0];

        Assert.Equal("ProtoScript", tool.AdditionalProperties["buffaly.toolSource"]);
        Assert.Equal("Project.pts", tool.AdditionalProperties["buffaly.projectFile"]);
        Assert.Equal("AddNumbers", tool.AdditionalProperties["buffaly.method"]);
        Assert.Equal("int", tool.AdditionalProperties["buffaly.returnType"]);
    }

    private static async Task<string> WriteToolAsync(string source, string exportJson)
    {
        string directory = Directory.CreateTempSubdirectory("agentkit-compiled-signature-").FullName;
        string project = Path.Combine(directory, "Project.pts");
        string manifest = Path.Combine(directory, "agentkit.json");
        await File.WriteAllTextAsync(project, source);
        await File.WriteAllTextAsync(manifest, $"{{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{exportJson}]}}");
        return manifest;
    }
}


