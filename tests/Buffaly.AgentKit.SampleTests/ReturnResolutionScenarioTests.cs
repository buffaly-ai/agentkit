using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using Commerce.ReturnResolution.Repositories;
using Commerce.ReturnResolution.Tools;
using Commerce.ReturnResolution.Scenarios;
using Microsoft.Extensions.AI;
using Xunit;

namespace Buffaly.AgentKit.SampleTests;

public class ReturnResolutionScenarioTests
{
    [Fact]
    public async Task Ord1042CreatesPendingProposalWithoutApprovingRefund()
    {
        string sample = Path.Combine(TestPaths.Root, "samples", "Commerce.ReturnResolution");
        string storage = Path.Combine(Path.GetTempPath(), "agentkit-return-test-" + Guid.NewGuid().ToString("n"));
        var orders = new JsonOrderRepository(Path.Combine(sample, "Data"));
        var proposals = new JsonRefundProposalStore(storage);
        string before = File.ReadAllText(Path.Combine(sample, "Data", "orders.json"));
        await using ProtoScriptToolSet proto = await ProtoScriptToolSet.LoadAsync(Path.Combine(sample, "AgentTools", "agentkit.json"));
        AIFunction[] tools = ReturnFunctions.Create(orders, proposals).Concat(proto.Tools).ToArray();
        Assert.DoesNotContain(tools, t => t.Name.Contains("approve", StringComparison.OrdinalIgnoreCase) || t.Name.Contains("issue", StringComparison.OrdinalIgnoreCase));
        var events = new InMemoryAgentEventSink();
        var runtime = new AgentKitRuntime(SampleChatClientFactory.Create(ReturnScenarioFactory.Create()), tools, eventSink: events);
        AgentTurnResult result = await runtime.RunTurnAsync(AgentConversation.Create(), "Review ORD-1042");
        IReadOnlyList<Commerce.ReturnResolution.Domain.RefundProposal> written = await proposals.ListAsync(default);
        Assert.Contains(events.Events, e => e.ToolName == "evaluate_return_eligibility" && e.Kind == AgentEventKind.ToolCallCompleted);
        Assert.Contains(events.Events, e => e.ToolName == "calculate_refund_amount" && e.Kind == AgentEventKind.ToolCallCompleted);
        Assert.Single(written);
        Assert.Equal("pending_human_approval", written[0].Status);
        Assert.Equal(before, File.ReadAllText(Path.Combine(sample, "Data", "orders.json")));
        Assert.DoesNotContain("refund has been issued", result.FinalAnswer!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("approval", result.FinalAnswer!, StringComparison.OrdinalIgnoreCase);
    }
}
