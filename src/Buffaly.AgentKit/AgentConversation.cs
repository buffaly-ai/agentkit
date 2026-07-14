using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
namespace Buffaly.AgentKit;
public sealed class AgentConversation
{
 private readonly SemaphoreSlim _turnLock=new(1,1); private readonly List<AgentMessage> _messages=[]; private AgentConversation(string id)=>Id=id;
 public string Id{get;} public IReadOnlyList<AgentMessage> Messages=>_messages;
 public static AgentConversation Create(string? id=null)=>new(id??Guid.NewGuid().ToString("n"));
 public void AddSystemMessage(string content)=>Add(new(AgentMessageRole.System,content));
 public string ExportState()=>JsonSerializer.Serialize(new AgentConversationState(Id,_messages));
 public static AgentConversation ImportState(string json){var s=JsonSerializer.Deserialize<AgentConversationState>(json)??throw new InvalidOperationException("Invalid conversation state.");var c=new AgentConversation(s.Id);c._messages.AddRange(s.Messages??[]);return c;}
 internal async ValueTask<IDisposable> EnterTurnAsync(CancellationToken ct){await _turnLock.WaitAsync(ct);return new Releaser(_turnLock);} internal void Add(AgentMessage m)=>_messages.Add(m);
 internal IEnumerable<ChatMessage> ToChatMessages(){foreach(var m in _messages){var contents=new List<AIContent>();foreach(var c in m.Contents)switch(c){case AgentTextContent t:contents.Add(new TextContent(t.Text));break;case AgentFunctionCallContent f:contents.Add(new FunctionCallContent(f.CallId,f.Name,f.Arguments.ToDictionary(x=>x.Key,x=>(object?)FromNode(x.Value))));break;case AgentFunctionResultContent r:contents.Add(new FunctionResultContent(r.CallId,r.Result));break;}yield return new ChatMessage(Role(m.Role),contents);}}
 private static object? FromNode(JsonNode? n)=>n is null?null:JsonSerializer.Deserialize<object>(n.ToJsonString());
 private static ChatRole Role(AgentMessageRole r)=>r switch{AgentMessageRole.System=>ChatRole.System,AgentMessageRole.Assistant=>ChatRole.Assistant,AgentMessageRole.Tool=>ChatRole.Tool,_=>ChatRole.User};
 private sealed class Releaser(SemaphoreSlim s):IDisposable{public void Dispose()=>s.Release();}
}
public sealed record AgentConversationState(string Id,List<AgentMessage>? Messages);
