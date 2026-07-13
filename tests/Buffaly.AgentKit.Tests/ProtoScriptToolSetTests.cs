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
        await File.WriteAllTextAsync(manifest, "{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{\"name\":\"echo_included\",\"method\":\"EchoIncluded\",\"parameters\":[{\"name\":\"value\",\"type\":\"string\"}],\"returnType\":\"string\"}]}");

        await using ProtoScriptToolSet toolSet = await ProtoScriptToolSet.LoadAsync(manifest);
        object? result = await toolSet.Functions[0].InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["value"] = "included" }));

        Assert.Equal("included", result?.ToString());
    }

    [Fact]
    public async Task ManifestRejectsUnsupportedTypes()
    {
        string temp = Path.Combine(Path.GetTempPath(), "agentkit-bad-" + Guid.NewGuid().ToString("n") + ".json");
        await File.WriteAllTextAsync(temp, "{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{\"name\":\"bad\",\"method\":\"Bad\",\"parameters\":[{\"name\":\"x\",\"type\":\"DateTime\"}],\"returnType\":\"string\"}]}");
        await Assert.ThrowsAsync<InvalidDataException>(() => AgentKitManifest.LoadAsync(temp));
    }

    [Fact]
    public async Task ManifestRejectsProjectFilePathEscape()
    {
        string dir = Directory.CreateTempSubdirectory("agentkit-manifest-").FullName;
        string outside = Path.Combine(Path.GetDirectoryName(dir)!, "outside.pts");
        await File.WriteAllTextAsync(outside, "function AddNumbers(int a, int b): int { return a + b; }");
        string manifest = Path.Combine(dir, "agentkit.json");
        await File.WriteAllTextAsync(manifest, "{\"schemaVersion\":1,\"projectFile\":\"../outside.pts\",\"exports\":[{\"name\":\"add_numbers\",\"method\":\"AddNumbers\",\"parameters\":[],\"returnType\":\"int\"}]}");
        AgentKitManifest parsed = await AgentKitManifest.LoadAsync(manifest);
        Assert.Throws<InvalidDataException>(() => parsed.ResolveProjectFile(manifest));
    }

    [Fact]
    public async Task ManifestRejectsDuplicateParameterNames()
    {
        string temp = Path.Combine(Path.GetTempPath(), "agentkit-dupe-param-" + Guid.NewGuid().ToString("n") + ".json");
        await File.WriteAllTextAsync(temp, "{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{\"name\":\"bad\",\"method\":\"Bad\",\"parameters\":[{\"name\":\"x\",\"type\":\"int\"},{\"name\":\"x\",\"type\":\"int\"}],\"returnType\":\"int\"}]}");
        await Assert.ThrowsAsync<InvalidDataException>(() => AgentKitManifest.LoadAsync(temp));
    }

    [Fact]
    public async Task ToolSetRejectsMissingExportMethod()
    {
        string dir = Directory.CreateTempSubdirectory("agentkit-missing-method-").FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "Project.pts"), "function AddNumbers(int a, int b): int { return a + b; }");
        string manifest = Path.Combine(dir, "agentkit.json");
        await File.WriteAllTextAsync(manifest, "{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{\"name\":\"missing\",\"method\":\"Missing\",\"parameters\":[],\"returnType\":\"int\"}]}");
        await Assert.ThrowsAsync<InvalidDataException>(() => ProtoScriptToolSet.LoadAsync(manifest));
    }
}

