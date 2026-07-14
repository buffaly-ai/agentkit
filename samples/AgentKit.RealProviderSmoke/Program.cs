using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Microsoft.Extensions.AI;
using OpenAI;

if (Environment.GetEnvironmentVariable("AGENTKIT_REAL_PROVIDER") != "1")
{
    Console.WriteLine("SKIPPED: set AGENTKIT_REAL_PROVIDER=1 to enable the opt-in network smoke test.");
    return;
}

string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("SKIPPED: OPENAI_API_KEY is not set.");
    return;
}

string model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
IChatClient chatClient = new OpenAIClient(apiKey).GetChatClient(model).AsIChatClient();
string manifestPath = Path.Combine(AppContext.BaseDirectory, "Tools", "agentkit.json");

await using ProtoScriptToolSet tools = await ProtoScriptToolSet.LoadAsync(manifestPath);
var events = new InMemoryAgentEventSink();
var runtime = new AgentKitRuntime(chatClient, tools.Tools, eventSink: events);
AgentTurnResult result = await runtime.RunTurnAsync(
    AgentConversation.Create(),
    "You must call add_numbers with a=17 and b=25. Then answer exactly: The result is <observed tool result>.");

AgentEvent completed = events.Events.SingleOrDefault(agentEvent =>
    agentEvent.Kind == AgentEventKind.ToolCallCompleted &&
    agentEvent.ToolName == "add_numbers" &&
    agentEvent.ToolSource == "ProtoScript")
    ?? throw new InvalidOperationException("No successful ProtoScript add_numbers tool call occurred.");

string observedResult = completed.Data["result"]?.GetValue<string>()
    ?? throw new InvalidOperationException("The ProtoScript tool event did not contain a result.");

if (observedResult != "42")
    throw new InvalidOperationException($"Expected the observed ProtoScript result 42, but received {observedResult}.");

if (result.FinalAnswer is null || !result.FinalAnswer.Contains(observedResult, StringComparison.Ordinal))
    throw new InvalidOperationException("The final answer did not use the observed ProtoScript result.");

Console.WriteLine(result.FinalAnswer);
Console.WriteLine($"REAL_PROVIDER_SMOKE_OK:{observedResult}");
