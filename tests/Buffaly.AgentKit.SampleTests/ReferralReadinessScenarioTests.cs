using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using Medical.ReferralReadiness.Repositories;
using Medical.ReferralReadiness.Tools;
using Medical.ReferralReadiness.Scenarios;
using Microsoft.Extensions.AI;
using Xunit;

namespace Buffaly.AgentKit.SampleTests;

public class ReferralReadinessScenarioTests
{
    [Fact]
    public async Task Ref1003ProducesAdministrativeMissingItemsOnly()
    {
        string root = TestPaths.Root;
        string sample = Path.Combine(root, "samples", "Medical.ReferralReadiness");
        var repo = new JsonReferralRepository(Path.Combine(sample, "Data"));
        await using ProtoScriptToolSet proto = await ProtoScriptToolSet.LoadAsync(Path.Combine(sample, "AgentTools", "agentkit.json"));
        AIFunction[] tools = ReferralFunctions.Create(repo).Concat(proto.Tools).ToArray();
        var events = new InMemoryAgentEventSink();
        var runtime = new AgentKitRuntime(SampleChatClientFactory.Create(ReferralScenarioFactory.Create()), tools, eventSink: events);
        AgentTurnResult result = await runtime.RunTurnAsync(AgentConversation.Create(), "Review REF-1003");
        string allToolResults = string.Join("\n", result.Messages.Where(m => m.Role == AgentMessageRole.Tool).Select(m => m.Content));
        Assert.Equal(3, events.Events.Count(e => e.Kind == AgentEventKind.ToolCallStarted));
        Assert.Contains(events.Events, e => e.ToolName == "assess_referral_readiness" && e.Kind == AgentEventKind.ToolCallCompleted);
        Assert.Contains("signed referral order", allToolResults);
        Assert.Contains("relevant imaging report", allToolResults);
        Assert.Contains("needs_information", result.FinalAnswer!);
        Assert.Contains("signed referral order and relevant imaging report", result.FinalAnswer!);
        Assert.DoesNotContain("diagnosis is", result.FinalAnswer!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recommend treatment", result.FinalAnswer!, StringComparison.OrdinalIgnoreCase);
        Assert.True(events.Events.First().Kind == AgentEventKind.TurnStarted && events.Events.Last().Kind == AgentEventKind.TurnCompleted);
        Assert.False(Directory.Exists(Path.Combine(sample, "outside-storage-marker")));
    }
}


