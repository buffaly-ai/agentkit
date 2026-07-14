using System.Text.Json;using System.Text.Json.Nodes;using Microsoft.Extensions.AI;
namespace Buffaly.AgentKit;
public sealed class AgentKitRuntime
{
 readonly IChatClient _chatClient;readonly IReadOnlyDictionary<string,AIFunction> _tools;readonly AgentKitOptions _options;readonly IAgentEventSink _events;readonly IAgentToolPolicy _policy;long _sequence;
 public AgentKitRuntime(IChatClient c,IEnumerable<AIFunction>? tools=null,AgentKitOptions? options=null,IAgentEventSink? eventSink=null,IAgentToolPolicy? toolPolicy=null){_chatClient=c;_tools=(tools??[]).ToDictionary(t=>t.Name);_options=options??new();_events=eventSink??NullAgentEventSink.Instance;_policy=toolPolicy??AllowAllAgentToolPolicy.Instance;}
 public async Task<AgentTurnResult> RunTurnAsync(AgentConversation conv,string input,CancellationToken ct=default){using var turn=await conv.EnterTurnAsync(ct);conv.Add(new(AgentMessageRole.User,input));await Emit(AgentEventKind.TurnStarted,"Turn started",ct:ct);for(int round=1;round<=_options.MaxRounds;round++){await Emit(AgentEventKind.RoundStarted,$"Round {round} started",ct:ct);var response=await _chatClient.GetResponseAsync(conv.ToChatMessages(),new ChatOptions{Tools=_tools.Values.Cast<AITool>().ToList(),AllowMultipleToolCalls=true},ct);await Emit(AgentEventKind.ModelResponseReceived,"Model response received",ct:ct);var assistant=new List<AgentMessageContent>();foreach(var m in response.Messages)foreach(var c in m.Contents)switch(c){case TextContent t:assistant.Add(new AgentTextContent(t.Text));break;case FunctionCallContent f:assistant.Add(new AgentFunctionCallContent(f.CallId,f.Name,JsonSerializer.SerializeToNode(f.Arguments)!.AsObject()));break;}if(assistant.Count>0)conv.Add(new(AgentMessageRole.Assistant,assistant));var calls=response.Messages.SelectMany(m=>m.Contents).OfType<FunctionCallContent>().ToList();if(calls.Count>_options.MaxToolCallsPerRound){await Emit(AgentEventKind.TurnLimitReached,$"Tool call limit exceeded: {calls.Count} > {_options.MaxToolCallsPerRound}.",ct:ct);return new(AgentStopReason.ToolCallLimit,null,round,conv.Messages);}if(calls.Count==0){string final=string.Concat(assistant.OfType<AgentTextContent>().Select(x=>x.Text));await Emit(AgentEventKind.TurnCompleted,"Turn completed",ct:ct);return new(AgentStopReason.FinalAnswer,final,round,conv.Messages);}foreach(var call in calls){var r=await Invoke(call,ct);conv.Add(new(AgentMessageRole.Tool,new AgentMessageContent[]{r}));}}await Emit(AgentEventKind.TurnLimitReached,"Maximum rounds reached",ct:ct);return new(AgentStopReason.MaxRounds,null,_options.MaxRounds,conv.Messages);}
 async Task<AgentFunctionResultContent> Invoke(FunctionCallContent call,CancellationToken ct)
 {
  if(!_tools.TryGetValue(call.Name,out var tool))
  {
   await Emit(AgentEventKind.ToolCallStarted,"Tool call started",call.Name,call.CallId,ct);
   var unknown=$"Unknown tool: {call.Name}";
   await Emit(AgentEventKind.ToolCallFailed,unknown,call.Name,call.CallId,ct);
   return new(call.CallId,call.Name,unknown,true);
  }
  string? source=tool.AdditionalProperties.TryGetValue("buffaly.toolSource",out object? sourceValue)?sourceValue?.ToString():"CSharp";
  await Emit(AgentEventKind.ToolCallStarted,"Tool call started",call.Name,call.CallId,ct,source);
  var args=(call.Arguments??new Dictionary<string,object?>()).ToDictionary(x=>x.Key,x=>(object?)x.Value);
  var decision=await _policy.EvaluateAsync(call.Name,args,ct);
  if(!decision.Allowed)
  {
   var denied="Tool denied: "+decision.Reason;
   await Emit(AgentEventKind.ToolCallDenied,denied,call.Name,call.CallId,ct,source);
   return new(call.CallId,call.Name,denied,true);
  }
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
  timeout.CancelAfter(_options.ToolTimeout);
  try
  {
   var value=await tool.InvokeAsync(new AIFunctionArguments(args!),timeout.Token);
   var result=value is string text?text:JsonSerializer.Serialize(value);
   if(result.Length>_options.MaxToolResultCharacters)result=result[.._options.MaxToolResultCharacters];
   await Emit(AgentEventKind.ToolCallCompleted,"Tool call completed",call.Name,call.CallId,ct,source);
   return new(call.CallId,call.Name,result,false);
  }
  catch(OperationCanceledException) when(!ct.IsCancellationRequested)
  {
   var error=$"Tool timed out after {_options.ToolTimeout}.";
   await Emit(AgentEventKind.ToolCallFailed,error,call.Name,call.CallId,ct,source);
   return new(call.CallId,call.Name,error,true);
  }
  catch(Exception ex)
  {
   var error=$"Tool error: {ex.Message}";
   await Emit(AgentEventKind.ToolCallFailed,error,call.Name,call.CallId,ct,source);
   return new(call.CallId,call.Name,error,true);
  }
 } ValueTask Emit(AgentEventKind k,string? m=null,string? n=null,string? id=null,CancellationToken ct=default,string? source=null)=>_events.EmitAsync(new(Interlocked.Increment(ref _sequence),DateTimeOffset.UtcNow,k,m,n,id,source),ct);
}


