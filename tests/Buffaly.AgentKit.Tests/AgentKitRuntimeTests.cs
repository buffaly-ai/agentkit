using System.Collections.Concurrent;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using Microsoft.Extensions.AI;
using Xunit;

namespace Buffaly.AgentKit.Tests;

public sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new(responses);
    public List<IList<ChatMessage>> Requests { get; } = new();
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        if (_responses.Count == 0) throw new InvalidOperationException("No scripted response available.");
        return Task.FromResult(_responses.Dequeue());
    }
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}

public sealed class DenyAllPolicy : IAgentToolPolicy
{
    public ValueTask<AgentToolPolicyDecision> EvaluateAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default) => ValueTask.FromResult(AgentToolPolicyDecision.Deny("blocked"));
}

public class AgentKitRuntimeTests
{
    [Fact]
    public async Task FinalAnswerWithoutToolsCompletes()
    {
        var runtime = new AgentKitRuntime(new ScriptedChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "hello"))));
        AgentTurnResult result = await runtime.RunTurnAsync(AgentConversation.Create(), "hi");
        Assert.Equal(AgentStopReason.FinalAnswer, result.StopReason);
        Assert.Equal("hello", result.FinalAnswer);
    }

    [Fact]
    public async Task OneToolCallReturnsResultToModel()
    {
        var call = new FunctionCallContent("1", "add_numbers", new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 });
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]));
        var second = new ChatResponse(new ChatMessage(ChatRole.Assistant, "5"));
        var tool = new DelegateAIFunction("add_numbers", "adds", (a, ct) => ValueTask.FromResult<object?>(Convert.ToInt32(a["a"]) + Convert.ToInt32(a["b"])));
        var conv = AgentConversation.Create();
        AgentTurnResult result = await new AgentKitRuntime(new ScriptedChatClient(first, second), [tool]).RunTurnAsync(conv, "add");
        Assert.Equal("5", conv.Messages.Single(m => m.Role == AgentMessageRole.Tool).Content);
        Assert.Equal(AgentStopReason.FinalAnswer, result.StopReason);
    }

    [Fact]
    public async Task UnknownToolNameReturnsErrorToModel()
    {
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("1", "missing", new Dictionary<string, object?>())]));
        var second = new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"));
        var conv = AgentConversation.Create();
        await new AgentKitRuntime(new ScriptedChatClient(first, second)).RunTurnAsync(conv, "call missing");
        Assert.Contains("Unknown tool", conv.Messages.Single(m => m.Role == AgentMessageRole.Tool).Content);
    }

    [Fact]
    public async Task DeniedToolReturnsErrorToModel()
    {
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("1", "tool", new Dictionary<string, object?>())]));
        var second = new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"));
        var tool = new DelegateAIFunction("tool", "tool", (a, ct) => ValueTask.FromResult<object?>("ok"));
        var conv = AgentConversation.Create();
        await new AgentKitRuntime(new ScriptedChatClient(first, second), [tool], toolPolicy: new DenyAllPolicy()).RunTurnAsync(conv, "call");
        Assert.Contains("Tool denied", conv.Messages.Single(m => m.Role == AgentMessageRole.Tool).Content);
    }

    [Fact]
    public async Task ToolExceptionReturnsErrorToModel()
    {
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("1", "boom", new Dictionary<string, object?>())]));
        var second = new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"));
        var tool = new DelegateAIFunction("boom", "boom", (a, ct) => throw new InvalidOperationException("bad"));
        var conv = AgentConversation.Create();
        await new AgentKitRuntime(new ScriptedChatClient(first, second), [tool]).RunTurnAsync(conv, "call");
        Assert.Contains("bad", conv.Messages.Single(m => m.Role == AgentMessageRole.Tool).Content);
    }

    [Fact]
    public async Task MaximumRoundsTerminates()
    {
        var calls = Enumerable.Range(0, 5).Select(i => new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(i.ToString(), "tool", new Dictionary<string, object?>())]))).ToArray();
        var tool = new DelegateAIFunction("tool", "tool", (a, ct) => ValueTask.FromResult<object?>("ok"));
        AgentTurnResult result = await new AgentKitRuntime(new ScriptedChatClient(calls), [tool], new AgentKitOptions { MaxRounds = 2 }).RunTurnAsync(AgentConversation.Create(), "loop");
        Assert.Equal(AgentStopReason.MaxRounds, result.StopReason);
    }

    [Fact]
    public void ConversationExportImportPreservesHistory()
    {
        var c = AgentConversation.Create("id1");
        c.GetType().GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(c, [new AgentMessage(AgentMessageRole.User, "hi")]);
        var imported = AgentConversation.ImportState(c.ExportState());
        Assert.Equal("id1", imported.Id);
        Assert.Equal("hi", imported.Messages.Single().Content);
    }

    [Fact]
    public async Task EventSequenceIsMonotonic()
    {
        var sink = new InMemoryAgentEventSink();
        await new AgentKitRuntime(new ScriptedChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))), eventSink: sink).RunTurnAsync(AgentConversation.Create(), "hi");
        Assert.Equal(sink.Events.Select(e => e.Sequence).Order(), sink.Events.Select(e => e.Sequence));
    }

    [Fact]
    public async Task MultipleToolCallsInOneRoundAreReturnedToModel()
    {
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, [
            new FunctionCallContent("1", "echo", new Dictionary<string, object?> { ["value"] = "a" }),
            new FunctionCallContent("2", "echo", new Dictionary<string, object?> { ["value"] = "b" })]));
        var second = new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"));
        var tool = new DelegateAIFunction("echo", "echo", (a, ct) => ValueTask.FromResult<object?>(a["value"]));
        var conv = AgentConversation.Create();
        await new AgentKitRuntime(new ScriptedChatClient(first, second), [tool]).RunTurnAsync(conv, "echo twice");
        Assert.Equal(["a", "b"], conv.Messages.Where(m => m.Role == AgentMessageRole.Tool).Select(m => m.Content).ToArray());
    }

    [Fact]
    public async Task MalformedArgumentsReturnToolErrorToModel()
    {
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("1", "needs_number", new Dictionary<string, object?> { ["n"] = "not-number" })]));
        var second = new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"));
        var tool = new DelegateAIFunction("needs_number", "needs number", (a, ct) => ValueTask.FromResult<object?>(Convert.ToInt32(a["n"])));
        var conv = AgentConversation.Create();
        await new AgentKitRuntime(new ScriptedChatClient(first, second), [tool]).RunTurnAsync(conv, "bad args");
        Assert.Contains("Tool error", conv.Messages.Single(m => m.Role == AgentMessageRole.Tool).Content);
    }

    [Fact]
    public async Task ToolTimeoutReturnsErrorToModel()
    {
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("1", "slow", new Dictionary<string, object?>())]));
        var second = new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"));
        var tool = new DelegateAIFunction("slow", "slow", async (a, ct) => { await Task.Delay(TimeSpan.FromSeconds(5), ct); return "late"; });
        var conv = AgentConversation.Create();
        await new AgentKitRuntime(new ScriptedChatClient(first, second), [tool], new AgentKitOptions { ToolTimeout = TimeSpan.FromMilliseconds(10) }).RunTurnAsync(conv, "slow");
        Assert.Contains("timed out", conv.Messages.Single(m => m.Role == AgentMessageRole.Tool).Content);
    }

    [Fact]
    public async Task SecondProviderRequestContainsAssistantFunctionCallAndMatchingResult()
    {
        var client = new ScriptedChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "add_numbers", new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 })])),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "5")));
        var tool = new DelegateAIFunction("add_numbers", "adds", (a, ct) => ValueTask.FromResult<object?>(5));

        await new AgentKitRuntime(client, [tool]).RunTurnAsync(AgentConversation.Create(), "add");

        IList<ChatMessage> request = client.Requests[1];
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool], request.Select(message => message.Role).ToArray());
        FunctionCallContent retainedCall = Assert.Single(request[1].Contents.OfType<FunctionCallContent>());
        FunctionResultContent retainedResult = Assert.Single(request[2].Contents.OfType<FunctionResultContent>());
        Assert.Equal("call-1", retainedCall.CallId);
        Assert.Equal("add_numbers", retainedCall.Name);
        Assert.Equal("call-1", retainedResult.CallId);
        Assert.Equal(5L, Convert.ToInt64(retainedResult.Result));
    }

    [Fact]
    public async Task CombinedAssistantTextAndFunctionCallArePreserved()
    {
        var firstMessage = new ChatMessage(ChatRole.Assistant, [
            new TextContent("I will calculate it."),
            new FunctionCallContent("call-1", "add_numbers", new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 })]);
        var client = new ScriptedChatClient(
            new ChatResponse(firstMessage),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "5")));
        var tool = new DelegateAIFunction("add_numbers", "adds", (a, ct) => ValueTask.FromResult<object?>(5));
        var conversation = AgentConversation.Create();

        await new AgentKitRuntime(client, [tool]).RunTurnAsync(conversation, "add");

        AgentMessage assistant = conversation.Messages.First(message => message.Role == AgentMessageRole.Assistant);
        Assert.Collection(
            assistant.Contents,
            content => Assert.Equal("I will calculate it.", Assert.IsType<AgentTextContent>(content).Text),
            content => Assert.Equal("call-1", Assert.IsType<AgentFunctionCallContent>(content).CallId));
        Assert.Collection(
            client.Requests[1][1].Contents,
            content => Assert.Equal("I will calculate it.", Assert.IsType<TextContent>(content).Text),
            content => Assert.Equal("call-1", Assert.IsType<FunctionCallContent>(content).CallId));
    }

    [Fact]
    public async Task MultipleFunctionCallsPreserveOrderAndCallIds()
    {
        var client = new ScriptedChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, [
                new FunctionCallContent("call-a", "echo", new Dictionary<string, object?> { ["value"] = "a" }),
                new FunctionCallContent("call-b", "echo", new Dictionary<string, object?> { ["value"] = "b" })])),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        var tool = new DelegateAIFunction("echo", "echo", (a, ct) => ValueTask.FromResult<object?>(a["value"]));

        await new AgentKitRuntime(client, [tool]).RunTurnAsync(AgentConversation.Create(), "echo");

        Assert.Equal(["call-a", "call-b"], client.Requests[1][1].Contents.OfType<FunctionCallContent>().Select(call => call.CallId).ToArray());
        Assert.Equal(["call-a", "call-b"], client.Requests[1].Skip(2).SelectMany(message => message.Contents).OfType<FunctionResultContent>().Select(result => result.CallId).ToArray());
    }

    [Fact]
    public async Task ConversationExportImportPreservesFunctionCallsAndResults()
    {
        var client = new ScriptedChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "add_numbers", new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 })])),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "5")));
        var tool = new DelegateAIFunction("add_numbers", "adds", (a, ct) => ValueTask.FromResult<object?>(5));
        var conversation = AgentConversation.Create("typed-history");
        await new AgentKitRuntime(client, [tool]).RunTurnAsync(conversation, "add");

        AgentConversation imported = AgentConversation.ImportState(conversation.ExportState());

        AgentFunctionCallContent call = Assert.Single(imported.Messages.SelectMany(message => message.Contents).OfType<AgentFunctionCallContent>());
        AgentFunctionResultContent result = Assert.Single(imported.Messages.SelectMany(message => message.Contents).OfType<AgentFunctionResultContent>());
        Assert.Equal("call-1", call.CallId);
        Assert.Equal(2, call.Arguments["a"]!.GetValue<int>());
        Assert.Equal("call-1", result.CallId);
        Assert.Equal("5", result.Result);
    }

    [Fact]
    public async Task ExcessToolCallsTerminateExplicitly()
    {
        var sink = new InMemoryAgentEventSink();
        var client = new ScriptedChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, [
            new FunctionCallContent("call-1", "echo", new Dictionary<string, object?>()),
            new FunctionCallContent("call-2", "echo", new Dictionary<string, object?>())])));
        var tool = new DelegateAIFunction("echo", "echo", (a, ct) => ValueTask.FromResult<object?>("unexpected"));

        AgentTurnResult turn = await new AgentKitRuntime(
            client,
            [tool],
            new AgentKitOptions { MaxToolCallsPerRound = 1 },
            sink).RunTurnAsync(AgentConversation.Create(), "too many");

        Assert.Equal(AgentStopReason.ToolCallLimit, turn.StopReason);
        Assert.Contains(sink.Events, agentEvent => agentEvent.Kind == AgentEventKind.TurnLimitReached);
        Assert.DoesNotContain(sink.Events, agentEvent => agentEvent.Kind == AgentEventKind.ToolCallStarted);
    }

    [Fact]
    public async Task ModelCallsProtoScriptToolAndUsesObservedResultInFinalAnswer()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string manifest = Path.Combine(root, "samples", "Tools", "agentkit.json");
        await using ProtoScriptToolSet toolSet = await ProtoScriptToolSet.LoadAsync(manifest);
        ProtoScriptAIFunction tool = Assert.Single(toolSet.Functions);
        Assert.Equal("ProtoScript", tool.AdditionalProperties["buffaly.toolSource"]);
        var provider = new StrictArithmeticChatClient(17, 25);
        var events = new InMemoryAgentEventSink();
        var conversation = AgentConversation.Create();

        AgentTurnResult result = await new AgentKitRuntime(provider, toolSet.Tools, eventSink: events)
            .RunTurnAsync(conversation, "Add 17 and 25.");

        AgentFunctionCallContent call = Assert.Single(conversation.Messages.SelectMany(message => message.Contents).OfType<AgentFunctionCallContent>());
        AgentFunctionResultContent functionResult = Assert.Single(conversation.Messages.SelectMany(message => message.Contents).OfType<AgentFunctionResultContent>());
        Assert.Equal(17, call.Arguments["a"]!.GetValue<int>());
        Assert.Equal(25, call.Arguments["b"]!.GetValue<int>());
        Assert.Equal("proof-call-1", call.CallId);
        Assert.Equal(call.CallId, functionResult.CallId);
        Assert.Equal("42", functionResult.Result);
        Assert.True(provider.ObservedMatchingResult);
        Assert.Equal("The result is 42.", result.FinalAnswer);
        Assert.Contains(events.Events, agentEvent => agentEvent.Kind == AgentEventKind.ToolCallCompleted && agentEvent.ToolName == tool.Name && agentEvent.ToolSource == "ProtoScript");
    }
}

