using System.Text.Json;
using Buffaly.AgentKit.ProtoScript;
using Microsoft.Extensions.AI;
using Xunit;

namespace Buffaly.AgentKit.SampleTests;

public sealed class DomainProtoScriptRuleTests
{
    [Fact]
    public async Task ReferralRuleProducesDifferentOutputsForDifferentPackets()
    {
        string sample = Path.Combine(TestPaths.Root, "samples", "Medical.ReferralReadiness", "AgentTools", "agentkit.json");
        await using ProtoScriptToolSet tools = await ProtoScriptToolSet.LoadAsync(sample);
        AIFunction tool = Assert.Single(tools.Tools);

        JsonElement ready = await InvokeJsonAsync(tool, new() { ["serviceLine"] = "Orthopedic consultation", ["hasSignedOrder"] = true, ["hasInsuranceAuthorization"] = true, ["hasClinicalSummary"] = true, ["hasRelevantImaging"] = true });
        JsonElement authorization = await InvokeJsonAsync(tool, new() { ["serviceLine"] = "Orthopedic consultation", ["hasSignedOrder"] = true, ["hasInsuranceAuthorization"] = false, ["hasClinicalSummary"] = true, ["hasRelevantImaging"] = true });
        JsonElement orderAndImaging = await InvokeJsonAsync(tool, new() { ["serviceLine"] = "Orthopedic consultation", ["hasSignedOrder"] = false, ["hasInsuranceAuthorization"] = true, ["hasClinicalSummary"] = true, ["hasRelevantImaging"] = false });
        JsonElement summary = await InvokeJsonAsync(tool, new() { ["serviceLine"] = "Orthopedic consultation", ["hasSignedOrder"] = true, ["hasInsuranceAuthorization"] = true, ["hasClinicalSummary"] = false, ["hasRelevantImaging"] = true });

        Assert.Equal("ready", ready.GetProperty("status").GetString());
        Assert.Empty(ready.GetProperty("missingItems").EnumerateArray());
        Assert.Equal(["insurance authorization"], Items(authorization));
        Assert.Equal(["signed referral order", "relevant imaging report"], Items(orderAndImaging));
        Assert.Equal(["clinical summary"], Items(summary));
    }

    [Fact]
    public async Task ReturnEligibilityChangesWithPolicyInputs()
    {
        string manifest = Path.Combine(TestPaths.Root, "samples", "Commerce.ReturnResolution", "AgentTools", "agentkit.json");
        await using ProtoScriptToolSet tools = await ProtoScriptToolSet.LoadAsync(manifest);
        AIFunction tool = tools.Tools.Single(candidate => candidate.Name == "evaluate_return_eligibility");

        JsonElement damaged = await InvokeJsonAsync(tool, new() { ["daysSinceDelivery"] = 6, ["reason"] = "damaged", ["itemCondition"] = "damaged", ["isFinalSale"] = false });
        JsonElement finalSale = await InvokeJsonAsync(tool, new() { ["daysSinceDelivery"] = 4, ["reason"] = "changed_mind", ["itemCondition"] = "opened", ["isFinalSale"] = true });
        JsonElement outsideWindow = await InvokeJsonAsync(tool, new() { ["daysSinceDelivery"] = 33, ["reason"] = "late_delivery", ["itemCondition"] = "partial", ["isFinalSale"] = false });
        JsonElement standard = await InvokeJsonAsync(tool, new() { ["daysSinceDelivery"] = 12, ["reason"] = "changed_mind", ["itemCondition"] = "unopened", ["isFinalSale"] = false });

        Assert.Equal("eligible", damaged.GetProperty("status").GetString());
        Assert.True(damaged.GetProperty("refundShipping").GetBoolean());
        Assert.Equal("ineligible", finalSale.GetProperty("status").GetString());
        Assert.Equal("manual_review", outsideWindow.GetProperty("status").GetString());
        Assert.Equal(10, standard.GetProperty("restockingPercent").GetInt32());
    }

    [Fact]
    public async Task RefundCalculationUsesAllInputs()
    {
        string manifest = Path.Combine(TestPaths.Root, "samples", "Commerce.ReturnResolution", "AgentTools", "agentkit.json");
        await using ProtoScriptToolSet tools = await ProtoScriptToolSet.LoadAsync(manifest);
        AIFunction tool = tools.Tools.Single(candidate => candidate.Name == "calculate_refund_amount");

        int noShipping = Convert.ToInt32(await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["merchandiseAmountCents"] = 10000, ["shippingAmountCents"] = 700, ["restockingPercent"] = 10, ["refundShipping"] = false })));
        int withShipping = Convert.ToInt32(await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["merchandiseAmountCents"] = 10000, ["shippingAmountCents"] = 700, ["restockingPercent"] = 10, ["refundShipping"] = true })));

        Assert.Equal(9000, noShipping);
        Assert.Equal(9700, withShipping);
    }

    [Fact]
    public async Task IncidentClassificationChangesWithMetrics()
    {
        string manifest = Path.Combine(TestPaths.Root, "samples", "DevOps.IncidentInvestigation", "AgentTools", "agentkit.json");
        await using ProtoScriptToolSet tools = await ProtoScriptToolSet.LoadAsync(manifest);
        AIFunction tool = Assert.Single(tools.Tools);

        JsonElement normal = await ClassifyAsync(tool, 2, 240, 52, 300);
        JsonElement elevated = await ClassifyAsync(tool, 14, 820, 78, 300);
        JsonElement high = await ClassifyAsync(tool, 48, 1850, 80, 300);
        JsonElement saturation = await ClassifyAsync(tool, 20, 900, 94, 300);
        JsonElement correlated = await ClassifyAsync(tool, 48, 1850, 94, 12);

        Assert.Equal("normal", normal.GetProperty("classification").GetString());
        Assert.Equal("elevated", elevated.GetProperty("classification").GetString());
        Assert.Equal("high", high.GetProperty("classification").GetString());
        Assert.Equal("database_pool_saturation", saturation.GetProperty("classification").GetString());
        Assert.Contains(correlated.GetProperty("signals").EnumerateArray(), signal => signal.GetString() == "recent_deployment_correlation");
    }

    private static Task<JsonElement> ClassifyAsync(AIFunction tool, int errorRate, int latency, int pool, int deploymentMinutes) => InvokeJsonAsync(tool, new()
    {
        ["errorRateTenthsPercent"] = errorRate,
        ["p95LatencyMs"] = latency,
        ["databasePoolUtilizationPercent"] = pool,
        ["minutesSinceDeployment"] = deploymentMinutes
    });

    private static async Task<JsonElement> InvokeJsonAsync(AIFunction tool, Dictionary<string, object?> arguments)
    {
        object? result = await tool.InvokeAsync(new AIFunctionArguments(arguments));
        using JsonDocument document = JsonDocument.Parse(result?.ToString() ?? throw new InvalidOperationException("Tool returned null."));
        return document.RootElement.Clone();
    }

    private static string[] Items(JsonElement result) => result.GetProperty("missingItems").EnumerateArray().Select(item => item.GetString()!).ToArray();
}
