using Buffaly.AgentKit;
using Buffaly.AgentKit.AspNetCore;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using Commerce.ReturnResolution.Repositories;
using Commerce.ReturnResolution.Tools;
using Commerce.ReturnResolution.Scenarios;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
string contentRoot = builder.Environment.ContentRootPath;
string dataRoot = Path.Combine(contentRoot, "Data");
string storageRoot = Path.Combine(contentRoot, ".agentkit");
Directory.CreateDirectory(storageRoot);
builder.Services.AddSingleton<IOrderRepository>(new JsonOrderRepository(dataRoot));
builder.Services.AddSingleton<IRefundProposalStore>(new JsonRefundProposalStore(storageRoot));
builder.Services.AddSingleton<IChatClient>(SampleChatClientFactory.Create(ReturnScenarioFactory.Create()));
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

