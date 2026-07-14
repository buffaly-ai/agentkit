namespace Buffaly.AgentKit;

public sealed class AgentKitOptions
{
    public int MaxRounds { get; set; } = 8;
    public int MaxToolCallsPerRound { get; set; } = 8;
    public TimeSpan ToolTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public int MaxToolResultCharacters { get; set; } = 100_000;
}

public enum AgentMessageRole { System, User, Assistant, Tool }
public enum AgentStopReason { FinalAnswer, MaxRounds, ToolCallLimit, Cancelled }
public sealed record AgentTurnResult(AgentStopReason StopReason, string? FinalAnswer, int Rounds, IReadOnlyList<AgentMessage> Messages);
public enum AgentEventKind { TurnStarted, RoundStarted, ModelResponseReceived, ToolCallStarted, ToolCallCompleted, ToolCallDenied, ToolCallFailed, TurnCompleted, TurnLimitReached }
public sealed record AgentEvent(long Sequence, DateTimeOffset Timestamp, AgentEventKind Kind, string? Message = null, string? ToolName = null, string? ToolCallId = null, string? ToolSource = null) { public int SchemaVersion { get; init; } = 1; }
public interface IAgentEventSink { ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default); }
public sealed class NullAgentEventSink : IAgentEventSink { public static NullAgentEventSink Instance { get; } = new(); public ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask; }
public sealed class InMemoryAgentEventSink : IAgentEventSink { private readonly List<AgentEvent> _events = new(); public IReadOnlyList<AgentEvent> Events => _events; public ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default) { _events.Add(agentEvent); return ValueTask.CompletedTask; } }
public sealed class CompositeAgentEventSink(IEnumerable<IAgentEventSink> sinks) : IAgentEventSink { private readonly IReadOnlyList<IAgentEventSink> _sinks = sinks.ToArray(); public async ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default) { foreach (IAgentEventSink sink in _sinks) await sink.EmitAsync(agentEvent, cancellationToken).ConfigureAwait(false); } }
public sealed record AgentToolPolicyDecision(bool Allowed, string? Reason = null) { public static AgentToolPolicyDecision Allow() => new(true); public static AgentToolPolicyDecision Deny(string reason) => new(false, reason); }
public interface IAgentToolPolicy { ValueTask<AgentToolPolicyDecision> EvaluateAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default); }
public sealed class AllowAllAgentToolPolicy : IAgentToolPolicy { public static AllowAllAgentToolPolicy Instance { get; } = new(); public ValueTask<AgentToolPolicyDecision> EvaluateAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default) => ValueTask.FromResult(AgentToolPolicyDecision.Allow()); }
