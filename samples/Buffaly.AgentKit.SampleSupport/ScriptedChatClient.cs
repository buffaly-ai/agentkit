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
        ValidateObservedConversation(next, observed);

        if (!string.IsNullOrWhiteSpace(next.FunctionName))
        {
            if (options?.Tools == null || !options.Tools.OfType<AIFunction>().Any(t => string.Equals(t.Name, next.FunctionName, StringComparison.Ordinal)))
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

        ChatMessage? lastTool = messages.LastOrDefault(m => m.Role == ChatRole.Tool);
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
