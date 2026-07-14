using Microsoft.Extensions.AI;
using Buffaly.AgentKit;

namespace Buffaly.AgentKit.SampleSupport;

public sealed class ScriptedChatClient(ScenarioDefinition scenario) : IChatClient
{
    private int _index;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_index >= scenario.Responses.Count)
            throw new InvalidOperationException($"Scenario '{scenario.ScenarioId}' has no response for round {_index + 1}.");

        ChatMessage[] observed = messages.ToArray();
        ScriptedChatResponse next = scenario.Responses[_index++];
        if (next.ResponseFactory is not null)
            next = next.ResponseFactory(observed);
        ValidateObservedConversation(next, observed);

        if (!string.IsNullOrWhiteSpace(next.FunctionName))
        {
            if (options?.Tools == null || !options.Tools.OfType<AIFunction>().Any(tool => string.Equals(tool.Name, next.FunctionName, StringComparison.Ordinal)))
                throw new InvalidOperationException($"Scenario '{scenario.ScenarioId}' expected tool '{next.FunctionName}', but it was not available in ChatOptions.Tools.");

            string callId = $"{scenario.ScenarioId}-call-{_index}";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent(callId, next.FunctionName, next.Arguments)])));
        }

        if (next.FinalText == null)
            throw new InvalidOperationException($"Scenario '{scenario.ScenarioId}' response {_index} has neither FunctionName nor FinalText.");

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, next.FinalText)));
    }

    private static void ValidateObservedConversation(ScriptedChatResponse expected, IReadOnlyList<ChatMessage> messages)
    {
        if (messages.Count == 0 || messages[^1].Role == ChatRole.System)
            throw new InvalidOperationException("Scripted chat expected a user or tool message before responding.");

        ChatMessage? lastTool = messages.LastOrDefault(message => message.Role == ChatRole.Tool);
        if (!string.IsNullOrWhiteSpace(expected.ExpectedLastToolName))
        {
            if (lastTool == null)
                throw new InvalidOperationException($"Expected previous tool '{expected.ExpectedLastToolName}', but no tool result was observed.");

            FunctionResultContent? result = lastTool.Contents.OfType<FunctionResultContent>().FirstOrDefault();
            string text = result?.Result?.ToString() ?? lastTool.Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(expected.ExpectedLastToolResultContains) && !text.Contains(expected.ExpectedLastToolResultContains, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Expected previous tool result to contain '{expected.ExpectedLastToolResultContains}', but observed '{text}'.");
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Scripted samples use non-streaming turns.");
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}

public sealed class StrictArithmeticChatClient(int a, int b) : IChatClient
{
    private const string ToolName = "add_numbers";
    private const string CallId = "proof-call-1";
    private int _round;

    public bool ObservedMatchingResult { get; private set; }
    public object? ObservedResult { get; private set; }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        ChatMessage[] transcript = messages.ToArray();
        _round++;
        return _round switch
        {
            1 => Task.FromResult(CreateFunctionCall(options)),
            2 => Task.FromResult(CreateObservedResultAnswer(transcript)),
            _ => throw new InvalidOperationException("Strict arithmetic proof supports exactly two provider rounds.")
        };
    }

    private ChatResponse CreateFunctionCall(ChatOptions? options)
    {
        AIFunction tool = options?.Tools?.OfType<AIFunction>().SingleOrDefault(candidate => candidate.Name == ToolName)
            ?? throw new InvalidOperationException("The add_numbers tool was not available to the provider.");
        var schema = tool.JsonSchema;
        var properties = schema.GetProperty("properties");
        string[] required = schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (!properties.TryGetProperty("a", out var aSchema) || !properties.TryGetProperty("b", out var bSchema)
            || aSchema.GetProperty("type").GetString() != "number" || bSchema.GetProperty("type").GetString() != "number"
            || !required.Contains("a", StringComparer.Ordinal) || !required.Contains("b", StringComparer.Ordinal))
            throw new InvalidOperationException("The add_numbers provider schema must expose required numeric a and b parameters.");

        return new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent(CallId, ToolName, new Dictionary<string, object?> { ["a"] = a, ["b"] = b })]));
    }

    private ChatResponse CreateObservedResultAnswer(IReadOnlyList<ChatMessage> transcript)
    {
        FunctionCallContent call = transcript.SelectMany(message => message.Contents).OfType<FunctionCallContent>()
            .SingleOrDefault(content => content.CallId == CallId && content.Name == ToolName)
            ?? throw new InvalidOperationException("The second provider request omitted the preceding assistant function call.");
        FunctionResultContent result = transcript.SelectMany(message => message.Contents).OfType<FunctionResultContent>()
            .SingleOrDefault(content => content.CallId == call.CallId)
            ?? throw new InvalidOperationException("The second provider request omitted the matching function result.");

        ObservedMatchingResult = true;
        ObservedResult = result.Result;
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, $"The result is {result.Result}."));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}

