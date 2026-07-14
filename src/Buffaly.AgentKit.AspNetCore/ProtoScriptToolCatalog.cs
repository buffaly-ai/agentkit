using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Microsoft.Extensions.AI;

namespace Buffaly.AgentKit.AspNetCore;

public sealed class AgentToolDescriptor
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public System.Text.Json.JsonElement Schema { get; init; }
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
}

public sealed class AgentToolCatalogSnapshot
{
    public string Status { get; init; } = "NotLoaded";
    public IReadOnlyList<AgentToolDescriptor> Tools { get; init; } = Array.Empty<AgentToolDescriptor>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public DateTimeOffset? LoadedAt { get; init; }
}

public sealed class ProtoScriptToolCatalog : IAsyncDisposable
{
    private readonly string _manifestFile;
    private readonly ProtoScriptToolSetOptions? _options;
    private readonly IAgentEventSink _events;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private ProtoScriptToolSet? _toolSet;
    private AgentToolCatalogSnapshot _snapshot = new();

    public ProtoScriptToolCatalog(string manifestFile, ProtoScriptToolSetOptions? options, IAgentEventSink events) { _manifestFile = manifestFile; _options = options; _events = events; }
    public AgentToolCatalogSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public IReadOnlyList<AIFunction> Tools => _toolSet?.Tools ?? Array.Empty<AIFunction>();

    public async Task<AgentToolCatalogSnapshot> ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _reloadGate.WaitAsync(cancellationToken);
        try
        {
            AgentToolCatalogSnapshot previous = Snapshot;
            Volatile.Write(ref _snapshot, new AgentToolCatalogSnapshot { Status = "Loading", Tools = previous.Tools, Errors = previous.Errors, LoadedAt = previous.LoadedAt });
            await EmitAsync(AgentEventKind.ToolsLoading, new(), cancellationToken);
            try
            {
                ProtoScriptToolSet candidate = await ProtoScriptToolSet.LoadAsync(_manifestFile, _options, cancellationToken);
                var descriptors = candidate.Tools.Select(ToDescriptor).ToArray();
                ProtoScriptToolSet? old = Interlocked.Exchange(ref _toolSet, candidate);
                if (old is not null) await old.DisposeAsync();
                var loaded = new AgentToolCatalogSnapshot { Status = "Loaded", Tools = descriptors, LoadedAt = DateTimeOffset.UtcNow };
                Volatile.Write(ref _snapshot, loaded);
                await EmitAsync(AgentEventKind.ToolsLoaded, new() { ["count"] = descriptors.Length }, cancellationToken);
                return loaded;
            }
            catch (Exception exception)
            {
                var failed = new AgentToolCatalogSnapshot { Status = "Failed", Tools = previous.Tools, Errors = new[] { exception.Message }, LoadedAt = previous.LoadedAt };
                Volatile.Write(ref _snapshot, failed);
                await EmitAsync(AgentEventKind.ToolsLoadFailed, new() { ["errorType"] = exception.GetType().FullName, ["errorMessage"] = exception.Message }, cancellationToken);
                return failed;
            }
        }
        finally { _reloadGate.Release(); }
    }

    private static AgentToolDescriptor ToDescriptor(AIFunction tool) => new() { Name = tool.Name, Description = tool.Description, Schema = tool.JsonSchema, Metadata = tool.AdditionalProperties };
    private ValueTask EmitAsync(AgentEventKind kind, System.Text.Json.Nodes.JsonObject data, CancellationToken ct) => _events.EmitAsync(new AgentEvent { EventId = Guid.NewGuid().ToString("n"), Sequence = DateTimeOffset.UtcNow.UtcTicks, ConversationId = "_tools", TurnId = "tool-load", CreatedAt = DateTimeOffset.UtcNow, Kind = kind, Data = data }, ct);
    public async ValueTask DisposeAsync() { if (_toolSet is not null) await _toolSet.DisposeAsync(); _reloadGate.Dispose(); }
}
