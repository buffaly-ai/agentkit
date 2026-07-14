using Xunit;

namespace Buffaly.AgentKit.Tests;

public class PackagingTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void SourceDoesNotContainForbiddenProductionReferences()
    {
        string[] forbidden = ["SessionObject", "TooledAgent", "BuffalyAgentService", "RooTrax", "WebAppUtilities", "JsonWs", @"C:\\dev\\buffaly-ai\\buffaly-development", @"C:\\dev\\buffaly-ai\\protoscript", @"C:\\dev\\buffaly-ai\\ontology", @"C:\\dev\\buffaly-ai\\buffaly-public-export"];
        string[] extensions = [".cs", ".csproj", ".props", ".json", ".config", ".md"];
        foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase) && IsSourceAuditFile(f)))
        {
            if (Path.GetFileName(file).Equals(nameof(PackagingTests) + ".cs", StringComparison.OrdinalIgnoreCase))
                continue;
            string text = File.ReadAllText(file);
            foreach (string token in forbidden)
                Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsSourceAuditFile(string file)
    {
        string relative = Path.GetRelativePath(Root, file);
        string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || part.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || part.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || part.Equals(".vs", StringComparison.OrdinalIgnoreCase)))
            return false;

        // Generated validation/package/dist artifacts intentionally contain audit pattern names and are not source.
        if (parts.Length > 0 && parts[0].Equals("artifacts", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    [Fact]
    public void RuntimeFoundationOnlyRedistributesBasicUtilitiesClosedDll()
    {
        string foundation = Path.Combine(Root, "src", "runtime", "foundation", "lib");
        string[] dlls = Directory.Exists(foundation) ? Directory.GetFiles(foundation, "*.dll").Select(f => Path.GetFileName(f)!).ToArray() : [];
        Assert.Contains("BasicUtilities.dll", dlls);
        Assert.DoesNotContain("RooTrax.Cache.dll", dlls);
        Assert.DoesNotContain("RooTrax.Common.dll", dlls);
        Assert.DoesNotContain("RooTrax.Common.DB.dll", dlls);
        Assert.DoesNotContain("WebAppUtilities.dll", dlls);
    }

    [Fact]
    public async Task ExternalConsoleRunsCompletePackagedProtoScriptFlow()
    {
        string packages = Path.Combine(Root, "artifacts", "packages");
        Assert.True(File.Exists(Path.Combine(packages, "Buffaly.AgentKit.1.0.0.nupkg")), "Run pack before embedding validation.");
        string temp = Path.Combine(Path.GetTempPath(), "agentkit-embed-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(temp);
        await File.WriteAllTextAsync(Path.Combine(temp, "NuGet.config"), $"<configuration><packageSources><clear/><add key=\"agentkit\" value=\"{packages}\"/><add key=\"nuget\" value=\"https://api.nuget.org/v3/index.json\"/></packageSources></configuration>");
        await File.WriteAllTextAsync(Path.Combine(temp, "Embed.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestorePackagesPath>packages-cache</RestorePackagesPath></PropertyGroup><ItemGroup><PackageReference Include=\"Buffaly.AgentKit\" Version=\"1.0.0\"/><PackageReference Include=\"Buffaly.AgentKit.ProtoScript\" Version=\"1.0.0\"/><PackageReference Include=\"Buffaly.AgentKit.AspNetCore\" Version=\"1.0.0\"/></ItemGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(temp, "Project.pts"), "function AddNumbers(int a, int b): int { return a + b; }");
        await File.WriteAllTextAsync(Path.Combine(temp, "agentkit.json"), "{\"schemaVersion\":1,\"projectFile\":\"Project.pts\",\"exports\":[{\"name\":\"add_numbers\",\"method\":\"AddNumbers\"}]}");
        await File.WriteAllTextAsync(Path.Combine(temp, "Program.cs"), ExternalConsumerProgram);
        ProcessResult restore = await RunProcessAsync("dotnet", "restore Embed.csproj", temp);
        Assert.True(restore.ExitCode == 0, restore.Output);
        ProcessResult run = await RunProcessAsync("dotnet", "run --project Embed.csproj -c Release --no-restore", temp);
        Assert.True(run.ExitCode == 0, run.Output);
        Assert.Contains("AGENTKIT_PACKAGED_FLOW_OK:42", run.Output);
        ValidatePackageDependencies(packages);
    }

    private static void ValidatePackageDependencies(string packages)
    {
        var expected = new Dictionary<string, string[]>
        {
            ["Buffaly.AgentKit.ProtoScript.1.0.0.nupkg"] = ["Buffaly.AgentKit", "ProtoScript.Interpretter", "ProtoScript.Parsers", "ProtoScript.Runtime"],
            ["ProtoScript.Interpretter.1.0.0.nupkg"] = ["Ontology.Parsers", "Ontology.Simulation", "Ontology.Runtime", "ProtoScript.Parsers", "ProtoScript.Runtime"],
            ["Ontology.Runtime.1.0.0.nupkg"] = ["Buffaly.Foundation.Runtime"]
        };
        foreach ((string package, string[] dependencies) in expected)
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(Path.Combine(packages, package));
            var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            using var reader = new StreamReader(nuspec.Open());
            string metadata = reader.ReadToEnd();
            foreach (string dependency in dependencies)
                Assert.Contains($"dependency id=\"{dependency}\"", metadata, StringComparison.OrdinalIgnoreCase);
        }
    }

    private const string ExternalConsumerProgram = """
using Buffaly.AgentKit; using Buffaly.AgentKit.ProtoScript; using Microsoft.Extensions.AI;
await using var tools=await ProtoScriptToolSet.LoadAsync("agentkit.json"); var client=new StrictClient(); var result=await new AgentKitRuntime(client,tools.Tools).RunTurnAsync(AgentConversation.Create(),"Add 17 and 25"); if(!client.Observed||result.FinalAnswer!="The result is 42.")throw new Exception("flow failed"); Console.WriteLine("AGENTKIT_PACKAGED_FLOW_OK:42");
sealed class StrictClient:IChatClient { int round; public bool Observed{get;private set;} public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,ChatOptions? options=null,CancellationToken ct=default){if(++round==1){if(!options!.Tools.OfType<AIFunction>().Any(t=>t.Name=="add_numbers"))throw new Exception("tool absent");return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,[new FunctionCallContent("proof-call-1","add_numbers",new Dictionary<string,object?>{{"a",17},{"b",25}})])));}var all=messages.SelectMany(m=>m.Contents);var call=all.OfType<FunctionCallContent>().Single();var value=all.OfType<FunctionResultContent>().Single(r=>r.CallId==call.CallId).Result;Observed=true;return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,$"The result is {value}.")));}public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> m,ChatOptions? o=null,CancellationToken c=default)=>throw new NotSupportedException();public object? GetService(Type t,object? k=null)=>null;public void Dispose(){} }
""";

    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, string workingDirectory)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo(fileName, arguments) { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        process.Start();
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, stdout + stderr);
    }
    private sealed record ProcessResult(int ExitCode, string Output);
}


