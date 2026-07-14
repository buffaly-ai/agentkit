using Buffaly.AgentKit;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Buffaly.AgentKit.AspNetCore;

public interface IAgentConversationStore
{
    Task SaveAsync(AgentConversation conversation, CancellationToken cancellationToken = default);
    Task<AgentConversation?> LoadAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemoryAgentConversationStore : IAgentConversationStore
{
    private readonly ConcurrentDictionary<string, string> _states = new(StringComparer.Ordinal);
    public Task SaveAsync(AgentConversation conversation, CancellationToken cancellationToken = default) { _states[conversation.Id] = conversation.ExportState(); return Task.CompletedTask; }
    public Task<AgentConversation?> LoadAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_states.TryGetValue(id, out string? state) ? AgentConversation.ImportState(state) : null);
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(_states.Keys.Order().ToArray());
}

public sealed class JsonlAgentConversationStore(string directory) : IAgentConversationStore
{
    private readonly string _directory = directory;
    private string FilePath => Path.Combine(_directory, "conversations.jsonl");
    private string SnapshotPath(string id) => Path.Combine(_directory, id, "conversation.json");
    public async Task SaveAsync(AgentConversation conversation, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        string line = JsonSerializer.Serialize(new ConversationRecord(conversation.Id, conversation.ExportState())) + Environment.NewLine;
        await File.AppendAllTextAsync(FilePath, line, cancellationToken).ConfigureAwait(false);
        string snapshot = SnapshotPath(conversation.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
        await File.WriteAllTextAsync(snapshot, conversation.ExportState(), cancellationToken).ConfigureAwait(false);
    }
    public async Task<AgentConversation?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        string snapshot = SnapshotPath(id);
        if (File.Exists(snapshot)) return AgentConversation.ImportState(await File.ReadAllTextAsync(snapshot, cancellationToken).ConfigureAwait(false));
        if (!File.Exists(FilePath)) return null;
        ConversationRecord? latest = null;
        foreach (string line in await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            ConversationRecord? record = JsonSerializer.Deserialize<ConversationRecord>(line);
            if (record?.Id == id) latest = record;
        }
        return latest == null ? null : AgentConversation.ImportState(latest.State);
    }
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath)) return Array.Empty<string>();
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string line in await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            ConversationRecord? record = JsonSerializer.Deserialize<ConversationRecord>(line);
            if (!string.IsNullOrWhiteSpace(record?.Id)) ids.Add(record.Id);
        }
        return ids.Order().ToArray();
    }
    private sealed record ConversationRecord(string Id, string State);
}

