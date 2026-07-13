using Buffaly.AgentKit;
using System.Text.Json;

namespace Buffaly.AgentKit.AspNetCore;

public sealed class FileAgentEventSink(string filePath) : IAgentEventSink
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        string line = JsonSerializer.Serialize(agentEvent) + Environment.NewLine;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await File.AppendAllTextAsync(filePath, line, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
}
