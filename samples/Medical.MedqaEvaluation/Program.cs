using Buffaly.AgentKit.Providers;
using Buffaly.ProviderContracts;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Medical.MedqaEvaluation;

public static class Program
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = false };

    public static async Task<int> Main(string[] args)
    {
        try { await RunAsync(RunOptions.Parse(args)); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception.Message); return 1; }
    }

    public static async Task RunAsync(RunOptions options, CancellationToken cancellationToken = default)
    {
        var registry = new ProviderRegistry();
        if (options.Fixture) new FixtureProviderModule().Register(registry);
        else if (options.Provider == "openai") new Buffaly.Provider.OpenAi.OpenAiProviderModule().Register(registry);
        else if (options.Provider == "xai") new Buffaly.Provider.Xai.XaiProviderModule().Register(registry);
        else if (options.Provider == "ollama") new Buffaly.Provider.Ollama.OllamaProviderModule().Register(registry);
        else throw new InvalidOperationException("Unsupported provider: " + options.Provider);
        var settings = options.BuildSettings();
        var catalogService = new ProviderCatalogService(registry, settings);
        ProviderCatalogContract catalog = await catalogService.GetProviderCatalogAsync(cancellationToken);
        var client = new ProviderCompletionClient(registry, catalogService);
        WriteManifest(options, catalog);
        HashSet<int> completed = ReadCompletedIds(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath))!);
        await using var output = new StreamWriter(new FileStream(options.OutputPath, FileMode.Append, FileAccess.Write, FileShare.Read));
        foreach (string line in File.ReadLines(options.InputPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            MedqaCase item = JsonSerializer.Deserialize<MedqaCase>(line, Json) ?? throw new InvalidOperationException("Invalid input row.");
            Validate(item);
            if (item.SourceCaseId % options.ShardCount != options.ShardIndex || completed.Contains(item.SourceCaseId)) continue;
            string prompt = RenderPrompt(item);
            var request = new BuffalyCompletionRequest
            {
                Provider = options.Fixture ? "fixture" : options.Provider, ModelName = options.Fixture ? "fixture-medqa" : options.Model,
                ReasoningLevel = options.Fixture ? string.Empty : options.ReasoningLevel,
                Messages = new[] { new BuffalyChatMessage { Role = "user", Content = prompt } },
                Tools = Array.Empty<BuffalyToolDefinition>(), Options = settings
            };
            Stopwatch clock = Stopwatch.StartNew();
            BuffalyCompletionResult response = await client.CompleteAsync(request, cancellationToken);
            clock.Stop();
            (string answer, string parseStatus) = ParseAnswer(response.Text, response.Success ? "ok" : "failed");
            var row = MedqaResult.From(item, options, response, answer, parseStatus, clock.ElapsedMilliseconds);
            await output.WriteLineAsync(JsonSerializer.Serialize(row, Json));
            await output.FlushAsync(cancellationToken);
        }
        WriteMetrics(options.OutputPath, options.MetricsPath);
    }

    public static void MergeShards(IEnumerable<string> shardPaths, string outputPath)
    {
        List<MedqaResult> rows = shardPaths.SelectMany(File.ReadLines).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => JsonSerializer.Deserialize<MedqaResult>(line, Json) ?? throw new InvalidOperationException("Invalid shard result row.")).OrderBy(row => row.SourceCaseId).ToList();
        int duplicate = rows.GroupBy(row => row.SourceCaseId).Where(group => group.Count() > 1).Select(group => group.Key).FirstOrDefault(-1);
        if (duplicate >= 0) throw new InvalidOperationException("Duplicate Source_Case_Id across shards: " + duplicate);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllLines(outputPath, rows.Select(row => JsonSerializer.Serialize(row, Json)));
    }

    public static string RenderPrompt(MedqaCase item) =>
        "You are answering a medical multiple-choice question from the MedQA-USMLE dataset.\n\n" +
        "Read the following clinical question and the four answer options (A, B, C, D).\n\n" +
        "Select the single best answer. Respond with ONLY the letter of your chosen option: A, B, C, or D.\n\n" +
        "Do NOT use any external tools, lookups, web searches, or medical ontology references.\n" +
        "Do NOT explain your reasoning.\n" +
        "Do NOT output anything other than the single letter A, B, C, or D.\n\n" +
        "Question:\n" + item.Question + "\n\nOptions:\n" +
        "A. " + item.Options.A + "\nB. " + item.Options.B + "\nC. " + item.Options.C + "\nD. " + item.Options.D + "\n\nAnswer:\n";

    public static (string Parsed, string ParseStatus) ParseAnswer(string raw, string status)
    {
        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase)) return (string.Empty, "error");
        string clean = raw.Trim().Trim('.', ':', ';', ',', ')', '(', '[', ']', '{', '}', '"', '\'');
        if (Regex.IsMatch(clean, "^[ABCD]$")) return (clean, "clean");
        Match first = Regex.Match(raw.Trim(), @"^([ABCD])\s*[\n\r\.\;\:\-—\s]");
        if (first.Success) return (first.Groups[1].Value, "verbose_recovered");
        List<string> matches = Regex.Matches(raw, "(?<![A-Za-z])[ABCD](?![A-Za-z])").Select(match => match.Value).Distinct().ToList();
        return matches.Count == 1 ? (matches[0], "verbose_recovered") : (string.Empty, "invalid");
    }

    private static HashSet<int> ReadCompletedIds(string path) => !File.Exists(path) ? new() : File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => JsonSerializer.Deserialize<MedqaResult>(line, Json)!.SourceCaseId).ToHashSet();
    private static void Validate(MedqaCase item) { if (item.SourceCaseId < 0 || string.IsNullOrWhiteSpace(item.Question) || string.IsNullOrWhiteSpace(item.Options.A) || string.IsNullOrWhiteSpace(item.Options.B) || string.IsNullOrWhiteSpace(item.Options.C) || string.IsNullOrWhiteSpace(item.Options.D) || !Regex.IsMatch(item.Answer, "^[ABCD]$")) throw new InvalidOperationException("Input row is missing required MedQA fields."); }
    private static void WriteMetrics(string outputPath, string metricsPath)
    {
        List<MedqaResult> rows = File.ReadLines(outputPath).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => JsonSerializer.Deserialize<MedqaResult>(line, Json)!).ToList();
        var metrics = new { TotalCases = rows.Count, Correct = rows.Count(row => row.IsCorrect), ExactMatchAccuracy = rows.Count == 0 ? 0 : (double)rows.Count(row => row.IsCorrect) / rows.Count, InvalidOutputRate = rows.Count == 0 ? 0 : (double)rows.Count(row => row.ParseStatus is "invalid" or "error") / rows.Count, ParseStatusBreakdown = rows.GroupBy(row => row.ParseStatus).ToDictionary(group => group.Key, group => group.Count()), LatencyMs = Stats(rows.Select(row => (double)row.LatencyMs)), TokenUsage = new { InputTokens = Stats(rows.Where(row => row.InputTokens.HasValue).Select(row => (double)row.InputTokens!.Value)), OutputTokens = Stats(rows.Where(row => row.OutputTokens.HasValue).Select(row => (double)row.OutputTokens!.Value)), TotalTokens = Stats(rows.Where(row => row.TotalTokens.HasValue).Select(row => (double)row.TotalTokens!.Value)), ReasoningTokens = Stats(rows.Where(row => row.ReasoningTokens.HasValue).Select(row => (double)row.ReasoningTokens!.Value)) } };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(metricsPath))!);
        File.WriteAllText(metricsPath, JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void WriteManifest(RunOptions options, ProviderCatalogContract catalog)
    {
        var manifest = new
        {
            PromptVersion = "arm1-v1", options.Provider, options.Model, options.ReasoningLevel, options.ShardIndex, options.ShardCount,
            CatalogVersion = catalog.CatalogVersion, Catalog = catalog,
            Versions = new Dictionary<string, string>
            {
                ["Buffaly.AgentKit"] = typeof(ProviderCompletionClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty,
                ["Buffaly.ProviderContracts"] = typeof(BuffalyCompletionRequest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty,
                ["Provider"] = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty
            }
        };
        File.WriteAllText(options.MetricsPath + ".manifest.json", JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static object Stats(IEnumerable<double> values) { double[] data = values.ToArray(); return new { Min = data.Length == 0 ? (double?)null : data.Min(), Max = data.Length == 0 ? (double?)null : data.Max(), Mean = data.Length == 0 ? (double?)null : data.Average() }; }
}

public sealed record RunOptions(string InputPath, string OutputPath, string MetricsPath, string Provider, string Model, string ReasoningLevel, int ShardIndex, int ShardCount, bool Fixture)
{
    public static RunOptions Parse(string[] args)
    {
        var values = args.Chunk(2).Where(pair => pair.Length == 2).ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
        string Required(string key) => values.TryGetValue(key, out string? value) ? value : throw new InvalidOperationException(key + " is required.");
        int shardIndex = values.TryGetValue("--shard-index", out string? si) ? int.Parse(si) : 0; int shardCount = values.TryGetValue("--shard-count", out string? sc) ? int.Parse(sc) : 1;
        if (shardCount <= 0 || shardIndex < 0 || shardIndex >= shardCount) throw new InvalidOperationException("Shard index must be within shard count.");
        return new RunOptions(Required("--input"), Required("--output"), Required("--metrics"), Required("--provider"), Required("--model"), values.GetValueOrDefault("--reasoning", string.Empty), shardIndex, shardCount, values.GetValueOrDefault("--fixture", "false") == "true");
    }
    public IReadOnlyDictionary<string, string> BuildSettings()
    {
        var settings = new Dictionary<string, string>();
        if (Provider == "openai") { settings["OpenAI.ApiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty; }
        if (Provider == "xai") { settings["XAI.ApiKey"] = Environment.GetEnvironmentVariable("XAI_API_KEY") ?? string.Empty; }
        if (Provider == "ollama") settings["Ollama.BaseUrl"] = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        return settings;
    }
}

public sealed class MedqaCase { [JsonPropertyName("source_case_id")] public int SourceCaseId { get; set; } [JsonPropertyName("question")] public string Question { get; set; } = string.Empty; [JsonPropertyName("options")] public MedqaOptions Options { get; set; } = new(); [JsonPropertyName("answer")] public string Answer { get; set; } = string.Empty; }
public sealed class MedqaOptions { public string A { get; set; } = string.Empty; public string B { get; set; } = string.Empty; public string C { get; set; } = string.Empty; public string D { get; set; } = string.Empty; }
public sealed class MedqaResult
{
    [JsonPropertyName("Source_Case_Id")] public int SourceCaseId { get; set; } public string Provider { get; set; } = string.Empty; public string ModelName { get; set; } = string.Empty; public string ReasoningLevel { get; set; } = string.Empty; public string PromptVersion { get; set; } = "arm1-v1"; public string RawModelOutput { get; set; } = string.Empty; public string ParsedAnswer { get; set; } = string.Empty; public string GroundTruthAnswer { get; set; } = string.Empty; public bool IsCorrect { get; set; } public string ParseStatus { get; set; } = string.Empty; public string Error { get; set; } = string.Empty; public long LatencyMs { get; set; } public DateTimeOffset Timestamp { get; set; } public int? InputTokens { get; set; } public int? OutputTokens { get; set; } public int? TotalTokens { get; set; } public int? ReasoningTokens { get; set; }
    public static MedqaResult From(MedqaCase item, RunOptions options, BuffalyCompletionResult response, string answer, string status, long latency) => new() { SourceCaseId = item.SourceCaseId, Provider = options.Fixture ? "fixture" : options.Provider, ModelName = options.Fixture ? "fixture-medqa" : options.Model, ReasoningLevel = options.Fixture ? string.Empty : options.ReasoningLevel, RawModelOutput = response.Text, ParsedAnswer = answer, GroundTruthAnswer = item.Answer, IsCorrect = answer == item.Answer, ParseStatus = status, Error = response.ErrorMessage, LatencyMs = latency, Timestamp = DateTimeOffset.UtcNow, InputTokens = Usage(response, "input_tokens"), OutputTokens = Usage(response, "output_tokens"), TotalTokens = Usage(response, "total_tokens"), ReasoningTokens = Usage(response, "reasoning_tokens") };
    private static int? Usage(BuffalyCompletionResult response, string dimension) { BuffalyUsageMetric? metric = response.UsageMetrics.FirstOrDefault(row => row.Dimension == dimension); return metric is null ? null : checked((int)metric.Value); }
}

internal sealed class FixtureProviderModule : IBuffalyProviderModule
{
    public void Register(IBuffalyProviderRegistry registry) { registry.AddCatalogSource(new FixtureCatalog()); registry.AddCompletionExecutor(new FixtureExecutor()); }
    private sealed class FixtureCatalog : IBuffalyProviderCatalogSource { public string Provider => "fixture"; public Task<ProviderCatalogSourceResult> BuildCatalogAsync(BuffalyProviderCatalogContext context, CancellationToken cancellationToken) => Task.FromResult(new ProviderCatalogSourceResult { ProviderItem = new ProviderCatalogItemContract { Provider = "fixture", DisplayName = "Fixture", IsConfigured = true, IsEnabled = true, DefaultTransport = ProviderCatalogDefaults.ProviderNativeTransport, DefaultModelName = "fixture-medqa", Transports = new List<ProviderTransportContract> { new() { Provider = "fixture", Transport = ProviderCatalogDefaults.ProviderNativeTransport, DisplayName = ProviderCatalogDefaults.ProviderNativeDisplayName, IsDefault = true, IsEnabled = true } }, Models = new List<ProviderModelContract> { new() { Provider = "fixture", Transport = ProviderCatalogDefaults.ProviderNativeTransport, ModelName = "fixture-medqa", DisplayName = "Fixture MedQA", IsDefault = true, SupportedInApi = true } } } }); }
    private sealed class FixtureExecutor : IBuffalyCompletionExecutor { public string Provider => "fixture"; public Task<BuffalyCompletionResult> CompleteAsync(BuffalyCompletionRequest request, CancellationToken cancellationToken) { if (request.Messages.Count != 1 || request.Messages[0].Role != "user" || request.Tools.Count != 0) throw new InvalidOperationException("Fixture detected contaminated request."); Match answer = Regex.Match(request.Messages[0].Content, @"\[fixture:([ABCD])\]"); string text = answer.Success ? answer.Groups[1].Value : "A"; return Task.FromResult(new BuffalyCompletionResult { Success = true, Text = text, UsageMetrics = new List<BuffalyUsageMetric> { new() { Dimension = "input_tokens", Value = 100 }, new() { Dimension = "output_tokens", Value = 1 }, new() { Dimension = "total_tokens", Value = 101 } } }); } }
}
