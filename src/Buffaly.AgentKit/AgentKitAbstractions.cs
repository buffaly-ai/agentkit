using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
namespace Buffaly.AgentKit;
public sealed class AgentKitOptions { public int MaxRounds{get;set;}=8; public int MaxToolCallsPerRound{get;set;}=8; public TimeSpan ToolTimeout{get;set;}=TimeSpan.FromMinutes(2); public int MaxToolResultCharacters{get;set;}=100_000; }
public enum AgentMessageRole { System,User,Assistant,Tool }
public enum AgentStopReason { FinalAnswer,MaxRounds,ToolCallLimit,Cancelled }
public sealed record AgentTurnResult(AgentStopReason StopReason,string? FinalAnswer,int Rounds,IReadOnlyList<AgentMessage> Messages);
public enum AgentEventKind { TurnStarted,UserMessageAdded,RoundStarted,ModelRequestStarted,ModelResponseReceived,AssistantFunctionCallAdded,ToolCallStarted,ToolCallCompleted,ToolCallDenied,ToolCallFailed,AssistantMessageAdded,TurnCompleted,TurnFailed,TurnLimitReached,ToolsLoading,ToolsLoaded,ToolsLoadFailed }
public sealed class AgentEvent
{
 public int SchemaVersion{get;init;}=1; public string EventId{get;init;}=string.Empty; public long Sequence{get;init;} public string ConversationId{get;init;}=string.Empty; public string TurnId{get;init;}=string.Empty; public int Round{get;init;} public DateTimeOffset CreatedAt{get;init;} public AgentEventKind Kind{get;init;} public JsonObject Data{get;init;}=new();
 [JsonIgnore] public DateTimeOffset Timestamp=>CreatedAt; [JsonIgnore] public string? Message=>Data["message"]?.GetValue<string>(); [JsonIgnore] public string? ToolName=>Data["toolName"]?.GetValue<string>(); [JsonIgnore] public string? ToolCallId=>Data["callId"]?.GetValue<string>(); [JsonIgnore] public string? ToolSource=>Data["toolSource"]?.GetValue<string>();
}
public interface IAgentEventSink { ValueTask EmitAsync(AgentEvent agentEvent,CancellationToken cancellationToken=default); }
public sealed class NullAgentEventSink:IAgentEventSink { public static NullAgentEventSink Instance{get;}=new(); public ValueTask EmitAsync(AgentEvent e,CancellationToken ct=default)=>ValueTask.CompletedTask; }
public sealed class InMemoryAgentEventSink:IAgentEventSink { readonly List<AgentEvent> _events=[]; public IReadOnlyList<AgentEvent> Events=>_events; public ValueTask EmitAsync(AgentEvent e,CancellationToken ct=default){_events.Add(e);return ValueTask.CompletedTask;} }
public sealed class CompositeAgentEventSink(IEnumerable<IAgentEventSink> sinks):IAgentEventSink { readonly IReadOnlyList<IAgentEventSink> _sinks=sinks.ToArray(); public async ValueTask EmitAsync(AgentEvent e,CancellationToken ct=default){foreach(var sink in _sinks)await sink.EmitAsync(e,ct);} }
public sealed record AgentToolPolicyDecision(bool Allowed,string? Reason=null){public static AgentToolPolicyDecision Allow()=>new(true);public static AgentToolPolicyDecision Deny(string reason)=>new(false,reason);}
public interface IAgentToolPolicy { ValueTask<AgentToolPolicyDecision> EvaluateAsync(string toolName,IReadOnlyDictionary<string,object?> arguments,CancellationToken cancellationToken=default); }
public sealed class AllowAllAgentToolPolicy:IAgentToolPolicy { public static AllowAllAgentToolPolicy Instance{get;}=new();public ValueTask<AgentToolPolicyDecision> EvaluateAsync(string n,IReadOnlyDictionary<string,object?> a,CancellationToken ct=default)=>ValueTask.FromResult(AgentToolPolicyDecision.Allow()); }
