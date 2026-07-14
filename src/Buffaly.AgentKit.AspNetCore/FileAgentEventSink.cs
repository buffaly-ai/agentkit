using System.Text.Json;
using Buffaly.AgentKit;

namespace Buffaly.AgentKit.AspNetCore;

public interface IAgentEventStore : IAgentEventSink
{
    Task<IReadOnlyList<AgentEvent>> ReadAsync(string conversationId, CancellationToken cancellationToken = default);
}

public sealed class FileAgentEventSink(string rootDirectory) : IAgentEventStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string EventFile(string conversationId) => Path.Combine(rootDirectory, conversationId, "events.jsonl");

    public async ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default)
    {
        string file = EventFile(agentEvent.ConversationId);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long lastSequence = await ReadLastSequenceAsync(file, cancellationToken).ConfigureAwait(false);
            AgentEvent persisted = agentEvent.Sequence > lastSequence
                ? agentEvent
                : new AgentEvent { SchemaVersion = agentEvent.SchemaVersion, EventId = agentEvent.EventId, Sequence = lastSequence + 1, ConversationId = agentEvent.ConversationId, TurnId = agentEvent.TurnId, Round = agentEvent.Round, CreatedAt = agentEvent.CreatedAt, Kind = agentEvent.Kind, Data = agentEvent.Data };
            await File.AppendAllTextAsync(file, JsonSerializer.Serialize(persisted) + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private static async Task<long> ReadLastSequenceAsync(string file, CancellationToken cancellationToken)
    {
        if (!File.Exists(file)) return 0;
        string? last = (await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false)).LastOrDefault(line => !string.IsNullOrWhiteSpace(line));
        return last is null ? 0 : JsonSerializer.Deserialize<AgentEvent>(last)?.Sequence ?? 0;
    }

    public async Task<IReadOnlyList<AgentEvent>> ReadAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        string file = EventFile(conversationId);
        if (!File.Exists(file)) return Array.Empty<AgentEvent>();
        var events = new List<AgentEvent>();
        foreach (string line in await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            AgentEvent? agentEvent = JsonSerializer.Deserialize<AgentEvent>(line);
            if (agentEvent is not null) events.Add(agentEvent);
        }
        return events.OrderBy(agentEvent => agentEvent.Sequence).ToArray();
    }
}
