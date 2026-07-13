using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Microsoft.Extensions.AI;

string manifest = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tools", "agentkit.json"));
await using ProtoScriptToolSet tools = await ProtoScriptToolSet.LoadAsync(manifest);
var events = new InMemoryAgentEventSink();
var chat = new ScriptedChatClient(
    new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "add_numbers", new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 })])),
    new ChatResponse(new ChatMessage(ChatRole.Assistant, "The answer is 5.")));
var runtime = new AgentKitRuntime(chat, tools.Tools, eventSink: events);
AgentTurnResult result = await runtime.RunTurnAsync(AgentConversation.Create(), "Add 2 and 3.");
Console.WriteLine(result.FinalAnswer);
foreach (AgentEvent e in events.Events)
    Console.WriteLine($"{e.Sequence}: {e.Kind} {e.ToolName} {e.Message}".Trim());

public sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new(responses);
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(_responses.Dequeue());
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}
