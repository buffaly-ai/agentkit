using System.Text.Json;
using Microsoft.Extensions.AI;
namespace Buffaly.AgentKit;
public sealed class AgentKitRuntime
{
    private readonly IChatClient _chatClient; private readonly IReadOnlyDictionary<string, AIFunction> _tools; private readonly AgentKitOptions _options; private readonly IAgentEventSink _events; private readonly IAgentToolPolicy _toolPolicy; private long _sequence;
    public AgentKitRuntime(IChatClient chatClient, IEnumerable<AIFunction>? tools = null, AgentKitOptions? options = null, IAgentEventSink? eventSink = null, IAgentToolPolicy? toolPolicy = null) { _chatClient = chatClient; _tools = (tools ?? []).ToDictionary(t => t.Name, StringComparer.Ordinal); _options = options ?? new AgentKitOptions(); _events = eventSink ?? NullAgentEventSink.Instance; _toolPolicy = toolPolicy ?? AllowAllAgentToolPolicy.Instance; }
    public async Task<AgentTurnResult> RunTurnAsync(AgentConversation conversation, string userMessage, CancellationToken cancellationToken = default)
    {
        using IDisposable turn = await conversation.EnterTurnAsync(cancellationToken).ConfigureAwait(false); conversation.Add(new AgentMessage(AgentMessageRole.User, userMessage)); await EmitAsync(AgentEventKind.TurnStarted, "Turn started", cancellationToken: cancellationToken).ConfigureAwait(false);
        for (int round = 1; round <= _options.MaxRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested(); await EmitAsync(AgentEventKind.RoundStarted, $"Round {round} started", cancellationToken: cancellationToken).ConfigureAwait(false);
            ChatOptions options = new() { Tools = _tools.Values.Cast<AITool>().ToList(), AllowMultipleToolCalls = true };
            ChatResponse response = await _chatClient.GetResponseAsync(conversation.ToChatMessages(), options, cancellationToken).ConfigureAwait(false); await EmitAsync(AgentEventKind.ModelResponseReceived, "Model response received", cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (ChatMessage m in response.Messages) if (!string.IsNullOrEmpty(m.Text)) conversation.Add(new AgentMessage(AgentMessageRole.Assistant, m.Text));
            List<FunctionCallContent> calls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Take(_options.MaxToolCallsPerRound + 1).ToList();
            if (calls.Count == 0) { await EmitAsync(AgentEventKind.TurnCompleted, "Turn completed", cancellationToken: cancellationToken).ConfigureAwait(false); return new AgentTurnResult(AgentStopReason.FinalAnswer, response.Text, round, conversation.Messages); }
            foreach (FunctionCallContent call in calls.Take(_options.MaxToolCallsPerRound)) conversation.Add(new AgentMessage(AgentMessageRole.Tool, await InvokeToolAsync(call, cancellationToken).ConfigureAwait(false), call.CallId, call.Name));
        }
        await EmitAsync(AgentEventKind.TurnCompleted, "Maximum rounds reached", cancellationToken: cancellationToken).ConfigureAwait(false); return new AgentTurnResult(AgentStopReason.MaxRounds, null, _options.MaxRounds, conversation.Messages);
    }
    private async Task<string> InvokeToolAsync(FunctionCallContent call, CancellationToken cancellationToken)
    {
        await EmitAsync(AgentEventKind.ToolCallStarted, "Tool call started", call.Name, call.CallId, cancellationToken).ConfigureAwait(false); if (!_tools.TryGetValue(call.Name, out AIFunction? tool)) { string e = $"Unknown tool: {call.Name}"; await EmitAsync(AgentEventKind.ToolCallFailed, e, call.Name, call.CallId, cancellationToken).ConfigureAwait(false); return e; }
        Dictionary<string, object?> args = (call.Arguments ?? new Dictionary<string, object?>()).ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value, StringComparer.Ordinal); AgentToolPolicyDecision decision = await _toolPolicy.EvaluateAsync(call.Name, args, cancellationToken).ConfigureAwait(false); if (!decision.Allowed) { string d = "Tool denied" + (string.IsNullOrWhiteSpace(decision.Reason) ? string.Empty : $": {decision.Reason}"); await EmitAsync(AgentEventKind.ToolCallDenied, d, call.Name, call.CallId, cancellationToken).ConfigureAwait(false); return d; }
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(_options.ToolTimeout);
        try { object? value = await tool.InvokeAsync(new AIFunctionArguments(args!), timeout.Token).ConfigureAwait(false); string result = value is string s ? s : JsonSerializer.Serialize(value); if (result.Length > _options.MaxToolResultCharacters) result = result[.._options.MaxToolResultCharacters]; await EmitAsync(AgentEventKind.ToolCallCompleted, "Tool call completed", call.Name, call.CallId, cancellationToken).ConfigureAwait(false); return result; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { string e = $"Tool timed out after {_options.ToolTimeout}."; await EmitAsync(AgentEventKind.ToolCallFailed, e, call.Name, call.CallId, cancellationToken).ConfigureAwait(false); return e; }
        catch (Exception ex) { string e = $"Tool error: {ex.Message}"; await EmitAsync(AgentEventKind.ToolCallFailed, e, call.Name, call.CallId, cancellationToken).ConfigureAwait(false); return e; }
    }
    private ValueTask EmitAsync(AgentEventKind kind, string? message = null, string? toolName = null, string? toolCallId = null, CancellationToken cancellationToken = default) => _events.EmitAsync(new AgentEvent(Interlocked.Increment(ref _sequence), DateTimeOffset.UtcNow, kind, message, toolName, toolCallId), cancellationToken);
}

