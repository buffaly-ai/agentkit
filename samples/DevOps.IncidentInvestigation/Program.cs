using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using DevOps.IncidentInvestigation.Tools;
using Microsoft.Extensions.AI;

string sampleRoot = AppContext.BaseDirectory;
string dataRoot = Path.Combine(sampleRoot, "Data");
string outputRoot = Path.GetFullPath(Path.Combine(sampleRoot, "..", "..", "..", "..", "DevOps.IncidentInvestigation", "output"));
Directory.CreateDirectory(outputRoot);
foreach (string file in Directory.EnumerateFiles(outputRoot)) File.Delete(file);
await using ProtoScriptToolSet protoScriptTools = await ProtoScriptToolSet.LoadAsync(Path.Combine(sampleRoot, "AgentTools", "agentkit.json"));
IReadOnlyList<AIFunction> tools = IncidentFunctions.Create(dataRoot).Concat(protoScriptTools.Tools).ToArray();
var events = new CompositeAgentEventSink(new IAgentEventSink[] { new ConsoleAgentEventSink(), new JsonlAgentEventSink(Path.Combine(outputRoot, "events.jsonl")) });
var runtime = new AgentKitRuntime(SampleChatClientFactory.Create(IncidentScenario.Create()), tools, eventSink: events);
AgentConversation conversation = AgentConversation.Create();
conversation.AddSystemMessage("You are an incident investigation assistant. Use evidence, do not perform remediation, and describe uncertainty clearly.");
AgentTurnResult result = await runtime.RunTurnAsync(conversation, "Investigate the checkout-api latency increase beginning at 14:05. Summarize the evidence, identify plausible contributing factors, and recommend the relevant runbook. Do not perform remediation.");
await File.WriteAllTextAsync(Path.Combine(outputRoot, "incident-report.md"), result.FinalAnswer ?? string.Empty);
await File.WriteAllTextAsync(Path.Combine(outputRoot, "conversation.json"), conversation.ExportState());
Console.WriteLine(); Console.WriteLine("Wrote:"); Console.WriteLine(Path.Combine(outputRoot, "incident-report.md")); Console.WriteLine(Path.Combine(outputRoot, "events.jsonl")); Console.WriteLine(Path.Combine(outputRoot, "conversation.json"));

public static class IncidentScenario
{
    public static ScenarioDefinition Create() => new()
    {
        ScenarioId = "incident-checkout-api",
        Responses = new[]
        {
            ScriptedChatResponse.Call("get_service_snapshot", new() { ["service_name"] = "checkout-api" }),
            ScriptedChatResponse.Call("get_metric_window", new() { ["service_name"] = "checkout-api", ["metric_name"] = "latency_and_pool", ["from_utc"] = "2026-01-15T14:00:00Z", ["to_utc"] = "2026-01-15T14:20:00Z" }, expectedLastToolName: "get_service_snapshot", expectedLastToolResultContains: "checkout-api"),
            ScriptedChatResponse.Call("get_recent_deployments", new() { ["service_name"] = "checkout-api" }, expectedLastToolName: "get_metric_window", expectedLastToolResultContains: "databasePoolUtilizationPercent"),
            ScriptedChatResponse.Call("read_log_excerpt", new() { ["service_name"] = "checkout-api", ["from_utc"] = "2026-01-15T14:05:00Z", ["to_utc"] = "2026-01-15T14:20:00Z", ["maximum_lines"] = 6 }, expectedLastToolName: "get_recent_deployments", expectedLastToolResultContains: "2026.01.15.1400"),
            ScriptedChatResponse.Call("classify_incident_evidence", new() { ["errorRateTenthsPercent"] = 48, ["p95LatencyMs"] = 1850, ["databasePoolUtilizationPercent"] = 94, ["minutesSinceDeployment"] = 7 }, expectedLastToolName: "read_log_excerpt", expectedLastToolResultContains: "timeout"),
            ScriptedChatResponse.Call("search_runbooks", new() { ["query"] = "database pool saturation" }, expectedLastToolName: "classify_incident_evidence", expectedLastToolResultContains: "database_pool_saturation"),
            ScriptedChatResponse.Final("# Incident report: checkout-api latency increase\n\nThe most plausible explanation supported by the synthetic fixtures is database connection-pool saturation correlated with a recent checkout-api deployment; this is not a proven root cause. Evidence: metrics show p95 latency rising to 1850 ms while databasePoolUtilizationPercent reached 94; logs show checkout-api timeout messages after 14:05; deployment history shows checkout-api build 2026.01.15.1400 shortly before the incident; payment-api is a control with no matching degradation. The ProtoScript classifier marked severity high with database_pool_saturation and recent_deployment_correlation signals. Recommended runbook: database-pool-saturation.md. Do not perform remediation from this sample; have an operator review the evidence and choose any action.", expectedLastToolName: "search_runbooks", expectedLastToolResultContains: "database-pool-saturation")
        }
    };
}


