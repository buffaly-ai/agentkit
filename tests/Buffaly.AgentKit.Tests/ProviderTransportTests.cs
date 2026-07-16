using Buffaly.ProviderContracts;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Buffaly.AgentKit.Tests;

public sealed class ProviderTransportTests
{
    [Theory]
    [InlineData("openai")]
    [InlineData("xai")]
    public async Task ResponsesProviders_SendOnlyCallerMessagesAndNoTools(string provider)
    {
        var handler = new CaptureHandler("{\"id\":\"response-1\",\"output_text\":\"B\",\"usage\":{\"input_tokens\":10,\"output_tokens\":2,\"total_tokens\":12,\"output_tokens_details\":{\"reasoning_tokens\":1}}}");
        using var client = new HttpClient(handler);
        IBuffalyCompletionExecutor executor = provider == "openai" ? new Buffaly.Provider.OpenAi.OpenAiCompletionExecutor(client) : new Buffaly.Provider.Xai.XaiCompletionExecutor(client);
        string keyName = provider == "openai" ? "OpenAI.ApiKey" : "XAI.ApiKey";
        string baseName = provider == "openai" ? "OpenAI.BaseUrl" : "XAI.BaseUrl";

        BuffalyCompletionResult result = await executor.CompleteAsync(Request(provider, new Dictionary<string, string> { [keyName] = "secret", [baseName] = "https://example.test/v1" }), default);

        Assert.True(result.Success);
        Assert.Equal("B", result.Text);
        using JsonDocument request = JsonDocument.Parse(handler.Body!);
        JsonElement root = request.RootElement;
        JsonElement input = root.GetProperty("input");
        Assert.Equal(1, input.GetArrayLength());
        Assert.Equal("user", input[0].GetProperty("role").GetString());
        Assert.False(root.TryGetProperty("tools", out _));
        Assert.DoesNotContain("system", handler.Body!, StringComparison.Ordinal);
        Assert.Equal(4, result.UsageMetrics.Count);
    }

    [Fact]
    public async Task Ollama_SendsOnlyCallerMessagesAndNoTools()
    {
        var handler = new CaptureHandler("{\"message\":{\"role\":\"assistant\",\"content\":\"C\"},\"prompt_eval_count\":8,\"eval_count\":1}");
        using var client = new HttpClient(handler);
        var executor = new Buffaly.Provider.Ollama.OllamaCompletionExecutor(client);

        BuffalyCompletionResult result = await executor.CompleteAsync(Request("ollama", new Dictionary<string, string> { ["Ollama.BaseUrl"] = "http://example.test" }), default);

        Assert.True(result.Success);
        Assert.Equal("C", result.Text);
        using JsonDocument request = JsonDocument.Parse(handler.Body!);
        JsonElement root = request.RootElement;
        JsonElement messages = root.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.False(root.TryGetProperty("tools", out _));
        Assert.DoesNotContain("system", handler.Body!, StringComparison.Ordinal);
        Assert.Equal(3, result.UsageMetrics.Count);
    }

    private static BuffalyCompletionRequest Request(string provider, IReadOnlyDictionary<string, string> options) => new()
    {
        Provider = provider,
        ModelName = provider == "ollama" ? "glm-5.2" : provider == "xai" ? "grok-4.3" : "gpt-5.5",
        ReasoningLevel = provider == "ollama" ? string.Empty : "medium",
        Messages = new[] { new BuffalyChatMessage { Role = "user", Content = "Medical question" } },
        Tools = Array.Empty<BuffalyToolDefinition>(),
        Options = options
    };

    private sealed class CaptureHandler(string responseJson) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
        }
    }
}
