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
    public async Task ExternalConsoleCanReferenceProducedPackages()
    {
        string packages = Path.Combine(Root, "artifacts", "packages");
        Assert.True(File.Exists(Path.Combine(packages, "Buffaly.AgentKit.1.0.0.nupkg")), "Run pack before embedding validation.");
        string temp = Path.Combine(Path.GetTempPath(), "agentkit-embed-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(temp);
        await File.WriteAllTextAsync(Path.Combine(temp, "NuGet.config"), $"<configuration><packageSources><clear/><add key=\"agentkit\" value=\"{packages}\"/><add key=\"nuget\" value=\"https://api.nuget.org/v3/index.json\"/></packageSources></configuration>");
        await File.WriteAllTextAsync(Path.Combine(temp, "Embed.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include=\"Buffaly.AgentKit\" Version=\"1.0.0\"/><PackageReference Include=\"Buffaly.AgentKit.ProtoScript\" Version=\"1.0.0\"/><PackageReference Include=\"Buffaly.AgentKit.AspNetCore\" Version=\"1.0.0\"/></ItemGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(temp, "Program.cs"), "using Buffaly.AgentKit; using Buffaly.AgentKit.ProtoScript; using Buffaly.AgentKit.AspNetCore; Console.WriteLine(AgentConversation.Create().Id.Length > 0 && SupportedTypes.IsSupported(\"int\") && typeof(BuffalyAgentKitEndpointOptions).Name.Length > 0);");
        string output = await RunProcessAsync("dotnet", "build Embed.csproj -c Release -v minimal", temp);
        Assert.Contains("Build succeeded", output);
    }

    private static async Task<string> RunProcessAsync(string fileName, string arguments, string workingDirectory)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo(fileName, arguments) { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        process.Start();
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout + stderr;
    }
}
