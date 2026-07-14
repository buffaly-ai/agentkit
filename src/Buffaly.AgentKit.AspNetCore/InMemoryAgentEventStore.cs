using System.Collections.Concurrent;
using Buffaly.AgentKit;

namespace Buffaly.AgentKit.AspNetCore;

public sealed class InMemoryAgentEventStore : IAgentEventStore
{
    private readonly ConcurrentDictionary<string, List<AgentEvent>> _events = new(StringComparer.Ordinal);
    public ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default)
    {
        List<AgentEvent> list = _events.GetOrAdd(agentEvent.ConversationId, _ => []);
        lock (list) list.Add(agentEvent);
        return ValueTask.CompletedTask;
    }
    public Task<IReadOnlyList<AgentEvent>> ReadAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (!_events.TryGetValue(conversationId, out List<AgentEvent>? list)) return Task.FromResult<IReadOnlyList<AgentEvent>>(Array.Empty<AgentEvent>());
        lock (list) return Task.FromResult<IReadOnlyList<AgentEvent>>(list.OrderBy(item => item.Sequence).ToArray());
    }
}
