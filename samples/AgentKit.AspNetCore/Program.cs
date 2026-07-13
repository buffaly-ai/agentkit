using Buffaly.AgentKit.AspNetCore;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IChatClient>(new ScriptedChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello from Agent Kit."))));
builder.Services.AddBuffalyAgentKit(agentKit =>
{
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    agentKit.UseJsonlStore(Path.Combine(root, "data"));
    agentKit.AddProtoScriptTools(Path.Combine(root, "Tools", "agentkit.json"));
});

var app = builder.Build();
app.MapBuffalyAgentKit("/agentkit");
app.Run();

public sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new(responses);
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}
