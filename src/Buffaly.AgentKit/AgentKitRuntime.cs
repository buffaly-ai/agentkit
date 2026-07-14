using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
namespace Buffaly.AgentKit;

// Public contracts are documented in docs/ and in the generated package XML documentation.

public sealed class AgentKitRuntime
{
    readonly IChatClient _chatClient; readonly IReadOnlyDictionary<string, AIFunction> _tools; readonly AgentKitOptions _options; readonly IAgentEventSink _events; readonly IAgentToolPolicy _policy; readonly Dictionary<string, long> _sequences = new(StringComparer.Ordinal); readonly object _sequenceLock = new();
    public AgentKitRuntime(IChatClient c, IEnumerable<AIFunction>? tools = null, AgentKitOptions? options = null, IAgentEventSink? eventSink = null, IAgentToolPolicy? toolPolicy = null) { _chatClient = c; _tools = (tools ?? []).ToDictionary(t => t.Name); _options = options ?? new(); _events = eventSink ?? NullAgentEventSink.Instance; _policy = toolPolicy ?? AllowAllAgentToolPolicy.Instance; }
    public async Task<AgentTurnResult> RunTurnAsync(AgentConversation conv, string input, CancellationToken ct = default)
    {
        using var turn = await conv.EnterTurnAsync(ct); string turnId = Guid.NewGuid().ToString("n"); int round = 0;
        await Emit(conv.Id, turnId, 0, AgentEventKind.TurnStarted, new() { { "message", "Turn started" } }, ct); conv.Add(new(AgentMessageRole.User, input)); await Emit(conv.Id, turnId, 0, AgentEventKind.UserMessageAdded, new() { { "text", input } }, ct);
        try
        {
            for (round = 1; round <= _options.MaxRounds; round++)
            {
                await Emit(conv.Id, turnId, round, AgentEventKind.RoundStarted, new(), ct); await Emit(conv.Id, turnId, round, AgentEventKind.ModelRequestStarted, new(), ct);
                var response = await _chatClient.GetResponseAsync(conv.ToChatMessages(), new ChatOptions { Tools = _tools.Values.Cast<AITool>().ToList(), AllowMultipleToolCalls = true }, ct); await Emit(conv.Id, turnId, round, AgentEventKind.ModelResponseReceived, new(), ct);
                var assistant = new List<AgentMessageContent>();
                foreach (var content in response.Messages.SelectMany(m => m.Contents))
                {
                    if (content is TextContent text) assistant.Add(new AgentTextContent(text.Text));
                    if (content is FunctionCallContent call) { var args = JsonSerializer.SerializeToNode(call.Arguments)?.AsObject() ?? new(); assistant.Add(new AgentFunctionCallContent(call.CallId, call.Name, args)); await Emit(conv.Id, turnId, round, AgentEventKind.AssistantFunctionCallAdded, ToolData(call.Name, call.CallId, args), ct); }
                }
                if (assistant.Count > 0) conv.Add(new(AgentMessageRole.Assistant, assistant)); var calls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
                if (calls.Count > _options.MaxToolCallsPerRound) { await Emit(conv.Id, turnId, round, AgentEventKind.TurnLimitReached, new() { { "limit", _options.MaxToolCallsPerRound }, { "observed", calls.Count } }, ct); return new(AgentStopReason.ToolCallLimit, null, round, conv.Messages); }
                if (calls.Count == 0) { string final = string.Concat(assistant.OfType<AgentTextContent>().Select(x => x.Text)); if (string.IsNullOrWhiteSpace(final)) { await Emit(conv.Id, turnId, round, AgentEventKind.TurnFailed, new() { { "errorType", "EmptyModelResponse" }, { "errorMessage", "The model returned no assistant text or function calls." } }, ct); return new(AgentStopReason.Failed, null, round, conv.Messages); } await Emit(conv.Id, turnId, round, AgentEventKind.AssistantMessageAdded, new() { { "text", final } }, ct); await Emit(conv.Id, turnId, round, AgentEventKind.TurnCompleted, new() { { "stopReason", "FinalAnswer" } }, ct); return new(AgentStopReason.FinalAnswer, final, round, conv.Messages); }
                foreach (var call in calls) conv.Add(new(AgentMessageRole.Tool, new AgentMessageContent[] { await Invoke(conv.Id, turnId, round, call, ct) }));
            }
            await Emit(conv.Id, turnId, _options.MaxRounds, AgentEventKind.TurnLimitReached, new() { { "limit", _options.MaxRounds } }, ct); return new(AgentStopReason.MaxRounds, null, _options.MaxRounds, conv.Messages);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { await Emit(conv.Id, turnId, round, AgentEventKind.TurnFailed, new() { { "errorType", "OperationCanceledException" }, { "errorMessage", "Turn canceled." } }, CancellationToken.None); return new(AgentStopReason.Cancelled, null, round, conv.Messages); }
        catch (Exception ex) { await Emit(conv.Id, turnId, round, AgentEventKind.TurnFailed, new() { { "errorType", ex.GetType().FullName }, { "errorMessage", ex.Message } }, CancellationToken.None); return new(AgentStopReason.Failed, null, round, conv.Messages); }
    }
    async Task<AgentFunctionResultContent> Invoke(string cid, string tid, int round, FunctionCallContent call, CancellationToken ct)
    {
        var argsNode = JsonSerializer.SerializeToNode(call.Arguments)?.AsObject() ?? new(); var data = ToolData(call.Name, call.CallId, argsNode); var sw = Stopwatch.StartNew(); await Emit(cid, tid, round, AgentEventKind.ToolCallStarted, data, ct);
        if (!_tools.TryGetValue(call.Name, out var tool)) return await Failed("UnknownTool", $"Unknown tool: {call.Name}"); AddSource(data, tool);
        var args = (call.Arguments ?? new Dictionary<string, object?>()).ToDictionary(x => x.Key, x => (object?)x.Value); var decision = await _policy.EvaluateAsync(call.Name, args, ct);
        if (!decision.Allowed) { data["reason"] = decision.Reason; data["durationMilliseconds"] = sw.ElapsedMilliseconds; await Emit(cid, tid, round, AgentEventKind.ToolCallDenied, data, ct); return new(call.CallId, call.Name, "Tool denied: " + decision.Reason, true); }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(_options.ToolTimeout);
        try { var value = await tool.InvokeAsync(new AIFunctionArguments(args), timeout.Token); string full = value is string text ? text : JsonSerializer.Serialize(value); bool truncated = full.Length > _options.MaxToolResultCharacters; string result = truncated ? full[.._options.MaxToolResultCharacters] : full; data["result"] = result; data["resultTruncated"] = truncated; data["durationMilliseconds"] = sw.ElapsedMilliseconds; await Emit(cid, tid, round, AgentEventKind.ToolCallCompleted, data, ct); return new(call.CallId, call.Name, result, false); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested) { return await Failed(ex is OperationCanceledException ? "Timeout" : ex.GetType().FullName!, ex is OperationCanceledException ? $"Tool timed out after {_options.ToolTimeout}." : "Tool error: " + ex.Message); }
        async Task<AgentFunctionResultContent> Failed(string type, string message) { data["errorType"] = type; data["errorMessage"] = message; data["durationMilliseconds"] = sw.ElapsedMilliseconds; await Emit(cid, tid, round, AgentEventKind.ToolCallFailed, data, ct); return new(call.CallId, call.Name, message, true); }
    }
    JsonObject ToolData(string name, string id, JsonObject args) { var data = new JsonObject { { "toolName", name }, { "callId", id }, { "arguments", args.DeepClone() } }; if (_tools.TryGetValue(name, out var tool)) AddSource(data, tool); return data; }
    static void AddSource(JsonObject data, AIFunction tool) { data["toolSource"] = tool.AdditionalProperties.GetValueOrDefault("buffaly.toolSource")?.ToString() ?? "CSharp"; foreach (string key in new[] { "projectFile", "prototype", "method" }) if (tool.AdditionalProperties.TryGetValue("buffaly." + key, out var value) && value is not null) data[key] = value.ToString(); }
    ValueTask Emit(string cid, string tid, int round, AgentEventKind kind, JsonObject data, CancellationToken ct) { long sequence; lock (_sequenceLock) { _sequences.TryGetValue(cid, out sequence); _sequences[cid] = ++sequence; } return _events.EmitAsync(new() { EventId = Guid.NewGuid().ToString("n"), Sequence = sequence, ConversationId = cid, TurnId = tid, Round = round, CreatedAt = DateTimeOffset.UtcNow, Kind = kind, Data = data }, ct); }
}
