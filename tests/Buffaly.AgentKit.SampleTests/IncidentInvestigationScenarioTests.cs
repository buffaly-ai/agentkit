using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using DevOps.IncidentInvestigation.Tools;
using DevOps.IncidentInvestigation.Scenarios;
using Microsoft.Extensions.AI;
using Xunit;

namespace Buffaly.AgentKit.SampleTests;

public class IncidentInvestigationScenarioTests
{
    [Fact]
    public async Task CheckoutIncidentUsesEvidenceAndNoRemediationTool()
    {
        string sample = Path.Combine(TestPaths.Root, "samples", "DevOps.IncidentInvestigation");
        string csproj = File.ReadAllText(Path.Combine(sample, "DevOps.IncidentInvestigation.csproj"));
        Assert.DoesNotContain("AspNetCore", csproj, StringComparison.OrdinalIgnoreCase);
        await using ProtoScriptToolSet proto = await ProtoScriptToolSet.LoadAsync(Path.Combine(sample, "AgentTools", "agentkit.json"));
        AIFunction[] tools = IncidentFunctions.Create(Path.Combine(sample, "Data")).Concat(proto.Tools).ToArray();
        Assert.DoesNotContain(tools, t => t.Name.Contains("remed", StringComparison.OrdinalIgnoreCase) || t.Name.Contains("rollback", StringComparison.OrdinalIgnoreCase));
        var events = new InMemoryAgentEventSink();
        var runtime = new AgentKitRuntime(SampleChatClientFactory.Create(IncidentScenarioFactory.Create()), tools, eventSink: events);
        AgentTurnResult result = await runtime.RunTurnAsync(AgentConversation.Create(), "Investigate checkout-api");
        Assert.Contains(events.Events, e => e.ToolName == "classify_incident_evidence" && e.Kind == AgentEventKind.ToolCallCompleted);
        Assert.Contains("1850", result.FinalAnswer);
        Assert.Contains("timeout", result.FinalAnswer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026.01.15.1400", result.FinalAnswer);
        Assert.Contains("not a proven root cause", result.FinalAnswer, StringComparison.OrdinalIgnoreCase);
        Assert.True(events.Events.First().Kind == AgentEventKind.TurnStarted && events.Events.Last().Kind == AgentEventKind.TurnCompleted);
    }
}
