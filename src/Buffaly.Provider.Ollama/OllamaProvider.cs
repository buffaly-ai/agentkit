using Buffaly.ProviderContracts;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Buffaly.Provider.Ollama;

public sealed class OllamaProviderModule : IBuffalyProviderModule
{
    public void Register(IBuffalyProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.AddCatalogSource(new OllamaProviderCatalogSource());
        registry.AddCompletionExecutor(new OllamaCompletionExecutor());
    }
}

public sealed class OllamaProviderCatalogSource : IBuffalyProviderCatalogSource
{
    public const string ProviderToken = "ollama";
    private static readonly string[] Models = { "glm-5.2", "gemma3:27b", "gemma4:31b", "medgemma:27b" };
    public string Provider => ProviderToken;
    public Task<ProviderCatalogSourceResult> BuildCatalogAsync(BuffalyProviderCatalogContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string baseUrl = context.Settings.TryGetValue("Ollama.BaseUrl", out string? configured) ? configured : "http://localhost:11434";
        bool enabled = Uri.TryCreate(baseUrl, UriKind.Absolute, out _);
        return Task.FromResult(new ProviderCatalogSourceResult
        {
            ProviderItem = new ProviderCatalogItemContract
            {
                Provider = ProviderToken, DisplayName = "Ollama", IsConfigured = enabled, IsEnabled = enabled, DefaultTransport = ProviderCatalogDefaults.ProviderNativeTransport, DefaultModelName = Models[0],
                Transports = new List<ProviderTransportContract> { new() { Provider = ProviderToken, Transport = ProviderCatalogDefaults.ProviderNativeTransport, DisplayName = ProviderCatalogDefaults.ProviderNativeDisplayName, IsDefault = true, IsEnabled = enabled } },
                Models = Models.Select(model => new ProviderModelContract { Provider = ProviderToken, Transport = ProviderCatalogDefaults.ProviderNativeTransport, ModelName = model, DisplayName = model, Visibility = "list", SupportedInApi = true, IsDefault = model == Models[0] }).ToList()
            }
        });
    }
}

public sealed class OllamaCompletionExecutor : IBuffalyCompletionExecutor
{
    private readonly HttpClient _httpClient;
    public OllamaCompletionExecutor() : this(new HttpClient()) { }
    public OllamaCompletionExecutor(HttpClient httpClient) => _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    public string Provider => OllamaProviderCatalogSource.ProviderToken;

    public async Task<BuffalyCompletionResult> CompleteAsync(BuffalyCompletionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string baseUrl = request.Options.TryGetValue("Ollama.BaseUrl", out string? configured) && !string.IsNullOrWhiteSpace(configured) ? configured.TrimEnd('/') : "http://localhost:11434";
        var body = new JsonObject
        {
            ["model"] = request.ModelName,
            ["stream"] = false,
            ["messages"] = new JsonArray(request.Messages.Select(message => (JsonNode)new JsonObject { ["role"] = message.Role, ["content"] = message.Content }).ToArray())
        };
        if (request.Tools.Count > 0) body["tools"] = new JsonArray(request.Tools.Select(tool => (JsonNode)new JsonObject { ["type"] = "function", ["function"] = new JsonObject { ["name"] = tool.Name, ["description"] = tool.Description, ["parameters"] = JsonNode.Parse(tool.JsonSchema) } }).ToArray());
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/api/chat") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        if (request.Options.TryGetValue("Ollama.ApiKey", out string? apiKey) && !string.IsNullOrWhiteSpace(apiKey)) httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new BuffalyCompletionResult { Success = false, Raw = raw, ErrorCode = ((int)response.StatusCode).ToString(), ErrorMessage = raw };
        using JsonDocument document = JsonDocument.Parse(raw);
        JsonElement root = document.RootElement;
        string text = root.TryGetProperty("message", out JsonElement message) && message.TryGetProperty("content", out JsonElement content) ? content.GetString() ?? string.Empty : string.Empty;
        var usage = new List<BuffalyUsageMetric>();
        Add("prompt_eval_count", "input_tokens"); Add("eval_count", "output_tokens");
        double total = usage.Sum(metric => metric.Value); if (usage.Count > 0) usage.Add(new BuffalyUsageMetric { Dimension = "total_tokens", Value = total });
        return new BuffalyCompletionResult { Success = true, Text = text, Raw = raw, RawUsageJson = JsonSerializer.Serialize(usage.ToDictionary(metric => metric.Dimension, metric => metric.Value)), UsageMetrics = usage };
        void Add(string source, string target) { if (root.TryGetProperty(source, out JsonElement value) && value.TryGetDouble(out double number)) usage.Add(new BuffalyUsageMetric { Dimension = target, Value = number }); }
    }
}
