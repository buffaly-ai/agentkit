using Buffaly.ProviderContracts;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Buffaly.Provider.OpenAi;

public sealed class OpenAiProviderModule : IBuffalyProviderModule
{
    public void Register(IBuffalyProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.AddCatalogSource(new OpenAiProviderCatalogSource());
        registry.AddCompletionExecutor(new OpenAiCompletionExecutor());
    }
}

public sealed class OpenAiProviderCatalogSource : IBuffalyProviderCatalogSource
{
    public const string ProviderToken = "openai";
    public string Provider => ProviderToken;

    public Task<ProviderCatalogSourceResult> BuildCatalogAsync(BuffalyProviderCatalogContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        bool configured = context.Settings.TryGetValue("OpenAI.ApiKey", out string? key) && !string.IsNullOrWhiteSpace(key);
        return Task.FromResult(ProviderCatalogFactory.Build(ProviderToken, "OpenAI", configured, new[] { "gpt-5.5", "gpt-5.4-mini" }, "gpt-5.5", new[] { "low", "medium", "high" }, "medium"));
    }
}

public sealed class OpenAiCompletionExecutor : IBuffalyCompletionExecutor
{
    private readonly HttpClient _httpClient;
    public OpenAiCompletionExecutor() : this(new HttpClient()) { }
    public OpenAiCompletionExecutor(HttpClient httpClient) => _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    public string Provider => OpenAiProviderCatalogSource.ProviderToken;

    public Task<BuffalyCompletionResult> CompleteAsync(BuffalyCompletionRequest request, CancellationToken cancellationToken)
    {
        return ResponsesApi.ExecuteAsync(_httpClient, request, "OpenAI.ApiKey", "OpenAI.BaseUrl", "https://api.openai.com/v1", cancellationToken);
    }
}

internal static class ProviderCatalogFactory
{
    public static ProviderCatalogSourceResult Build(string provider, string displayName, bool configured, IEnumerable<string> models, string defaultModel, IEnumerable<string> reasoningLevels, string defaultReasoning)
    {
        List<string> levels = reasoningLevels.ToList();
        return new ProviderCatalogSourceResult
        {
            ProviderItem = new ProviderCatalogItemContract
            {
                Provider = provider, DisplayName = displayName, IsConfigured = configured, IsEnabled = configured,
                DefaultTransport = ProviderCatalogDefaults.ProviderNativeTransport, DefaultModelName = defaultModel,
                Transports = new List<ProviderTransportContract> { new() { Provider = provider, Transport = ProviderCatalogDefaults.ProviderNativeTransport, DisplayName = ProviderCatalogDefaults.ProviderNativeDisplayName, IsDefault = true, IsEnabled = configured } },
                Models = models.Select(model => new ProviderModelContract { Provider = provider, Transport = ProviderCatalogDefaults.ProviderNativeTransport, ModelName = model, DisplayName = model, Visibility = "list", SupportedInApi = true, IsDefault = model == defaultModel, DefaultReasoningLevel = defaultReasoning, SupportedReasoningLevels = levels.ToList() }).ToList()
            },
            ReasoningLevelOptions = levels.Select(level => new ProviderReasoningLevelOptionContract { Value = level, Label = ProviderCatalogDefaults.ToReasoningLabel(level) }).ToList()
        };
    }
}

internal static class ResponsesApi
{
    public static async Task<BuffalyCompletionResult> ExecuteAsync(HttpClient client, BuffalyCompletionRequest request, string apiKeySetting, string baseUrlSetting, string defaultBaseUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Options.TryGetValue(apiKeySetting, out string? apiKey) || string.IsNullOrWhiteSpace(apiKey))
            return new BuffalyCompletionResult { Success = false, ErrorCode = "TOKEN_MISSING", ErrorMessage = apiKeySetting + " is required." };
        string baseUrl = request.Options.TryGetValue(baseUrlSetting, out string? configuredBaseUrl) && !string.IsNullOrWhiteSpace(configuredBaseUrl) ? configuredBaseUrl.TrimEnd('/') : defaultBaseUrl;
        var body = new JsonObject
        {
            ["model"] = request.ModelName,
            ["input"] = new JsonArray(request.Messages.Select(message => (JsonNode)new JsonObject { ["role"] = message.Role, ["content"] = message.Content }).ToArray()),
            ["store"] = false
        };
        if (!string.IsNullOrWhiteSpace(request.ReasoningLevel))
            body["reasoning"] = new JsonObject { ["effort"] = request.ReasoningLevel };
        if (request.Tools.Count > 0)
            body["tools"] = new JsonArray(request.Tools.Select(tool => (JsonNode)new JsonObject { ["type"] = "function", ["name"] = tool.Name, ["description"] = tool.Description, ["parameters"] = JsonNode.Parse(tool.JsonSchema) }).ToArray());

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/responses") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using HttpResponseMessage response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return new BuffalyCompletionResult { Success = false, Raw = raw, ErrorCode = ((int)response.StatusCode).ToString(), ErrorMessage = raw };
        using JsonDocument document = JsonDocument.Parse(raw);
        JsonElement root = document.RootElement;
        string text = ReadText(root);
        JsonElement usage = root.TryGetProperty("usage", out JsonElement usageElement) ? usageElement : default;
        return new BuffalyCompletionResult
        {
            Success = true, Text = text, Raw = raw,
            ProviderRequestId = response.Headers.TryGetValues("x-request-id", out IEnumerable<string>? requestIds) ? requestIds.FirstOrDefault() ?? string.Empty : string.Empty,
            ProviderResponseId = root.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty,
            RawUsageJson = usage.ValueKind == JsonValueKind.Undefined ? string.Empty : usage.GetRawText(),
            UsageMetrics = ReadUsage(usage)
        };
    }

    private static string ReadText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out JsonElement direct)) return direct.GetString() ?? string.Empty;
        if (!root.TryGetProperty("output", out JsonElement output) || output.ValueKind != JsonValueKind.Array) return string.Empty;
        foreach (JsonElement item in output.EnumerateArray())
            if (item.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array)
                foreach (JsonElement part in content.EnumerateArray())
                    if (part.TryGetProperty("text", out JsonElement text)) return text.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static IReadOnlyList<BuffalyUsageMetric> ReadUsage(JsonElement usage)
    {
        if (usage.ValueKind != JsonValueKind.Object) return Array.Empty<BuffalyUsageMetric>();
        var metrics = new List<BuffalyUsageMetric>();
        Add("input_tokens"); Add("output_tokens"); Add("total_tokens");
        if (usage.TryGetProperty("output_tokens_details", out JsonElement details) && details.TryGetProperty("reasoning_tokens", out JsonElement reasoning) && reasoning.TryGetDouble(out double reasoningValue)) metrics.Add(new BuffalyUsageMetric { Dimension = "reasoning_tokens", Value = reasoningValue });
        return metrics;
        void Add(string name) { if (usage.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double number)) metrics.Add(new BuffalyUsageMetric { Dimension = name, Value = number }); }
    }
}
