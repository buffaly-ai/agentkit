# Events and tracing

Agent Kit emits events through `IAgentEventSink`. Events are intended for live traces, JSONL logs, tests, and simple inspectors.

## Current `AgentEvent` structure

The current implementation is:

```csharp
public sealed record AgentEvent(
    long Sequence,
    DateTimeOffset Timestamp,
    AgentEventKind Kind,
    string? Message = null,
    string? ToolName = null,
    string? ToolCallId = null)
{
    public int SchemaVersion { get; init; } = 1;
}
```

Fields:

- `SchemaVersion`: event schema version, currently `1`.
- `Sequence`: monotonic sequence within one `AgentKitRuntime` instance.
- `Timestamp`: UTC-ish runtime timestamp from `DateTimeOffset.UtcNow`.
- `Kind`: event kind.
- `Message`: human-readable event text.
- `ToolName`: set for tool events.
- `ToolCallId`: set for tool events when the provider supplied a call id.

Some future/spec documents mention richer fields such as `EventId`, `ConversationId`, `TurnId`, `Round`, `CreatedAt`, and `Data`. Those are not present in the current code.

## Event kinds

Current enum values:

- `TurnStarted`
- `RoundStarted`
- `ModelResponseReceived`
- `ToolCallStarted`
- `ToolCallCompleted`
- `ToolCallDenied`
- `ToolCallFailed`
- `TurnCompleted`

## Sink interface

```csharp
public interface IAgentEventSink
{
    ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default);
}
```

## Built-in sinks

Core package:

- `NullAgentEventSink`: ignores events.
- `InMemoryAgentEventSink`: stores events in a list for tests/UI.
- `CompositeAgentEventSink`: forwards each event to multiple sinks.

ASP.NET package:

- `FileAgentEventSink`: appends serialized `AgentEvent` records to a JSONL file.

Sample support:

- `ConsoleAgentEventSink`: compact console trace.
- `JsonlAgentEventSink`: sample-local JSONL writer without ASP.NET dependency.

## Typical ordering

A simple tool turn emits:

```text
TurnStarted
RoundStarted
ModelResponseReceived
ToolCallStarted
ToolCallCompleted
RoundStarted
ModelResponseReceived
TurnCompleted
```

If a tool is denied or fails, `ToolCallDenied` or `ToolCallFailed` replaces completion for that tool.

## Custom sink example

```csharp
public sealed class ConsoleJsonEventSink : IAgentEventSink
{
    public ValueTask EmitAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(agentEvent));
        return ValueTask.CompletedTask;
    }
}
```

## Replay JSONL events

```csharp
foreach (string line in File.ReadLines("events.jsonl"))
{
    AgentEvent? item = JsonSerializer.Deserialize<AgentEvent>(line);
    Console.WriteLine($"{item?.Sequence}: {item?.Kind} {item?.ToolName}");
}
```

## Repository examples

- `samples/DevOps.IncidentInvestigation/output/events.jsonl` after running the sample.
- `samples/Buffaly.AgentKit.SampleSupport/ConsoleAgentEventSink.cs`.
- `src/Buffaly.AgentKit.AspNetCore/FileAgentEventSink.cs`.
