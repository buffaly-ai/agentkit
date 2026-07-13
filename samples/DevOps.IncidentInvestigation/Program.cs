using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using DevOps.IncidentInvestigation.Tools;
using DevOps.IncidentInvestigation.Scenarios;
using Microsoft.Extensions.AI;

string sampleRoot = AppContext.BaseDirectory;
string dataRoot = Path.Combine(sampleRoot, "Data");
string outputRoot = Path.GetFullPath(Path.Combine(sampleRoot, "..", "..", "..", "..", "DevOps.IncidentInvestigation", "output"));
Directory.CreateDirectory(outputRoot);
foreach (string file in Directory.EnumerateFiles(outputRoot)) File.Delete(file);
await using ProtoScriptToolSet protoScriptTools = await ProtoScriptToolSet.LoadAsync(Path.Combine(sampleRoot, "AgentTools", "agentkit.json"));
IReadOnlyList<AIFunction> tools = IncidentFunctions.Create(dataRoot).Concat(protoScriptTools.Tools).ToArray();
var events = new CompositeAgentEventSink(new IAgentEventSink[] { new ConsoleAgentEventSink(), new JsonlAgentEventSink(Path.Combine(outputRoot, "events.jsonl")) });
var runtime = new AgentKitRuntime(SampleChatClientFactory.Create(IncidentScenarioFactory.Create()), tools, eventSink: events);
AgentConversation conversation = AgentConversation.Create();
conversation.AddSystemMessage("You are an incident investigation assistant. Use evidence, do not perform remediation, and describe uncertainty clearly.");
AgentTurnResult result = await runtime.RunTurnAsync(conversation, "Investigate the checkout-api latency increase beginning at 14:05. Summarize the evidence, identify plausible contributing factors, and recommend the relevant runbook. Do not perform remediation.");
await File.WriteAllTextAsync(Path.Combine(outputRoot, "incident-report.md"), result.FinalAnswer ?? string.Empty);
await File.WriteAllTextAsync(Path.Combine(outputRoot, "conversation.json"), conversation.ExportState());
Console.WriteLine(); Console.WriteLine("Wrote:"); Console.WriteLine(Path.Combine(outputRoot, "incident-report.md")); Console.WriteLine(Path.Combine(outputRoot, "events.jsonl")); Console.WriteLine(Path.Combine(outputRoot, "conversation.json"));

