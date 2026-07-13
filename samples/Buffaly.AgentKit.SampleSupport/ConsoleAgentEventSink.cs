using Buffaly.AgentKit;

namespace Buffaly.AgentKit.SampleSupport;

public sealed class ConsoleAgentEventSink : IAgentEventSink
{
    public ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default)
    {
        if (agentEvent.Kind == AgentEventKind.TurnStarted) Console.WriteLine("[turn] started");
        else if (agentEvent.Kind == AgentEventKind.ToolCallStarted) Console.WriteLine($"[tool] {agentEvent.ToolName}");
        else if (agentEvent.Kind == AgentEventKind.TurnCompleted) Console.WriteLine("[turn] completed");
        return ValueTask.CompletedTask;
    }
}
