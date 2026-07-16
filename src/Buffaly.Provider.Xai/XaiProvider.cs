using Buffaly.ProviderContracts;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Buffaly.Provider.Xai;

public sealed class XaiProviderModule : IBuffalyProviderModule
{
    public void Register(IBuffalyProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.AddCatalogSource(new XaiProviderCatalogSource());
        registry.AddCompletionExecutor(new XaiCompletionExecutor());
    }
}

public sealed class XaiProviderCatalogSource : IBuffalyProviderCatalogSource
{
    public const string ProviderToken = "xai";
    public string Provider => ProviderToken;
    public Task<ProviderCatalogSourceResult> BuildCatalogAsync(BuffalyProviderCatalogContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        bool configured = context.Settings.TryGetValue("XAI.ApiKey", out string? key) && !string.IsNullOrWhiteSpace(key);
        var levels = new List<string> { "low", "medium", "high" };
        return Task.FromResult(new ProviderCatalogSourceResult
        {
            ProviderItem = new ProviderCatalogItemContract
            {
                Provider = ProviderToken, DisplayName = "xAI", IsConfigured = configured, IsEnabled = configured, DefaultTransport = ProviderCatalogDefaults.ProviderNativeTransport, DefaultModelName = "grok-4.3",
                Transports = new List<ProviderTransportContract> { new() { Provider = ProviderToken, Transport = ProviderCatalogDefaults.ProviderNativeTransport, DisplayName = ProviderCatalogDefaults.ProviderNativeDisplayName, IsDefault = true, IsEnabled = configured } },
                Models = new List<ProviderModelContract> { new() { Provider = ProviderToken, Transport = ProviderCatalogDefaults.ProviderNativeTransport, ModelName = "grok-4.3", DisplayName = "grok-4.3", Visibility = "list", SupportedInApi = true, IsDefault = true, DefaultReasoningLevel = "medium", SupportedReasoningLevels = levels } }
            },
            ReasoningLevelOptions = levels.Select(level => new ProviderReasoningLevelOptionContract { Value = level, Label = ProviderCatalogDefaults.ToReasoningLabel(level) }).ToList()
        });
    }
}

public sealed class XaiCompletionExecutor : IBuffalyCompletionExecutor
{
    private readonly HttpClient _httpClient;
    public XaiCompletionExecutor() : this(new HttpClient()) { }
    public XaiCompletionExecutor(HttpClient httpClient) => _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    public string Provider => XaiProviderCatalogSource.ProviderToken;

    public async Task<BuffalyCompletionResult> CompleteAsync(BuffalyCompletionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Options.TryGetValue("XAI.ApiKey", out string? apiKey) || string.IsNullOrWhiteSpace(apiKey))
            return new BuffalyCompletionResult { Success = false, ErrorCode = "TOKEN_MISSING", ErrorMessage = "XAI.ApiKey is required." };
        string baseUrl = request.Options.TryGetValue("XAI.BaseUrl", out string? configured) && !string.IsNullOrWhiteSpace(configured) ? configured.TrimEnd('/') : "https://api.x.ai/v1";
        var body = new JsonObject
        {
            ["model"] = request.ModelName,
            ["input"] = new JsonArray(request.Messages.Select(message => (JsonNode)new JsonObject { ["role"] = message.Role, ["content"] = message.Content }).ToArray()),
            ["store"] = false
        };
        if (!string.IsNullOrWhiteSpace(request.ReasoningLevel)) body["reasoning"] = new JsonObject { ["effort"] = request.ReasoningLevel };
        if (request.Tools.Count > 0) body["tools"] = new JsonArray(request.Tools.Select(tool => (JsonNode)new JsonObject { ["type"] = "function", ["name"] = tool.Name, ["description"] = tool.Description, ["parameters"] = JsonNode.Parse(tool.JsonSchema) }).ToArray());
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/responses") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new BuffalyCompletionResult { Success = false, Raw = raw, ErrorCode = ((int)response.StatusCode).ToString(), ErrorMessage = raw };
        using JsonDocument document = JsonDocument.Parse(raw);
        JsonElement root = document.RootElement;
        JsonElement usage = root.TryGetProperty("usage", out JsonElement usageElement) ? usageElement : default;
        return new BuffalyCompletionResult
        {
            Success = true, Text = ReadText(root), Raw = raw,
            ProviderRequestId = response.Headers.TryGetValues("x-request-id", out IEnumerable<string>? requestIds) ? requestIds.FirstOrDefault() ?? string.Empty : string.Empty,
            ProviderResponseId = root.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty,
            RawUsageJson = usage.ValueKind == JsonValueKind.Undefined ? string.Empty : usage.GetRawText(), UsageMetrics = ReadUsage(usage)
        };
    }

    private static string ReadText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out JsonElement direct)) return direct.GetString() ?? string.Empty;
        if (root.TryGetProperty("output", out JsonElement output) && output.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in output.EnumerateArray()) if (item.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array) foreach (JsonElement part in content.EnumerateArray()) if (part.TryGetProperty("text", out JsonElement text)) return text.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static IReadOnlyList<BuffalyUsageMetric> ReadUsage(JsonElement usage)
    {
        if (usage.ValueKind != JsonValueKind.Object) return Array.Empty<BuffalyUsageMetric>();
        var metrics = new List<BuffalyUsageMetric>();
        Add("input_tokens"); Add("output_tokens"); Add("total_tokens");
        if (usage.TryGetProperty("output_tokens_details", out JsonElement details) && details.TryGetProperty("reasoning_tokens", out JsonElement reasoning) && reasoning.TryGetDouble(out double reasoned)) metrics.Add(new BuffalyUsageMetric { Dimension = "reasoning_tokens", Value = reasoned });
        return metrics;
        void Add(string name) { if (usage.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double number)) metrics.Add(new BuffalyUsageMetric { Dimension = name, Value = number }); }
    }
}
