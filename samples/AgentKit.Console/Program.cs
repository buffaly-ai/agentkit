using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;

string manifest = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tools", "agentkit.json"));
await using ProtoScriptToolSet tools = await ProtoScriptToolSet.LoadAsync(manifest);
var events = new InMemoryAgentEventSink();
var chat = new StrictArithmeticChatClient(17, 25);
var runtime = new AgentKitRuntime(chat, tools.Tools, eventSink: events);
AgentTurnResult result = await runtime.RunTurnAsync(AgentConversation.Create(), "Add 17 and 25.");
Console.WriteLine(result.FinalAnswer);
foreach (AgentEvent agentEvent in events.Events)
    Console.WriteLine($"{agentEvent.Sequence}: {agentEvent.Kind} {agentEvent.ToolName} {agentEvent.Message}".Trim());
