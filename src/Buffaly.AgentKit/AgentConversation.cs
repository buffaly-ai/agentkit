using System.Text.Json;
using Microsoft.Extensions.AI;
namespace Buffaly.AgentKit;
public sealed class AgentConversation
{
    private readonly SemaphoreSlim _turnLock = new(1, 1); private readonly List<AgentMessage> _messages = new(); private AgentConversation(string id) => Id = id;
    public string Id { get; } public IReadOnlyList<AgentMessage> Messages => _messages;
    public static AgentConversation Create(string? id = null) => new(id ?? Guid.NewGuid().ToString("n"));
    public void AddSystemMessage(string content) => Add(new AgentMessage(AgentMessageRole.System, content));
    public string ExportState() => JsonSerializer.Serialize(new AgentConversationState(Id, _messages));
    public static AgentConversation ImportState(string json) { AgentConversationState state = JsonSerializer.Deserialize<AgentConversationState>(json) ?? throw new InvalidOperationException("Invalid conversation state."); AgentConversation c = new(state.Id); c._messages.AddRange(state.Messages ?? []); return c; }
    internal async ValueTask<IDisposable> EnterTurnAsync(CancellationToken cancellationToken) { await _turnLock.WaitAsync(cancellationToken).ConfigureAwait(false); return new Releaser(_turnLock); }
    internal void Add(AgentMessage message) => _messages.Add(message);
    internal IEnumerable<ChatMessage> ToChatMessages() { foreach (AgentMessage m in _messages) { if (m.Role == AgentMessageRole.Tool) yield return new ChatMessage(ChatRole.Tool, [new FunctionResultContent(m.ToolCallId ?? string.Empty, m.Content)]); else yield return new ChatMessage(ToChatRole(m.Role), m.Content); } }
    private static ChatRole ToChatRole(AgentMessageRole role) => role switch { AgentMessageRole.System => ChatRole.System, AgentMessageRole.Assistant => ChatRole.Assistant, _ => ChatRole.User };
    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable { public void Dispose() => semaphore.Release(); }
}
public sealed record AgentConversationState(string Id, List<AgentMessage>? Messages);
