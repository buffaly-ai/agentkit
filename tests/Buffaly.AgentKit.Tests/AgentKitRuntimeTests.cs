using System.Collections.Concurrent;
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
}
