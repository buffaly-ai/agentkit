using Buffaly.AgentKit;
using Buffaly.AgentKit.AspNetCore;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using Commerce.ReturnResolution.Repositories;
using Commerce.ReturnResolution.Tools;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
string contentRoot = builder.Environment.ContentRootPath;
string dataRoot = Path.Combine(contentRoot, "Data");
string storageRoot = Path.Combine(contentRoot, ".agentkit");
Directory.CreateDirectory(storageRoot);
builder.Services.AddSingleton<IOrderRepository>(new JsonOrderRepository(dataRoot));
builder.Services.AddSingleton<IRefundProposalStore>(new JsonRefundProposalStore(storageRoot));
builder.Services.AddSingleton<IChatClient>(SampleChatClientFactory.Create(ReturnScenario.Create()));
builder.Services.AddSingleton<IReadOnlyList<AIFunction>>(sp =>
{
    var orders = sp.GetRequiredService<IOrderRepository>(); var proposals = sp.GetRequiredService<IRefundProposalStore>();
    ProtoScriptToolSet proto = ProtoScriptToolSet.LoadAsync(Path.Combine(contentRoot, "AgentTools", "agentkit.json")).GetAwaiter().GetResult();
    return ReturnFunctions.Create(orders, proposals).Concat(proto.Tools).ToArray();
});
builder.Services.AddBuffalyAgentKit(b => b.UseJsonlStore(storageRoot));
var app = builder.Build();
app.UseDefaultFiles(); app.UseStaticFiles(); app.MapBuffalyAgentKit("/_agentkit");
app.MapGet("/orders", async (IOrderRepository repo, CancellationToken ct) => Results.Json(await repo.ListOrdersAsync(ct)));
app.MapGet("/api/refund-proposals", async (IRefundProposalStore store, CancellationToken ct) => Results.Json(await store.ListAsync(ct)));
app.MapPost("/api/refund-proposals/{proposalId}/approve", async (string proposalId, IRefundProposalStore store, CancellationToken ct) => Results.Json(await store.ApproveAsync(proposalId, ct)));
app.MapGet("/", () => Results.Redirect("/orders.html"));
app.Run();

public static class ReturnScenario
{
    public static ScenarioDefinition Create() => new()
    {
        ScenarioId = "return-ord-1042",
        Responses = new[]
        {
            ScriptedChatResponse.Call("get_order_facts", new() { ["order_id"] = "ORD-1042" }),
            ScriptedChatResponse.Call("get_return_policy", new() { ["policy_id"] = "STANDARD-30" }, expectedLastToolName: "get_order_facts", expectedLastToolResultContains: "ORD-1042"),
            ScriptedChatResponse.Call("evaluate_return_eligibility", new() { ["daysSinceDelivery"] = 6, ["reason"] = "damaged", ["itemCondition"] = "damaged", ["isFinalSale"] = false }, expectedLastToolName: "get_return_policy", expectedLastToolResultContains: "STANDARD-30"),
            ScriptedChatResponse.Call("calculate_refund_amount", new() { ["merchandiseAmountCents"] = 8495, ["shippingAmountCents"] = 795, ["restockingPercent"] = 0, ["refundShipping"] = false }, expectedLastToolName: "evaluate_return_eligibility", expectedLastToolResultContains: "damaged_item_within_window"),
            ScriptedChatResponse.Call("create_refund_proposal", new() { ["order_id"] = "ORD-1042", ["amount"] = 84.95m, ["reason"] = "Damaged item within standard return window", ["evidence"] = "Eligibility rule damaged_item_within_window; calculated merchandise refund 84.95" }, expectedLastToolName: "calculate_refund_amount", expectedLastToolResultContains: "8495"),
            ScriptedChatResponse.Final("Prepared refund proposal for ORD-1042. Proposal ID will be shown in the application record and remains pending_human_approval. No refund has been approved, issued, transmitted, or settled; a human must approve the proposal through the normal application endpoint.", expectedLastToolName: "create_refund_proposal", expectedLastToolResultContains: "pending_human_approval")
        }
    };
}
